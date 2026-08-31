@echo off
setlocal
cd /d "%~dp0.."

if exist "bin\VencordFix.exe" (
    "bin\VencordFix.exe" --uninstall-startup
) else (
    powershell -ExecutionPolicy Bypass -File "VencordFix.ps1" -UninstallStartup
)

echo.
pause
