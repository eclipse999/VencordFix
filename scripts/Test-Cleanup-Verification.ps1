<#
.SYNOPSIS
    驗證 VencordFix 暫存檔自動清理機制
#>

$tempDir = Join-Path $env:LOCALAPPDATA "VencordFix\temp"

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host "  VencordFix 暫存檔無痕清理驗證工具" -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan

Write-Host "`n[步驟 1] 檢查暫存目錄目前狀態..." -ForegroundColor Yellow
Write-Host "暫存路徑: $tempDir"
if (Test-Path $tempDir) {
    $beforeFiles = Get-ChildItem -Path $tempDir -Filter "*VencordInstallerCli*" -ErrorAction SilentlyContinue
    Write-Host "執行前檔案數量: $($beforeFiles.Count) 個"
} else {
    Write-Host "暫存目錄尚未建立 (0 個檔案)"
}

Write-Host "`n[步驟 2] 觸發強制重新修補 (強制從 GitHub 下載安裝檔並自動清理)..." -ForegroundColor Yellow
$exePath = Join-Path $PSScriptRoot "..\bin\VencordFix.exe"
if (Test-Path $exePath) {
    & $exePath --force --no-launch
} else {
    & (Join-Path $PSScriptRoot "..\VencordFix.ps1") -Force -NoLaunch
}

Write-Host "`n[步驟 3] 檢查執行後暫存目錄是否有任何檔案殘留..." -ForegroundColor Yellow
if (Test-Path $tempDir) {
    $afterFiles = Get-ChildItem -Path $tempDir -Filter "*VencordInstallerCli*" -ErrorAction SilentlyContinue
    if ($afterFiles -and $afterFiles.Count -gt 0) {
        Write-Host "[-] 警告：仍有 $($afterFiles.Count) 個檔案殘留:" -ForegroundColor Red
        $afterFiles | Format-Table Name, Length, LastWriteTime
    } else {
        Write-Host "[✔ 驗證成功] 暫存目錄乾淨無物，所有下載的安裝檔已 100% 自動無痕刪除！" -ForegroundColor Green
    }
} else {
    Write-Host "[✔ 驗證成功] 暫存目錄無任何殘留！" -ForegroundColor Green
}

Write-Host "==========================================================" -ForegroundColor Cyan
