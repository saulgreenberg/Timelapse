@echo off
REM Wrapper batch file to run PublishAll.ps1 from Visual Studio or command line
REM This orchestrates all Timelapse publishing operations in the correct sequence

cd /d "%~dp0..\.."
powershell -ExecutionPolicy Bypass -NoProfile -File "Properties\PublishProfiles\PublishAll.ps1"

if errorlevel 1 (
    echo.
    echo ERROR: PublishAll script failed
    exit /b 1
)

echo.
echo ========================================
echo All publishing operations completed successfully!
echo ========================================
echo.
echo Output files are located in:
echo %CD%\..\Installers\bin\Release
echo.
