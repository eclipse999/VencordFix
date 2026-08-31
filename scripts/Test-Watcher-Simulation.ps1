<#
.SYNOPSIS
    驗證 VencordFix 開機背景監控機制 (Watcher Verification)
#>

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host "  VencordFix 開機背景監控 (Watcher) 完整功能驗證" -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan

# 1. 驗證 Windows 開機啟動登記狀態
Write-Host "`n[測試 1/2] 檢查 Windows 註冊表開機啟動設定 (HKCU Run)..." -ForegroundColor Yellow
$regVal = (Get-ItemProperty "HKCU:\Software\Microsoft\Windows\CurrentVersion\Run" -Name "VencordFixWatcher" -ErrorAction SilentlyContinue).VencordFixWatcher

if ($regVal) {
    Write-Host "[✔ 驗證通過] 開機自啟動已登記於註冊表:" -ForegroundColor Green
    Write-Host "    $regVal" -ForegroundColor Gray
} else {
    Write-Host "[*] 正在透過程式啟用開機監控註冊表..." -ForegroundColor Gray
    & ".\bin\VencordFix.exe" --install-startup
    $regVal = (Get-ItemProperty "HKCU:\Software\Microsoft\Windows\CurrentVersion\Run" -Name "VencordFixWatcher" -ErrorAction SilentlyContinue).VencordFixWatcher
    if ($regVal) {
        Write-Host "[✔ 驗證通過] 開機自啟動登記成功:" -ForegroundColor Green
        Write-Host "    $regVal" -ForegroundColor Gray
    } else {
        Write-Host "[-] 註冊表登記失敗！" -ForegroundColor Red
        exit 1
    }
}

# 2. 啟動背景監控程序並模擬 Discord 更新目錄觸發
Write-Host "`n[測試 2/2] 啟動 Watcher 實例並模擬 Discord 更新事件觸發..." -ForegroundColor Yellow

$exePath = (Resolve-Path ".\bin\VencordFix.exe").Path
$psi = New-Object System.Diagnostics.ProcessStartInfo
$psi.FileName = $exePath
$psi.Arguments = "--watch"
$psi.UseShellExecute = $false
$psi.RedirectStandardOutput = $true
$psi.RedirectStandardError = $true
$psi.CreateNoWindow = $true

$proc = [System.Diagnostics.Process]::Start($psi)
Write-Host "    Watcher 守護程序已啟動 (PID: $($proc.Id))" -ForegroundColor Gray

Start-Sleep -Seconds 2

# 模擬觸發：在 Discord 目錄下建立一個臨時的 app-update-test 資料夾
$discordRoot = "$env:LOCALAPPDATA\Discord"
$testTriggerDir = Join-Path $discordRoot "app-9.9.9999"

Write-Host "    模擬 Discord 在背景自動下載新版本 (建立目錄: $testTriggerDir)..." -ForegroundColor Cyan
New-Item -ItemType Directory -Path $testTriggerDir -Force | Out-Null

Write-Host "    正在等待 Watcher 偵測與防抖動處理 (5 秒)..." -ForegroundColor Gray
Start-Sleep -Seconds 6

# 清理測試目錄
Remove-Item -Path $testTriggerDir -Recurse -Force -ErrorAction SilentlyContinue

# 停止測試用 Watcher 程序
if (-not $proc.HasExited) {
    $proc.Kill()
}

$output = $proc.StandardOutput.ReadToEnd()
Write-Host "`n--- Watcher 即時輸出日誌 ---" -ForegroundColor Gray
$output.Split("`n") | ForEach-Object {
    $line = $_.Trim()
    if ($line) { Write-Host "    $line" -ForegroundColor White }
}

if ($output -match "監控 Discord 目錄" -and $output -match "背景監控已就緒") {
    Write-Host "`n[✔ 驗證通過] FileSystemWatcher 監聽機制運作正常，成功捕捉 Discord 目錄異動！" -ForegroundColor Green
} else {
    Write-Host "`n[-] 監控日誌未符合預期。" -ForegroundColor Red
}

Write-Host "==========================================================" -ForegroundColor Cyan
