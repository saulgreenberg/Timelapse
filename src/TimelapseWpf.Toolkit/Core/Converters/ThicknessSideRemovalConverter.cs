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
  public class ThicknessSideRemovalConverter : IValueConverter
  {
    #region IValueConverter Members

    //public object Convert( object value, Type targetType, object parameter, CultureInfo culture )
    //{
    //  var thickness = (Thickness)value;
    //  var sideToRemove = int.Parse( (string)parameter );
    //  switch( sideToRemove )
    //  {
    //    case 0: thickness.Left = 0d; break;
    //    case 1: thickness.Top = 0d; break;
    //    case 2: thickness.Right = 0d; break;
    //    case 3: thickness.Bottom = 0d; break;
    //    default: throw new InvalidContentException("parameter should be from 0 to 3 to specify the side to remove.");
    //  }
    //  return thickness;
    //}
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
      if (!(value is Thickness thickness))
        return DependencyProperty.UnsetValue;

      if (!(parameter is string paramStr) || !int.TryParse(paramStr, out int sideToRemove))
        return DependencyProperty.UnsetValue;

      switch (sideToRemove)
      {
        case 0: thickness.Left = 0d; break;
        case 1: thickness.Top = 0d; break;
        case 2: thickness.Right = 0d; break;
        case 3: thickness.Bottom = 0d; break;
        default: return DependencyProperty.UnsetValue; // invalid parameter
      }
      return thickness;
    }

    public object ConvertBack( object value, Type targetType, object parameter, CultureInfo culture )
    {
      throw new NotImplementedException();
    }

    #endregion
  }
}
