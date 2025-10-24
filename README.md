# Timelapse
This repository contains the source code for and releases of [Timelapse](https://timelapse.ucalgary.ca). Timelapse is created and maintained by Saul Greenberg of Greenberg Consulting Inc. and the University of Calgary. 

Timelapse is an image analyser for camera traps. It is primarily used by scientists to visually analyze images and encode data as tags from thousands to millions of images and videos. While it is agnostic to its use, ecologists are currently the primary users. See  the [Timelapse web site](https://timelapse.ucalgary.ca) for packaged downloads, various guides (including a QuickStart guide) oriented towards end-users, and many other resources. 

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
This repository begins at Timelapse Version 2.4.0.0. Earlier Timelapse versions can be supplied if needed by contacting Saul Greenberg.

## For Developers
If you wish to co-develop this project, contact saul@ucalgary.ca to see if our project goals coincide.

### Development environment
My build was created using [Visual Studio 2022](https://www.visualstudio.com/vs/)
* Common Tools -> GitHub Extension for Visual Studio 

Clone the repository locally using Visual Studio's Team Explorer, or by using a GIT interface such as SourceTree, or through GitHub's clone or download options

Development is against .NET 8 (net8.0-windows for WPF applications).

### Prerequisites
  * Visual Studio 2022 (or .NET 8 SDK for CLI builds)
  * [FFMPEG](https://www.ffmpeg.org/)
  * [ExifTool](https://exiftool.org/)
  * 
### Building Timelapse
To successfully build Timelapse, do the following.
0. Timelapse uses two executables that are not included in this repository: [FFMPEG](https://www.ffmpeg.org/) and [ExifTool](https://exiftool.org/). These exes and their dependencies should be downloaded and copied into the Timelapse folder.
1. Using Visual Studio, open Timelapse.sln located in the top level folder, OR use the .NET CLI with `dotnet build Timelapse.sln`.
2. Build and try to run the projects:
   * Timelapse
   * TimelapseTemplateEditor (C++ wrapper)
   * TimelapseViewOnly (C++ wrapper)
3. Several pre-built dlls are already included and should be correctly referenced.
4. If something goes wrong, just contact me.

### Also helpful are
* Atlassian's [SourceTree](https://www.atlassian.com/software/sourcetree), a recommended augmentation of the git support available in Visual Studio's Team Explorer.
*  [Nuget Package Manager](https://docs.nuget.org/ndocs/guides/install-nuget#nuget-package-manager-in-visual-studio) for accessing and including 3rd party packages used by Timelapse.

### Dependencies
* Timelapse software requires .NET 8. It is included in both the per-user MSI and zip installation, but not included in the per-machine MSI installation.
* Timelapse requires FFMPEG and ExifTool, that must be downloaded separately (see Building section)."
* Timelapse uses various Nuget packages to automatically retrieve required dlls. 

### Other things
* The License file lists various required 3rd party dependencies, packages and executables. Each specify their own particular license terms.
* Timelapse is currently tested  on Windows 11 and - as far as we know - should run without issue on prior versions of Windows. 

### Installing and Running Timelapse
* Timelapse has three different installers: per user (self contained), per machine (requires .Net 8 install) and as a zip file (self-contained). All install the same software (excepting the separate .Net 8 install for per machine).
* Timelapse runs as a normal windows desktop application.
* If using the AddaxAI image recognizer, that runs best on a computer with a graphics processor as otherwise it's slow.
* Screen size of 1600 x 900 or larger is preferred, although it is usable on smaller displays.
