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

using System.Reflection;
using System.Windows.Input;
using TimelapseWpf.Toolkit.Core.Utilities;

namespace TimelapseWpf.Toolkit.Zoombox
{
  public class ZoomboxCursors
  {
    #region Constructors

    static ZoomboxCursors()
    {
      // Load custom cursors directly - .NET 8 always runs with full trust
      Zoom = new( ResourceHelper.LoadResourceStream( Assembly.GetExecutingAssembly(), "Zoombox/Resources/Zoom.cur" ) );
      ZoomRelative = new( ResourceHelper.LoadResourceStream( Assembly.GetExecutingAssembly(), "Zoombox/Resources/ZoomRelative.cur" ) );
    }

    #endregion

    #region Zoom Static Property

    public static Cursor Zoom { get; }

    #endregion

    #region ZoomRelative Static Property

    public static Cursor ZoomRelative { get; }

    #endregion
  }
}
