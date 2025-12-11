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
  /// <summary>
  /// This converter allow to blend two colors into one based on a specified ratio
  /// </summary>
  public class ColorBlendConverter : IValueConverter
  {
    private double _blendedColorRatio;

    /// <summary>
    /// The ratio of the blended color. Must be between 0 and 1.
    /// </summary>
    public double BlendedColorRatio
    {
      get => _blendedColorRatio;

      set
      {
        if( value < 0d || value > 1d )
          throw new ArgumentException( "BlendedColorRatio must be greater than or equal to 0 and lower than or equal to 1 " );

        _blendedColorRatio = value;
      }
    }

    /// <summary>
    /// The color to blend with the source color
    /// </summary>
    public Color BlendedColor { get; set; }

    public object Convert( object value, Type targetType, object parameter, System.Globalization.CultureInfo culture )
    {
      if( value == null || value.GetType() != typeof( Color ) )
        return null;

      Color color = ( Color )value;
      return new Color()
      {
        A = this.BlendValue( color.A, this.BlendedColor.A ),
        R = this.BlendValue( color.R, this.BlendedColor.R ),
        G = this.BlendValue( color.G, this.BlendedColor.G ),
        B = this.BlendValue( color.B, this.BlendedColor.B )
      };
    }

    private byte BlendValue( byte original, byte blend )
    {
      double blendRatio = this.BlendedColorRatio;
      double sourceRatio = 1 - blendRatio;

      double result = ( original * sourceRatio ) + ( blend * blendRatio );
      result = Math.Round( result );
      result = Math.Min( 255d, Math.Max( 0d, result ) );
      return System.Convert.ToByte( result );
    }

    public object ConvertBack( object value, Type targetType, object parameter, System.Globalization.CultureInfo culture )
    {
      throw new NotImplementedException();
    }
  }
}
