@echo off
setlocal
cd /d "%~dp0.."

if exist "bin\VencordFix.exe" (
    "bin\VencordFix.exe" --install-shortcut
) else (
    powershell -ExecutionPolicy Bypass -File "VencordFix.ps1" -InstallShortcut
)

echo.
pause
