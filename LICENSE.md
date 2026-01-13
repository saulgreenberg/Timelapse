# License

## Primary License

**Timelapse Image Analyzer**
 Copyright © 2011+ Greenberg Consulting Inc. & University of Calgary

Licensed under Creative Commons Attribution-NonCommercial-ShareAlike 4.0 International (CC BY-NC-SA 4.0)
 https://creativecommons.org/licenses/by-nc-sa/4.0/

 SPDX-License-Identifier: CC-BY-NC-SA-4.0

---

## Overview

Timelapse is a tool to simplify analysis of large numbers of images and videos, typically captured from remote cameras. It allows users to review and enter data describing those images, optionally assisted by image recognition. Timelapse is agnostic to what is being analyzed, as it allows users to create custom data fields to fit their particular purposes.

---

## Usage Guidelines

### Non-Commercial Use

Timelapse is freely available for non-commercial use by a broad audience, although its primary users are currently ecologists, biologists, fishery scientists, researchers, students, and similar users analyzing camera trap imagery for environmental research. We encourage the free use of Timelapse executables for these intended purposes, where the results are used for the public good.

Timelapse includes third-party components and executables, as listed in the Third-Party Components section along with a summary of their license and a link to their actual license. They all generally allow non-commercial use, but it is your responsibility to check their license terms. 

### Source Code Modification

To comply with the license terms when modifying source code:
- Fork the project to your own open repository (similar to this one)
- Include this license (or at least a visible link to it) in your fork
- Receive no monetary compensation for the changes you make
- If you would like to work directly with the current repository, contact Saul Greenberg at saul@ucalgary.ca

### Commercial Use

**Commercial use requires prior arrangement.** Contact saul@ucalgary.ca to discuss your situation and, if required, licensing terms.

**IMPORTANT:** Even if granted permission to use Timelapse for commercial purposes, you are responsible for ensuring compliance with all third-party licenses (see below), which may require separate arrangements via their vendor.

---

## Third-Party Components
While a brief summary is include, it is your responsibility to visit each web site and their license terms to determine accuracy and details.

### Included Executables

**ExifTool** by Phil Harvey
- License: Perl Artistic License / GPL
- Free to redistribute & modify
- https://exiftool.org/#license

**FFmpeg**
- License: GNU LGPL v2.1
- Free software to share and modify
- https://ffmpeg.org/legal.html

---

### NuGet Packages (DLLs) – Licenses are free for commercial use

**CsvHelper** by Josh Close
- License: Microsoft Public License MS-PL and/or Apache 2.0
- Free for commercial use
- https://joshclose.github.io/CsvHelper/

**Dirkster.AvalonDeck** 
- License: Microsoft Public License MS-PL.
- Free for commercial use
- https://github.com/Dirkster99/AvalonDock 

**MetadataExtractor + XmpCore** by Drew Noakes
- License: Apache 2.0
- Free to commercial use
- https://www.nuget.org/packages/MetadataExtractor/

**Microsoft EntityFrameworkCore**
- License: MIT
- Free for commercial use
- https://github.com/dotnet/efcore

**Newtonsoft.Json**
- License: MIT licemse
- Free for commercial use
- https://www.newtonsoft.com/json

**NReco Video Converter**
- License: Non-commercial use only (free version)
- ⚠️ **Commercial license may be required in some cases - review their terms**
- Note: Used by Timelapse to convert video frame into bitmap; other conversion options available if needed - Contact Saul Greenberg for more information.
- https://www.nrecosite.com/video_converter_net.aspx

**SixLabors.ImageSharp**
- License: Six Labors community free developer license granted for Timelapse
- ⚠️ As Timelapse is open source, it appears that it is free for commercial use. However, you should review their terms to understand restrictions, if any
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

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.

---

## Contact
- Saul Greenberg saul@ucalgary.ca
  (Greenberg Consulting Inc. / University of Calgary)

---

**Last Updated:** 2026-01-12
**Timelapse Version:** 2.5.0.1+
