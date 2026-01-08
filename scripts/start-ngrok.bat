@echo off
echo Starting ngrok tunnel...
echo.

REM Try to find ngrok in PATH first, then common locations
where ngrok >nul 2>&1 && set "NGROK=ngrok" || (
  if exist "D:\Tools\ngrok\ngrok.exe" (
    set "NGROK=D:\Tools\ngrok\ngrok.exe"
  ) else if exist "%USERPROFILE%\AppData\Local\Microsoft\WinGet\Links\ngrok.exe" (
    set "NGROK=%USERPROFILE%\AppData\Local\Microsoft\WinGet\Links\ngrok.exe"
  ) else (
    echo ERROR: ngrok not found
    echo Please install ngrok and add it to PATH or place it in D:\Tools\ngrok\
    pause
    exit /b 1
  )
)

echo Starting ngrok tunnel...
echo Mapping port 5174 to public URL...
echo.

REM Start ngrok
%NGROK% http 5174
