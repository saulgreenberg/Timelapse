@echo off
echo Removing Timelapse shortcuts...
echo.

powershell.exe -ExecutionPolicy Bypass -File "%~dp0Timelapse-ExecutablesAndDependencyFiles\UninstallTimelapseShortcuts.ps1"

if %ERRORLEVEL% EQU 0 (
    echo.
    echo Shortcut removal completed!
) else (
    echo.
    echo Shortcut removal failed with error code %ERRORLEVEL%
)

pause

