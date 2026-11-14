# Licensing and Third-Party Notices

## Timelapse License

**Timelapse Image Analyzer**
 Copyright © 2011+ Greenberg Consulting Inc. & University of Calgary

Timelapse is free software: you can redistribute it and/or modify it under the terms of the GNU General Public License as published by the Free Software Foundation, either version 3 of the License, or (at your option) any later version. The full license text is in the LICENSE file, and at https://www.gnu.org/licenses/gpl-3.0.html.

SPDX-License-Identifier: GPL-3.0-or-later

### Additional Permission (GPLv3 section 7)
As an additional permission under section 7 of the GPLv3, you may combine Timelapse with the third-party components listed in this file that are licensed under the Microsoft Public License (MS-PL), the NReco Video Converter license, or the Six Labors license, and distribute the result. Those components remain under their own licenses.

### Dual Licensing
Timelapse is available under two licenses:
- **GPLv3 or later.** This is free of charge, for any purpose, including commercial work. Simply using Timelapse, even within a company, places no obligations on you. The GPL's conditions apply only when you distribute Timelapse or software derived from it. To share unmodified Timelapse, including it with the license and pointing to the source code on GitHub is enough.
- **Commercial license.** Anyone may share or distribute Timelapse without a commercial license, including on their own website, as long as they follow the GPL, for example by including the license and linking to the Timelapse source code. A commercial license is only needed if you want to modify Timelapse or build it into your own software, and then distribute the result without releasing that software's source code under the GPL. To arrange one, contact Saul Greenberg (saul@ucalgary.ca).

A commercial license covers only code owned by Greenberg Consulting Inc. and the University of Calgary. Third-party components stay under their own licenses.

### Quick Summary of GPLv3
- **Permissions:** you may use Timelapse for any purpose, including commercially, and copy, modify and distribute it.
- **Conditions:** if you distribute Timelapse, modified or not, you must license it under the GPLv3 (or later), make the matching source code available to those who receive it, keep the copyright and license notices, and mark any changes you made.
- **No warranty:** the software is provided "as is", without warranty or liability.

This summary is not the license; the full text in LICENSE governs.

### Third-party component licensing is separate
Timelapse includes third-party components and executables, as listed in the Third-Party Components section along with a summary of their license and a link to their actual license. 
You are responsible for ensuring compliance with all third-party licenses (see below), which in particular cases may require separate arrangements via their vendor. 
They all generally allow commercial use, but it is your responsibility to check their license terms. 

---

## Overview

Timelapse is a tool to simplify analysis of large numbers of images and videos, typically captured from remote cameras (camera traps). 
It allows users to review and enter data describing those images, optionally assisted by image recognition. 
Timelapse is agnostic to what is being analyzed, as it allows users to create custom data fields to fit their particular purposes. 
In practice, it is broadly used for environmental work, where the results are often used for the public good.

---

## Typical Users
Its primary users tend to be ecologists, biologists, fishery scientists, researchers, students, and similar users analyzing camera trap imagery for environmental research. 
Organizations using Timelapse include universities, national and regional park services, environmental consulting firms, zoos, independent consultants, and the like. 

---

## Usage Guidelines

### Source Code Modification

- You may obtain the source code and modify it. If you distribute your modified version, you must also release its source code under the GPLv3 (or later), or have a commercial license.
- If you would like to work directly with the current repository, contact Saul Greenberg at saul@ucalgary.ca

---

## Third-Party Components
While a brief summary is included, it is your responsibility to check each component's license terms to determine accuracy and details.
Generally speaking, these licenses allow commercial use as expected in typical Timelapse use and deployments.

### Included Executables

**ExifTool** by Phil Harvey
- License: Perl Artistic License / GPL
- Free to redistribute & modify
- https://exiftool.org/#license

**FFmpeg**
- License: GNU Lesser General Public License (LGPL) v2.1
- Free software to share and modify
- Source code: https://ffmpeg.org/download.html (the release matching the version included with Timelapse)
- https://ffmpeg.org/legal.html

---

### NuGet Packages (DLLs)

**CsvHelper** by Josh Close
- License: Used under Apache 2.0
- Free for commercial use
- https://joshclose.github.io/CsvHelper/

**Dirkster.AvalonDock** 
- License: Microsoft Public License MS-PL.
- Free for commercial use
- https://github.com/Dirkster99/AvalonDock 

**MetadataExtractor + XmpCore** by Drew Noakes
- License: Apache 2.0
- Free for commercial use
- https://www.nuget.org/packages/MetadataExtractor/

**Microsoft EntityFrameworkCore**
- License: MIT
- Free for commercial use
- https://github.com/dotnet/efcore

**Newtonsoft.Json**
- License: MIT license
- Free for commercial use
- https://www.newtonsoft.com/json

**NReco Video Converter**
- License (free version): See https://www.nrecosite.com/video_converter_net.aspx
- Free for commercial use for non-SaaS .NET apps that have only one single-server production deployment. Timelapse fits this free use description: it is a desktop app that normally runs on either a desktop or single server.
 - ⚠️ **If in doubt or if you have an unusual deployment setup - review their terms**. 
- https://www.nrecosite.com/video_converter_net.aspx

**SixLabors.ImageSharp**
- License: Six Labors community free developer license granted for Timelapse
- ⚠️ Review their terms to understand restrictions, if any
- https://sixlabors.com/pricing/

**System.Data.SQLite**
- License: Public Domain
- No license required
- https://system.data.sqlite.org/

### Modified Sources Included in Timelapse

**ExifToolWrapper**
- License: Public Domain
- https://github.com/FileMeta/ExifToolWrapper/blob/master/license.md

**DotNetProjects.Extended.Wpf.Toolkit** 
- Timelapse includes a module TimelapseWpf.Toolkit, which is based on a (now renamed and heavily modified) fork of the Extended.Wpf.Toolkit, which is itself a fork of an early version of Xceed software that was published with a MS-PL license.
- License: Microsoft Public License MS-PL.
- Free for commercial use
- https://github.com/dotnetprojects/WpfExtendedToolkit

---

## Disclaimer

See sections 15 and 16 of the GPLv3.

---

## Contact
For any questions, including licensing questions or to arrange a commercial license
- Saul Greenberg saul@ucalgary.ca
  (Greenberg Consulting Inc. / University of Calgary)

---

**Last Updated:** 2026-11-14
**Timelapse Version:** 2.5.3.0+
