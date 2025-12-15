@echo off
REM CleanBinObjInstallBin.bat
REM Wrapper to run the PowerShell cleanup script

echo ========================================
echo Timelapse Complete Cleanup
echo ========================================
echo.
echo This will delete all bin, obj, and cache folders for a fresh rebuild.
echo.
echo IMPORTANT: Close Visual Studio before continuing!
echo           (Files may be locked if VS is open)
echo.
pause

REM Run the PowerShell script
powershell -ExecutionPolicy Bypass -NoProfile -File "%~dp0CleanBinObjInstallBin.ps1"

echo.
echo ========================================
echo Cleanup complete!
echo ========================================
echo.
pause
