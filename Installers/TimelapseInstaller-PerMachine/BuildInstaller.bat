@echo off
REM Build script for Timelapse MSI Installer using WiX v6
REM This script generates file list and builds the MSI installer

echo ========================================
echo Timelapse MSI Installer Build Script
echo ========================================
echo.

REM Set paths
set RELEASE_DIR=..\..\src\Timelapse\bin\Publish\RequiresDotNet10-win-x64
set OUTPUT_DIR=..\bin\Release

REM Check if release folder exists
if not exist "%RELEASE_DIR%" (
    echo ERROR: Release folder not found: %RELEASE_DIR%
    echo Please publish Timelapse with RequiresDotNet10 profile first.
    echo Run: dotnet publish ..\..\Timelapse\Timelapse.csproj -p:PublishProfile=RequiresDotNet10-win-x64
    pause
    exit /b 1
)

echo Step 1: Updating version from executable...
echo.

REM Run PowerShell script to update version in Product.wxs
powershell -ExecutionPolicy Bypass -File UpdateVersion.ps1

if errorlevel 1 (
    echo ERROR: Failed to update version
    pause
    exit /b 1
)

echo.
echo Step 2: Generating file list from release folder...
echo Source: %RELEASE_DIR%
echo.

REM Run PowerShell script to generate Files.wxs
powershell -ExecutionPolicy Bypass -File GenerateFileList.ps1

if errorlevel 1 (
    echo ERROR: Failed to generate file list
    pause
    exit /b 1
)

echo.
echo Step 3: Building MSI installer using WiX...
echo.

REM Create output directory if it doesn't exist
if not exist "%OUTPUT_DIR%" mkdir "%OUTPUT_DIR%"

REM Build the installer using WiX v6
wix build Product.wxs Files.wxs ^
    -arch x64 ^
    -ext WixToolset.UI.wixext ^
    -ext WixToolset.Netfx.wixext ^
    -loc en-us.wxl ^
    -out "%OUTPUT_DIR%\TimelapseInstaller-PerMachine.msi" ^
    -d "SourceDir=%RELEASE_DIR%"

if errorlevel 1 (
    echo.
    echo ERROR: MSI build failed
    pause
    exit /b 1
)

echo.
echo ========================================
echo Build completed successfully!
echo ========================================
echo.
echo MSI installer created:
echo %CD%\%OUTPUT_DIR%\TimelapseInstaller-PerMachine.msi
echo.

REM Clean up generated Files.wxs and .wixpdb
del Files.wxs
if exist "%OUTPUT_DIR%\TimelapseInstaller-PerMachine.wixpdb" del "%OUTPUT_DIR%\TimelapseInstaller-PerMachine.wixpdb"
echo Cleaned up temporary files (Files.wxs, .wixpdb)
echo.
