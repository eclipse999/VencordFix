@echo off
chcp 65001 >nul
echo ==========================================================
echo  正在為 Discord (Vencord) 建立桌面捷徑...
echo ==========================================================

if exist "%~dp0..\bin\VencordAutoPatcher.exe" (
    "%~dp0..\bin\VencordAutoPatcher.exe" --install-shortcut
) else (
    powershell -ExecutionPolicy Bypass -File "%~dp0..\VencordAutoPatcher.ps1" -InstallShortcut
)

echo.
echo 完成！您現在可以直接使用桌面的捷徑啟動 Discord。
echo 每次點擊都會自動檢查修補狀態，讓 Vencord 永不失效！
echo.
pause
