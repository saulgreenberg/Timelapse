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
  public class TimeItem(string display, TimeSpan time)
  {
    public string Display
    {
      get;
      set;
    } = display;

    public TimeSpan Time
    {
      get;
      set;
    } = time;

    #region Base Class Overrides

    public override bool Equals( object obj )
    {
      if( obj is TimeItem item )
        return Time == item.Time;
      else
        return false;
    }

    public override int GetHashCode()
    {
      return Time.GetHashCode();
    }

    #endregion //Base Class Overrides
  }
}
