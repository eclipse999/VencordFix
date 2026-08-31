<#
.SYNOPSIS
    編譯 VencordFix.exe
#>

$cscPath = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if (-not (Test-Path $cscPath)) {
    $cscPath = "C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe"
}

if (-not (Test-Path $cscPath)) {
    Write-Error "找不到 C# 編譯器 (csc.exe)。"
    exit 1
}

$outputDir = Join-Path $PSScriptRoot "bin"
if (-not (Test-Path $outputDir)) {
    New-Item -ItemType Directory -Path $outputDir | Out-Null
}

$outputExe = Join-Path $outputDir "VencordFix.exe"
$srcFiles = (Get-ChildItem -Path (Join-Path $PSScriptRoot "src") -Filter "*.cs").FullName

# 嘗試尋找 Discord 的 app.ico
$iconOption = ""
$discordIco = "$env:LOCALAPPDATA\Discord\app.ico"
if (Test-Path $discordIco) {
    $iconOption = "/win32icon:`"$discordIco`""
}

$refs = @(
    "System.dll",
    "System.Core.dll",
    "System.Windows.Forms.dll",
    "System.Drawing.dll",
    "Microsoft.CSharp.dll"
)

$rArgs = ($refs | ForEach-Object { "/r:$_" }) -join " "
$srcArgs = ($srcFiles | ForEach-Object { "`"$_`"" }) -join " "

$cmd = "& `"$cscPath`" /target:exe /optimize+ /nologo /out:`"$outputExe`" $iconOption $rArgs $srcArgs"

Write-Host "正在編譯 VencordFix.exe..." -ForegroundColor Cyan
Invoke-Expression $cmd

if ($LASTEXITCODE -eq 0 -and (Test-Path $outputExe)) {
    Write-Host "編譯成功: $outputExe" -ForegroundColor Green
    $size = (Get-Item $outputExe).Length / 1KB
    Write-Host "檔案大小: $([math]::Round($size, 2)) KB" -ForegroundColor Gray
} else {
    Write-Host "編譯失敗！" -ForegroundColor Red
    exit 1
}
