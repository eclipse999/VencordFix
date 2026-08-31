@echo off
chcp 65001 >nul
echo ==========================================================
echo  正在將 Vencord 背景更新監控加入開機自動啟動...
echo ==========================================================

if exist "%~dp0..\bin\VencordAutoPatcher.exe" (
    "%~dp0..\bin\VencordAutoPatcher.exe" --install-startup
) else (
    powershell -ExecutionPolicy Bypass -File "%~dp0..\VencordAutoPatcher.ps1" -InstallStartup
)

echo.
echo 完成！電腦每次開機時將會在後台靜默監控 Discord 更新。
echo 當 Discord 在背景下載更新目錄時，會自動進行 Vencord 修補！
echo.
pause
