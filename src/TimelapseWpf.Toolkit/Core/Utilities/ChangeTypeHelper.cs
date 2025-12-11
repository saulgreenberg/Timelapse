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

namespace TimelapseWpf.Toolkit.Core.Utilities
{
  internal static class ChangeTypeHelper
  {
    internal static object ChangeType( object value, Type conversionType, IFormatProvider provider )
    {
      if( conversionType == null )
      {
        throw new ArgumentNullException( nameof(conversionType) );
      }
      if( conversionType == typeof( Guid ) )
      {
        return new Guid( value.ToString()! );
      }
      else if( conversionType == typeof( Guid? ) )
      {
        if( value == null )
          return null;
        return new Guid( value.ToString()! );
      }
      else if( conversionType.IsGenericType && conversionType.GetGenericTypeDefinition() == typeof( Nullable<> ) )
      {
        if( value == null )
          return null;
        NullableConverter nullableConverter = new( conversionType );
        conversionType = nullableConverter.UnderlyingType;
      }

      return System.Convert.ChangeType( value, conversionType, provider );
    }
  }
}
