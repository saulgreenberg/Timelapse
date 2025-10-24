@echo off
echo Building Timelapse Distribution Package...
echo.

powershell.exe -ExecutionPolicy Bypass -File "%~dp0BuildTimelapseZipFile.ps1"

if %ERRORLEVEL% EQU 0 (
    echo.
    echo Build completed successfully!
    echo Timelapse-Executables.zip has been created.
) else (
    echo.
    echo Build failed with error code %ERRORLEVEL%
    exit /b %ERRORLEVEL%
)