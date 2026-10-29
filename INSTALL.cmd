@echo off
setlocal DisableDelayedExpansion
title DEVOUR Monster Freeze - Install
if not exist "%~dp0Support\Install.ps1" (
    echo Extract ALL files from the ZIP before running this program.
    pause
    exit /b 1
)
set "DEVOUR_PS=%SystemRoot%\System32\WindowsPowerShell\v1.0\powershell.exe"
if exist "%SystemRoot%\Sysnative\WindowsPowerShell\v1.0\powershell.exe" set "DEVOUR_PS=%SystemRoot%\Sysnative\WindowsPowerShell\v1.0\powershell.exe"
"%DEVOUR_PS%" -NoLogo -NoProfile -ExecutionPolicy Bypass -File "%~dp0Support\Install.ps1"
set "DEVOUR_RESULT=%ERRORLEVEL%"
echo.
pause
exit /b %DEVOUR_RESULT%
