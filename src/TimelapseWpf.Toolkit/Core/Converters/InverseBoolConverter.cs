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

namespace TimelapseWpf.Toolkit.Core.Converters
{
  public class InverseBoolConverter : IValueConverter
  {
    #region IValueConverter Members

    //public object Convert( object value, Type targetType, object parameter, System.Globalization.CultureInfo culture )
    //{
    //  return !( bool )value;
    //}
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
      if (value is not bool boolValue)
        return DependencyProperty.UnsetValue;

      return !boolValue;
    }

    public object ConvertBack( object value, Type targetType, object parameter, System.Globalization.CultureInfo culture )
    {
      throw new NotImplementedException();
    }

    #endregion
  }
}
