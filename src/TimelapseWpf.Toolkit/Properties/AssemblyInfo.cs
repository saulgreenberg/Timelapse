/*************************************************************************************
   
   Toolkit for WPF
   Copyright (C) 2007-2019 Xceed Software Inc.
   This program is provided to you under the terms of the Microsoft Public
   License (Ms-PL) as published at https://opensource.org/license/ms-pl-html

   Fork origin: https://github.com/dotnetprojects/WpfExtendedToolkit
   - based on: https://github.com/xceedsoftware/wpftoolkit, Version 3
   This fork: modified for use in Timelapse project
    by Saul Greenberg, 2025 onwards

  ***********************************************************************************/

#region Using directives

using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Markup;

#endregion

// General Information about an assembly is controlled through the following 
// set of attributes. Change these attribute values to modify the information
// associated with an assembly.
[assembly: CLSCompliant(true)]
[assembly: ComVisible(false)]

[assembly: ThemeInfo(
    ResourceDictionaryLocation.None, //where theme specific resource dictionaries are located
    //(used if a resource is not found in the page,
    // or application resource dictionaries)
    ResourceDictionaryLocation.SourceAssembly //where the generic resource dictionary is located
    //(used if a resource is not found in the page,
    // app, or any theme specific resource dictionaries)
)]

//In order to begin building localizable applications, set 
//<UICulture>CultureYouAreCodingWith</UICulture> in your .csproj file
//inside a <PropertyGroup>.  For example, if you are using US english
//in your source files, set the <UICulture> to en-US.  Then uncomment
//the NeutralResourceLanguage attribute below.  Update the "en-US" in
//the line below to match the UICulture setting in the project file.

//[assembly: NeutralResourcesLanguage("en-US", UltimateResourceFallbackLocation.Satellite)]

[assembly: XmlnsPrefix("http://timelapsewpf.toolkit/xaml", "toolkit")]
[assembly: XmlnsDefinition("http://timelapsewpf.toolkit/xaml", "TimelapseWpf.Toolkit")]
[assembly: XmlnsDefinition("http://timelapsewpf.toolkit/xaml", "TimelapseWpf.Toolkit.Core.Converters" )]
[assembly: XmlnsDefinition("http://timelapsewpf.toolkit/xaml", "TimelapseWpf.Toolkit.Core.Input" )]
[assembly: XmlnsDefinition("http://timelapsewpf.toolkit/xaml", "TimelapseWpf.Toolkit.Core.Media")]
[assembly: XmlnsDefinition("http://timelapsewpf.toolkit/xaml", "TimelapseWpf.Toolkit.Core.Utilities")]
[assembly: XmlnsDefinition("http://timelapsewpf.toolkit/xaml", "TimelapseWpf.Toolkit.Chromes")]
[assembly: XmlnsDefinition("http://timelapsewpf.toolkit/xaml", "TimelapseWpf.Toolkit.Primitives")]
[assembly: XmlnsDefinition("http://timelapsewpf.toolkit/xaml", "TimelapseWpf.Toolkit.Zoombox" )]
[assembly: XmlnsDefinition("http://timelapsewpf.toolkit/xaml", "TimelapseWpf.Toolkit.Panels" )]
