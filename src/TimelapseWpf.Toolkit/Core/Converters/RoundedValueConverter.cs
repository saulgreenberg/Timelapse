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
  public class RoundedValueConverter : IValueConverter
  {
    #region Precision Property

    public int Precision
    {
      get => _precision;
      set => _precision = value;
    }

    private int _precision;

    #endregion

    public object Convert( object value, Type targetType, object parameter, CultureInfo culture )
    {
      if( value is double d)
      {
        return Math.Round( d, _precision );
      }
      else if( value is Point point )
      {
        return new Point( Math.Round( point.X, _precision ), Math.Round( point.Y, _precision ) );
      }
      else
      {
        return value;
      }
    }

    public object ConvertBack( object value, Type targetType, object parameter, CultureInfo culture )
    {
      return value;
    }
  }
}
