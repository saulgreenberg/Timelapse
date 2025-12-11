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
using System.ComponentModel;
using System.Linq;
using System.Windows.Data;

namespace TimelapseWpf.Toolkit.Core.Converters
{
  public class ObjectTypeToNameConverter : IValueConverter
  {
    public object Convert( object value, Type targetType, object parameter, System.Globalization.CultureInfo culture )
    {
      if( value != null )
      {
        if( value is Type type1 )
        {
          var displayNameAttribute = type1.GetCustomAttributes( false ).OfType<DisplayNameAttribute>().FirstOrDefault();
          return ( displayNameAttribute != null ) ? displayNameAttribute.DisplayName : type1.Name;
        }

        var type = value.GetType();
        var valueString = value.ToString();
        if( string.IsNullOrEmpty( valueString )
         || ( valueString == type.UnderlyingSystemType.ToString() ) )
        {
          var displayNameAttribute = type.GetCustomAttributes( false ).OfType<DisplayNameAttribute>().FirstOrDefault();
          return ( displayNameAttribute != null ) ? displayNameAttribute.DisplayName : type.Name;
        }

        return value; 
      }
      return null;
    }
    public object ConvertBack( object value, Type targetType, object parameter, System.Globalization.CultureInfo culture )
    {
      throw new NotImplementedException();
    }
  }
}
