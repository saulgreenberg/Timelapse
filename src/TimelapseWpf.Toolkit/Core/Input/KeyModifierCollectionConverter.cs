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
using System.ComponentModel.Design.Serialization;
using System.Globalization;
using System.Reflection;
using System.Security;

namespace TimelapseWpf.Toolkit.Core.Input
{
  public sealed class KeyModifierCollectionConverter : TypeConverter
  {
    #region Static Fields

    private static readonly TypeConverter _keyModifierConverter = TypeDescriptor.GetConverter( typeof( KeyModifier ) );

    #endregion

    public override bool CanConvertFrom( ITypeDescriptorContext typeDescriptorContext, Type type )
    {
      return _keyModifierConverter.CanConvertFrom( typeDescriptorContext, type );
    }

    public override bool CanConvertTo( ITypeDescriptorContext typeDescriptorContext, Type type )
    {
      return ( type == typeof( InstanceDescriptor )
          || type == typeof( KeyModifierCollection )
          || type == typeof( string ) );
    }

    public override object ConvertFrom( ITypeDescriptorContext typeDescriptorContext,
        CultureInfo cultureInfo, object value )
    {
      KeyModifierCollection result = [];
      string stringValue = value as string;

      // convert null as None
      if( stringValue != null && stringValue.Trim() == string.Empty  )
      {
        result.Add( KeyModifier.None );
      }
      else
      {
        // respect the following separators: '+', ' ', '|', or ','
        foreach( string token in stringValue!.Split( new[] { '+', ' ', '|', ',' },
                StringSplitOptions.RemoveEmptyEntries ) )
          result.Add( ( KeyModifier )_keyModifierConverter.ConvertFrom( typeDescriptorContext, cultureInfo, token )! );

        // if nothing added, assume None
        if( result.Count == 0 )
          result.Add( KeyModifier.None );
      }
      return result;
    }

    public override object ConvertTo( ITypeDescriptorContext typeDescriptorContext,
        CultureInfo cultureInfo, object value, Type destinationType )
    {
      // special handling for null or an empty collection
      if( value == null || ( ( KeyModifierCollection )value ).Count == 0 )
      {
        if( destinationType == typeof( InstanceDescriptor ) )
        {
          object result = null;
          try
          {
            result = ConstructInstanceDescriptor();
          }
          catch( SecurityException )
          {
          }
          return result;
        }
        else if( destinationType == typeof( string ) )
        {
          return _keyModifierConverter.ConvertTo( typeDescriptorContext,
                  cultureInfo, KeyModifier.None, destinationType );
        }
      }

      // return a '+' delimited string containing the modifiers
      if( destinationType == typeof( string ) )
      {
        string result = string.Empty;
        foreach( KeyModifier modifier in ( KeyModifierCollection )value! )
        {
          if( result != string.Empty )
            result = result + '+';

          result = result + _keyModifierConverter.ConvertTo( typeDescriptorContext,
                  cultureInfo, modifier, destinationType );
        }
        return result;
      }

      // unexpected type requested so return null
      return null;
    }

    private static object ConstructInstanceDescriptor()
    {
      ConstructorInfo ci = typeof( KeyModifierCollection ).GetConstructor([]);
      return new InstanceDescriptor( ci, new Object[] { } );
    }
  }
}
