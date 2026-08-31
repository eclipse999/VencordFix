@echo off
setlocal
cd /d "%~dp0.."

if exist "bin\VencordAutoPatcher.exe" (
    "bin\VencordAutoPatcher.exe" --install-shortcut
) else (
    powershell -ExecutionPolicy Bypass -File "VencordAutoPatcher.ps1" -InstallShortcut
)

echo.
pause
