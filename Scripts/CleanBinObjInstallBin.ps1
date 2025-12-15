# CleanBinObjInstallBin.ps1
# Complete cleanup script for Timelapse project
# Removes all bin, obj folders, installer outputs, and caches for a fresh rebuild

param(
    [switch]$WhatIf,
    [switch]$Verbose
)

$ErrorActionPreference = "Continue"
$rootPath = "D:\@Timelapse\Timelapse"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Timelapse Complete Cleanup Script" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

if ($WhatIf) {
    Write-Host "Running in WhatIf mode - no files will be deleted" -ForegroundColor Yellow
    Write-Host ""
}

# Function to remove directory if it exists
function Remove-DirectoryIfExists {
    param(
        [string]$Path,
        [string]$Description
    )

    if (Test-Path $Path) {
        Write-Host "[CLEANING] $Description" -ForegroundColor Yellow
        if ($Verbose) {
            Write-Host "  Path: $Path" -ForegroundColor Gray
        }

        if (-not $WhatIf) {
            try {
                Remove-Item -Path $Path -Recurse -Force -ErrorAction Stop
                Write-Host "[DELETED]  $Description" -ForegroundColor Green
            } catch {
                Write-Host "[ERROR]    Failed to delete $Description`: $($_.Exception.Message)" -ForegroundColor Red
            }
        } else {
            Write-Host "[WHATIF]   Would delete $Description" -ForegroundColor Cyan
        }
    } else {
        if ($Verbose) {
            Write-Host "[SKIP]     $Description (not found)" -ForegroundColor Gray
        }
    }
}

# 1. Clean all bin and obj folders in src directory
Write-Host "Step 1: Removing bin and obj folders from all projects..." -ForegroundColor Cyan
Write-Host ""

$projectFolders = @(
    "$rootPath\src\Timelapse",
    "$rootPath\src\TimelapseWpf.Toolkit",
    "$rootPath\src\TimelapseTemplateEditor",
    "$rootPath\src\TimelapseViewOnly"
)

foreach ($projectFolder in $projectFolders) {
    if (Test-Path $projectFolder) {
        $projectName = Split-Path $projectFolder -Leaf

        Remove-DirectoryIfExists -Path "$projectFolder\bin" -Description "$projectName\bin (entire folder)"
        Remove-DirectoryIfExists -Path "$projectFolder\obj" -Description "$projectName\obj (entire folder)"
    }
}

Write-Host ""

# 2. Clean Installers bin folder
Write-Host "Step 2: Removing Installer output folders..." -ForegroundColor Cyan
Write-Host ""

Remove-DirectoryIfExists -Path "$rootPath\Installers\bin" -Description "Installers bin folder"
Remove-DirectoryIfExists -Path "$rootPath\Installers\TimelapseBuildZip\bin" -Description "TimelapseBuildZip bin folder"
Remove-DirectoryIfExists -Path "$rootPath\Installers\TimelapseInstaller-PerMachine\bin" -Description "PerMachine installer bin folder"
Remove-DirectoryIfExists -Path "$rootPath\Installers\TimelapseInstaller-PerMachine\obj" -Description "PerMachine installer obj folder"
Remove-DirectoryIfExists -Path "$rootPath\Installers\TimelapseInstaller-PerUser\bin" -Description "PerUser installer bin folder"
Remove-DirectoryIfExists -Path "$rootPath\Installers\TimelapseInstaller-PerUser\obj" -Description "PerUser installer obj folder"

Write-Host ""

# 3. Clean Visual Studio .vs folder
Write-Host "Step 3: Cleaning Visual Studio cache..." -ForegroundColor Cyan
Write-Host ""

Remove-DirectoryIfExists -Path "$rootPath\.vs" -Description "Visual Studio .vs folder"

Write-Host ""

# 4. Clean Publish folders
Write-Host "Step 4: Cleaning Publish output folders..." -ForegroundColor Cyan
Write-Host ""

Remove-DirectoryIfExists -Path "$rootPath\src\Timelapse\bin\Publish" -Description "Timelapse Publish folder"

Write-Host ""

# 5. Clean NuGet packages cache (optional - only local project cache)
Write-Host "Step 5: Cleaning project-level NuGet cache..." -ForegroundColor Cyan
Write-Host ""

Remove-DirectoryIfExists -Path "$rootPath\packages" -Description "NuGet packages folder (if exists)"

Write-Host ""

# 6. Clean old .NET 8 folders (no longer needed after upgrade to .NET 10)
Write-Host "Step 6: Cleaning old .NET 8 output folders..." -ForegroundColor Cyan
Write-Host ""

foreach ($projectFolder in $projectFolders) {
    if (Test-Path $projectFolder) {
        $projectName = Split-Path $projectFolder -Leaf

        Remove-DirectoryIfExists -Path "$projectFolder\bin\Debug\net8.0-windows" -Description "$projectName\bin\Debug\net8.0-windows (old .NET 8)"
        Remove-DirectoryIfExists -Path "$projectFolder\bin\Release\net8.0-windows" -Description "$projectName\bin\Release\net8.0-windows (old .NET 8)"
    }
}

Write-Host ""

# 7. Clean any temporary build files
Write-Host "Step 7: Cleaning temporary build files..." -ForegroundColor Cyan
Write-Host ""

# Clean C++ build artifacts
if (Test-Path "$rootPath\src\TimelapseTemplateEditor") {
    $cppFiles = @("*.obj", "*.res")
    foreach ($pattern in $cppFiles) {
        $files = Get-ChildItem -Path "$rootPath\src\TimelapseTemplateEditor" -Filter $pattern -ErrorAction SilentlyContinue
        foreach ($file in $files) {
            if ($Verbose) {
                Write-Host "  Removing: $($file.FullName)" -ForegroundColor Gray
            }
            if (-not $WhatIf) {
                Remove-Item $file.FullName -Force -ErrorAction SilentlyContinue
            }
        }
    }
}

if (Test-Path "$rootPath\src\TimelapseViewOnly") {
    $cppFiles = @("*.obj", "*.res")
    foreach ($pattern in $cppFiles) {
        $files = Get-ChildItem -Path "$rootPath\src\TimelapseViewOnly" -Filter $pattern -ErrorAction SilentlyContinue
        foreach ($file in $files) {
            if ($Verbose) {
                Write-Host "  Removing: $($file.FullName)" -ForegroundColor Gray
            }
            if (-not $WhatIf) {
                Remove-Item $file.FullName -Force -ErrorAction SilentlyContinue
            }
        }
    }
}

Write-Host "[DONE]     Temporary files cleaned" -ForegroundColor Green
Write-Host ""

# 8. Verify cleanup - check if folders still exist
if (-not $WhatIf) {
    Write-Host "Step 8: Verifying cleanup..." -ForegroundColor Cyan
    Write-Host ""

    $stillExist = @()

    # Check project bin/obj folders
    foreach ($projectFolder in $projectFolders) {
        if (Test-Path $projectFolder) {
            $projectName = Split-Path $projectFolder -Leaf

            if (Test-Path "$projectFolder\bin") {
                $stillExist += "$projectName\bin"
            }
            if (Test-Path "$projectFolder\obj") {
                $stillExist += "$projectName\obj"
            }
        }
    }

    # Check installer folders
    $installerFolders = @(
        "$rootPath\Installers\bin",
        "$rootPath\Installers\TimelapseBuildZip\bin",
        "$rootPath\Installers\TimelapseInstaller-PerMachine\bin",
        "$rootPath\Installers\TimelapseInstaller-PerMachine\obj",
        "$rootPath\Installers\TimelapseInstaller-PerUser\bin",
        "$rootPath\Installers\TimelapseInstaller-PerUser\obj"
    )

    foreach ($folder in $installerFolders) {
        if (Test-Path $folder) {
            $relativePath = $folder.Replace("$rootPath\", "")
            $stillExist += $relativePath
        }
    }

    # Check .vs folder
    if (Test-Path "$rootPath\.vs") {
        $stillExist += ".vs"
    }

    # Check publish folder
    if (Test-Path "$rootPath\src\Timelapse\bin\Publish") {
        $stillExist += "src\Timelapse\bin\Publish"
    }

    if ($stillExist.Count -gt 0) {
        Write-Host "[WARNING]  Some folders were NOT deleted:" -ForegroundColor Yellow
        Write-Host ""
        foreach ($folder in $stillExist) {
            Write-Host "  - $folder" -ForegroundColor Yellow
        }
        Write-Host ""
        Write-Host "Possible reasons:" -ForegroundColor Yellow
        Write-Host "  - Visual Studio is still running (close all instances)" -ForegroundColor White
        Write-Host "  - Files are locked by another process" -ForegroundColor White
        Write-Host "  - Insufficient permissions" -ForegroundColor White
        Write-Host "  - Files are read-only" -ForegroundColor White
        Write-Host ""
        Write-Host "Try:" -ForegroundColor Cyan
        Write-Host "  1. Close ALL Visual Studio instances" -ForegroundColor White
        Write-Host "  2. Close any file explorers showing these folders" -ForegroundColor White
        Write-Host "  3. Run this script again" -ForegroundColor White
        Write-Host ""
    } else {
        Write-Host "[SUCCESS]  All folders verified as deleted!" -ForegroundColor Green
        Write-Host ""
    }
}

Write-Host ""

# Summary
Write-Host "========================================" -ForegroundColor Green
if ($WhatIf) {
    Write-Host "Cleanup Preview Completed!" -ForegroundColor Green
    Write-Host "Run without -WhatIf to actually delete files" -ForegroundColor Yellow
} else {
    Write-Host "Cleanup Completed Successfully!" -ForegroundColor Green
}
Write-Host "========================================" -ForegroundColor Green
Write-Host ""
Write-Host "Next steps:" -ForegroundColor Cyan
Write-Host "  1. Rebuild the solution in Visual Studio" -ForegroundColor White
Write-Host "  2. Or run: dotnet build D:\@Timelapse\Timelapse\src\Timelapse\Timelapse.csproj" -ForegroundColor White
Write-Host ""
