@echo off
rem ============================================================
rem  Sleepify - uninstall and revert everything
rem  Double-click. Self-elevates, shows a confirmation, then
rem  runs uninstall.ps1.
rem ============================================================
setlocal

fltmc >nul 2>&1
if errorlevel 1 (
    powershell -NoProfile -Command "Start-Process -FilePath '%~f0' -Verb RunAs" >nul 2>&1
    exit /b
)

cd /d "%~dp0"
title Sleepify - uninstall

echo ============================================================
echo  Sleepify - uninstall
echo ============================================================
echo.
echo  This will:
echo    1. stop Sleepify and remove its autostart task
echo    2. restore the lid-close action to "Sleep"
echo    3. restore CPU max 100%% and cooling policy "Active"
echo    4. re-enable the automatic maintenance window
echo    5. restart the background services it may have stopped
echo    6. turn the wireless radios back on
echo    7. delete C:\ProgramData\Sleepify
echo    8. delete this folder
echo.
choice /c YN /m "Continue"
if errorlevel 2 exit /b 0

rem leave the install folder so it can be deleted afterwards
cd /d "%TEMP%"

if not exist "%~dp0uninstall.ps1" (
    echo.
    echo   ERROR: uninstall.ps1 not found next to uninstall.cmd
    pause
    exit /b 1
)

powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0uninstall.ps1"
endlocal
