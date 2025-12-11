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
using System.Windows;
using System.Windows.Data;

namespace TimelapseWpf.Toolkit.Converters
{
  public class BorderThicknessConverter : IValueConverter
  {
    public object Convert( object value, Type targetType, object parameter, CultureInfo culture )
    {
      // Use the left value on the right as Thickness.
      if( value is Thickness originalThickness )
        return new Thickness( originalThickness.Left, originalThickness.Top, originalThickness.Left, originalThickness.Bottom );

      return value;
    }

    public object ConvertBack( object value, Type targetType, object parameter, CultureInfo culture )
    {
      throw new NotImplementedException();
    }
  }
}
