@echo off
echo Trying to create Timelapse shortcuts in the following locations...
echo.

powershell.exe -ExecutionPolicy Bypass -File "%~dp0Timelapse-ExecutablesAndDependencyFiles\CreateTimelapseShortcuts.ps1"

echo.
echo Shortcut creation process completed.
pause

