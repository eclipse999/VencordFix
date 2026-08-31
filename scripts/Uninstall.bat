@echo off
chcp 65001 >nul
echo ==========================================================
echo  正在移除 Vencord Auto Patcher 開機啟動與設定...
echo ==========================================================

if exist "%~dp0..\bin\VencordAutoPatcher.exe" (
    "%~dp0..\bin\VencordAutoPatcher.exe" --uninstall-startup
) else (
    powershell -ExecutionPolicy Bypass -File "%~dp0..\VencordAutoPatcher.ps1" -UninstallStartup
)

echo.
echo 已完成移除設定。
echo.
pause
