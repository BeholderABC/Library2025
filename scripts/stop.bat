@echo off
echo Stopping Library Management System...
echo.

echo Stopping Web App processes...
taskkill /f /im dotnet.exe 2>nul
if %errorlevel% equ 0 (echo Web App stopped) else (echo No Web App processes found)

echo.
echo Stopping ngrok processes...
taskkill /f /im ngrok.exe 2>nul  
if %errorlevel% equ 0 (echo ngrok stopped) else (echo No ngrok processes found)

echo.
echo All services stopped!
pause
