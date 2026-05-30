@echo off
title Bimasakti BI Web Manager Launcher
:: Force UTF-8 encoding to support nice box-drawing characters
chcp 65001 >nul
setlocal enabledelayedexpansion

:: Define local directory paths using portable relative notation
set "ROOT_DIR=%~dp0"
set "BACKEND_DIR=%ROOT_DIR%backend"
set "FRONTEND_DIR=%ROOT_DIR%frontend"
set "GODMODE_FILE=%BACKEND_DIR%\engines\.god_mode_enabled"

:: Parse defined ports dynamically from settings files
set "BACKEND_PORT="
for /f %%a in ('powershell -Command "(Get-Content '%ROOT_DIR%backend\appsettings.json' | ConvertFrom-Json).Server.Port" 2^>nul') do set "BACKEND_PORT=%%a"
if "%BACKEND_PORT%"=="" set "BACKEND_PORT=8001"

set "FRONTEND_PORT="
for /f %%a in ('powershell -Command "$txt = Get-Content '%ROOT_DIR%frontend\vite.config.js' -Raw; if ($txt -match 'port:\s*(\d+)') { $Matches[1] } else { '5173' }" 2^>nul') do set "FRONTEND_PORT=%%a"
if "%FRONTEND_PORT%"=="" set "FRONTEND_PORT=5173"

:: Set dynamic developer theme (Cyan/Aqua on Black)
color 0B

:MENU
cls
echo ====================================================================
echo   ╔══════════════════════════════════════════════════════════════╗
echo   ║                 BIMASAKTI BI SERVICE MANAGER                 ║
echo   ║               --- CLI Interactive Dashboard ---              ║
echo   ╚══════════════════════════════════════════════════════════════╝
echo ====================================================================
echo.
echo  [SYSTEM LIVE STATUS MONITOR]
echo  -------------------------------------------------------------

:: 1. Check if Web Manager (Port 5050) is active
netstat -ano | findstr LISTENING | findstr :5050 >nul
if errorlevel 1 (
    set "MGR_STATUS=OFFLINE"
) else (
    set "MGR_STATUS=ACTIVE "
)

:: 2. Check if Backend API (Port !BACKEND_PORT!) is active
netstat -ano | findstr LISTENING | findstr :!BACKEND_PORT! >nul
if errorlevel 1 (
    set "BACK_STATUS=OFFLINE"
) else (
    set "BACK_STATUS=ACTIVE "
)

:: 3. Check if Frontend Web (Port !FRONTEND_PORT!) is active
netstat -ano | findstr LISTENING | findstr :!FRONTEND_PORT! >nul
if errorlevel 1 (
    set "FRONT_STATUS=OFFLINE"
) else (
    set "FRONT_STATUS=ACTIVE "
)

:: 4. Check God Mode status
if exist "%GODMODE_FILE%" (
    set "GOD_STATUS=ENABLED  (Bypassing Admin Filter locks)"
) else (
    set "GOD_STATUS=DISABLED"
)

:: 5. Extract Local IP Address using robust PowerShell commands
set "LOCAL_IP="
for /f "delims=" %%a in ('powershell -Command "Get-NetIPAddress -InterfaceAlias 'Wi-Fi', 'Ethernet' -AddressFamily IPv4 | Select-Object -ExpandProperty IPAddress -First 1" 2^>nul') do set "LOCAL_IP=%%a"
if "%LOCAL_IP%"=="" (
    for /f "delims=" %%a in ('powershell -Command "Get-NetIPAddress -AddressFamily IPv4 | Where-Object IPAddress -ne '127.0.0.1' | Select-Object -ExpandProperty IPAddress -First 1" 2^>nul') do set "LOCAL_IP=%%a"
)
if "%LOCAL_IP%"=="" set "LOCAL_IP=127.0.0.1"

:: 6. Get local network hostname alias
set "COOL_ALIAS=%COMPUTERNAME%.local"

echo   » Web Manager (Port 5050) : [!MGR_STATUS!]
echo   » Backend API (Port !BACKEND_PORT!) : [!BACK_STATUS!]
echo   » Frontend App (Port !FRONTEND_PORT!): [!FRONT_STATUS!]
echo   » Developer God Mode      : [!GOD_STATUS!]
echo   » Local Network Endpoint  : http://!LOCAL_IP!:5050  (Alias: http://!COOL_ALIAS!:5050)
echo  -------------------------------------------------------------
echo.
echo  [DASHBOARD LAUNCH CONTROLS]
echo   ┌──────────────────────────────────────────────────────────┐
echo   │ [1] Start Web Manager (Runs ^& Launches Web Interface)    │
echo   │ [2] Start Dev Services Directly (Separate Windows)       │
echo   │ [3] Deep Clean ^& Optimize Cache (Fix compile/run issues) │
echo   │ [4] Force Free Port Locks (Kill stuck Node/Dotnet tasks) │
echo   │ [5] Toggle Developer God Mode                            │
echo   │ [6] Run Environment Diagnostics                           │
echo   │ [7] Exit                                                 │
echo   └──────────────────────────────────────────────────────────┘
echo.
set "choice="
set /p choice="Enter your selection [1-7]: "

if "%choice%"=="1" goto START_MANAGER
if "%choice%"=="2" goto START_DEV_SERVICES
if "%choice%"=="3" goto CLEAN_SYSTEM
if "%choice%"=="4" goto KILL_PORTS
if "%choice%"=="5" goto TOGGLE_GOD_MODE
if "%choice%"=="6" goto RUN_DIAGNOSTICS
if "%choice%"=="7" goto EXIT_LAUNCHER
goto MENU


:START_MANAGER
cls
echo ====================================================================
echo   [OPTION 1] Launching Bimasakti BI Web Manager...
echo ====================================================================
echo.
echo  Step 1: Freeing port 5050 if occupied to prevent launch errors...
netstat -ano | findstr LISTENING | findstr :5050 >nul
if not errorlevel 1 (
    echo  Port 5050 is occupied! Terminating the blocking process...
    for /f "tokens=5" %%a in ('netstat -aon ^| findstr LISTENING ^| findstr :5050') do (
        taskkill /F /T /PID %%a >nul 2>&1
    )
)

echo  Step 2: Restoring and running Manager via Dotnet CLI...
echo  (The manager will open the browser at http://localhost:5050 automatically)
echo.
    start "Bimasakti Manager" /D "%BACKEND_DIR%\manager" cmd /k "dotnet run > manager_startup.log 2>&1"
    timeout /t 3 >nul
    start "" "http://localhost:5050"
    goto MENU


:START_DEV_SERVICES
cls
echo ====================================================================
echo   [OPTION 2] Launching Developer Services (Separate Windows)
echo ====================================================================
echo.
echo  Step 1: Releasing ports (!BACKEND_PORT!, !FRONTEND_PORT!) to avoid server conflicts...
:: Port !BACKEND_PORT! check
netstat -ano | findstr LISTENING | findstr :!BACKEND_PORT! >nul
if not errorlevel 1 (
    echo  Port !BACKEND_PORT! is active. Releasing port !BACKEND_PORT!...
    for /f "tokens=5" %%a in ('netstat -aon ^| findstr LISTENING ^| findstr :!BACKEND_PORT!') do (
        taskkill /F /T /PID %%a >nul 2>&1
    )
)
:: Port !FRONTEND_PORT! check
netstat -ano | findstr LISTENING | findstr :!FRONTEND_PORT! >nul
if not errorlevel 1 (
    echo  Port !FRONTEND_PORT! is active. Releasing port !FRONTEND_PORT!...
    for /f "tokens=5" %%a in ('netstat -aon ^| findstr LISTENING ^| findstr :!FRONTEND_PORT!') do (
        taskkill /F /T /PID %%a >nul 2>&1
    )
)

echo  Step 2: Spawning C# Backend API Service (Port !BACKEND_PORT!) in separate console...
start "Bimasakti C# Backend API" /D "%BACKEND_DIR%" cmd /k "color 0A & echo Running Bimasakti API Backend... & dotnet run --project BiPortal.csproj"

echo  Step 3: Spawning Vue Frontend Web Server (Port !FRONTEND_PORT!) in separate console...
start "Bimasakti Vue Frontend" /D "%FRONTEND_DIR%" cmd /k "color 0E & echo Running Bimasakti Frontend... & npm run dev"

echo.
echo  ==============================================================
echo  ✔ Both services have been launched in separate terminal windows!
echo  ==============================================================
echo  » C# Backend API listening on:  http://localhost:!BACKEND_PORT!
echo  » Vue Frontend Web listening on: http://localhost:!FRONTEND_PORT!
echo  --------------------------------------------------------------
echo.
echo  Press any key to return to the dashboard menu...
pause >nul
goto MENU


:CLEAN_SYSTEM
cls
echo ====================================================================
echo   [OPTION 3] Deep Cleaning and Re-optimizing Cache/Builds
echo ====================================================================
echo.
echo  [1/4] Cleaning C# Backend directories (bin / obj)...
for /d /r "%BACKEND_DIR%" %%d in (bin obj) do (
    if exist "%%d" (
        echo  Removing: %%d
        rmdir /s /q "%%d" >nul 2>&1
    )
)

echo  [2/4] Cleaning Vite compiler cache...
if exist "%FRONTEND_DIR%\node_modules\.vite" (
    echo  Removing: %FRONTEND_DIR%\node_modules\.vite
    rmdir /s /q "%FRONTEND_DIR%\node_modules\.vite" >nul 2>&1
)

echo  [3/4] Restoring C# NuGet Dependencies...
cd /d "%BACKEND_DIR%"
dotnet restore
if errorlevel 1 (
    echo.
    echo  [WARNING] C# NuGet restore encountered errors. Please check your internet connection or NuGet configuration.
) else (
    echo  ✔ C# dependencies successfully restored!
)

echo  [4/4] Optimizing C# Project Build...
dotnet build --configuration Debug --no-restore
if errorlevel 1 (
    echo.
    echo  [WARNING] Build failed. Please inspect compiler errors above.
) else (
    echo  ✔ C# Project built successfully!
)

echo.
echo  ==============================================================
echo  ✔ System deep clean and optimization complete!
echo  ==============================================================
echo.
echo  Press any key to return to the dashboard menu...
pause >nul
goto MENU


:KILL_PORTS
cls
echo ====================================================================
echo   [OPTION 4] Port Liberator - Forcefully Terminate Port Locks
echo ====================================================================
echo.
echo  This tool terminates any background process locking the dev ports:
echo  - Port 5050 (BI Web Manager)
echo  - Port !BACKEND_PORT! (C# Web API Backend)
echo  - Port !FRONTEND_PORT! (Vue Frontend)
echo.

set "KILLED_ANY=0"

:: 5050
netstat -ano | findstr LISTENING | findstr :5050 >nul
if not errorlevel 1 (
    echo  [+] Port 5050: Found active process! Terminating...
    for /f "tokens=5" %%a in ('netstat -aon ^| findstr LISTENING ^| findstr :5050') do (
        taskkill /F /T /PID %%a >nul 2>&1
        echo      ✔ PID %%a successfully terminated.
    )
    set "KILLED_ANY=1"
) else (
    echo  [-] Port 5050: Clean (No active process)
)

:: !BACKEND_PORT!
netstat -ano | findstr LISTENING | findstr :!BACKEND_PORT! >nul
if not errorlevel 1 (
    echo  [+] Port !BACKEND_PORT!: Found active process! Terminating...
    for /f "tokens=5" %%a in ('netstat -aon ^| findstr LISTENING ^| findstr :!BACKEND_PORT!') do (
        taskkill /F /T /PID %%a >nul 2>&1
        echo      ✔ PID %%a successfully terminated.
    )
    set "KILLED_ANY=1"
) else (
    echo  [-] Port !BACKEND_PORT!: Clean (No active process)
)

:: !FRONTEND_PORT!
netstat -ano | findstr LISTENING | findstr :!FRONTEND_PORT! >nul
if not errorlevel 1 (
    echo  [+] Port !FRONTEND_PORT!: Found active process! Terminating...
    for /f "tokens=5" %%a in ('netstat -aon ^| findstr LISTENING ^| findstr :!FRONTEND_PORT!') do (
        taskkill /F /T /PID %%a >nul 2>&1
        echo      ✔ PID %%a successfully terminated.
    )
    set "KILLED_ANY=1"
) else (
    echo  [-] Port !FRONTEND_PORT!: Clean (No active process)
)

echo.
if "!KILLED_ANY!"=="1" (
    echo  ==============================================================
    echo  ✔ All locking processes have been terminated! Ports are now free.
    echo  ==============================================================
) else (
    echo  ==============================================================
    echo  ✔ No zombie processes found. All developer ports are free!
    echo  ==============================================================
)
echo.
echo  Press any key to return to the dashboard menu...
pause >nul
goto MENU


:TOGGLE_GOD_MODE
cls
echo ====================================================================
echo   [OPTION 5] Toggle Developer God Mode (Admin Bypass)
echo ====================================================================
echo.
if exist "%GODMODE_FILE%" (
    echo  God Mode is currently [ENABLED].
    echo  This bypasses normal administrative filters.
    echo.
    echo  Deactivating God Mode...
    del /f /q "%GODMODE_FILE%" >nul 2>&1
    if errorlevel 1 (
        echo  [ERROR] Failed to delete God Mode file. Check file permissions.
    ) else (
        echo  ✔ God Mode successfully disabled!
    )
) else (
    echo  God Mode is currently [DISABLED].
    echo.
    echo  Activating God Mode...
    :: Create directory if missing
    if not exist "%BACKEND_DIR%\engines" mkdir "%BACKEND_DIR%\engines"
    echo enabled > "%GODMODE_FILE%"
    if exist "%GODMODE_FILE%" (
        echo  ✔ God Mode successfully enabled! Admin filters are now bypassed.
    ) else (
        echo  [ERROR] Failed to write God Mode file. Check file permissions.
    )
)
echo.
echo  Press any key to return to the dashboard menu...
pause >nul
goto MENU


:RUN_DIAGNOSTICS
cls
echo ====================================================================
echo   [OPTION 6] Environment and Dependency Health Diagnostics
echo ====================================================================
echo.
echo  [1] Software Tools Check:
echo  --------------------------------------------------------------

:: Check .NET
where dotnet >nul 2>&1
if errorlevel 1 (
    echo   [-] .NET CLI:          NOT FOUND! (Install from https://dotnet.microsoft.com/)
) else (
    for /f "delims=" %%v in ('dotnet --version') do set "DOTNET_VER=%%v"
    echo   [+] .NET SDK:          Found (v!DOTNET_VER!)
)

:: Check Node
where node >nul 2>&1
if errorlevel 1 (
    echo   [-] Node.js:           NOT FOUND! (Install from https://nodejs.org/)
) else (
    for /f "delims=" %%v in ('node --version') do set "NODE_VER=%%v"
    echo   [+] Node.js runtime:   Found (!NODE_VER!)
)

:: Check NPM
where npm >nul 2>&1
if errorlevel 1 (
    echo   [-] NPM:               NOT FOUND!
) else (
    for /f "delims=" %%v in ('npm --version') do set "NPM_VER=%%v"
    echo   [+] NPM package mgr:   Found (v!NPM_VER!)
)

echo.
echo  [2] Directory Structure Check:
echo  --------------------------------------------------------------
if exist "%BACKEND_DIR%" (
    echo   [+] /backend folder:   Found
) else (
    echo   [-] /backend folder:   MISSING!
)
if exist "%FRONTEND_DIR%" (
    echo   [+] /frontend folder:  Found
) else (
    echo   [-] /frontend folder:  MISSING!
)
if exist "%BACKEND_DIR%\manager\manager.csproj" (
    echo   [+] Web Manager csprj: Found
) else (
    echo   [-] Web Manager csprj: MISSING! (%BACKEND_DIR%\manager\manager.csproj)
)
if exist "%FRONTEND_DIR%\package.json" (
    echo   [+] Vue package.json:  Found
) else (
    echo   [-] Vue package.json:  MISSING! (%FRONTEND_DIR%\package.json)
)

echo.
echo  ==============================================================
echo  ✔ Health diagnostics check complete!
echo  ==============================================================
echo.
echo  Press any key to return to the dashboard menu...
pause >nul
goto MENU


:EXIT_LAUNCHER
cls
echo.
echo  Thank you for using Bimasakti BI Web Manager Launcher!
echo  Have a productive coding session.
echo.
timeout /t 2 >nul
exit
