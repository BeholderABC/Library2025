@echo off
echo Starting Library Management System Web App...
echo.

REM Get project root directory (one level up from scripts)
cd /d "%~dp0.."

REM Switch to project directory
cd WebLibrary

REM Start ASP.NET Core application
echo Starting Web application server...
start "WebLibrary" cmd /k "dotnet run"

REM Wait for application to start
echo Waiting for application to start...
timeout /t 10 /nobreak >nul

echo.
echo Web application started!
echo Local access URL: http://localhost:5174
echo.
echo Press any key to continue...
pause >nul
