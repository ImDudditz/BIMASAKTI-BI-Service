@echo off
title Bimasakti BI Web Manager Launcher
echo ===================================================
echo   BIMASAKTI BI SERVICE - WEB MANAGER LAUNCHER
echo ===================================================
echo.
echo Restoring and running Bimasakti Web Manager...
echo.
dotnet run --project backend/manager/manager.csproj
pause
