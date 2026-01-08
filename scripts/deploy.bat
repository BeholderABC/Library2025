@echo off
REM Simple deployment script - no encoding issues
echo ==============================
echo Library Management System
echo One-Click Deploy (English)
echo ==============================
echo.

cd /d "%~dp0.."
echo Starting Web App...
start "WebApp" cmd /c "cd WebLibrary && dotnet run"

echo Waiting 15 seconds...
timeout /t 15 /nobreak >nul

echo Starting ngrok...
REM Try different ngrok locations
where ngrok >nul 2>&1 && set "NGROK=ngrok" || (
  if exist "D:\Tools\ngrok\ngrok.exe" (set "NGROK=D:\Tools\ngrok\ngrok.exe") else (
    echo ERROR: ngrok not found
    pause
    exit /b 1
  )
)

echo.
echo Local:  http://localhost:5174
echo Panel:  http://127.0.0.1:4040
echo.
%NGROK% http 5174
