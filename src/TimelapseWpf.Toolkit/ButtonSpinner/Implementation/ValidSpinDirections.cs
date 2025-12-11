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

using System;

namespace TimelapseWpf.Toolkit
{
  /// <summary>
  /// Represents spin directions that are valid.
  /// </summary>
  [Flags]
  public enum ValidSpinDirections
  {
    /// <summary>
    /// Can not increase nor decrease.
    /// </summary>
    None = 0,

    /// <summary>
    /// Can increase.
    /// </summary>
    Increase = 1,

    /// <summary>
    /// Can decrease.
    /// </summary>
    Decrease = 2
  }
}
