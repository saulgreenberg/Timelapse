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
using System.Windows.Media;

namespace TimelapseWpf.Toolkit.Core.Converters
{
  public class SolidColorBrushToColorConverter : IValueConverter
  {
    #region IValueConverter Members

    public object Convert( object value, Type targetType, object parameter, System.Globalization.CultureInfo culture )
    {
      if( value is SolidColorBrush brush )
        return brush.Color;

      return default( Color? );
    }

    public object ConvertBack( object value, Type targetType, object parameter, System.Globalization.CultureInfo culture )
    {
      if( value != null )
      {
        Color color = ( Color )value;
        return new SolidColorBrush( color );
      }

      return default( SolidColorBrush );
    }

    #endregion
  }
}
