# Timelapse
This repository contains the source code for and releases of [Timelapse](http://saul.cpsc.ucalgary.ca/timelapse/pmwiki.php?n=Main.HomePage), created by Saul Greenberg of the University of Calgary and Greenberg Consulting Inc.

Timelapse is an image analyser for camera traps. It is primarily used by scientists to visually analyze images and encode data as tags from thousands to millions  of images and videos. While it is agnostic to its use, ecologists are currently the primary users. See  the [Timelapse web site](https://timelapse.ucalgary.ca) for packaged downloads, a tutorial guide and manual oriented towards end-users, and other resources.

Timelapse is currently in use across the world by various biologists / ecologists / scientists and resource managers within broadly varying institutions --- national and regional parks, ecological agencies, fishery departments, conservation societies, university groups, etc. --- for quite different needs (e.g., wildlife monitoring, fisheries usage, resource monitoring and management, social science studies etc). What they have in common is
* they collect extremely large numbers of images from one to very many field cameras
* they are interested in examining and tagging images (i.e., information describing image details)
* they want to create data fields specific to their projects (i.e., highly customizable data)
* they have their own needs and ways for performing analytics on that data.
Timelapse is optimized for those wanting to turn their images into meaningful data. Users can easily export the data for later analysis in another package of their choosing (e.g., R, spreadsheets, etc.)

Timelapse  works with image recognition data, where it integrates access to EcoAssist / Megadetector. Timelapse imports the recognition file produced by that software (or other compatable image recognition tool), and displays detected items within bounding boxes. Queries can be run against recognized entities.

### Contributing

Bug reports, feature requests, and feedback are most welcome. Let us know! We can't improve the system if we don't hear from you. If you wish to co-develop this project, see below. 

### History
Timelapse was originally designed for a fisheries biologist who ran many camera traos in Timelapse mode, hence its name. Over time, its interface and functionality has been extended to meet the needs of a broad variety of user who use camera traps in many different ways. 

While a few developers have contributed to Timelapse over the years, the overwhelming majority was done as a solo effort by Saul Greenberg.
This repository begins at Timelapse Version 2.2.3.2. Earlier Timelapse versions are maintained in a different repository   named TimelapseDeprecated

## For Developers
If you wish to co-develop this project, contact saul@ucalgary.ca to see if our project goals coincide.

### Development environment
Install [Visual Studio](https://www.visualstudio.com/vs/), and then include the options below:

* Common Tools -> GitHub Extension for Visual Studio

After installation clone the repository locally through Visual Studio's Team Explorer or by using a GIT interface such as SourceTree.

* clone the repo locally through Visual Studio's Team Explorer or GitHub's clone or download options
* install Visual StyleCop (Tools -> Extensions and Updates -> Online)

Development is done uising .NET 4.8.1

### Building Timelapse
To run all parts of Timelapse, do the following.
1. Download and install ffmpeg.exe and exiftool.exe into the Dependencies folder. You can get the Windows executables from https://ffmpeg.org/ and   https://exiftool.org/ 
2. Open the Timelapse solution in the top level Timelapse folder (e.g., using Visual Studio).
3. Open the Nuget package manager and restore the required packages.
4. Build the following:
    - Timelapse
    - TimelapseTemplateEditor
    - TimelapseViewOnly
    - only if you need it: UpdateCSVFile 
    - you don't have to build DialogUpgradeFiles as the dll is already included.
5. Check that the folders x86 and x64, which should each contain an SQLite.interoper.dll file 
   are in your debug/release folders. If they aren't, copy those folders from the Dependencies folder.
   I am not sure why they are sometimes missing, but...
6. If something goes wrong, just contact me.

###Also helpful are

* Atlassian's [SourceTree](https://www.atlassian.com/software/sourcetree), a recommended augmentation of the git support available in Visual Studio's Team Explorer.
*  [Nuget Package Manager](https://docs.nuget.org/ndocs/guides/install-nuget#nuget-package-manager-in-visual-studio) for accessing and including 3rd party packages used by Timelapse.

### Dependencies
* Timelapse software requires .NET 4.8.1.
* Timelapse is currently tested  on Windows 11 and - as far as we know - should run without issue on all versions of Windows. End users may need to [install .NET 4.8](https://msdn.microsoft.com/en-us/library/bb822049.aspx) if it's not already present. 
* Timelapse uses various packages (including dlls available via NuGet). See the License file for a list of packages and their particular license terms.
* Screen size of 1600 x 900 or larger is preferred, although it is usable on smaller displays.
