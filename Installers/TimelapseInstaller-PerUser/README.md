# Timelapse MSI Installer (Per-User)

This directory contains the WiX Toolset-based MSI installer for Timelapse Image Analyzer (Per-User installation, includes .NET 8 Desktop Runtime ).

## Requirements

- WiX Toolset v6.0 or later
- .NET 8 SDK
- PowerShell 5.1 or later

## Building the Installer

### Prerequisites

1. Build Timelapse in Release mode first:
   ```batch
   cd ..
   dotnet build Timelapse\Timelapse.csproj --configuration Release
   ```

2. Ensure the release folder exists at: `Timelapse\bin\Release\net8.0-windows\win-x64`

3. Remove uncessary files in: `Timelapse\bin\Release\net8.0-windows\win-x64`
- e.g., language files


### Build Steps

#### Option 1: Using BuildInstaller.bat (Windows)
```batch
cd TimelapseInstaller-PerUser
BuildInstaller.bat
```

#### Option 2: Manual Build
```batch
cd TimelapseInstaller-PerUser

REM Step 1: Generate file list
powershell -ExecutionPolicy Bypass -File GenerateFileList.ps1

REM Step 2: Build MSI
wix build Product.wxs Files.wxs -arch x64 -ext WixToolset.UI.wixext -out "bin\Release\TimelapseInstaller-PerUser.msi" -d "SourceDir=..\Timelapse\bin\Release\net8.0-windows\win-x64"
```

### Output

The MSI installer will be created at: `TimelapseInstaller-PerUser\bin\Release\TimelapseInstaller-PerUser.msi`

## Installer Features

### Installation Behavior

- **Installation Type**: Per-user (does not require administrator privileges)
- **License Agreement**: Displays custom LICENSE.txt and requires user acceptance
- **Installation Directory**: Default to `%LOCALAPPDATA%\Programs\Timelapse` (e.g., `C:\Users\[Username]\AppData\Local\Programs\Timelapse`)
- **Platform**: x64 only
- **Upgrade Behavior**: Automatically uninstalls previous versions before installing

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
- **Version**: 2.5.0.0
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

- `Files.wxs` - Generated during build (contains ~1019 components)
- `bin/Release/TimelapseInstaller-PerUser.msi` - Final installer package (per-user installation, includes .NET 8 Desktop Runtime, ~126MB)

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

**Issue**: Installation folder is read-only
- Not applicable - per-user installations in AppData\Local\Programs are writable by the user

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
