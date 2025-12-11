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
using System.Windows.Media;

namespace TimelapseWpf.Toolkit.Core.Converters
{
  public class WindowControlBackgroundConverter : IMultiValueConverter
  {
    /// <summary>
    /// Used in the WindowContainer Template to calculate the resulting background brush
    /// from the WindowBackground (values[0]) and WindowOpacity (values[1]) propreties.
    /// </summary>
    /// <param name="values"></param>
    /// <param name="targetType"></param>
    /// <param name="parameter"></param>
    /// <param name="culture"></param>
    /// <returns></returns>
    public object Convert( object[] values, Type targetType, object parameter, CultureInfo culture )
    {
      Brush backgroundColor = ( Brush )values[ 0 ];
      double opacity = ( double )values[ 1 ];

      if( backgroundColor != null )
      {
        // Do not override any possible opacity value specifically set by the user.
        // Only use WindowOpacity value if the user did not set an opacity first.
        if( backgroundColor.ReadLocalValue( Brush.OpacityProperty ) == System.Windows.DependencyProperty.UnsetValue )
        {
          backgroundColor = backgroundColor.Clone();
          backgroundColor.Opacity = opacity;
        }
      }
      return backgroundColor;
    }

    public object[] ConvertBack( object value, Type[] targetTypes, object parameter, CultureInfo culture )
    {
      throw new NotImplementedException();
    }
  }
}
