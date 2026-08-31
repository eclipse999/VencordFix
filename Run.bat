@echo off
setlocal
cd /d "%~dp0"

if exist "bin\VencordFix.exe" (
    "bin\VencordFix.exe" %*
) else (
    powershell -ExecutionPolicy Bypass -File "VencordFix.ps1" %*
)
