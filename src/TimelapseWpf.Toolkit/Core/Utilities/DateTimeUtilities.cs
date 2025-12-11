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

namespace TimelapseWpf.Toolkit.Core.Utilities
{
  internal static class DateTimeUtilities
  {
    public static DateTime GetContextNow( DateTimeKind kind )
    {
      if( kind == DateTimeKind.Unspecified )
        return DateTime.SpecifyKind( DateTime.Now, DateTimeKind.Unspecified );

      return ( kind == DateTimeKind.Utc )
        ? DateTime.UtcNow
        : DateTime.Now;
    }

    public static bool IsSameDate( DateTime? date1, DateTime? date2 )
    {
      if( date1 == null || date2 == null )
        return false;

      return ( date1.Value.Date == date2.Value.Date );
    }
  }
}
