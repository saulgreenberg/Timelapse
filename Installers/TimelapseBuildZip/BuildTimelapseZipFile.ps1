# BuildTimelapseZipFile.ps1
# Creates a distribution package for Timelapse executables

param(
    [string]$SourcePath = "..\..\src\Timelapse\bin\Publish\SelfContained-win-x64\",
    [string]$TempFolder = "BuildTimelapseZipFile",
    [string]$ZipFileName = "Timelapse-Executables.zip"
)

Write-Host "=== Building Timelapse Distribution Package ===" -ForegroundColor Green
Write-Host ""

# Get the directory where this script is located
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$OutputDir = Join-Path (Split-Path $ScriptDir -Parent) "bin\Release"
$SourceFullPath = Join-Path $ScriptDir $SourcePath
$TempFullPath = Join-Path $ScriptDir $TempFolder
$ZipFullPath = Join-Path $OutputDir $ZipFileName

# Create output directory if it doesn't exist
if (-not (Test-Path $OutputDir)) {
    New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null
}

# Language folders to exclude
$LanguageFolders = @("cs-CZ", "de", "es", "fr", "hu", "it", "ja-JP", "pt-BR", "ro", "ru", "sv", "zh-Hans")

try {
    # Check if source directory exists
    if (-not (Test-Path $SourceFullPath)) {
        throw "Source directory not found: $SourceFullPath"
    }

    Write-Host "Source: $SourceFullPath"
    Write-Host "Creating temporary folder: $TempFullPath"

    # Clean up existing temp folder if it exists
    if (Test-Path $TempFullPath) {
        Remove-Item $TempFullPath -Recurse -Force
    }

    # Create temp folder structure
    New-Item -ItemType Directory -Path $TempFullPath -Force | Out-Null
    $NetFolder = Join-Path $TempFullPath "Timelapse-ExecutablesAndDependencyFiles"
    New-Item -ItemType Directory -Path $NetFolder -Force | Out-Null

    Write-Host "Copying files (excluding language folders)..."

    # Copy all items from source, excluding language folders
    Get-ChildItem $SourceFullPath | ForEach-Object {
        if ($_.PSIsContainer) {
            # It's a directory - check if it's a language folder
            if ($LanguageFolders -contains $_.Name) {
                Write-Host "  Skipping language folder: $($_.Name)" -ForegroundColor Yellow
            } else {
                Write-Host "  Copying folder: $($_.Name)"
                Copy-Item $_.FullName -Destination $NetFolder -Recurse -Force
            }
        } else {
            # It's a file - copy it
            Write-Host "  Copying file: $($_.Name)"
            Copy-Item $_.FullName -Destination $NetFolder -Force
        }
    }

    # Copy README-Instructions.txt if it exists
    $ReadmePath = Join-Path $ScriptDir "README-Instructions.txt"
    if (Test-Path $ReadmePath) {
        Copy-Item $ReadmePath -Destination $TempFullPath -Force
        Write-Host "  Copied README-Instructions.txt"
    }

    # Copy Timelapse web site - Tutorial Guides if it exists
    $TutorialPath = Join-Path $ScriptDir "Timelapse web site - Tutorial Guides.url"
    if (Test-Path $TutorialPath) {
        Copy-Item $TutorialPath -Destination $TempFullPath -Force
        Write-Host "  Copied Timelapse web site - Tutorial Guides.url"
    }

    Write-Host "Copying shortcut management scripts..."

    # Copy existing CreateTimelapseShortcutsAndFileAssociations.bat to temp folder
    $CreateBatSource = Join-Path $ScriptDir "CreateTimelapseShortcutsAndFileAssociations.bat"
    if (Test-Path $CreateBatSource) {
        Copy-Item $CreateBatSource -Destination $TempFullPath -Force
        Write-Host "  Copied CreateTimelapseShortcutsAndFileAssociations.bat"
    } else {
        Write-Host "  WARNING: CreateTimelapseShortcutsAndFileAssociations.bat not found in BuildTimelapse folder" -ForegroundColor Yellow
    }

    # Copy existing CreateTimelapseShortcuts.ps1 to NetFolder
    $CreatePs1Source = Join-Path $ScriptDir "CreateTimelapseShortcuts.ps1"
    if (Test-Path $CreatePs1Source) {
        Copy-Item $CreatePs1Source -Destination $NetFolder -Force
        Write-Host "  Copied CreateTimelapseShortcuts.ps1"
    } else {
        Write-Host "  WARNING: CreateTimelapseShortcuts.ps1 not found in BuildTimelapse folder" -ForegroundColor Yellow
    }

    # Copy existing UninstallTimelapseShortcuts.bat to temp folder
    $UninstallBatSource = Join-Path $ScriptDir "UninstallTimelapseShortcuts.bat"
    if (Test-Path $UninstallBatSource) {
        Copy-Item $UninstallBatSource -Destination $TempFullPath -Force
        Write-Host "  Copied UninstallTimelapseShortcuts.bat"
    } else {
        Write-Host "  WARNING: UninstallTimelapseShortcuts.bat not found in BuildTimelapse folder" -ForegroundColor Yellow
    }

    # Copy existing UninstallTimelapseShortcuts.ps1 to NetFolder
    $UninstallPs1Source = Join-Path $ScriptDir "UninstallTimelapseShortcuts.ps1"
    if (Test-Path $UninstallPs1Source) {
        Copy-Item $UninstallPs1Source -Destination $NetFolder -Force
        Write-Host "  Copied UninstallTimelapseShortcuts.ps1"
    } else {
        Write-Host "  WARNING: UninstallTimelapseShortcuts.ps1 not found in BuildTimelapse folder" -ForegroundColor Yellow
    }

    Write-Host "Creating zip file: $ZipFileName"

    # Remove existing zip file if it exists
    if (Test-Path $ZipFullPath) {
        Remove-Item $ZipFullPath -Force
    }

    # Create the zip file
    Compress-Archive -Path "$TempFullPath\*" -DestinationPath $ZipFullPath -Force

    Write-Host "Cleaning up temporary files..."
    Remove-Item $TempFullPath -Recurse -Force

    Write-Host ""
    Write-Host "=== Build Completed Successfully ===" -ForegroundColor Green
    Write-Host "Created: $ZipFileName" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "The zip file contains:" -ForegroundColor White
    Write-Host "  - Timelapse-Executables/ (runtime files, excluding language packs)"
    Write-Host "  - CreateTimelapseShortcutsAndFileAssociations.bat/.ps1 (shortcut and file association installer)"
    Write-Host "  - UninstallTimelapseShortcuts.bat/.ps1 (shortcut and file association remover)"
    Write-Host "  - README-Instructions.txt (if present)"
    Write-Host ""
    Write-Host "Extract the zip file and run CreateTimelapseShortcutsAndFileAssociations.bat to install shortcuts and file associations"
    Write-Host ""

} catch {
    Write-Host ""
    Write-Host "=== Build Failed ===" -ForegroundColor Red
    Write-Host "Error: $($_.Exception.Message)" -ForegroundColor Red

    # Clean up on error
    if (Test-Path $TempFullPath) {
        Remove-Item $TempFullPath -Recurse -Force
    }

    exit 1
}