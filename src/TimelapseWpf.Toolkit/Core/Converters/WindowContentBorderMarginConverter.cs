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
using System.Windows.Data;
using System.Globalization;
using System.Windows;

namespace TimelapseWpf.Toolkit.Core.Converters
{
  /// <summary>
  /// Sets the margin for the thumb grip, the top buttons, or for the content border in the WindowControl.
  /// </summary>
  public class WindowContentBorderMarginConverter : IMultiValueConverter
  {
    public object Convert( object[] values, Type targetType, object parameter, CultureInfo culture )
    {
      double horizontalContentBorderOffset = ( double )values[ 0 ];
      double verticalContentBorderOffset = ( double )values[ 1 ];

      switch( ( string )parameter )
      {
        // Content Border Margin in the WindowControl
        case "0":
          return new Thickness( horizontalContentBorderOffset
                              , 0d
                              , horizontalContentBorderOffset
                              , verticalContentBorderOffset );
        // Thumb Grip Margin in the WindowControl
        case "1":
          return new Thickness( 0d
                              , 0d
                              , horizontalContentBorderOffset
                              , verticalContentBorderOffset );
        // Header Buttons Margin in the WindowControl
        case "2":
          return new Thickness( 0d
                              , 0d
                              , horizontalContentBorderOffset
                              , 0d );
        default:
          throw new NotSupportedException( "'parameter' for WindowContentBorderMarginConverter is not valid." );
      }
    }

    public object[] ConvertBack( object value, Type[] targetTypes, object parameter, CultureInfo culture )
    {
      throw new NotImplementedException();
    }
  }
}
