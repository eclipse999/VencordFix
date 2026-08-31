@echo off
chcp 65001 >nul
echo 正在編譯 VencordAutoPatcher.exe...
powershell -ExecutionPolicy Bypass -File "%~dp0build.ps1"
if %ERRORLEVEL% equ 0 (
    echo.
    echo 編譯完成！可執行檔位於 bin\VencordAutoPatcher.exe
) else (
    echo.
    echo 編譯失敗，請檢查錯誤訊息。
)
pause
