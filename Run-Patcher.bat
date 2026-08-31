@echo off
setlocal
cd /d "%~dp0"

if exist "bin\VencordAutoPatcher.exe" (
    "bin\VencordAutoPatcher.exe" %*
) else (
    powershell -ExecutionPolicy Bypass -File "VencordAutoPatcher.ps1" %*
)
