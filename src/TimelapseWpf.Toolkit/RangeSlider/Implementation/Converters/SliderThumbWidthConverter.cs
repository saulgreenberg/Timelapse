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
using System.Windows.Controls;
using System.Windows.Data;

namespace TimelapseWpf.Toolkit.Converters
{
  [Obsolete("This class is no longer used internaly and may be removed in a future release")]
  public class SliderThumbWidthConverter : IValueConverter
  {
    //public object Convert( object value, Type targetType, object parameter, CultureInfo culture )
    //{
    //  if( value is Slider slider )
    //  {
    //    string param = parameter.ToString();
    //    if( param == "0" )
    //      return RangeSlider.GetThumbWidth( slider );
    //    else if( param == "1" )
    //      return RangeSlider.GetThumbHeight( slider );
    //  }
    //  return 0d;
    //}
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
      if (!(value is Slider slider) || parameter == null)
        return 0d;

      string param = parameter.ToString();
      if (param == "0")
        return RangeSlider.GetThumbWidth(slider);
      else if (param == "1")
        return RangeSlider.GetThumbHeight(slider);

      return 0d;
    }

    public object ConvertBack( object value, Type targetType, object parameter, CultureInfo culture )
    {
      throw new NotImplementedException();
    }
  }
}
