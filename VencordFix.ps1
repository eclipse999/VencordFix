<#
.SYNOPSIS
    VencordFix - Discord Auto Patcher & Smart Launcher for Windows
.DESCRIPTION
    自動檢測 Discord 更新狀態，並在需要時從官方 GitHub 下載 Vencord 安裝檔執行修補，
    修補完成後自動刪除安裝檔並啟動 Discord。
.PARAMETER Branch
    指定 Discord 分支：auto (預設), stable, ptb, canary, dev
.PARAMETER Force
    強制重新下載並修補 Vencord，即使當前版本已被修補
.PARAMETER NoLaunch
    修補完成後不自動啟動 Discord
.PARAMETER NoSyncBuilds
    關閉「啟動前自動同步官方最新 Vencord 執行檔」，改由 Vencord 自行更新
.PARAMETER OpenAsar
    同時安裝 OpenAsar
.PARAMETER Watch
    以背景監控模式執行，即時偵測 Discord 自動更新並自動修補
.PARAMETER InstallShortcut
    在桌面建立 VencordFix 啟動捷徑
.PARAMETER InstallStartup
    將背景監控程式加入 Windows 開機自動啟動
.PARAMETER UninstallStartup
    移除 Windows 開機自動啟動設定
.PARAMETER Silent
    靜默模式（隱藏不必要的輸出與提示）
#>

[CmdletBinding()]
param (
    [ValidateSet("auto", "stable", "ptb", "canary", "dev")]
    [string]$Branch = "auto",

    [switch]$Force,
    [switch]$NoLaunch,
    [switch]$NoSyncBuilds,
    [switch]$OpenAsar,
    [switch]$Watch,
    [switch]$InstallShortcut,
    [switch]$InstallStartup,
    [switch]$UninstallStartup,
    [switch]$Silent
)

# 啟用 TLS 1.2 / 1.3 支援
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12 -bor [Net.SecurityProtocolType]::Tls13

$VencordInstallerUrl = "https://github.com/Vencord/Installer/releases/latest/download/VencordInstallerCli.exe"
$VencordReleaseUrl = "https://api.github.com/repos/Vendicated/Vencord/releases/latest"
$VencordReleaseUrlFallback = "https://vencord.dev/releases/vencord"

# Discord 分支對應清單
$DiscordBranches = @(
    @{ Name = "stable"; Folder = "Discord"; Exe = "Discord.exe"; Process = "Discord"; Title = "Discord (Stable)" },
    @{ Name = "ptb"; Folder = "DiscordPTB"; Exe = "DiscordPTB.exe"; Process = "DiscordPTB"; Title = "Discord PTB" },
    @{ Name = "canary"; Folder = "DiscordCanary"; Exe = "DiscordCanary.exe"; Process = "DiscordCanary"; Title = "Discord Canary" },
    @{ Name = "dev"; Folder = "DiscordDevelopment"; Exe = "DiscordDevelopment.exe"; Process = "DiscordDevelopment"; Title = "Discord Development" }
)

function Write-Log {
    param([string]$Message, [string]$Level = "INFO")
    if ($Silent -and $Level -eq "DEBUG") { return }
    $timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
    $color = switch ($Level) {
        "ERROR" { "Red" }
        "WARN"  { "Yellow" }
        "SUCCESS" { "Green" }
        "TITLE" { "Cyan" }
        default { "Gray" }
    }
    Write-Host "[$timestamp] [$Level] $Message" -ForegroundColor $color
}

# 取得已安裝的 Discord 分支
function Get-InstalledDiscords {
    $results = @()
    $localAppData = $env:LOCALAPPDATA

    foreach ($b in $DiscordBranches) {
        $branchPath = Join-Path $localAppData $b.Folder
        if (Test-Path $branchPath) {
            $appDirs = Get-ChildItem -Path $branchPath -Directory -Filter "app-*" |
                Sort-Object {
                    try {
                        [version]($_.Name -replace '^app-', '')
                    } catch {
                        $_.LastWriteTime
                    }
                } -Descending

            if ($appDirs.Count -gt 0) {
                $latestAppDir = $appDirs[0]
                $results += [PSCustomObject]@{
                    BranchName   = $b.Name
                    Title        = $b.Title
                    RootPath     = $branchPath
                    LatestAppDir = $latestAppDir.FullName
                    Version      = $latestAppDir.Name -replace '^app-', ''
                    ExeName      = $b.Exe
                    ProcessName  = $b.Process
                    UpdateExe    = Join-Path $branchPath "Update.exe"
                }
            }
        }
    }
    return $results
}

# 解析 app.asar 注入器內容，取得實際載入的 patcher.js 路徑
function Get-InjectedPatcherPath {
    param([string]$AppAsarPath)

    if (-not (Test-Path $AppAsarPath)) { return $null }

    try {
        $text = [System.Text.Encoding]::UTF8.GetString([System.IO.File]::ReadAllBytes($AppAsarPath))
        $match = [regex]::Match($text, 'require\("([^"]+\.js)"\)')
        if ($match.Success) {
            return $match.Groups[1].Value.Replace('\\', '\')
        }
    } catch { }

    return $null
}

# 取得 Vencord 執行檔目錄 (與官方 Installer 的 FilesDir 邏輯一致)
function Get-VencordDistDir {
    param([PSCustomObject]$DiscordInfo)

    if ($env:VENCORDFIX_DIST_DIR) { return $env:VENCORDFIX_DIST_DIR }

    if ($DiscordInfo) {
        $resourcesDir = Join-Path $DiscordInfo.LatestAppDir "resources"
        $injected = Get-InjectedPatcherPath (Join-Path $resourcesDir "app.asar")
        if ($injected) {
            $dir = Split-Path -Parent $injected
            if ($dir) { return $dir }
        }
    }

    return (Join-Path $env:APPDATA "Vencord\dist")
}

# 讀取目前已安裝的 Vencord 版本雜湊 (patcher.js 第一行: // Vencord <hash>)
function Get-InstalledVencordHash {
    param([string]$DistDir)

    if (-not $DistDir) { return $null }

    $patcherPath = Join-Path $DistDir "patcher.js"
    if (-not (Test-Path $patcherPath)) { return $null }

    $firstLine = Get-Content $patcherPath -TotalCount 1 -Encoding UTF8
    if ($firstLine -match '^// Vencord\s+(\S+)') {
        return $Matches[1].Trim()
    }

    return $null
}

# 取得官方最新發行資訊
function Get-LatestVencordRelease {
    $headers = @{
        "User-Agent" = "VencordFix (Windows NT 10.0; Win64; x64)"
        "Accept" = "application/vnd.github+json"
    }

    foreach ($url in @($VencordReleaseUrl, $VencordReleaseUrlFallback)) {
        try {
            $release = Invoke-RestMethod -Uri $url -Headers $headers -UseBasicParsing -TimeoutSec 15
            if ($release -and $release.name) {
                $hash = ($release.name -split '\s+')[-1]
                if ($hash) {
                    return [PSCustomObject]@{
                        Name   = $release.name
                        Hash   = $hash
                        Assets = $release.assets
                    }
                }
            }
        } catch {
            Write-Log "讀取 $url 失敗: $_" "WARN"
        }
    }

    return $null
}

# 下載並置換 Vencord 檔案 (先全部下載到暫存檔，驗證成功後才置換)
function Update-VencordFiles {
    param(
        [PSCustomObject]$Release,
        [string]$DistDir
    )

    $requiredFiles = @("patcher.js", "preload.js", "renderer.js", "renderer.css")

    $assets = @($Release.Assets | Where-Object { $_.name -match '^(patcher\.js|preload\.js|renderer\.js|renderer\.css)' })

    foreach ($required in $requiredFiles) {
        if (-not ($assets | Where-Object { $_.name -eq $required })) {
            Write-Log "官方發行檔缺少 $required，已中止同步以避免損壞 Vencord。" "ERROR"
            return $false
        }
    }

    if (-not (Test-Path $DistDir)) {
        New-Item -ItemType Directory -Force -Path $DistDir | Out-Null
    }

    $staged = @()
    try {
        foreach ($asset in $assets) {
            $finalPath = Join-Path $DistDir $asset.name
            $stagingPath = "$finalPath.vcnew"

            Write-Log "  下載 $($asset.name) ..." "INFO"
            Invoke-WebRequest -Uri $asset.browser_download_url -OutFile $stagingPath -UseBasicParsing -TimeoutSec 60

            if ($asset.size -gt 0) {
                $actualSize = (Get-Item $stagingPath).Length
                if ($actualSize -ne $asset.size) {
                    throw "$($asset.name) 下載不完整 ($actualSize/$($asset.size) bytes)"
                }
            }

            $staged += [PSCustomObject]@{ Staging = $stagingPath; Final = $finalPath }
        }

        foreach ($item in $staged) {
            Move-Item -Path $item.Staging -Destination $item.Final -Force
        }
    } catch {
        Write-Log "同步 Vencord 檔案失敗: $_" "ERROR"
        foreach ($item in $staged) {
            Remove-Item -Path $item.Staging -Force -ErrorAction SilentlyContinue
        }
        return $false
    }

    # 官方 Installer 會建立空的 package.json，避免 node 往上層目錄尋找 package.json
    $pkgPath = Join-Path $DistDir "package.json"
    if (-not (Test-Path $pkgPath)) {
        Set-Content -Path $pkgPath -Value "{}" -NoNewline -Encoding ASCII
    }

    Write-Log "已更新 $($staged.Count) 個 Vencord 檔案至版本 $($Release.Hash)。" "SUCCESS"
    return $true
}

# 啟動前同步官方最新 Vencord 執行檔，避免啟動後才由 Vencord 自行更新而要求重啟
function Sync-VencordBuilds {
    param([PSCustomObject[]]$Discords)

    $distDirs = @()
    foreach ($d in $Discords) {
        $dir = Get-VencordDistDir $d
        if ($dir -and ($distDirs -notcontains $dir)) { $distDirs += $dir }
    }
    if ($distDirs.Count -eq 0) { $distDirs = @(Get-VencordDistDir $null) }

    $release = Get-LatestVencordRelease
    if (-not $release) {
        Write-Log "無法取得 Vencord 最新版本資訊，略過同步，直接啟動。" "WARN"
        return $false
    }

    $applied = $false
    foreach ($distDir in $distDirs) {
        $installedHash = Get-InstalledVencordHash $distDir

        if ($installedHash -and $installedHash -eq $release.Hash) {
            Write-Log "Vencord 執行檔已是最新版本 ($($release.Hash))。" "SUCCESS"
            continue
        }

        $shownHash = if ($installedHash) { $installedHash } else { "未知" }
        Write-Log "偵測到 Vencord 新版本: $shownHash -> $($release.Hash)" "WARN"

        foreach ($d in $Discords) {
            if (Get-Process -Name $d.ProcessName -ErrorAction SilentlyContinue) {
                Stop-DiscordProcesses -ProcessName $d.ProcessName
            }
        }

        if (Update-VencordFiles -Release $release -DistDir $distDir) {
            $applied = $true
            Write-Log "啟動後即為最新版 Vencord，不會再出現「重新啟動」提示。" "SUCCESS"
        } else {
            Write-Log "同步失敗，維持現有版本啟動。" "ERROR"
        }
    }

    return $applied
}

# 檢查指定 Discord 是否已被 Vencord 修補
function Test-IsDiscordPatched {
    param([PSCustomObject]$DiscordInfo)

    $resourcesDir = Join-Path $DiscordInfo.LatestAppDir "resources"
    if (-not (Test-Path $resourcesDir)) {
        return $false
    }

    $appAsar = Join-Path $resourcesDir "app.asar"
    $origAppAsar = Join-Path $resourcesDir "_app.asar"

    if (Test-Path $origAppAsar) {
        if (Test-Path $appAsar) {
            $asarItem = Get-Item $appAsar
            if ($asarItem.Length -lt 200000) {
                # app.asar 只是注入器；若它指向的 Vencord 檔案不存在，Discord 會啟動失敗，必須重新修補。
                $injected = Get-InjectedPatcherPath $appAsar
                if ($injected) {
                    return (Test-Path $injected)
                }

                return $true
            }
        }
    }

    return $false
}

# 關閉正在運行的 Discord
function Stop-DiscordProcesses {
    param([string]$ProcessName)
    $processes = Get-Process -Name $ProcessName -ErrorAction SilentlyContinue
    if ($processes) {
        Write-Log "正在關閉執行中的 $ProcessName 程序以進行修補..." "WARN"
        foreach ($proc in $processes) {
            try {
                $proc.CloseMainWindow() | Out-Null
                Start-Sleep -Milliseconds 500
                if (-not $proc.HasExited) {
                    Stop-Process -Id $proc.Id -Force -ErrorAction SilentlyContinue
                }
            } catch {
                Stop-Process -Id $proc.Id -Force -ErrorAction SilentlyContinue
            }
        }
        Start-Sleep -Seconds 1
    }
}

# 下載 Vencord 安裝檔、執行修補並自動清理
function Invoke-VencordPatch {
    param(
        [string]$TargetBranch = "auto",
        [switch]$IncludeOpenAsar
    )

    $workDir = Join-Path $env:LOCALAPPDATA "VencordFix\temp"
    if (-not (Test-Path $workDir)) {
        New-Item -ItemType Directory -Force -Path $workDir | Out-Null
    }

    $tempInstaller = Join-Path $workDir ("VencordInstallerCli_" + [Guid]::NewGuid().ToString("N") + ".exe")
    Write-Log "正在從官方發行版下載最新 Vencord 安裝檔..." "INFO"
    Write-Log "下載網址: $VencordInstallerUrl" "DEBUG"

    try {
        Invoke-WebRequest -Uri $VencordInstallerUrl -OutFile $tempInstaller -UseBasicParsing
        Write-Log "Vencord 安裝檔下載完成！" "SUCCESS"

        $argsList = @("-install", "-branch", $TargetBranch)
        if ($IncludeOpenAsar) {
            $argsList += "-install-openasar"
        }

        Write-Log "開始對 Discord 執行修補 (參數: $($argsList -join ' '))..." "INFO"

        $processInfo = New-Object System.Diagnostics.ProcessStartInfo
        $processInfo.FileName = $tempInstaller
        $processInfo.Arguments = ($argsList -join ' ')
        $processInfo.WorkingDirectory = $workDir
        $processInfo.RedirectStandardInput = $true
        $processInfo.RedirectStandardOutput = $true
        $processInfo.RedirectStandardError = $true
        $processInfo.UseShellExecute = $false
        $processInfo.CreateNoWindow = $true

        $process = [System.Diagnostics.Process]::Start($processInfo)
        $process.StandardInput.Close()
        $stdout = $process.StandardOutput.ReadToEnd()
        $stderr = $process.StandardError.ReadToEnd()
        $process.WaitForExit(60000)

        if ($stdout) {
            $stdout.Split("`n") | ForEach-Object {
                $line = $_.Trim()
                if ($line) { Write-Log "  $line" "INFO" }
            }
        }

        if ($process.ExitCode -eq 0) {
            Write-Log "Discord 已成功修補 Vencord！" "SUCCESS"
            return $true
        } else {
            Write-Log "修補過程中出現錯誤 (ExitCode: $($process.ExitCode))" "ERROR"
            if ($stderr) { Write-Log "$stderr" "ERROR" }
            return $false
        }
    } catch {
        Write-Log "下載或修補失敗: $_" "ERROR"
        return $false
    } finally {
        if (Test-Path $tempInstaller) {
            Write-Log "正在清理暫存安裝檔: $tempInstaller" "DEBUG"
            Remove-Item -Path $tempInstaller -Force -ErrorAction SilentlyContinue
            Write-Log "暫存檔清理完畢。" "INFO"
        }
    }
}

# 啟動 Discord
function Start-DiscordApp {
    param([PSCustomObject]$DiscordInfo)

    Write-Log "正在啟動 $($DiscordInfo.Title)..." "INFO"

    if (Test-Path $DiscordInfo.UpdateExe) {
        Start-Process -FilePath $DiscordInfo.UpdateExe -ArgumentList "--processStart $($DiscordInfo.ExeName)"
    } else {
        $directExe = Join-Path $DiscordInfo.LatestAppDir $DiscordInfo.ExeName
        if (Test-Path $directExe) {
            Start-Process -FilePath $directExe
        } else {
            Write-Log "找不到 Discord 執行檔！" "ERROR"
        }
    }
}

# 背景監控模式
function Start-WatchMode {
    Write-Log "啟動 Discord 更新背景監控模式 (Watcher Mode)..." "TITLE"
    Write-Log "正在監控 %LocalAppData% 下的 Discord 目錄更新..." "INFO"

    $discords = Get-InstalledDiscords
    if ($discords.Count -eq 0) {
        Write-Log "未偵測到任何本機已安裝的 Discord。" "ERROR"
        return
    }

    $watchers = @()
    foreach ($d in $discords) {
        Write-Log "監控目標: $($d.Title) -> $($d.RootPath)" "INFO"
        $fsw = New-Object System.IO.FileSystemWatcher
        $fsw.Path = $d.RootPath
        $fsw.Filter = "*.*"
        $fsw.IncludeSubdirectories = $true
        $fsw.EnableRaisingEvents = $true

        $action = {
            param($source, $eventArgs)
            $changeType = $eventArgs.ChangeType
            $fullPath = $eventArgs.FullPath
            
            if ($fullPath -match 'app-[\d\.]+\\resources\\app\.asar' -or ($eventArgs.Name -match '^app-[\d\.]+$' -and $changeType -eq 'Created')) {
                Write-Host "`n[監控觸發] 偵測到 Discord 可能已更新: $fullPath" -ForegroundColor Yellow
                Start-Sleep -Seconds 3
                
                $installed = Get-InstalledDiscords
                foreach ($inst in $installed) {
                    if (-not (Test-IsDiscordPatched $inst)) {
                        Write-Host "[自動修補] $($inst.Title) 尚未修補，開始自動修補..." -ForegroundColor Cyan
                        Invoke-VencordPatch -TargetBranch $inst.BranchName
                    }
                }
            }
        }

        Register-ObjectEvent $fsw "Created" -Action $action | Out-Null
        Register-ObjectEvent $fsw "Changed" -Action $action | Out-Null
        $watchers += $fsw
    }

    Write-Log "背景監控已啟動。按 Ctrl+C 可停止監控。" "SUCCESS"
    try {
        while ($true) {
            Start-Sleep -Seconds 30
        }
    } finally {
        foreach ($w in $watchers) {
            $w.EnableRaisingEvents = $false
            $w.Dispose()
        }
        Get-EventSubscriber | Unregister-Event -ErrorAction SilentlyContinue
        Write-Log "監控已停止。" "INFO"
    }
}

# 建立桌面捷徑
function Install-DesktopShortcut {
    $scriptPath = $PSCommandPath
    if (-not $scriptPath) {
        $scriptPath = Join-Path $PSScriptRoot "VencordFix.ps1"
    }

    $wshShell = New-Object -ComObject WScript.Shell
    $desktopPath = [System.Environment]::GetFolderPath([System.Environment+SpecialFolder]::Desktop)
    $shortcutPath = Join-Path $desktopPath "Discord (VencordFix).lnk"
    
    $discords = Get-InstalledDiscords
    $iconLocation = ""
    if ($discords.Count -gt 0) {
        $iconLocation = Join-Path $discords[0].RootPath "app.ico"
    }

    $shortcut = $wshShell.CreateShortcut($shortcutPath)
    $shortcut.TargetPath = "powershell.exe"
    $shortcut.Arguments = "-WindowStyle Hidden -ExecutionPolicy Bypass -File `"$scriptPath`""
    $shortcut.WorkingDirectory = $PSScriptRoot
    $shortcut.Description = "啟動 Discord 並自動檢查/修補 Vencord"
    if ($iconLocation -and (Test-Path $iconLocation)) {
        $shortcut.IconLocation = "$iconLocation,0"
    }
    $shortcut.Save()

    Write-Log "已在桌面建立快捷方式: $shortcutPath" "SUCCESS"
}

# 設定開機啟動
function Set-StartupTask {
    param([bool]$Enable)

    $taskName = "VencordFixWatcher"
    if ($Enable) {
        $scriptPath = $PSCommandPath
        if (-not $scriptPath) {
            $scriptPath = Join-Path $PSScriptRoot "VencordFix.ps1"
        }
        $cmd = "powershell.exe -WindowStyle Hidden -ExecutionPolicy Bypass -File `"$scriptPath`" -Watch -Silent"
        
        $regKey = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Run"
        Set-ItemProperty -Path $regKey -Name $taskName -Value $cmd
        Write-Log "已成功將 VencordFix 背景更新監控加入開機自動啟動 (HKCU Run)" "SUCCESS"
    } else {
        $regKey = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Run"
        Remove-ItemProperty -Path $regKey -Name $taskName -ErrorAction SilentlyContinue
        Write-Log "已移除 VencordFix 開機自動啟動設定" "SUCCESS"
    }
}

# 主執行流程
function Main {
    Write-Log "=== VencordFix: Discord 自動修補與啟動器 ===" "TITLE"

    if ($InstallShortcut) {
        Install-DesktopShortcut
        return
    }

    if ($InstallStartup) {
        Set-StartupTask -Enable $true
        return
    }

    if ($UninstallStartup) {
        Set-StartupTask -Enable $false
        return
    }

    if ($Watch) {
        Start-WatchMode
        return
    }

    $discords = Get-InstalledDiscords
    if ($discords.Count -eq 0) {
        Write-Log "未偵測到任何已安裝的 Discord！請確認 Discord 是否已安裝在 %LocalAppData% 目錄下。" "ERROR"
        return
    }

    $targetDiscords = if ($Branch -eq "auto") {
        $discords
    } else {
        $discords | Where-Object { $_.BranchName -eq $Branch }
    }

    if ($targetDiscords.Count -eq 0) {
        Write-Log "找不到指定的 Discord 分支: $Branch" "ERROR"
        return
    }

    $needPatching = $false
    $discordsToPatch = @()
    $patchedByInstaller = $false

    foreach ($d in $targetDiscords) {
        $isPatched = Test-IsDiscordPatched $d
        Write-Log "偵測到 $($d.Title) (版本: $($d.Version))" "INFO"
        if ($isPatched) {
            Write-Log "  狀態: [已修補 Vencord]" "SUCCESS"
        } else {
            Write-Log "  狀態: [尚未修補或剛更新]" "WARN"
            $needPatching = $true
            $discordsToPatch += $d
        }
    }

    if ($needPatching -or $Force) {
        if ($Force) {
            Write-Log "已指定 -Force 參數，將強制執行 Vencord 重新修補..." "WARN"
            $discordsToPatch = $targetDiscords
        } else {
            Write-Log "偵測到 Discord 尚未修補 Vencord (可能是 Discord 剛自動更新)，開始執行修補..." "INFO"
        }

        foreach ($d in $discordsToPatch) {
            Stop-DiscordProcesses -ProcessName $d.ProcessName
        }

        $patchBranch = if ($Branch -eq "auto") { "auto" } else { $targetDiscords[0].BranchName }
        $success = Invoke-VencordPatch -TargetBranch $patchBranch -IncludeOpenAsar:$OpenAsar

        if ($success) {
            # 官方 Installer 的 -install 已會依雜湊同步最新執行檔，本次不需重複檢查。
            $patchedByInstaller = $true
        } else {
            Write-Log "修補失敗，請檢查網路連線或手動嘗試。" "ERROR"
        }
    } else {
        Write-Log "Discord 已是最新修補狀態，無需重新修補！" "SUCCESS"
    }

    # 啟動前先同步官方最新 Vencord 執行檔，避免啟動後才由 Vencord 自行更新而要求「點此重啟」。
    if (-not $NoSyncBuilds -and -not $patchedByInstaller) {
        Sync-VencordBuilds -Discords $targetDiscords | Out-Null
    }

    if (-not $NoLaunch) {
        Start-DiscordApp -DiscordInfo $targetDiscords[0]
    }

    Write-Log "完成！" "SUCCESS"
}

# 執行
Main
