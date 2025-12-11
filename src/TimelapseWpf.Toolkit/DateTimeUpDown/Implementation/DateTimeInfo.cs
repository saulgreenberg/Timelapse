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
  internal class DateTimeInfo
  {
    public string Content
    {
      get;
      set;
    }
    public string Format
    {
      get;
      set;
    }
    public bool IsReadOnly
    {
      get;
      set;
    }
    public int Length
    {
      get;
      set;
    }
    public int StartPosition
    {
      get;
      set;
    }
    public DateTimePart Type
    {
      get;
      set;
    }
  }
}
