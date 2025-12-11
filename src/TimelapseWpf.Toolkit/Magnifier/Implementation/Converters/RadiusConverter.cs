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
using System.Diagnostics;
using System.Windows.Data;

namespace TimelapseWpf.Toolkit.Mag.Converters
{
  public class RadiusConverter : IValueConverter
  {
    #region IValueConverter Members

    //public object Convert( object value, Type targetType, object parameter, System.Globalization.CultureInfo culture )
    //{
    //  return ( double )value * 2;
    //}
    public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
    {
      value ??= 0;
      Debug.Print("RadiusConverter: Convert value is null");
      return (double)value * 2;
    }


    public object ConvertBack( object value, Type targetType, object parameter, System.Globalization.CultureInfo culture )
    {
      throw new NotImplementedException();
    }

    #endregion
  }
}
