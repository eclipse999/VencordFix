@echo off
setlocal
cd /d "%~dp0.."

if exist "bin\VencordAutoPatcher.exe" (
    "bin\VencordAutoPatcher.exe" --install-startup
) else (
    powershell -ExecutionPolicy Bypass -File "VencordAutoPatcher.ps1" -InstallStartup
)

echo.
pause
