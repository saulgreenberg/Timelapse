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
using System.Globalization;
using System.Windows.Data;

namespace TimelapseWpf.Toolkit.Converters
{
  public class ProgressBarWidthConverter : IMultiValueConverter
  {
    public object Convert( object[] values, Type targetType, object parameter, CultureInfo culture )
    { 
      var contentWidth = ( double )values[ 0 ];
      var parentMinWidth = ( double )values[ 1 ];

      return Math.Max( contentWidth, parentMinWidth );
    }

    public object[] ConvertBack( object value, Type[] targetTypes, object parameter, CultureInfo culture )
    {
      throw new NotImplementedException();
    }
  }
}
