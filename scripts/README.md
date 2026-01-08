# Library Management System Deployment Scripts

## Overview

This directory contains automated deployment scripts for the Library Management System. These scripts will start both the ASP.NET Core web application and ngrok tunnel to make your local application accessible from the internet.

## Features

✅ **One-Click Deploy** - Single script to start everything  
✅ **Portable** - Uses relative paths, works from any location  
✅ **Smart Detection** - Automatically finds ngrok installation  
✅ **English Interface** - No encoding issues  

## Script Files

### Core Scripts
- **`deploy.bat`** ⭐ - **Main deployment script (recommended)**
- **`stop.bat`** - Stop all services
- **`start-app.bat`** - Start only the web application  
- **`start-ngrok.bat`** - Start only the ngrok tunnel

### Quick Start
```bash
# Double-click this file or run in command line:
deploy.bat
```

## Prerequisites

### 1. .NET 8.0 SDK
Download and install from: https://dotnet.microsoft.com/download/dotnet/8.0

**Verify installation:**
```bash
dotnet --version
```

### 2. Oracle Database Connection
Ensure your Oracle database server is accessible and connection strings in `appsettings.json` are correct.

### 3. ngrok Installation and Configuration

#### Option A: Install via winget (Windows 10/11)
```bash
# Install ngrok
winget install ngrok

# Verify installation
ngrok version
```

#### Option B: Manual Installation
1. **Download ngrok:**
   - Visit https://ngrok.com/download
   - Download the Windows version (ZIP file)

2. **Extract and Install:**
   ```bash
   # Extract to a permanent location (recommended):
   # D:\Tools\ngrok\ngrok.exe
   # OR
   # C:\Program Files\ngrok\ngrok.exe
   ```

3. **Add to PATH (Important!):**
   - Open Windows Settings → System → About → Advanced system settings
   - Click "Environment Variables"
   - Under "User variables", find and select "Path", click "Edit"
   - Click "New" and add your ngrok directory path
   - **Example:** `D:\Tools\ngrok` (NOT `D:\Tools\ngrok\ngrok.exe`)
   - Click OK to save all dialogs

4. **Verify PATH setup:**
   Open a **new** command prompt or PowerShell window:
   ```bash
   ngrok version
   # Should display version information
   ```

#### ngrok Account Setup (Required)
1. **Create free account:**
   - Visit https://dashboard.ngrok.com/signup
   - Sign up with email or GitHub

2. **Get authentication token:**
   - After login, go to https://dashboard.ngrok.com/get-started/your-authtoken
   - Copy your authtoken (long string starting with numbers/letters)

3. **Configure authtoken:**
   ```bash
   ngrok config add-authtoken YOUR_TOKEN_HERE
   ```
   **Example:**
   ```bash
   ngrok config add-authtoken 2ABC123xyz456def789ghi012jkl345
   ```

4. **Verify configuration:**
   ```bash
   ngrok config check
   # Should show: "Valid configuration file at ..."
   ```

## Usage Instructions

### First Time Setup Test
Before using the deployment scripts, verify everything is working:

```bash
# Test .NET
dotnet --version

# Test ngrok
ngrok version

# Test ngrok authentication
ngrok config check
```

### Running the Deployment

1. **Navigate to the scripts directory:**
   ```bash
   cd path\to\Library2025\scripts
   ```

2. **Run the deployment script:**
   ```bash
   # Method 1: Double-click the file
   deploy.bat

   # Method 2: Run in command line
   .\deploy.bat

   # Method 3: Run from project root
   scripts\deploy.bat
   ```

3. **Wait for startup (about 15-20 seconds):**
   - Web application will start on port 5174
   - ngrok will create a public tunnel
   - URLs will be displayed

4. **Access your application:**
   - **Local:** http://localhost:5174
   - **Public:** The ngrok URL shown in the terminal (e.g., https://abc123.ngrok-free.app)
   - **Dashboard:** http://127.0.0.1:4040 (ngrok web interface)

### Stopping the Services

```bash
# Run the stop script
stop.bat

# Or manually close the terminal windows
```

## What Each Script Does

### deploy.bat (Main Script)
- Changes to project root directory
- Starts the ASP.NET Core web application
- Waits 15 seconds for app initialization
- Searches for ngrok in PATH and common locations
- Starts ngrok tunnel on port 5174
- Displays access URLs

### stop.bat
- Terminates all dotnet.exe processes (web app)
- Terminates all ngrok.exe processes
- Shows status of each operation

### start-app.bat
- Only starts the web application
- Useful for local development without public access

### start-ngrok.bat
- Only starts the ngrok tunnel
- Requires the web app to already be running

## Troubleshooting

### ngrok Issues

#### "ngrok not found" Error
**Problem:** Script shows "ERROR: ngrok not found"

**Solutions:**
1. **Check if ngrok is installed:**
   ```bash
   ngrok version
   ```
   If this fails, ngrok is not in PATH.

2. **Add ngrok to PATH:**
   - Find where ngrok.exe is located
   - Add that directory (not the .exe file) to your PATH environment variable
   - Open a new terminal and test again

3. **Alternative locations checked by script:**
   - `D:\Tools\ngrok\ngrok.exe`
   - `%USERPROFILE%\AppData\Local\Microsoft\WinGet\Links\ngrok.exe`

#### "Authentication failed" Error
**Problem:** ngrok starts but shows authentication error

**Solution:**
```bash
ngrok config add-authtoken YOUR_TOKEN_FROM_DASHBOARD
```

#### ngrok Shows "Visit Site" Button
**Problem:** ngrok free tier shows a warning page before accessing your site

**This is normal** - users will see a warning page with "Visit Site" button. They need to click it to access your application.

### Web Application Issues

#### Port 5174 Already in Use
**Problem:** "Unable to bind to http://localhost:5174"

**Solutions:**
1. Stop any existing instances:
   ```bash
   stop.bat
   ```
2. Check what's using the port:
   ```bash
   netstat -ano | findstr :5174
   ```
3. Kill the process or change port in `WebLibrary/Properties/launchSettings.json`

#### Database Connection Error
**Problem:** Application starts but can't connect to Oracle database

**Solutions:**
1. Verify Oracle database is running
2. Check connection string in `WebLibrary/appsettings.json`
3. Ensure network connectivity to database server

### Script Issues

#### Script Won't Run in PowerShell
**Problem:** PowerShell doesn't recognize .bat files with `.\deploy.bat`

**Solution:**
```bash
# Use one of these methods:
cmd /c deploy.bat
.\deploy.bat    # Should work in most cases
deploy.bat      # If in PATH
```

## Important Notes

### ngrok Free Tier Limitations
- **Session time:** Tunnels auto-close after 2 hours
- **Concurrent tunnels:** 1 tunnel at a time
- **URL changes:** New random URL each time you restart
- **Warning page:** Users see a "Visit Site" button before accessing

### Security Considerations
- **Database access:** Ensure your Oracle database is properly secured
- **Temporary URLs:** ngrok URLs are temporary - don't use for production
- **Public access:** Your application will be accessible to anyone with the URL

### Performance
- **Network latency:** Requests go through ngrok servers, expect some delay
- **Free tier speed:** May be slower than paid plans
- **Local testing:** Use http://localhost:5174 for fastest local access

## Advanced Configuration

### Custom ngrok Configuration
Create a config file for advanced options:

**Location:** `%USERPROFILE%\.ngrok2\ngrok.yml`

**Example:**
```yaml
authtoken: YOUR_TOKEN_HERE
tunnels:
  web:
    proto: http
    addr: 5174
    # Optional: custom subdomain (requires paid plan)
    # subdomain: myapp
    # Optional: custom domain (requires paid plan)  
    # hostname: myapp.example.com
```

### Modifying Scripts
All scripts use relative paths and can be customized:
- Change port numbers in scripts and `launchSettings.json`
- Modify wait times for slower startup
- Add additional services or commands

## Support

### Getting Help
1. **Check this README** for common issues
2. **ngrok documentation:** https://ngrok.com/docs
3. **ASP.NET Core docs:** https://docs.microsoft.com/aspnet/core

### Useful Commands
```bash
# Check what's running on port 5174
netstat -ano | findstr :5174

# Kill all dotnet processes
taskkill /f /im dotnet.exe

# Kill all ngrok processes  
taskkill /f /im ngrok.exe

# View ngrok dashboard
start http://127.0.0.1:4040
```
