# Timelapse MSI Installer (Per-Machine)

This directory contains the WiX Toolset-based MSI installer for Timelapse Image Analyzer (Per-Machine installation, framework-dependent version).

## Requirements

- WiX Toolset v6.0 or later
- .NET 8 SDK
- PowerShell 5.1 or later

## Building the Installer

### Prerequisites

1. Publish Timelapse with the framework-dependent profile first:
   ```batch
   cd ..
   dotnet publish Timelapse\Timelapse.csproj -p:PublishProfile=FrameworkDependent-win-x64
   ```

2. Ensure the publish folder exists at: `Timelapse\bin\Publish\FrameworkDependent-win-x64`


### Build Steps

#### Option 1: Using BuildInstaller.bat (Windows)
```batch
cd TimelapseInstaller-PerMachine
BuildInstaller.bat
```

#### Option 2: Manual Build
```batch
cd TimelapseInstaller-PerMachine

REM Step 1: Generate file list
powershell -ExecutionPolicy Bypass -File GenerateFileList.ps1

REM Step 2: Build MSI
wix build Product.wxs Files.wxs -arch x64 -ext WixToolset.UI.wixext -ext WixToolset.Netfx.wixext -out "bin\Release\TimelapseInstaller-PerMachine.msi" -d "SourceDir=..\Timelapse\bin\Publish\RequiresDotNet8-win-x64"
```

### Output

The MSI installer will be created at: `TimelapseInstaller-PerMachine\bin\Release\TimelapseInstaller-PerMachine.msi`

## Installer Features

### Installation Behavior

- **Installation Type**: Per-machine (requires administrator privileges, installs for all users)
- **License Agreement**: Displays custom LICENSE.txt and requires user acceptance
- **Installation Directory**: Default to `C:\Program Files\Timelapse` (user-changeable)
- **Platform**: x64 only
- **Upgrade Behavior**: Automatically uninstalls previous versions before installing
- **Runtime Requirement**: Requires .NET 8 Desktop Runtime to be installed separately

### Shortcuts

The installer automatically creates shortcuts for:
1. **Timelapse** - Main application
2. **Timelapse View Only** - Read-only mode
3. **Timelapse Template Editor** - Template editing tool

Shortcuts are created in:
- **Start Menu**: `Programs\Timelapse\`
- **Desktop**: Desktop shortcuts for each application

All shortcuts are created automatically during installation.

### File Associations

The installer registers Timelapse as a handler for `.tdb` (Timelapse Database) and `.ddb` (Timelapse Template) files. After installation:

**Setting Timelapse as Default Handler:**

Due to Windows security policies, the installer cannot automatically set Timelapse as the default application. Users must manually set it as the default handler using one of these methods:

**Method 1: Right-click on a file**
1. Right-click on a `.tdb` or `.ddb` file
2. Select "Open with" → "Choose another app"
3. Select "Timelapse" from the list
4. Check "Always use this app to open .tdb files" (or .ddb files)
5. Click "OK"

**Method 2: Windows Settings**
1. Open Windows Settings → Apps → Default apps
2. Scroll down and click "Choose default apps by file type"
3. Find `.tdb` and `.ddb` in the list
4. Click on the current default (or "Choose a default")
5. Select "Timelapse Image Analyzer"

Once set as the default, double-clicking `.tdb` or `.ddb` files will automatically open them in Timelapse.

### Publisher Information

- **Manufacturer**: Greenberg Consulting Inc. and University of Calgary
- **Version**: 2.4.0.0
- **Product Name**: Timelapse Image Analyzer

## Files Included

### Installer Source Files
- `BuildInstaller.bat` - Build script, which should be run by Saul
- `..\License.rtf` - License agreement in RTF format, expected in parent folder
- WelcomeDialog.bmp: An image displayed in the Initial and Last dialog.
- Banner.bmp: A banner displayed atop most dialog windows
- en-us.wxl: A custom welcome dialog. Note that the amount of text allowed in it is very limited.
- `Product.wxs` - Main WiX installer definition
- `Files.wxs` - Auto-generated file list (created by GenerateFileList.ps1)
- `GenerateFileList.ps1` - PowerShell script to generate Files.wxs


### Generated Files

- `Files.wxs` - Generated during build (contains ~547 components)
- `bin/Release/TimelapseInstaller-PerMachine.msi` - Final installer package (per-machine installation, framework-dependent, requires .NET 8 Desktop Runtime)

## Architecture Notes

### Component Structure

The installer uses two main component groups:

1. **ProductComponents**: All application files (~1016 files)
   - DLLs, executables, config files
   - Subdirectories (136 total) with proper directory structure

2. **MainExecutables**: Three main executable files
   - Timelapse.exe
   - Timelapse-ViewOnly.exe
   - TimelapseTemplateEditor.exe

### Directory Structure

The installer preserves the complete directory structure from the release folder, including:
- Language resource folders (cs, de, es, fr, hu, it, ja, ja-JP, ko, etc.)
- exiftool_files subdirectory
- All .NET runtime subdirectories

### External Dependencies

The installer includes:
- FFmpeg.exe (92MB) - Video processing
- ExifTool (exiftool(-k).exe) - Metadata extraction
- All .NET 8 runtime libraries
- All application dependencies

## Troubleshooting

### Build Errors

**Error**: "Release folder not found"
- **Solution**: Build Timelapse in Release mode first

**Error**: "WiX Toolset not found"
- **Solution**: Install WiX Toolset v6.0+ from https://wixtoolset.org/

**Error**: "License.rtf not found"
- **Solution**: Ensure License.rtf exists in TimelapseInstaller parent folder

### Installer Issues

**Issue**: Shortcuts not created
- All shortcuts are created by default. Check Start Menu and Desktop.

**Issue**: Previous version not uninstalled
- The installer uses MajorUpgrade with AllowSameVersionUpgrades=yes
- Previous versions should be automatically removed

**Issue**: Installation requires administrator
- Expected behavior - installation to Program Files requires admin rights

## Modifying the Installer

### Version Number
Should be extracted from Timelapse.exe

### Adding/Removing Files

Files are automatically discovered from the release folder. Just rebuild:
```batch
powershell -ExecutionPolicy Bypass -File GenerateFileList.ps1
```

### Modifying UI

The installer uses `WixUI_InstallDir` which provides:
- Welcome dialog
- License agreement (required)
- Installation directory selection
- Progress indicators
- Completion dialog

## License

This installer packages Timelapse Image Analyzer which is licensed under Creative Commons Attribution-NonCommercial-ShareAlike 4.0 International license. See LICENSE.txt for details.

## Support

For issues or questions:
- Email: saul@ucalgary.ca
- Website: https://saul.cpsc.ucalgary.ca/timelapse/
