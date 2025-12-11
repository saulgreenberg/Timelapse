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
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Windows;
using TimelapseWpf.Toolkit.Core.Utilities;
// ReSharper disable SpecifyACultureInStringConversionExplicitly

namespace TimelapseWpf.Toolkit.Zoombox
{
  public sealed class ZoomboxViewConverter : TypeConverter
  {
    #region Converter Static Property

    internal static ZoomboxViewConverter Converter => _converter ??= new();

    private static ZoomboxViewConverter _converter; //null

    #endregion

    public override bool CanConvertFrom( ITypeDescriptorContext typeDescriptorContext, Type type )
    {
      return ( type == typeof( string ) )
          || ( type == typeof( double ) )
          || ( type == typeof( Point ) )
          || ( type == typeof( Rect ) )
          || ( base.CanConvertFrom( typeDescriptorContext, type ) );
    }

    public override bool CanConvertTo( ITypeDescriptorContext typeDescriptorContext, Type type )
    {
      return ( type == typeof( string ) )
          || ( base.CanConvertTo( typeDescriptorContext, type ) );
    }

    public override object ConvertFrom( 
      ITypeDescriptorContext typeDescriptorContext,
      CultureInfo cultureInfo, 
      object value )
    {
      ZoomboxView result = null;
      if( value is double value1)
      {
        result = new( value1 );
      }
      else if( value is Point point )
      {
        result = new( point );
      }
      else if( value is Rect rect )
      {
        result = new( rect );
      }
      else if( value is string s)
      {
        if( string.IsNullOrEmpty( s.Trim() ) )
        {
          result = ZoomboxView.Empty;
        }
        else
        {
          switch( s.Trim().ToLower() )
          {
            case "center":
              result = ZoomboxView.Center;
              break;

            case "empty":
              result = ZoomboxView.Empty;
              break;

            case "fill":
              result = ZoomboxView.Fill;
              break;

            case "fit":
              result = ZoomboxView.Fit;
              break;

            default:
              // parse double values; respect the following separators: ' ', ';', or ','
              List<double> values = [];
              foreach( string token in s.Split( new[] { ' ', ';', ',' }, StringSplitOptions.RemoveEmptyEntries ) )
              {
                if( double.TryParse( token, out var d ) )
                {
                  values.Add( d );
                }
                if( values.Count >= 4 )
                {
                  // disregard additional values
                  break;
                }
              }

              switch( values.Count )
              {
                case 1: // scale
                  result = new( values[ 0 ] );
                  break;

                case 2: // x, y
                  result = new( values[ 0 ], values[ 1 ] );
                  break;

                case 3: // scale, x, y
                  result = new( values[ 0 ], values[ 1 ], values[ 2 ] );
                  break;

                case 4: // x, y, width, height
                  result = new( values[ 0 ], values[ 1 ], values[ 2 ], values[ 3 ] );
                  break;
              }
              break;
          }
        }
      }
      return ( result == null ? base.ConvertFrom( typeDescriptorContext, cultureInfo, value ) : result );
    }

    public override object ConvertTo( 
      ITypeDescriptorContext typeDescriptorContext,
      CultureInfo cultureInfo, 
      object value, 
      Type destinationType )
    {
      object result = null;
      ZoomboxView view = value as ZoomboxView;

      if( view != null )
      {
        if( destinationType == typeof( string ) )
        {
          result = "Empty";
          switch( view.ViewKind )
          {
            case ZoomboxViewKind.Absolute:
              if( PointHelper.IsEmpty( view.Position ) )
              {
                if( !DoubleHelper.IsNaN( view.Scale ) )
                {
                  result = view.Scale.ToString();
                }
              }
              else if( DoubleHelper.IsNaN( view.Scale ) )
              {
                result = view.Position.X.ToString() + "," + view.Position.Y.ToString();
              }
              else
              {
                result = view.Scale.ToString() + ","
                                                                           + view.Position.X.ToString() + ","
                                                                           + view.Position.Y.ToString();
              }
              break;

            case ZoomboxViewKind.Center:
              result = "Center";
              break;

            case ZoomboxViewKind.Fill:
              result = "Fill";
              break;

            case ZoomboxViewKind.Fit:
              result = "Fit";
              break;

            case ZoomboxViewKind.Region:
              result = view.Region.X.ToString() + ","
                                                                            + view.Region.Y.ToString() + ","
                                                                            + view.Region.Width.ToString() + ","
                                                                            + view.Region.Height.ToString();
              break;
          }
        }
      }
      return result ?? base.ConvertTo( typeDescriptorContext, cultureInfo, value, destinationType );
    }
  }
}
