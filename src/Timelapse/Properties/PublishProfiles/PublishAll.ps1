# PublishAll.ps1
# Master script to publish all Timelapse builds and create installers

param(
    [string]$ProjectPath = "$PSScriptRoot\..\..\Timelapse.csproj",
    [string]$InstallersDir = "$PSScriptRoot\..\..\..\..\Installers"
)

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Timelapse Master Publish Script" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

try {
    # Step 1: Publish RequiresDotNet8-win-x64
    Write-Host "[1/5] Publishing RequiresDotNet8-win-x64..." -ForegroundColor Yellow
    $result = & dotnet publish $ProjectPath -p:PublishProfile=RequiresDotNet8-win-x64 -p:Configuration=Release
    if ($LASTEXITCODE -ne 0) {
        throw "RequiresDotNet8-win-x64 publish failed"
    }
    Write-Host "[DONE] RequiresDotNet8-win-x64 publish completed" -ForegroundColor Green
    Write-Host ""

    # Step 2: Publish SelfContained-win-x64
    Write-Host "[2/5] Publishing SelfContained-win-x64..." -ForegroundColor Yellow
    $result = & dotnet publish $ProjectPath -p:PublishProfile=SelfContained-win-x64 -p:Configuration=Release
    if ($LASTEXITCODE -ne 0) {
        throw "SelfContained-win-x64 publish failed"
    }
    Write-Host "[DONE] SelfContained-win-x64 publish completed" -ForegroundColor Green
    Write-Host ""

    # Step 3: Build Timelapse Zip File
    Write-Host "[3/5] Building Timelapse Zip Distribution..." -ForegroundColor Yellow
    $zipScriptPath = Join-Path $InstallersDir "TimelapseBuildZip\BuildTimelapseZipFile.bat"
    $zipWorkingDir = Join-Path $InstallersDir "TimelapseBuildZip"

    Push-Location $zipWorkingDir
    try {
        cmd /c "`"$zipScriptPath`""
        if ($LASTEXITCODE -ne 0) {
            throw "Zip build failed"
        }
    } finally {
        Pop-Location
    }
    Write-Host "[DONE] Zip distribution created" -ForegroundColor Green
    Write-Host ""

    # Step 4: Build PerMachine Installer
    Write-Host "[4/5] Building PerMachine MSI Installer..." -ForegroundColor Yellow
    $perMachineScriptPath = Join-Path $InstallersDir "TimelapseInstaller-PerMachine\BuildInstaller.bat"
    $perMachineWorkingDir = Join-Path $InstallersDir "TimelapseInstaller-PerMachine"

    Push-Location $perMachineWorkingDir
    try {
        cmd /c "`"$perMachineScriptPath`""
        if ($LASTEXITCODE -ne 0) {
            throw "PerMachine installer build failed"
        }
    } finally {
        Pop-Location
    }
    Write-Host "[DONE] PerMachine installer created" -ForegroundColor Green
    Write-Host ""

    # Step 5: Build PerUser Installer
    Write-Host "[5/5] Building PerUser MSI Installer..." -ForegroundColor Yellow
    $perUserScriptPath = Join-Path $InstallersDir "TimelapseInstaller-PerUser\BuildInstaller.bat"
    $perUserWorkingDir = Join-Path $InstallersDir "TimelapseInstaller-PerUser"

    Push-Location $perUserWorkingDir
    try {
        cmd /c "`"$perUserScriptPath`""
        if ($LASTEXITCODE -ne 0) {
            throw "PerUser installer build failed"
        }
    } finally {
        Pop-Location
    }
    Write-Host "[DONE] PerUser installer created" -ForegroundColor Green
    Write-Host ""

    # Summary
    Write-Host "========================================" -ForegroundColor Green
    Write-Host "PublishAll Process Completed Successfully!" -ForegroundColor Green
    Write-Host "========================================" -ForegroundColor Green
    Write-Host ""
    Write-Host "Output locations:" -ForegroundColor Cyan

    $projectDir = Split-Path $ProjectPath -Parent
    Write-Host "  - RequiresDotNet8: $projectDir\bin\Publish\RequiresDotNet8-win-x64\" -ForegroundColor White
    Write-Host "  - SelfContained:   $projectDir\bin\Publish\SelfContained-win-x64\" -ForegroundColor White
    Write-Host "  - Zip Package:     $InstallersDir\bin\Release\Timelapse-Executables.zip" -ForegroundColor White
    Write-Host "  - PerMachine MSI:  $InstallersDir\bin\Release\TimelapseInstaller-PerMachine.msi" -ForegroundColor White
    Write-Host "  - PerUser MSI:     $InstallersDir\bin\Release\TimelapseInstaller-PerUser.msi" -ForegroundColor White
    Write-Host ""

    exit 0

} catch {
    Write-Host ""
    Write-Host "========================================" -ForegroundColor Red
    Write-Host "PublishAll Process Failed!" -ForegroundColor Red
    Write-Host "========================================" -ForegroundColor Red
    Write-Host ""
    Write-Host "Error: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host ""

    exit 1
}
