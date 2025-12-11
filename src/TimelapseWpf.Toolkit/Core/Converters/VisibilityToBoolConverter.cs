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
  public class VisibilityToBoolConverter : IValueConverter
  {
    #region Inverted Property

    public bool Inverted { get; set; }

    #endregion

    #region Not Property

    public bool Not { get; set; }

    #endregion

    public object Convert( object value, Type targetType, object parameter, CultureInfo culture )
    {
      return this.Inverted ? this.BoolToVisibility( value ) : this.VisibilityToBool( value );
    }

    public object ConvertBack( object value, Type targetType, object parameter, CultureInfo culture )
    {
      return this.Inverted ? this.VisibilityToBool( value ) : this.BoolToVisibility( value );
    }

    private object VisibilityToBool( object value )
    {
      if( !( value is Visibility visibility ) )
        throw new InvalidOperationException( ErrorMessages.GetMessage( "SuppliedValueWasNotVisibility" ) );

      return ( visibility == Visibility.Visible ) ^ Not;
    }

    private object BoolToVisibility( object value )
    {
      if( !( value is bool b) )
        throw new InvalidOperationException( ErrorMessages.GetMessage( "SuppliedValueWasNotBool" ) );

      return ( b ^ Not ) ? Visibility.Visible : Visibility.Collapsed;
    }
  }
}
