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

namespace TimelapseWpf.Toolkit
{
  /// <summary>
  /// Represents spin directions that could be initiated by the end-user.
  /// </summary>
  /// <QualityBand>Preview</QualityBand>
  public enum SpinDirection
  {
    /// <summary>
    /// Represents a spin initiated by the end-user in order to Increase a value.
    /// </summary>
    Increase = 0,

    /// <summary>
    /// Represents a spin initiated by the end-user in order to Decrease a value.
    /// </summary>
    Decrease = 1
  }
}
