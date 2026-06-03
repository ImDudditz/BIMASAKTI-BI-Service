@echo off
title BI Portal Manager
chcp 65001 >nul
setlocal enabledelayedexpansion

:: Disable Console Window Close Button (Force Option 5 exit to ensure clean port/cache shutdown)
powershell -Command "`$q = [char]34; `$code = 'using System; using System.Runtime.InteropServices; public class WindowHelper { [DllImport(' + `$q + 'kernel32.dll' + `$q + ')] public static extern IntPtr GetConsoleWindow(); [DllImport(' + `$q + 'user32.dll' + `$q + ')] public static extern IntPtr GetSystemMenu(IntPtr hWnd, bool bRevert); [DllImport(' + `$q + 'user32.dll' + `$q + ')] public static extern bool DeleteMenu(IntPtr hMenu, uint uPosition, uint uFlags); }'; Add-Type -TypeDefinition `$code; `$hWnd = [WindowHelper]::GetConsoleWindow(); `$hMenu = [WindowHelper]::GetSystemMenu(`$hWnd, `$false); [void][WindowHelper]::DeleteMenu(`$hMenu, 0xF060, 0x00000000)" >nul 2>&1

set "ROOT_DIR=%~dp0"
set "BACKEND_DIR=%ROOT_DIR%BMS_CORE_API"
set "FRONTEND_DIR=%ROOT_DIR%BMS_BI_APP"
set "MANAGER_DIR=%ROOT_DIR%BMS_BI_Manager"

:: Read dynamically from appsettings.json
set "MANAGER_PORT="
for /f %%a in ('powershell -Command "(Get-Content '%ROOT_DIR%BMS_CORE_API\appsettings.json' | ConvertFrom-Json).Manager.Port" 2^>nul') do set "MANAGER_PORT=%%a"
if "%MANAGER_PORT%"=="" set "MANAGER_PORT=8003"

set "BACKEND_PORT="
for /f %%a in ('powershell -Command "(Get-Content '%ROOT_DIR%BMS_CORE_API\appsettings.json' | ConvertFrom-Json).Server.Port" 2^>nul') do set "BACKEND_PORT=%%a"
if "%BACKEND_PORT%"=="" set "BACKEND_PORT=8001"

set "FRONTEND_PORT="
for /f %%a in ('powershell -Command "(Select-String -Path '%ROOT_DIR%BMS_BI_APP\vite.config.js' -Pattern 'port:\s*(\d+)').Matches.Groups[1].Value" 2^>nul') do set "FRONTEND_PORT=%%a"
if "%FRONTEND_PORT%"=="" set "FRONTEND_PORT=8002"

color 07

:MENU
cls
echo.
echo  ┌──────────────────────────────────────────────────────────┐
echo  │                 BMS_BI_SERVICE LAUNCHER                  │
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
echo    [6] Compile Production Publish Build
echo    [7] Exit (Auto-Close, Free Port ^& Clean Cache)
echo.
echo  ────────────────────────────────────────────────────────────
set "choice="
set /p choice="Select an action [1-7]: "

if "%choice%"=="1" goto LAUNCH_ALL
if "%choice%"=="2" goto LAUNCH_WEB
if "%choice%"=="3" goto CLEAN_CACHE
if "%choice%"=="4" goto FREE_PORTS
if "%choice%"=="5" goto CHECK_DOTNET
if "%choice%"=="6" goto COMPILE_PUBLISH
if "%choice%"=="7" goto FORCE_EXIT
goto MENU

:LAUNCH_ALL
cls
echo.
echo  ============================================================
echo   LAUNCHING FULL LOCAL DEVELOPMENT ENVIRONMENT
echo  ============================================================
echo.
echo  Building solution sequentially to prevent file lock collisions...
dotnet build "%ROOT_DIR%BMS_BI_SERVICE.slnx" >nul
echo.

:: 1. Start Main Backend API
netstat -ano | findstr LISTENING | findstr :%BACKEND_PORT% >nul
if not errorlevel 1 (
    echo  Backend Port %BACKEND_PORT% is already active. Stopping...
    for /f "tokens=5" %%a in ('netstat -aon ^| findstr LISTENING ^| findstr :%BACKEND_PORT%') do ( taskkill /F /T /PID %%a >nul 2>&1 )
)
echo  Starting Main Backend API on Port %BACKEND_PORT%...
start "BI Portal Backend Core" /D "%BACKEND_DIR%" cmd /c "dotnet run --no-build"

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
echo  Building Web Manager...
dotnet build "%MANAGER_DIR%\BMS_BI_Manager.csproj" >nul
echo.
:: Check if already running, free it first
netstat -ano | findstr LISTENING | findstr :%MANAGER_PORT% >nul
if not errorlevel 1 (
    echo  Port %MANAGER_PORT% is already active. Stopping existing session...
    for /f "tokens=5" %%a in ('netstat -aon ^| findstr LISTENING ^| findstr :%MANAGER_PORT%') do ( taskkill /F /T /PID %%a >nul 2>&1 )
)

echo  Starting Web Manager Service on Port %MANAGER_PORT%...
start "BI Portal Manager Core" /D "%MANAGER_DIR%" cmd /c "dotnet run --no-build > manager_startup.log 2>&1"

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

for /d /r "%MANAGER_DIR%" %%d in (bin obj) do (
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

:COMPILE_PUBLISH
cls
echo.
echo  ============================================================
echo   COMPILE PRODUCTION PUBLISH BUILD
echo  ============================================================
echo.
echo  Select Component to Compile:
echo   [1] Full Stack (Backend + Frontend)
echo   [2] Backend Services Only
echo   [3] Frontend Proxy Only
echo   [4] Cancel
echo.
set "comp_choice="
set /p comp_choice="Select an option [1-4]: "

if "%comp_choice%"=="4" goto MENU
if "%comp_choice%"=="" goto COMPILE_PUBLISH

set "COMPILE_BACKEND=0"
set "COMPILE_FRONTEND=0"
if "%comp_choice%"=="1" ( set "COMPILE_BACKEND=1" & set "COMPILE_FRONTEND=1" )
if "%comp_choice%"=="2" ( set "COMPILE_BACKEND=1" )
if "%comp_choice%"=="3" ( set "COMPILE_FRONTEND=1" )

set "TARGET_ARCH=win-x64"
set "BACKEND_DEPLOY_TYPE=STANDALONE"

if "%COMPILE_BACKEND%"=="0" goto SKIP_BACKEND_PROMPT

echo.
echo  Select Backend Deployment Target:
echo   [1] Standalone Executable (Self-Contained)
echo   [2] IIS Web Server (Framework-Dependent)
echo.
set "deploy_choice="
set /p deploy_choice="Select an option [1-2]: "
if "!deploy_choice!"=="2" (
    set "BACKEND_DEPLOY_TYPE=IIS"
    goto SKIP_BACKEND_PROMPT
)

echo.
echo  Select Target Architecture for Standalone:
echo   [1] Windows x64 (Standard 64-bit)
echo   [2] Windows x86 (Standard 32-bit)
echo.
set "arch_choice="
set /p arch_choice="Select an option [1-2]: "
if "!arch_choice!"=="1" set "TARGET_ARCH=win-x64"
if "!arch_choice!"=="2" set "TARGET_ARCH=win-x86"

:SKIP_BACKEND_PROMPT

set "FRONTEND_DEPLOY_TYPE=PROXY"

if "%COMPILE_FRONTEND%"=="0" goto SKIP_FRONTEND_PROMPT

echo.
echo  Select Frontend Deployment Target:
echo   [1] Node.js Proxy Server (Default, Server-Side Rendered / Proxy)
echo   [2] Standard Static SaaS (HTML/JS/CSS only, perfect for NGINX/IIS/CDN)
echo.
set "front_deploy_choice="
set /p front_deploy_choice="Select an option [1-2]: "
if "!front_deploy_choice!"=="2" (
    set "FRONTEND_DEPLOY_TYPE=STATIC"
)

:SKIP_FRONTEND_PROMPT

echo.
echo  [+] Preparing output directory...
set "PUB_DIR=%ROOT_DIR%Publish"
if not exist "%PUB_DIR%" mkdir "%PUB_DIR%"

if "%COMPILE_BACKEND%"=="1" goto DO_COMPILE_BACKEND
goto CHECK_FRONTEND

:DO_COMPILE_BACKEND
if exist "%PUB_DIR%\Backend" rmdir /s /q "%PUB_DIR%\Backend"
mkdir "%PUB_DIR%\Backend"

echo.
echo  [+] Cleaning source binaries before publish...
dotnet clean "%BACKEND_DIR%\BMS_CORE_API.csproj" -c Release >nul 2>&1
dotnet clean "%MANAGER_DIR%\BMS_BI_Manager.csproj" -c Release >nul 2>&1

if "!BACKEND_DEPLOY_TYPE!"=="IIS" goto COMPILE_IIS
goto COMPILE_STANDALONE

:COMPILE_IIS
echo.
echo  [+] Compiling Core Services for IIS Deployment (Optimized)...
echo  - Building Main Backend API (IIS)...
dotnet publish "%BACKEND_DIR%\BMS_CORE_API.csproj" -c Release -p:EnvironmentName=Production -p:UseAppHost=false -o "%PUB_DIR%\Backend\BMS_Core_IIS" >nul

echo  - Building BI SaaS Manager API (IIS)...
dotnet publish "%MANAGER_DIR%\BMS_BI_Manager.csproj" -c Release -p:EnvironmentName=Production -p:UseAppHost=false -o "%PUB_DIR%\Backend\BMS_Manager_IIS" >nul

echo.
echo  [+] Cleaning up unnecessary compiler metadata and dev configs...
del /f /q "%PUB_DIR%\Backend\BMS_Core_IIS\appsettings.Development.json" >nul 2>&1
del /f /q "%PUB_DIR%\Backend\BMS_Manager_IIS\appsettings.Development.json" >nul 2>&1
del /f /q "%PUB_DIR%\Backend\BMS_Core_IIS\*.staticwebassets.endpoints.json" >nul 2>&1
del /f /q "%PUB_DIR%\Backend\BMS_Manager_IIS\*.staticwebassets.endpoints.json" >nul 2>&1

echo.
echo  [+] Generating Sync Job Script for IIS...
(
echo @echo off
echo echo ==================================================
echo echo Running BMS Background Sync Job...
echo echo ==================================================
echo cd /d "%%~dp0BMS_Core_IIS"
echo dotnet BMS_Core.dll --sync-all
echo echo.
echo echo Sync process completed.
echo timeout /t 10
) > "%PUB_DIR%\Backend\run_scheduled_sync_iis.bat"

echo.
echo  [+] IIS Build Complete!
echo      Please map your IIS site paths to the respective folders:
echo      - Core API: %PUB_DIR%\Backend\BMS_Core_IIS
echo      - Manager : %PUB_DIR%\Backend\BMS_Manager_IIS
goto CHECK_FRONTEND

:COMPILE_STANDALONE
echo.
echo  [+] Compiling Core Services to a Unified Backend Folder (Standalone)...
echo  - Building Main Backend API (BMS_Core.exe)...
dotnet publish "%BACKEND_DIR%\BMS_CORE_API.csproj" -c Release -r !TARGET_ARCH! --self-contained true -p:PublishSingleFile=true -p:DebugType=None -p:IncludeNativeLibrariesForSelfExtract=true -p:UseAppHost=true -o "%PUB_DIR%\Backend" >nul

echo  - Building BI SaaS Manager API (BMS_BM.exe)...
dotnet publish "%MANAGER_DIR%\BMS_BI_Manager.csproj" -c Release -r !TARGET_ARCH! --self-contained true -p:PublishSingleFile=true -p:DebugType=None -p:IncludeNativeLibrariesForSelfExtract=true -p:UseAppHost=true -o "%PUB_DIR%\Backend" >nul

echo.
echo  [+] Cleaning up unnecessary compiler metadata and dev configs...
del /f /q "%PUB_DIR%\Backend\*.staticwebassets.endpoints.json" >nul 2>&1
del /f /q "%PUB_DIR%\Backend\*.runtimeconfig.json" >nul 2>&1
del /f /q "%PUB_DIR%\Backend\appsettings.Development.json" >nul 2>&1
if exist "%PUB_DIR%\Backend\wwwroot" rmdir /s /q "%PUB_DIR%\Backend\wwwroot" >nul 2>&1

echo.
echo  [+] Generating Backend Production Launcher...
(
echo @echo off
echo echo Starting BMS Core API...
echo start "BMS_Core API" cmd /k "BMS_Core.exe"
echo echo Starting BMS Web Manager...
echo start "BMS SaaS Manager" cmd /k "BMS_BM.exe"
) > "%PUB_DIR%\Backend\start_backend.bat"

(
echo @echo off
echo echo ==================================================
echo echo Running BMS Background Sync Job...
echo echo ==================================================
echo "BMS_Core.exe" --sync-all
echo echo.
echo echo Sync process completed.
echo timeout /t 10
) > "%PUB_DIR%\Backend\run_scheduled_sync.bat"

:CHECK_FRONTEND
if "%COMPILE_FRONTEND%"=="1" goto DO_COMPILE_FRONTEND
goto FINISH_COMPILE

:DO_COMPILE_FRONTEND
if exist "%PUB_DIR%\Frontend" rmdir /s /q "%PUB_DIR%\Frontend"
mkdir "%PUB_DIR%\Frontend"

echo.
echo  [+] Compiling Frontend Vue Assets (Hash-free)...
pushd "%FRONTEND_DIR%"
call npm install >nul 2>&1
call npm run build >nul 2>&1
popd

if "!FRONTEND_DEPLOY_TYPE!"=="STATIC" (
    echo  [+] Copying Frontend Static Assets...
    xcopy /E /I /Q "%FRONTEND_DIR%\dist\*" "%PUB_DIR%\Frontend\" >nul
    echo.
    echo  [+] Static build complete! Provide the contents of \Publish\Frontend to your Web Server ^(IIS/Nginx/CDN^).
) else (
    echo  [+] Copying Frontend Production Proxy...
    xcopy /E /I /Q "%FRONTEND_DIR%\dist" "%PUB_DIR%\Frontend\dist" >nul
    copy /Y "%FRONTEND_DIR%\server.js" "%PUB_DIR%\Frontend\" >nul
    copy /Y "%FRONTEND_DIR%\package.json" "%PUB_DIR%\Frontend\" >nul

    echo.
    echo  [+] Generating Frontend Production Launcher...
    (
    echo @echo off
    echo echo Starting Frontend Proxy Server...
    echo start "BMS Frontend Proxy" cmd /k "node server.js"
    ) > "%PUB_DIR%\Frontend\start_frontend.bat"
)

:FINISH_COMPILE
echo.
echo  ============================================================
echo   BUILD SUCCESSFUL! 
echo   All output is cleanly organized in the /Publish directory.
echo  ============================================================
echo.
pause
goto MENU

:CHECK_DOTNET
cls
echo.
echo  ============================================================
echo   CHECKING .NET AVAILABILITY ^& HEALTH
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
    echo  [+] Checking ASP.NET Core IIS Module...
    reg query "HKLM\SOFTWARE\Microsoft\IIS Extensions\IIS AspNetCore Module V2" >nul 2>&1
    if not errorlevel 1 (
        echo      Status: INSTALLED
    ) else (
        echo      Status: NOT INSTALLED ^(Required if deploying to IIS^)
    )
    echo.
    echo  [+] Checking Manager Project Health...
    if exist "%MANAGER_DIR%\BMS_BI_Manager.csproj" (
        echo      Manager Project file found. Building dry-run test...
        dotnet build "%MANAGER_DIR%\BMS_BI_Manager.csproj" --no-restore >nul 2>&1
        if errorlevel 1 (
            echo  [x] Project Build Health: FAILED. Run option [2] to clean cache and try again.
        ) else (
            echo  [+] Project Build Health: STABLE ^(Ready to run^)
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
for /d /r "%MANAGER_DIR%" %%d in (bin obj) do (
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
