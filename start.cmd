@echo off
rem ============================================================
rem  Sleepify - device check + sleep plan  (entry point)
rem  Double-click. Self-elevates, runs start.ps1, keeps the window
rem  open so the report can be read.
rem ============================================================
setlocal

fltmc >nul 2>&1
if errorlevel 1 (
    powershell -NoProfile -Command "Start-Process -FilePath '%~f0' -Verb RunAs" >nul 2>&1
    exit /b
)

cd /d "%~dp0"
title Sleepify - device check

if not exist "%~dp0start.ps1" (
    echo.
    echo   ERROR: start.ps1 not found next to start.cmd
    echo          Both files must stay in the same folder.
    echo.
    pause
    exit /b 1
)

powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0start.ps1"
if errorlevel 1 (
    echo.
    echo   The device check reported an error. See setup-log.txt
    pause
)
endlocal
