# PublishAll.ps1
# Master script to publish all Timelapse builds and create installers

param(
    [string]$ProjectPath = "$PSScriptRoot\..\..\Timelapse.csproj",
    [string]$InstallersDir = "$PSScriptRoot\..\..\..\..\Installers",
    [string]$SignTool = 'C:\Program Files (x86)\Windows Kits\10\bin\10.0.22621.0\x64\signtool.exe',
    [string]$Thumbprint = 'B6FF9831D50B47E1500DD47A0612E01E371CABC4',
    [string]$TimestampUrl = 'http://timestamp.certum.pl'
)

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Timelapse Master Publish Script" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

$projectDir = Split-Path $ProjectPath -Parent

function Sign-Binaries([string]$Directory) {
    & "$PSScriptRoot\SignBinaries.ps1" `
        -Directory $Directory `
        -SignTool $SignTool `
        -Thumbprint $Thumbprint `
        -TimestampUrl $TimestampUrl
    if ($LASTEXITCODE -ne 0) {
        throw "Signing failed in: $Directory"
    }
}

function Sign-File([string]$FilePath) {
    Write-Host "  Signing: $FilePath" -ForegroundColor Cyan
    & $SignTool sign /sha1 $Thumbprint /fd SHA256 /tr $TimestampUrl /td sha256 /v $FilePath
    if ($LASTEXITCODE -ne 0) {
        throw "signtool failed for: $FilePath"
    }
    Write-Host "  Signing complete." -ForegroundColor Green
}

try {
    # Step 1: Publish RequiresDotNet10-win-x64
    Write-Host "[1/5] Publishing RequiresDotNet10-win-x64..." -ForegroundColor Yellow
    & dotnet publish $ProjectPath -p:PublishProfile=RequiresDotNet10-win-x64 -p:Configuration=Release
    if ($LASTEXITCODE -ne 0) {
        throw "RequiresDotNet10-win-x64 publish failed"
    }
    Write-Host "[DONE] RequiresDotNet10-win-x64 publish completed" -ForegroundColor Green
    Write-Host ""

    # Step 1b: Sign first-party binaries in RequiresDotNet10 publish folder
    Write-Host "[1b/5] Signing RequiresDotNet10 binaries (authorize on phone when prompted)..." -ForegroundColor Yellow
    Sign-Binaries "$projectDir\bin\Publish\RequiresDotNet10-win-x64"
    Write-Host "[DONE] RequiresDotNet10 binaries signed" -ForegroundColor Green
    Write-Host ""

    # Step 2: Publish SelfContained-win-x64
    Write-Host "[2/5] Publishing SelfContained-win-x64..." -ForegroundColor Yellow
    & dotnet publish $ProjectPath -p:PublishProfile=SelfContained-win-x64 -p:Configuration=Release
    if ($LASTEXITCODE -ne 0) {
        throw "SelfContained-win-x64 publish failed"
    }
    Write-Host "[DONE] SelfContained-win-x64 publish completed" -ForegroundColor Green
    Write-Host ""

    # Step 2b: Sign first-party binaries in SelfContained publish folder
    Write-Host "[2b/5] Signing SelfContained binaries (authorize on phone when prompted)..." -ForegroundColor Yellow
    Sign-Binaries "$projectDir\bin\Publish\SelfContained-win-x64"
    Write-Host "[DONE] SelfContained binaries signed" -ForegroundColor Green
    Write-Host ""

    # Step 3: Build Timelapse Zip File (zips already-signed binaries - no signing needed)
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

    # Step 4b: Sign PerMachine MSI
    Write-Host "[4b/5] Signing PerMachine MSI (authorize on phone when prompted)..." -ForegroundColor Yellow
    Sign-File "$InstallersDir\bin\Release\TimelapseInstaller-PerMachine.msi"
    Write-Host "[DONE] PerMachine MSI signed" -ForegroundColor Green
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

    # Step 5b: Sign PerUser MSI
    Write-Host "[5b/5] Signing PerUser MSI (authorize on phone when prompted)..." -ForegroundColor Yellow
    Sign-File "$InstallersDir\bin\Release\TimelapseInstaller-PerUser.msi"
    Write-Host "[DONE] PerUser MSI signed" -ForegroundColor Green
    Write-Host ""

    # Step 6: Verify all signatures
    Write-Host "[6/5] Verifying all signatures..." -ForegroundColor Yellow
    & "$PSScriptRoot\VerifySignatures.ps1" `
        -RequiresDotNet10Dir "$projectDir\bin\Publish\RequiresDotNet10-win-x64" `
        -SelfContainedDir    "$projectDir\bin\Publish\SelfContained-win-x64" `
        -InstallersOutputDir "$InstallersDir\bin\Release" `
        -SignTool $SignTool
    if ($LASTEXITCODE -ne 0) {
        throw "Signature verification failed - one or more files did not verify"
    }
    Write-Host "[DONE] All signatures verified" -ForegroundColor Green
    Write-Host ""

    # Step 7: Generate SHA-256 checksums for distributable artifacts
    Write-Host "[7/5] Generating SHA-256 checksums..." -ForegroundColor Yellow
    $releaseDir = "$InstallersDir\bin\Release"
    $checksumFile = "$releaseDir\checksums-SHA256.txt"
    $distributables = @(
        "TimelapseInstaller-PerMachine.msi",
        "TimelapseInstaller-PerUser.msi",
        "Timelapse-Executables.zip"
    )
    $lines = @()
    foreach ($name in $distributables) {
        $path = Join-Path $releaseDir $name
        if (Test-Path $path) {
            $hash = (Get-FileHash $path -Algorithm SHA256).Hash
            $lines += "$hash  $name"
        } else {
            Write-Host "  WARNING: $name not found - skipped" -ForegroundColor Yellow
        }
    }
    $lines | Set-Content $checksumFile -Encoding UTF8
    Write-Host "[DONE] Checksums written to: $checksumFile" -ForegroundColor Green
    Write-Host ""
    Write-Host "  SHA-256 checksums (copy to website):" -ForegroundColor Cyan
    foreach ($line in $lines) {
        Write-Host "  $line" -ForegroundColor White
    }
    Write-Host ""

    # Generate VerifyChecksums.ps1 with baked-in hashes
    $verifyScript = "$releaseDir\VerifyChecksums.ps1"
    $scriptLines = @()
    $scriptLines += '# Run this script from the folder containing the installer files to verify their integrity.'
    $scriptLines += '# Usage:  powershell -ExecutionPolicy Bypass -File VerifyChecksums.ps1'
    $scriptLines += ''
    $scriptLines += '$checksums = @{'
    foreach ($line in $lines) {
        $parts = $line -split '  ', 2
        $scriptLines += "    '$($parts[1])' = '$($parts[0])'"
    }
    $scriptLines += '}'
    $scriptLines += ''
    $scriptLines += '$dir = $PSScriptRoot'
    $scriptLines += '$allOk = $true'
    $scriptLines += 'foreach ($file in $checksums.Keys) {'
    $scriptLines += '    $path = Join-Path $dir $file'
    $scriptLines += '    if (-not (Test-Path $path)) {'
    $scriptLines += '        Write-Host "MISSING  $file" -ForegroundColor Red'
    $scriptLines += '        $allOk = $false'
    $scriptLines += '        continue'
    $scriptLines += '    }'
    $scriptLines += '    $actual = (Get-FileHash $path -Algorithm SHA256).Hash'
    $scriptLines += '    if ($actual -eq $checksums[$file]) {'
    $scriptLines += '        Write-Host "OK       $file" -ForegroundColor Green'
    $scriptLines += '    } else {'
    $scriptLines += '        Write-Host "FAIL     $file" -ForegroundColor Red'
    $scriptLines += '        Write-Host "  Expected: $($checksums[$file])"'
    $scriptLines += '        Write-Host "  Got:      $actual"'
    $scriptLines += '        $allOk = $false'
    $scriptLines += '    }'
    $scriptLines += '}'
    $scriptLines += ''
    $scriptLines += 'if ($allOk) {'
    $scriptLines += '    Write-Host ""'
    $scriptLines += '    Write-Host "All checksums verified OK." -ForegroundColor Green'
    $scriptLines += '} else {'
    $scriptLines += '    Write-Host ""'
    $scriptLines += '    Write-Host "One or more checksums FAILED." -ForegroundColor Red'
    $scriptLines += '    exit 1'
    $scriptLines += '}'
    $scriptLines | Set-Content $verifyScript -Encoding UTF8
    Write-Host "[DONE] Verification script written to: $verifyScript" -ForegroundColor Green
    Write-Host ""

    # Summary
    Write-Host "========================================" -ForegroundColor Green
    Write-Host "PublishAll Process Completed Successfully!" -ForegroundColor Green
    Write-Host "========================================" -ForegroundColor Green
    Write-Host ""
    Write-Host "Output locations:" -ForegroundColor Cyan
    Write-Host "  - RequiresDotNet10: $projectDir\bin\Publish\RequiresDotNet10-win-x64\" -ForegroundColor White
    Write-Host "  - SelfContained:    $projectDir\bin\Publish\SelfContained-win-x64\" -ForegroundColor White
    Write-Host "  - Zip Package:      $InstallersDir\bin\Release\Timelapse-Executables.zip" -ForegroundColor White
    Write-Host "  - PerMachine MSI:   $InstallersDir\bin\Release\TimelapseInstaller-PerMachine.msi" -ForegroundColor White
    Write-Host "  - PerUser MSI:      $InstallersDir\bin\Release\TimelapseInstaller-PerUser.msi" -ForegroundColor White
    Write-Host ""
    Write-Host "All first-party binaries and MSI installers are signed." -ForegroundColor Cyan
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
