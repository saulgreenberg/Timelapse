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

namespace TimelapseWpf.Toolkit.Media.Animation
{
  public class AnimationRateConverter : TypeConverter
  {
    public override bool CanConvertFrom( ITypeDescriptorContext td, Type t )
    {
      return ( t == typeof( string ) )
          || ( t == typeof( double ) )
          || ( t == typeof( int ) )
          || ( t == typeof( TimeSpan ) );
    }

    public override bool CanConvertTo( ITypeDescriptorContext context, Type destinationType )
    {
      return ( destinationType == typeof( InstanceDescriptor ) )
          || ( destinationType == typeof( string ) )
          || ( destinationType == typeof( double ) )
          || ( destinationType == typeof( TimeSpan ) );
    }

    public override object ConvertFrom(
      ITypeDescriptorContext td,
      CultureInfo cultureInfo,
      object value )
    {
      Type valueType = value.GetType();
      if( value is string stringValue)
      {
        if( stringValue.Contains( ":" ) )
        {
          TimeSpan duration = TimeSpan.Zero;
          duration = ( TimeSpan )TypeDescriptor.GetConverter( duration ).ConvertFrom( td, cultureInfo, stringValue )!;
          return new AnimationRate( duration );
        }
        else
        {
          double speed = 0;
          speed = ( double )TypeDescriptor.GetConverter( speed ).ConvertFrom( td, cultureInfo, stringValue )!;
          return new AnimationRate( speed );
        }
      }
      else if( valueType == typeof( double ) )
      {
        return ( AnimationRate )( double )value;
      }
      else if( valueType == typeof( int ) )
      {
        return ( AnimationRate )( int )value;
      }
      else // TimeSpan
      {
        return ( AnimationRate )( TimeSpan )value;
      }
    }

    public override object ConvertTo(
      ITypeDescriptorContext context,
      CultureInfo cultureInfo,
      object value,
      Type destinationType )
    {
      if( value is AnimationRate rateValue )
      {
        if( destinationType == typeof( InstanceDescriptor ) )
        {
          MemberInfo mi;
          if( rateValue.HasDuration )
          {
            mi = typeof( AnimationRate ).GetConstructor([typeof( TimeSpan )]);
            return new InstanceDescriptor( mi, new object[] { rateValue.Duration } );
          }
          else if( rateValue.HasSpeed )
          {
            mi = typeof( AnimationRate ).GetConstructor([typeof( double )]);
            return new InstanceDescriptor( mi, new object[] { rateValue.Speed } );
          }
        }
        else if( destinationType == typeof( string ) )
        {
          return rateValue.ToString();
        }
        else if( destinationType == typeof( double ) )
        {
          return rateValue.HasSpeed ? rateValue.Speed : 0.0d;
        }
        else if( destinationType == typeof( TimeSpan ) )
        {
          return rateValue.HasDuration ? rateValue.Duration : TimeSpan.FromSeconds( 0 );
        }
      }

      return base.ConvertTo( context, cultureInfo, value, destinationType );
    }
  }
}
