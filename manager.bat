@echo off
setlocal enabledelayedexpansion

:: ============================================================
:: Enable ANSI Virtual Terminal Processing (Windows 10+)
:: ============================================================
for /f "tokens=*" %%a in ('reg query HKCU\Console /v VirtualTerminalLevel 2^>nul') do set VT=%%a
if "!VT!"=="" (
    reg add HKCU\Console /v VirtualTerminalLevel /t REG_DWORD /d 1 /f >nul 2>&1
)

:: ============================================================
:: ANSI Color Constants
:: ============================================================
set "ESC="
set "RESET=!ESC![0m"
set "BOLD=!ESC![1m"
set "DIM=!ESC![2m"
set "RED=!ESC![91m"
set "GREEN=!ESC![92m"
set "YELLOW=!ESC![93m"
set "BLUE=!ESC![94m"
set "MAGENTA=!ESC![95m"
set "CYAN=!ESC![96m"
set "WHITE=!ESC![97m"
set "GRAY=!ESC![90m"
set "BG_BLUE=!ESC![44m"
set "BG_CYAN=!ESC![46m"

:: ============================================================
:: Paths
:: ============================================================
set "ROOT_DIR=%~dp0"
set "BACKEND_DIR=%ROOT_DIR%BI-API"
set "FRONTEND_DIR=%ROOT_DIR%BI-APP"
set "MANAGER_DIR=%ROOT_DIR%BI-MGR"
set "PUB_DIR=%ROOT_DIR%Publish"

:: ============================================================
:: Fast Port Defaults (override below from config)
:: ============================================================
set "BACKEND_PORT=8001"
set "FRONTEND_PORT=8002"
set "MANAGER_PORT=8003"

:: ============================================================
:: Read all ports in ONE powershell call (fast startup)
:: ============================================================
for /f "usebackq tokens=1,2 delims==" %%a in (`powershell -NoProfile -Command ^
    "$s=(Get-Content '%ROOT_DIR%BI-API\appsettings.json' | ConvertFrom-Json); [string]$s.Server.Port + '=' + [string]$s.Manager.Port" ^
    2^>nul`) do (
    set "BACKEND_PORT=%%a"
    set "MANAGER_PORT=%%b"
)

for /f "usebackq" %%a in (`powershell -NoProfile -Command ^
    "(Select-String -Path '%ROOT_DIR%BI-APP\vite.config.js' -Pattern 'port:\s*(\d+)').Matches.Groups[1].Value" ^
    2^>nul`) do set "FRONTEND_PORT=%%a"

title BMS BI Service  ^|  Dev Manager

:MENU
cls

:: ── Banner ─────────────────────────────────────────────────
echo !BOLD!!CYAN!
echo   ██████╗ ██╗    ███████╗███████╗██████╗ ██╗   ██╗██╗ ██████╗███████╗
echo   ██╔══██╗██║    ██╔════╝██╔════╝██╔══██╗██║   ██║██║██╔════╝██╔════╝
echo   ██████╔╝██║    ███████╗█████╗  ██████╔╝██║   ██║██║██║     █████╗
echo   ██╔══██╗██║    ╚════██║██╔══╝  ██╔══██╗╚██╗ ██╔╝██║██║     ██╔══╝
echo   ██████╔╝██║    ███████║███████╗██║  ██║ ╚████╔╝ ██║╚██████╗███████╗
echo   ╚═════╝ ╚═╝    ╚══════╝╚══════╝╚═╝  ╚═╝  ╚═══╝  ╚═╝ ╚═════╝╚══════╝
echo !RESET!
echo   !DIM!!GRAY!BIMASAKTI BI Service  ─  Developer CLI Launcher!RESET!
echo.

:: ── Service Status ─────────────────────────────────────────
echo   !BOLD!!WHITE![ SERVICE STATUS ]!RESET!
echo   !GRAY!──────────────────────────────────────────────────────!RESET!

call :CHECK_PORT %BACKEND_PORT% BACK_STATUS
call :CHECK_PORT %FRONTEND_PORT% FRONT_STATUS
call :CHECK_PORT %MANAGER_PORT% MGR_STATUS

call :PRINT_STATUS "  Backend  API Core " %BACKEND_PORT% "!BACK_STATUS!"
call :PRINT_STATUS "  Frontend  Portal  " %FRONTEND_PORT% "!FRONT_STATUS!"
call :PRINT_STATUS "  Web Control Panel " %MANAGER_PORT% "!MGR_STATUS!"

echo   !GRAY!──────────────────────────────────────────────────────!RESET!
echo.

:: ── Actions ────────────────────────────────────────────────
echo   !BOLD!!WHITE![ ACTIONS ]!RESET!
echo.
echo   !BOLD!!CYAN! [1]!RESET!  !WHITE!Launch Full Development Stack !GRAY!(BI-API + BI-APP + BI-MGR)!RESET!
echo   !BOLD!!CYAN! [2]!RESET!  !WHITE!Launch Web Control Panel Only!RESET!
echo   !BOLD!!CYAN! [3]!RESET!  !WHITE!Check Dependencies!RESET!
echo   !BOLD!!CYAN! [4]!RESET!  !WHITE!Free Ports!RESET!
echo   !BOLD!!CYAN! [5]!RESET!  !WHITE!Clean Build Cache!RESET!
echo   !BOLD!!YELLOW! [6]!RESET!  !WHITE!Compile IIS Production Build!RESET!
echo   !BOLD!!RED! [7]!RESET!  !WHITE!Exit!RESET!
echo.
echo   !GRAY!──────────────────────────────────────────────────────!RESET!
echo.
set "choice="
set /p "choice=  !BOLD!Select option!RESET! !GRAY![1-7]:!RESET! "
echo.

if "!choice!"=="1" goto :LAUNCH_ALL
if "!choice!"=="2" goto :LAUNCH_MGR
if "!choice!"=="3" goto :CHECK_DEPS
if "!choice!"=="4" goto :FREE_PORTS
if "!choice!"=="5" goto :CLEAN_CACHE
if "!choice!"=="6" goto :COMPILE_IIS
if "!choice!"=="7" goto :EXIT

call :WARN "Invalid choice. Please select 1-7."
timeout /t 1 >nul
goto :MENU


:: ============================================================
:LAUNCH_ALL
:: ============================================================
cls
call :SECTION_HEADER "LAUNCHING FULL DEVELOPMENT STACK"

call :INFO "Building solution..."
echo.
dotnet build "!ROOT_DIR!BI-Service.slnx" --nologo
if errorlevel 1 (
    echo.
    call :ERROR "Build failed. Resolve errors above before launching."
    pause
    goto :MENU
)

echo.
call :INFO "Releasing occupied ports..."

for %%p in (!BACKEND_PORT! !FRONTEND_PORT! !MANAGER_PORT!) do (
    netstat -ano | findstr LISTENING | findstr :%%p >nul
    if not errorlevel 1 (
        for /f "tokens=5" %%a in ('netstat -aon ^| findstr LISTENING ^| findstr :%%p') do (
            taskkill /F /T /PID %%a >nul 2>&1
        )
    )
)

echo.
call :INFO "Starting !CYAN!BI-API!RESET! (Backend) on port !BOLD!!WHITE!!BACKEND_PORT!!RESET!..."
start "BMS  Backend Core [:!BACKEND_PORT!]" /D "!BACKEND_DIR!" cmd /k "dotnet run --no-build"

call :INFO "Starting !CYAN!BI-APP!RESET! (Frontend) on port !BOLD!!WHITE!!FRONTEND_PORT!!RESET!..."
start "BMS  Frontend Portal [:!FRONTEND_PORT!]" /D "!FRONTEND_DIR!" cmd /k "npm run dev"

echo.
goto :LAUNCH_MGR_INTERNAL


:: ============================================================
:LAUNCH_MGR
:: ============================================================
cls
call :SECTION_HEADER "LAUNCHING WEB CONTROL PANEL"

:LAUNCH_MGR_INTERNAL
call :INFO "Building !CYAN!BI-MGR!RESET!..."
echo.
dotnet build "!MANAGER_DIR!\BI-MGR.csproj" --nologo
if errorlevel 1 (
    echo.
    call :ERROR "BI-MGR build failed. Check output above."
    pause
    goto :MENU
)
echo.

netstat -ano | findstr LISTENING | findstr :!MANAGER_PORT! >nul
if not errorlevel 1 (
    call :WARN "Port !MANAGER_PORT! in use. Releasing..."
    for /f "tokens=5" %%a in ('netstat -aon ^| findstr LISTENING ^| findstr :!MANAGER_PORT!') do (
        taskkill /F /T /PID %%a >nul 2>&1
    )
)

call :INFO "Starting !CYAN!BI-MGR!RESET! (Manager) on port !BOLD!!WHITE!!MANAGER_PORT!!RESET!..."
start "BMS  Web Control Panel [:!MANAGER_PORT!]" /D "!MANAGER_DIR!" cmd /k "dotnet run --no-build > manager_startup.log 2>&1 & type manager_startup.log"

:: Wait for manager to boot (up to 12s)
echo.
set /p "=  Booting services: [" <nul
set "BOOT_SUCCESS=0"
for /L %%i in (1,1,24) do (
    if !BOOT_SUCCESS!==0 (
        netstat -ano | findstr LISTENING | findstr :!MANAGER_PORT! >nul
        if not errorlevel 1 (
            set "BOOT_SUCCESS=1"
            set /p "=!GREEN!●!RESET!" <nul
        ) else (
            set /p "=!GRAY!·!RESET!" <nul
            powershell -NoProfile -Command "Start-Sleep -m 500" >nul
        )
    )
)
echo  ]
echo.

if !BOOT_SUCCESS!==0 (
    call :ERROR "Manager did not respond on port !MANAGER_PORT! within 12s."
    call :WARN "Review: !MANAGER_DIR!\manager_startup.log"
    echo.
    pause
    goto :MENU
)

call :SUCCESS "Web Control Panel is online at !CYAN!http://localhost:!MANAGER_PORT!!RESET!"
echo.

:: Check for generated password
findstr /C:"A secure random password has been generated:" "!MANAGER_DIR!\manager_startup.log" >nul 2>&1
if not errorlevel 1 (
    echo   !YELLOW!╔══════════════════════════════════════════════════╗!RESET!
    echo   !YELLOW!║    ⚠  NEW TEMPORARY SUPERADMIN PASSWORD  ⚠     ║!RESET!
    for /f "tokens=*" %%p in ('findstr /C:"A secure random password has been generated:" "!MANAGER_DIR!\manager_startup.log"') do (
        set "line=%%p"
        set "pwd=!line:*A secure random password has been generated: =!"
        echo   !YELLOW!║  Password: !BOLD!!WHITE!!pwd!!RESET!!YELLOW!                           ║!RESET!
    )
    echo   !YELLOW!╚══════════════════════════════════════════════════╝!RESET!
    echo.
    pause
)

:: Open browser
call :INFO "Opening browser → !CYAN!http://localhost:!MANAGER_PORT!!RESET!"
set "BrowserProgId="
for /f "tokens=2* skip=2" %%a in ('reg query HKCU\Software\Microsoft\Windows\Shell\Associations\UrlAssociations\http\UserChoice /v ProgId 2^>nul') do set "BrowserProgId=%%b"
echo !BrowserProgId! | findstr /i "Chrome" >nul && start chrome "http://localhost:!MANAGER_PORT!" && goto :BROWSER_DONE
echo !BrowserProgId! | findstr /i "Edge"   >nul && start msedge "http://localhost:!MANAGER_PORT!" && goto :BROWSER_DONE
echo !BrowserProgId! | findstr /i "Firefox">nul && start firefox "http://localhost:!MANAGER_PORT!" && goto :BROWSER_DONE
echo !BrowserProgId! | findstr /i "Brave"  >nul && start brave "http://localhost:!MANAGER_PORT!"   && goto :BROWSER_DONE
start "" "http://localhost:!MANAGER_PORT!"

:BROWSER_DONE
timeout /t 2 >nul
goto :MENU


:: ============================================================
:CHECK_DEPS
:: ============================================================
cls
call :SECTION_HEADER "DEPENDENCY CHECK"

set "DEPS_OK=1"

:: .NET SDK
echo   !BOLD!Checking .NET SDK...!RESET!
dotnet --version >nul 2>&1
if errorlevel 1 (
    call :ERROR ".NET SDK not found. Install from https://dotnet.microsoft.com/download"
    set "DEPS_OK=0"
) else (
    for /f "delims=" %%v in ('dotnet --version 2^>nul') do (
        call :SUCCESS ".NET SDK  →  %%v"
    )
)

:: .NET SDK list
dotnet --list-sdks 2>nul | findstr /r "[0-9]" >nul 2>&1
if not errorlevel 1 (
    echo.
    echo   !GRAY!Installed SDKs:!RESET!
    for /f "delims=" %%s in ('dotnet --list-sdks 2^>nul') do echo     !DIM!%%s!RESET!
)

echo.

:: Node.js
echo   !BOLD!Checking Node.js...!RESET!
node --version >nul 2>&1
if errorlevel 1 (
    call :ERROR "Node.js not found. Install from https://nodejs.org"
    set "DEPS_OK=0"
) else (
    for /f "delims=" %%v in ('node --version 2^>nul') do (
        call :SUCCESS "Node.js  →  %%v"
    )
)

echo.

:: npm
echo   !BOLD!Checking npm...!RESET!
npm --version >nul 2>&1
if errorlevel 1 (
    call :ERROR "npm not found."
    set "DEPS_OK=0"
) else (
    for /f "delims=" %%v in ('npm --version 2^>nul') do (
        call :SUCCESS "npm  →  v%%v"
    )
)

echo.

:: node_modules presence
echo   !BOLD!Checking BI-APP node_modules...!RESET!
if exist "!FRONTEND_DIR!\node_modules\" (
    call :SUCCESS "node_modules present"
) else (
    call :WARN "node_modules missing in BI-APP."
    echo.
    set /p "install_choice=  !YELLOW!Run npm install now?!RESET! !GRAY![Y/N]:!RESET! "
    if /i "!install_choice!"=="Y" (
        echo.
        call :INFO "Running npm install..."
        pushd "!FRONTEND_DIR!"
        npm install
        popd
        call :SUCCESS "npm install complete."
    )
)

echo.

:: IIS ASP.NET Core Module
echo   !BOLD!Checking ASP.NET Core IIS Module...!RESET!
reg query "HKLM\SOFTWARE\Microsoft\IIS Extensions\IIS AspNetCore Module V2" >nul 2>&1
if not errorlevel 1 (
    call :SUCCESS "ASP.NET Core IIS Module V2  →  INSTALLED"
) else (
    call :WARN "ASP.NET Core IIS Module V2  →  NOT INSTALLED !GRAY!(only required for IIS deployment)!RESET!"
)

echo.

if !DEPS_OK!==1 (
    call :SUCCESS "All core dependencies satisfied!"
) else (
    call :ERROR "One or more dependencies are missing. Install them before proceeding."
)

echo.
pause
goto :MENU


:: ============================================================
:FREE_PORTS
:: ============================================================
cls
call :SECTION_HEADER "FREEING PORTS"

set "freed=0"
for %%p in (!BACKEND_PORT! !FRONTEND_PORT! !MANAGER_PORT!) do (
    netstat -ano | findstr LISTENING | findstr :%%p >nul
    if not errorlevel 1 (
        for /f "tokens=5" %%a in ('netstat -aon ^| findstr LISTENING ^| findstr :%%p') do (
            taskkill /F /T /PID %%a >nul 2>&1
            call :SUCCESS "Released port %%p  !GRAY!(PID %%a)!RESET!"
            set /a freed+=1
        )
    )
)

echo.
if !freed! GTR 0 (
    call :SUCCESS "!freed! port(s) released."
) else (
    call :INFO "No ports were in use. All clear."
)
echo.
pause
goto :MENU


:: ============================================================
:CLEAN_CACHE
:: ============================================================
cls
call :SECTION_HEADER "CLEAN BUILD CACHE"

set "cleaned=0"
call :INFO "Scanning for build artifacts..."

for /d /r "!BACKEND_DIR!" %%d in (bin obj) do (
    if exist "%%d" (
        rmdir /s /q "%%d" >nul 2>&1
        call :SUCCESS "Removed: !GRAY!%%d!RESET!"
        set /a cleaned+=1
    )
)

for /d /r "!MANAGER_DIR!" %%d in (bin obj) do (
    if exist "%%d" (
        rmdir /s /q "%%d" >nul 2>&1
        call :SUCCESS "Removed: !GRAY!%%d!RESET!"
        set /a cleaned+=1
    )
)

if exist "!FRONTEND_DIR!\node_modules\.vite" (
    rmdir /s /q "!FRONTEND_DIR!\node_modules\.vite" >nul 2>&1
    call :SUCCESS "Removed: !GRAY!BI-APP/node_modules/.vite (Vite cache)!RESET!"
    set /a cleaned+=1
)

echo.
if !cleaned! GTR 0 (
    call :SUCCESS "Cleaned !cleaned! director^(ies^)."
) else (
    call :INFO "Nothing to clean. Cache is already empty."
)
echo.
pause
goto :MENU


:: ============================================================
:COMPILE_IIS
:: ============================================================
cls
call :SECTION_HEADER "COMPILE IIS PRODUCTION BUILD"

echo   !GRAY!Target: IIS Framework-Dependent Deployment!RESET!
echo.

set /p "confirm_choice=  !YELLOW!This will overwrite existing Publish/IIS output. Continue?!RESET! !GRAY![Y/N]:!RESET! "
if /i not "!confirm_choice!"=="Y" goto :MENU

echo.
call :INFO "Preparing output directories..."

if not exist "!PUB_DIR!\IIS" mkdir "!PUB_DIR!\IIS"
if exist "!PUB_DIR!\IIS\BI-API" rmdir /s /q "!PUB_DIR!\IIS\BI-API"
if exist "!PUB_DIR!\IIS\BI-MGR" rmdir /s /q "!PUB_DIR!\IIS\BI-MGR"
if exist "!PUB_DIR!\IIS\BI-APP" rmdir /s /q "!PUB_DIR!\IIS\BI-APP"

:: Drop app_offline.htm to gracefully unload IIS app domains
if exist "!PUB_DIR!\IIS\BI-API" echo Updating... > "!PUB_DIR!\IIS\BI-API\app_offline.htm"
if exist "!PUB_DIR!\IIS\BI-MGR" echo Updating... > "!PUB_DIR!\IIS\BI-MGR\app_offline.htm"
powershell -NoProfile -Command "Start-Sleep -s 2" >nul

:: ── Backend API ────────────────────────────────────────────
echo.
call :INFO "Publishing !CYAN!BI-API!RESET!  (IIS, Framework-Dependent)..."
dotnet clean "!BACKEND_DIR!\BI-API.csproj" -c Release --nologo >nul 2>&1
dotnet publish "!BACKEND_DIR!\BI-API.csproj" ^
    -c Release ^
    --nologo ^
    -p:EnvironmentName=Production ^
    -p:UseAppHost=false ^
    -o "!PUB_DIR!\IIS\BI-API"
if errorlevel 1 (
    call :ERROR "BI-API publish failed."
    pause
    goto :MENU
)
call :SUCCESS "BI-API published."

:: ── Manager ───────────────────────────────────────────────
echo.
call :INFO "Publishing !CYAN!BI-MGR!RESET!  (IIS, Framework-Dependent)..."
dotnet clean "!MANAGER_DIR!\BI-MGR.csproj" -c Release --nologo >nul 2>&1
dotnet publish "!MANAGER_DIR!\BI-MGR.csproj" ^
    -c Release ^
    --nologo ^
    -p:EnvironmentName=Production ^
    -p:UseAppHost=false ^
    -o "!PUB_DIR!\IIS\BI-MGR"
if errorlevel 1 (
    call :ERROR "BI-MGR publish failed."
    pause
    goto :MENU
)
call :SUCCESS "BI-MGR published."

:: ── Frontend ──────────────────────────────────────────────
echo.
call :INFO "Building !CYAN!BI-APP!RESET!  (Static Assets)..."
if not exist "!FRONTEND_DIR!\node_modules\" (
    call :WARN "node_modules missing. Running npm install..."
    pushd "!FRONTEND_DIR!"
    call npm install >nul 2>&1
    popd
)
pushd "!FRONTEND_DIR!"
call npm run build
if errorlevel 1 (
    popd
    call :ERROR "BI-APP frontend build failed."
    pause
    goto :MENU
)
popd
call :SUCCESS "BI-APP built."

echo.
call :INFO "Copying frontend static assets..."
mkdir "!PUB_DIR!\IIS\BI-APP"
xcopy /E /I /Q "!FRONTEND_DIR!\dist\*" "!PUB_DIR!\IIS\BI-APP\" >nul

:: ── Cleanup ───────────────────────────────────────────────
echo.
call :INFO "Cleaning up dev-only files..."
del /f /q "!PUB_DIR!\IIS\BI-API\appsettings.Development.json" >nul 2>&1
del /f /q "!PUB_DIR!\IIS\BI-MGR\appsettings.Development.json" >nul 2>&1
del /f /q "!PUB_DIR!\IIS\BI-API\*.staticwebassets.endpoints.json" >nul 2>&1
del /f /q "!PUB_DIR!\IIS\BI-MGR\*.staticwebassets.endpoints.json" >nul 2>&1
if exist "!PUB_DIR!\IIS\BI-API\app_offline.htm" del /f /q "!PUB_DIR!\IIS\BI-API\app_offline.htm"
if exist "!PUB_DIR!\IIS\BI-MGR\app_offline.htm" del /f /q "!PUB_DIR!\IIS\BI-MGR\app_offline.htm"

:: ── Sync job script ───────────────────────────────────────
call :INFO "Generating scheduled sync helper script..."
(
echo @echo off
echo echo Running BMS scheduled sync...
echo cd /d "%%~dp0BI-MGR"
echo dotnet BI-MGR.dll --sync-all
echo timeout /t 10
) > "!PUB_DIR!\IIS\run_scheduled_sync.bat"

:: ── Init DB assets ────────────────────────────────────────
call :INFO "Initializing database assets..."
pushd "!PUB_DIR!\IIS\BI-MGR"
dotnet BI-MGR.dll --sync-all >nul 2>&1
popd

echo.
echo   !GREEN!╔═══════════════════════════════════════════════════════╗!RESET!
echo   !GREEN!║   ✓  IIS BUILD SUCCESSFUL                             ║!RESET!
echo   !GREEN!║                                                       ║!RESET!
echo   !GREEN!║   Output: Publish\IIS\                               ║!RESET!
echo   !GREEN!║     ├─ BI-API\   → IIS Site: Backend API             ║!RESET!
echo   !GREEN!║     ├─ BI-MGR\   → IIS Site: SaaS Manager            ║!RESET!
echo   !GREEN!║     └─ BI-APP\   → IIS Site: Frontend Portal         ║!RESET!
echo   !GREEN!╚═══════════════════════════════════════════════════════╝!RESET!
echo.
pause
goto :MENU


:: ============================================================
:EXIT
:: ============================================================
cls
call :SECTION_HEADER "SHUTTING DOWN"

call :INFO "Releasing all ports..."
for %%p in (!BACKEND_PORT! !FRONTEND_PORT! !MANAGER_PORT!) do (
    netstat -ano | findstr LISTENING | findstr :%%p >nul
    if not errorlevel 1 (
        for /f "tokens=5" %%a in ('netstat -aon ^| findstr LISTENING ^| findstr :%%p') do (
            taskkill /F /T /PID %%a >nul 2>&1
        )
    )
)

call :INFO "Cleaning Vite cache..."
if exist "!FRONTEND_DIR!\node_modules\.vite" rmdir /s /q "!FRONTEND_DIR!\node_modules\.vite" >nul 2>&1

echo.
call :SUCCESS "All services stopped. Goodbye!"
timeout /t 2 >nul
exit


:: ============================================================
:: SUBROUTINES
:: ============================================================

:CHECK_PORT <port> <out_var>
    netstat -ano | findstr LISTENING | findstr :"%~1" >nul 2>&1
    if errorlevel 1 ( set "%~2=OFFLINE" ) else ( set "%~2=ONLINE" )
goto :EOF

:PRINT_STATUS <label> <port> <status>
    set "_lbl=%~1"
    set "_port=%~2"
    set "_st=%~3"
    if "!_st!"=="ONLINE" (
        echo   !WHITE!!_lbl!!RESET!!GRAY!:%_port%!RESET!  !GREEN!● ONLINE!RESET!
    ) else (
        echo   !WHITE!!_lbl!!RESET!!GRAY!:%_port%!RESET!  !GRAY!○ OFFLINE!RESET!
    )
goto :EOF

:SECTION_HEADER <title>
    echo   !BOLD!!CYAN!┌──────────────────────────────────────────────────────┐!RESET!
    echo   !BOLD!!CYAN!│  !WHITE!%~1!CYAN!
    echo   !BOLD!!CYAN!└──────────────────────────────────────────────────────┘!RESET!
    echo.
goto :EOF

:INFO <msg>
    echo   !CYAN!→!RESET!  %~1
goto :EOF

:SUCCESS <msg>
    echo   !GREEN!✓!RESET!  %~1
goto :EOF

:WARN <msg>
    echo   !YELLOW!⚠!RESET!  %~1
goto :EOF

:ERROR <msg>
    echo   !RED!✗!RESET!  !RED!%~1!RESET!
goto :EOF
