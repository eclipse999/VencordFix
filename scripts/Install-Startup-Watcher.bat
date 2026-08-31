@echo off
setlocal
cd /d "%~dp0.."

if exist "bin\VencordFix.exe" (
    "bin\VencordFix.exe" --install-startup
) else (
    powershell -ExecutionPolicy Bypass -File "VencordFix.ps1" -InstallStartup
)

echo.
pause
