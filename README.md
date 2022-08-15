# Timelapse2
This repository contains the source code for and releases of [Timelapse 2](http://saul.cpsc.ucalgary.ca/timelapse/pmwiki.php?n=Main.HomePage), created by Saul Greenberg of the University of Calgary and Greenberg Consulting Inc.

Timelapse2 is an Image Analyser for Camera Traps, where it is used by scientists to visually analyze and encode data as tags from thousands to millions  of images and videos. See  the [Timelapse web site](http://saul.cpsc.ucalgary.ca/timelapse/pmwiki.php?) for packaged downloads, a tutorial guide and manual oriented towards end-users, and other resources.

Timelapse is currently in use across the world by various biologists / ecologists / scientists and resource managers within broadly varying institutions --- national and regional parks, ecological agencies, fishery departments, conservation societies, university groups, etc. --- for quite different needs (e.g., wildlife monitoring, fisheries usage, resource monitoring and management, social science studies etc). What they have in common is
* they collect extremely large numbers of images from one to very many field cameras
* they are interested in examining and tagging images (i.e., information describing image details), where tags that is usually specific to their projects
* they have their own needs and ways for performing analytics on that data.
Timelapse is optimized for the tagging step, where users then export the data tags so that it can be analyzed in another package of their choosing (e.g., R, spreadsheets, etc.)

Timelapse also works with image recognition data, particularly using recognition data from Microsoft's Megadetector.Timelapse imports the recognition file produced by Megadetector (or other compatable image recognition tool), and displays detected items within bounding boxes. Queries can be run against recognized entities.

### Contributing

Bug reports, feature requests, and feedback are most welcome. Let us know! We can't improve the system if we don't hear from you. If you wish to co-develop this project, see below. 

### History
Timelapse was originally designed for a fisheries biologist who ran many camera traos in Timelapse mode, hence its name. Over time, its interface and functionality has been extended to meet the needs of a broad variety of user who use camera traps in many different ways. 

While a few developers have contributed to Timelapse over the years, the vast majority has been done by Saul Greenberg.
This repository begins at Timelapse Version 2.2.3.0. Earlier Timelapse versions are maintained in a different repository

## For Developers
If you wish to co-develop this project, contact saul@ucalgary.ca to see if our project goals coincide.

### Development environment
Install [Visual Studio](https://www.visualstudio.com/vs/), and then include the options below:

* Common Tools -> GitHub Extension for Visual Studio

After installation clone the repository locally through Visual Studio's Team Explorer or by using a GIT interface such as SourceTree.

* clone the repo locally through Visual Studio's Team Explorer or GitHub's clone or download options
* install Visual StyleCop (Tools -> Extensions and Updates -> Online)

Development is currently done against .NET 4.8.

Also helpful are

* Atlassian's [SourceTree](https://www.atlassian.com/software/sourcetree), a recommended augmentation of the git support available in Visual Studio's Team Explorer.
*  [Nuget Package Manager](https://docs.nuget.org/ndocs/guides/install-nuget#nuget-package-manager-in-visual-studio) for accessing and including 3rd party packages used by Timelapse.
* The [Visual Studio Image Library](https://msdn.microsoft.com/en-us/library/ms246582.aspx) for icons.
* John Skeet's discussion of [DateTime, DateTimeOffset, and TimeZoneInfo limitations](http://blog.nodatime.org/2011/08/what-wrong-with-datetime-anyway.html).

### Dependencies
* Timelapse software requires .NET 4.8.
* Timelapse is tested on Windows 10 and - as far as we know - should run without issue on all versions of Windows. End users may need to [install .NET 4.8](https://msdn.microsoft.com/en-us/library/bb822049.aspx) if it's not already present. 
* Timelapse uses various packages (including dlls available via NuGet). See the License file for a list of packages and their particular license terms.
* Screen size of 1600 x 900 or larger is preferred, although it is usable on smaller displays.

