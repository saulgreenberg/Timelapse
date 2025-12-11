# Timelapse

[![Download](https://img.shields.io/badge/Download-Timelapse-blue?style=flat-square)](https://timelapse.ucalgary.ca/download/) [![Documentation](https://img.shields.io/badge/Documentation-Website-blue?style=flat-square)](https://timelapse.ucalgary.ca/) [![Tutorial Guides](https://img.shields.io/badge/Guides-Tutorials-orange?style=flat-square)](https://timelapse.ucalgary.ca/guides/) [![Video Lessons](https://img.shields.io/badge/Videos-Lessons-red?style=flat-square&logo=youtube)](https://timelapse.ucalgary.ca/guides/) [![Version](https://img.shields.io/badge/version-2.4.0.1-green?style=flat-square)](https://timelapse.ucalgary.ca/versions/)

[![License](https://img.shields.io/badge/License-CC%20BY--NC--SA%204.0-lightgrey?style=flat-square)](LICENSE.md) [![Platform](https://img.shields.io/badge/platform-Windows%20x64-lightgrey?style=flat-square)](https://timelapse.ucalgary.ca/) [![.NET](https://img.shields.io/badge/.NET-8.0-512BD4?style=flat-square&logo=dotnet)](https://dotnet.microsoft.com/) [![GitHub Issues](https://img.shields.io/github/issues/saulgreenberg/Timelapse?style=flat-square)](https://github.com/saulgreenberg/Timelapse/issues) [![GitHub Stars](https://img.shields.io/github/stars/saulgreenberg/Timelapse?style=flat-square)](https://github.com/saulgreenberg/Timelapse/stargazers)

This repository, beginning with v2.4.0.1, contains the source code for [Timelapse](https://timelapse.ucalgary.ca). Timelapse is created and maintained by Saul Greenberg of Greenberg Consulting Inc. and the University of Calgary. 

Timelapse is an image analyser for camera traps. It is primarily used by scientists to visually analyze images and encode data as tags from thousands to millions of images and videos. While it is agnostic to its use, ecologists are currently the primary users. See  the [Timelapse web site](https://timelapse.ucalgary.ca) for packaged downloads and installers, various guides (including a QuickStart guide) and videos oriented towards end-users, and many other resources. 

Timelapse is currently in use across the world by various biologists / ecologists / scientists and resource managers within broadly varying institutions --- national and regional parks, ecological agencies, fishery departments, conservation societies, university groups, etc. --- for quite different needs (e.g., wildlife monitoring, fisheries usage, resource monitoring and management, social science studies etc). What they have in common is
* they collect extremely large numbers of images from one to very many field cameras
* they are interested in examining and tagging images (i.e., information describing image details)
* they want to create data fields specific to their projects (i.e., highly customizable data)
* they have their own needs and ways for performing analytics on that data.
Timelapse is optimized for those wanting to turn their images into meaningful data. Users can easily export the data for later analysis in another package of their choosing (e.g., R, spreadsheets, etc.)

Timelapse  works with image recognition data, where it integrates access to AddaxAI / Megadetector. Timelapse imports the recognition file produced by that software (or other compatible image recognition tool), and displays detected items within bounding boxes. Queries can be run against recognized entities.

### Contributing

Bug reports, feature requests, and feedback are most welcome. Let us know! We can't improve the system if we don't hear from you. If you wish to co-develop this project, see below. 

### History
Timelapse was originally designed for a fisheries biologist who ran many camera traps in Timelapse mode, hence its name. Over time, its interface and functionality has been extended to meet the needs of a broad variety of users who use camera traps in many different ways. 

While a few developers have contributed to Timelapse over the years, the overwhelming majority was (and continues to be) done as a solo effort by Saul Greenberg.
This repository begins at Timelapse Version 2.4.0.1. Earlier Timelapse versions can be supplied if needed by contacting Saul Greenberg.

## For Developers
If you wish to co-develop this project, contact saul@ucalgary.ca to see if our project goals coincide.

### Development environment
My build was created using [Visual Studio 2022](https://www.visualstudio.com/vs/)
* Common Tools -> GitHub Extension for Visual Studio 

Clone the repository locally using Visual Studio's Team Explorer, or by using a GIT interface such as SourceTree, or through GitHub's clone or download options

Development is against .NET 8 (net8.0-windows for WPF applications).

### Prerequisites
  * Build environment
    * Visual Studio 2022
    * .[Net 8 SDK](https:https://dotnet.microsoft.com/en-us/download/dotnet/8.0) (the .Net 8 desktop runtime works, but generates non-critical build errors as it does not contain System.Private.CoreLib.dll)
    * [C++ Redistributables](https://learn.microsoft.com/en-us/cpp/windows/latest-supported-vc-redist?view=msvc-170)   (used by TimelapseTemplateEditor and TimelapseViewOnly)
    * [WiX Toolset v6.0+](https://wixtoolset.org/) (only required if you are building MSI installers)
  * These exe's must be downloaded and located in the Timelapse folder (not the root folder)
    * [FFMPEG](https://www.ffmpeg.org/) - only ffmpeg.exe is required (the built release included other executables)
    * [Exiftool](https://exiftool.org/) - Download and unzip the Windows 64-bit build, then move the exiftool(-k).exe and the exiftool_files folder into the Timelapse folder.
  
### Building Timelapse
To successfully build Timelapse, do the following.
0. Timelapse uses two executables that are not included in this repository. These exes and their dependencies should be downloaded and copied into the Timelapse folder. See Prerequiesites
   * [FFMPEG](https://www.ffmpeg.org/) and
   * [ExifTool](https://exiftool.org/).
1. Using Visual Studio, open Timelapse.sln located in the top level folder, OR use the .NET CLI with `dotnet build Timelapse.sln`.
2. Build and try to run the projects:
   * Timelapse
   * TimelapseTemplateEditor (C++ wrapper)
   * TimelapseViewOnly (C++ wrapper)
3. Several pre-built dlls are already included and should be correctly referenced.
4. If something goes wrong, just contact me.

### Publishing and Creating Installers

Timelapse publishes three different installation methods, as described on the Timelapse web site and in the Installing and Running Timelapse section below.  To create the complete set of installers (zip package, per-machine MSI, and per-user MSI), you can use the automated publishing workflows below. Both options do the same thing

**Option 1: Run the PowerShell script directly (recommended)**
```powershell
powershell -ExecutionPolicy Bypass -File "src\Timelapse\Properties\PublishProfiles\PublishAll.ps1"
```

**Option 2: Set up Visual Studio External Tool**
1. In Visual Studio: Tools → External Tools → Add
2. Configure as follows:
   - **Title:** `Publish All Timelapse msi and zip files`
   - **Command:** `powershell.exe`
   - **Arguments:** `-ExecutionPolicy Bypass -NoProfile -File "$(ProjectDir)Properties\PublishProfiles\PublishAll.ps1"`
   - **Initial directory:** `$(ProjectDir)`
   - Check "Use Output window"
3. After setup, select the Timelapse project in Solution Explorer and run: Tools → Publish All Timelapse msi and zip files

**What the two options do:**
Both automated workflows executes all 5 publishing steps in sequence:
1. Publishes RequiresDotNet8-win-x64 build
2. Publishes SelfContained-win-x64 build
3. Builds the zip distribution package
4. Builds the PerMachine MSI installer
5. Builds the PerUser MSI installer

**Output locations:**
- Zip package: `Installers\bin\Release\Timelapse-Executables.zip`
- PerMachine MSI: `Installers\bin\Release\TimelapseInstaller-PerMachine.msi`
- PerUser MSI: `Installers\bin\Release\TimelapseInstaller-PerUser.msi`

For more details, see `Installers\Readme.txt` and the README files in each installer subdirectory.

### Also helpful are
* Atlassian's [SourceTree](https://www.atlassian.com/software/sourcetree), a recommended augmentation of the git support available in Visual Studio's Team Explorer.
*  [Nuget Package Manager](https://docs.nuget.org/ndocs/guides/install-nuget#nuget-package-manager-in-visual-studio) for accessing and including 3rd party packages used by Timelapse.

### Dependencies
* Timelapse software requires .NET 8.  It is included in both the per-user MSI and zip installation, but not included in the per-machine MSI installation.
* TimelapseTemplateEditor and TimelapseViewOnly require the C++ Redistributables. Necessary dlls from that are included in both the per-user MSI and zip installation, but not included in the per-machine MSI installation.
* Timelapse requires FFMPEG and ExifTool, that must be downloaded separately (see Building section)."
* Timelapse uses various Nuget packages to automatically retrieve other required dlls. 

### Other things
* The License file lists various required 3rd party dependencies, packages and executables. Each specify their own particular license terms.
* Timelapse is currently tested  on Windows 11 and - as far as we know - should run without issue on prior versions of Windows. 

### Installing and Running Timelapse
* Timelapse has three different installers: per user (self contained), per machine (requires .Net 8 install) and as a zip file (self-contained). All install the same software (excepting the separate .Net 8 install for per machine).
* Timelapse runs as a normal windows desktop application.
* If using the AddaxAI image recognizer, that runs best on a computer with a graphics processor as otherwise it's slow.
* Screen size of 1600 x 900 or larger is preferred, although it is usable on smaller displays.
