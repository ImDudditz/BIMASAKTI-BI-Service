@echo off
title BI Portal Manager
chcp 65001 >nul
setlocal enabledelayedexpansion

:: Disable Console Window Close Button (Force Option 5 exit to ensure clean port/cache shutdown)
powershell -Command "`$q = [char]34; `$code = 'using System; using System.Runtime.InteropServices; public class WindowHelper { [DllImport(' + `$q + 'kernel32.dll' + `$q + ')] public static extern IntPtr GetConsoleWindow(); [DllImport(' + `$q + 'user32.dll' + `$q + ')] public static extern IntPtr GetSystemMenu(IntPtr hWnd, bool bRevert); [DllImport(' + `$q + 'user32.dll' + `$q + ')] public static extern bool DeleteMenu(IntPtr hMenu, uint uPosition, uint uFlags); }'; Add-Type -TypeDefinition `$code; `$hWnd = [WindowHelper]::GetConsoleWindow(); `$hMenu = [WindowHelper]::GetSystemMenu(`$hWnd, `$false); [void][WindowHelper]::DeleteMenu(`$hMenu, 0xF060, 0x00000000)" >nul 2>&1

set "ROOT_DIR=%~dp0"
set "BACKEND_DIR=%ROOT_DIR%backend"
set "FRONTEND_DIR=%ROOT_DIR%frontend"
set "MANAGER_DIR=%BACKEND_DIR%\BI_Portal_Manager"

:: Read dynamically from appsettings.json
set "MANAGER_PORT="
for /f %%a in ('powershell -Command "(Get-Content '%ROOT_DIR%backend\appsettings.json' | ConvertFrom-Json).Manager.Port" 2^>nul') do set "MANAGER_PORT=%%a"
if "%MANAGER_PORT%"=="" set "MANAGER_PORT=8003"

set "BACKEND_PORT="
for /f %%a in ('powershell -Command "(Get-Content '%ROOT_DIR%backend\appsettings.json' | ConvertFrom-Json).Server.Port" 2^>nul') do set "BACKEND_PORT=%%a"
if "%BACKEND_PORT%"=="" set "BACKEND_PORT=8001"

set "FRONTEND_PORT="
for /f %%a in ('powershell -Command "(Select-String -Path '%ROOT_DIR%frontend\vite.config.js' -Pattern 'port:\s*(\d+)').Matches.Groups[1].Value" 2^>nul') do set "FRONTEND_PORT=%%a"
if "%FRONTEND_PORT%"=="" set "FRONTEND_PORT=8002"

color 07

:MENU
cls
echo.
echo  ┌──────────────────────────────────────────────────────────┐
echo  │             BIMASAKTI BI SERVICE LAUNCHER              │
echo  └──────────────────────────────────────────────────────────┘
echo.
echo   [ System Status ]
echo.

netstat -ano | findstr LISTENING | findstr :%MANAGER_PORT% >nul
if errorlevel 1 ( set "MGR_STATUS=OFFLINE" ) else ( set "MGR_STATUS=ACTIVE " )

netstat -ano | findstr LISTENING | findstr :%BACKEND_PORT% >nul
if errorlevel 1 ( set "BACK_STATUS=OFFLINE" ) else ( set "BACK_STATUS=ACTIVE " )

netstat -ano | findstr LISTENING | findstr :%FRONTEND_PORT% >nul
if errorlevel 1 ( set "FRONT_STATUS=OFFLINE" ) else ( set "FRONT_STATUS=ACTIVE " )

echo    Web Control Panel (Port %MANAGER_PORT%) : [!MGR_STATUS!]
echo    Backend API Core  (Port %BACKEND_PORT%) : [!BACK_STATUS!]
echo    Frontend Portal   (Port %FRONTEND_PORT%) : [!FRONT_STATUS!]
echo.
echo  ────────────────────────────────────────────────────────────
echo   [ Actions ]
echo.
echo    [1] Launch Full Local Development Environment (All Services)
echo    [2] Launch Web Control Panel Only
echo    [3] Clean the Cache
echo    [4] Free the Ports
echo    [5] Check .NET Availability
echo    [6] Exit (Auto-Close, Free Port ^& Clean Cache)
echo.
echo  ────────────────────────────────────────────────────────────
set "choice="
set /p choice="Select an action [1-6]: "

if "%choice%"=="1" goto LAUNCH_ALL
if "%choice%"=="2" goto LAUNCH_WEB
if "%choice%"=="3" goto CLEAN_CACHE
if "%choice%"=="4" goto FREE_PORTS
if "%choice%"=="5" goto CHECK_DOTNET
if "%choice%"=="6" goto FORCE_EXIT
goto MENU

:LAUNCH_ALL
cls
echo.
echo  ============================================================
echo   LAUNCHING FULL LOCAL DEVELOPMENT ENVIRONMENT
echo  ============================================================
echo.

:: 1. Start Main Backend API
netstat -ano | findstr LISTENING | findstr :%BACKEND_PORT% >nul
if not errorlevel 1 (
    echo  Backend Port %BACKEND_PORT% is already active. Stopping...
    for /f "tokens=5" %%a in ('netstat -aon ^| findstr LISTENING ^| findstr :%BACKEND_PORT%') do ( taskkill /F /T /PID %%a >nul 2>&1 )
)
echo  Starting Main Backend API on Port %BACKEND_PORT%...
start "BI Portal Backend Core" /D "%BACKEND_DIR%" cmd /c "dotnet run"

:: 2. Start Frontend Dev Server
netstat -ano | findstr LISTENING | findstr :%FRONTEND_PORT% >nul
if not errorlevel 1 (
    echo  Frontend Port %FRONTEND_PORT% is already active. Stopping...
    for /f "tokens=5" %%a in ('netstat -aon ^| findstr LISTENING ^| findstr :%FRONTEND_PORT%') do ( taskkill /F /T /PID %%a >nul 2>&1 )
)
echo  Starting Vue Frontend Dev Server on Port %FRONTEND_PORT%...
start "BI Portal Frontend" /D "%FRONTEND_DIR%" cmd /c "npm run dev"

goto LAUNCH_WEB

:LAUNCH_WEB
cls
echo.
echo  ============================================================
echo   LAUNCHING WEB CONTROL PANEL
echo  ============================================================
echo.
:: Check if already running, free it first
netstat -ano | findstr LISTENING | findstr :%MANAGER_PORT% >nul
if not errorlevel 1 (
    echo  Port %MANAGER_PORT% is already active. Stopping existing session...
    for /f "tokens=5" %%a in ('netstat -aon ^| findstr LISTENING ^| findstr :%MANAGER_PORT%') do ( taskkill /F /T /PID %%a >nul 2>&1 )
)

echo  Starting Web Manager Service on Port %MANAGER_PORT%...
start "BI Portal Manager Core" /D "%MANAGER_DIR%" cmd /c "dotnet run > manager_startup.log 2>&1"

echo  Waiting for service initialization...
set "BOOT_SUCCESS=0"
<nul set /p "= Booting services: ["

:: Poll every 500ms, up to 24 times (12 seconds max)
for /L %%i in (1,1,24) do (
    if !BOOT_SUCCESS!==0 (
        netstat -ano | findstr LISTENING | findstr :%MANAGER_PORT% >nul
        if not errorlevel 1 (
            set "BOOT_SUCCESS=1"
            <nul set /p "=■■■"
        ) else (
            <nul set /p "=■"
            powershell -Command "Start-Sleep -m 500"
        )
    )
)
echo ] Done.
echo.

:: Verify if running
if !BOOT_SUCCESS!==0 (
    echo  [x] The server failed to respond on Port %MANAGER_PORT% within 12 seconds.
    echo  [x] Please review '%MANAGER_DIR%\manager_startup.log' for details.
    echo.
    pause
) else (
    echo  [+] Web Control Panel is online.

    :: Extract and display the generated password if it exists
    findstr /C:"A secure random password has been generated:" "%MANAGER_DIR%\manager_startup.log" >nul 2>&1
    if not errorlevel 1 (
        echo.
        echo  ============================================================
        echo   [!] NEW TEMPORARY SUPERADMIN PASSWORD [!]
        for /f "tokens=*" %%p in ('findstr /C:"A secure random password has been generated:" "%MANAGER_DIR%\manager_startup.log"') do (
            set "line=%%p"
            set "pwd=!line:*A secure random password has been generated: =!"
            echo   Password: !pwd!
        )
        echo  ============================================================
        echo  Please copy this password. You will need it to log in.
        echo.
        pause
    )

    echo  [+] Redirecting your browser to http://localhost:%MANAGER_PORT% in Incognito/Private mode ...
    
    set "BrowserProgId="
    for /f "tokens=2* skip=2" %%a in ('reg query HKCU\Software\Microsoft\Windows\Shell\Associations\UrlAssociations\http\UserChoice /v ProgId 2^>nul') do set "BrowserProgId=%%b"
    
    echo !BrowserProgId! | findstr /i "Chrome" >nul
    if not errorlevel 1 (
        start chrome -incognito "http://localhost:%MANAGER_PORT%"
        goto BROWSER_DONE
    )
    echo !BrowserProgId! | findstr /i "Edge" >nul
    if not errorlevel 1 (
        start msedge -inprivate "http://localhost:%MANAGER_PORT%"
        goto BROWSER_DONE
    )
    echo !BrowserProgId! | findstr /i "Firefox" >nul
    if not errorlevel 1 (
        start firefox -private-window "http://localhost:%MANAGER_PORT%"
        goto BROWSER_DONE
    )
    echo !BrowserProgId! | findstr /i "Brave" >nul
    if not errorlevel 1 (
        start brave -incognito "http://localhost:%MANAGER_PORT%"
        goto BROWSER_DONE
    )
    
    :: Fallback if unknown browser
    start "" "http://localhost:%MANAGER_PORT%"
    
    :BROWSER_DONE
    timeout /t 2 >nul
)
goto MENU

:CLEAN_CACHE
cls
echo.
echo  ============================================================
echo   CLEANING SYSTEM CACHE
echo  ============================================================
echo.

:: Animated Loading Bar
powershell -Command "Write-Host -NoNewline ' Clearing temporary folders: ['; 1..20 | %% { Write-Host -NoNewline '■'; Start-Sleep -m 60 }; Write-Host '] Done.'"
echo.

set "cleaned=0"
for /d /r "%BACKEND_DIR%" %%d in (bin obj) do (
    if exist "%%d" (
        rmdir /s /q "%%d" >nul 2>&1
        echo   - Cleared: %%d
        set /a cleaned+=1
    )
)

if exist "%FRONTEND_DIR%\node_modules\.vite" (
    rmdir /s /q "%FRONTEND_DIR%\node_modules\.vite" >nul 2>&1
    echo   - Cleared: %FRONTEND_DIR%\node_modules\.vite
    set /a cleaned+=1
)

echo.
if !cleaned! GTR 0 (
    echo  [+] Cache clean-up completed successfully.
) else (
    echo  [+] No temporary directories were found. The system is already clean.
)
echo.
pause
goto MENU

:FREE_PORTS
cls
echo.
echo  ============================================================
echo   FREEING ALL NETWORK PORTS
echo  ============================================================
echo.

:: Animated Loading Bar
powershell -Command "Write-Host -NoNewline ' Terminating active ports:   ['; 1..20 | %% { Write-Host -NoNewline '■'; Start-Sleep -m 50 }; Write-Host '] Done.'"
echo.

set "freed=0"
for %%p in (%MANAGER_PORT% %BACKEND_PORT% %FRONTEND_PORT%) do (
    netstat -ano | findstr LISTENING | findstr :%%p >nul
    if not errorlevel 1 (
        for /f "tokens=5" %%a in ('netstat -aon ^| findstr LISTENING ^| findstr :%%p') do (
            taskkill /F /T /PID %%a >nul 2>&1
            echo   - Freed Port %%p (PID: %%a)
            set /a freed+=1
        )
    )
)

echo.
if !freed! GTR 0 (
    echo  [+] Network ports successfully cleared.
) else (
    echo  [+] All ports are already free and inactive.
)
echo.
pause
goto MENU

:CHECK_DOTNET
cls
echo.
echo  ============================================================
echo   CHECKING .NET AVAILABILITY & HEALTH
echo  ============================================================
echo.

:: Animated Loading Bar
powershell -Command "Write-Host -NoNewline ' Running environment diagnostic: ['; 1..20 | %% { Write-Host -NoNewline '■'; Start-Sleep -m 40 }; Write-Host '] Verified.'"
echo.
echo.

dotnet --version >nul 2>&1
if errorlevel 1 (
    echo  [x] .NET CLI is NOT installed or not present in your system PATH.
    echo      Please install .NET Core SDK 8.0 or newer to run the server.
) else (
    for /f "delims=" %%v in ('dotnet --version') do set "DOTNET_VERSION=%%v"
    echo  [+] .NET CLI Availability  : OK
    echo  [+] Installed CLI Version  : !DOTNET_VERSION!
    echo.
    echo  [+] Checking Installed SDK Runtimes...
    dotnet --list-sdks
    echo.
    echo  [+] Checking Manager Project Health...
    if exist "%MANAGER_DIR%\BI_Portal_Manager.csproj" (
        echo      Manager Project file found. Building dry-run test...
        dotnet build "%MANAGER_DIR%\BI_Portal_Manager.csproj" --no-restore >nul 2>&1
        if errorlevel 1 (
            echo  [x] Project Build Health: FAILED. Run option [2] to clean cache and try again.
        ) else (
            echo  [+] Project Build Health: STABLE (Ready to run)
        )
    ) else (
        echo  [x] Error: Manager project file not found at %MANAGER_DIR%.
    )
)
echo.
pause
goto MENU

:FORCE_EXIT
cls
echo.
echo  ============================================================
echo   SHUTTING DOWN SYSTEM
echo  ============================================================
echo.

:: Exit loader
powershell -Command "Write-Host -NoNewline ' Force closing active sessions: ['; 1..25 | %% { Write-Host -NoNewline '■'; Start-Sleep -m 30 }; Write-Host '] Closed.'"
echo.

:: Close open browser windows/tabs for the BI Portal Manager and Frontend Portal
powershell -Command "Get-Process | Where-Object { `$_.MainWindowTitle -like '*BI Portal*' -or `$_.MainWindowTitle -like '*Financial Reports*' } | ForEach-Object { `$_.CloseMainWindow() }" >nul 2>&1

:: 1. Free ports (close any running manager, backend, frontend)
for %%p in (%MANAGER_PORT% %BACKEND_PORT% %FRONTEND_PORT%) do (
    netstat -ano | findstr LISTENING | findstr :%%p >nul
    if not errorlevel 1 (
        for /f "tokens=5" %%a in ('netstat -aon ^| findstr LISTENING ^| findstr :%%p') do (
            taskkill /F /T /PID %%a >nul 2>&1
        )
    )
)

:: 2. Clean cache
for /d /r "%BACKEND_DIR%" %%d in (bin obj) do (
    if exist "%%d" rmdir /s /q "%%d" >nul 2>&1
)
if exist "%FRONTEND_DIR%\node_modules\.vite" (
    rmdir /s /q "%FRONTEND_DIR%\node_modules\.vite" >nul 2>&1
)

echo.
echo  [+] All systems shut down, ports freed, and cache cleaned.
echo  [+] Goodbye.
timeout /t 2 >nul
exit
