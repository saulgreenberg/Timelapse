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
using System.Windows;
using TimelapseWpf.Toolkit.Core.Utilities;
using TimelapseWpf.Toolkit.Core;

namespace TimelapseWpf.Toolkit.Zoombox
{
  [TypeConverter( typeof( ZoomboxViewConverter ) )]
  public class ZoomboxView
  {
    #region Constructors

    public ZoomboxView()
    {
    }

    public ZoomboxView( double scale )
    {
      this.Scale = scale;
    }

    public ZoomboxView( Point position )
    {
      this.Position = position;
    }

    public ZoomboxView( double scale, Point position )
    {
      this.Position = position;
      this.Scale = scale;
    }

    public ZoomboxView( Rect region )
    {
      this.Region = region;
    }

    public ZoomboxView( double x, double y )
      : this( new Point( x, y ) )
    {
    }

    public ZoomboxView( double scale, double x, double y )
      : this( scale, new Point( x, y ) )
    {
    }

    public ZoomboxView( double x, double y, double width, double height )
      : this( new Rect( x, y, width, height ) )
    {
    }

    #endregion

    #region Empty Static Property

    public static ZoomboxView Empty { get; } = new( ZoomboxViewKind.Empty );

    #endregion

    #region Fill Static Property

    public static ZoomboxView Fill { get; } = new( ZoomboxViewKind.Fill );

    #endregion

    #region Fit Static Property

    public static ZoomboxView Fit { get; } = new( ZoomboxViewKind.Fit );

    #endregion

    #region Center Static Property

    public static ZoomboxView Center { get; } = new( ZoomboxViewKind.Center );

    #endregion

    #region ViewKind Property

    public ZoomboxViewKind ViewKind
    {
      get
      {
        if( _kindHeight > 0 )
        {
          return ZoomboxViewKind.Region;
        }
        else
        {
          return ( ZoomboxViewKind )( int )_kindHeight;
        }
      }
    }

    private double _kindHeight = ( int )ZoomboxViewKind.Empty;

    #endregion

    #region Position Property

    public Point Position
    {
      get
      {
        if( this.ViewKind != ZoomboxViewKind.Absolute )
          throw new InvalidOperationException( ErrorMessages.GetMessage( "PositionOnlyAccessibleOnAbsolute" ) );

        return new( _x, _y );
      }
      set
      {
        if( this.ViewKind != ZoomboxViewKind.Absolute && this.ViewKind != ZoomboxViewKind.Empty )
          throw new InvalidOperationException( String.Format( ErrorMessages.GetMessage( "ZoomboxViewAlreadyInitialized" ), this.ViewKind.ToString() ) );

        _x = value.X;
        _y = value.Y;
        _kindHeight = ( int )ZoomboxViewKind.Absolute;
      }
    }

    private double _x = double.NaN;
    private double _y = double.NaN;

    #endregion

    #region Scale Property

    public double Scale
    {
      get
      {
        if( this.ViewKind != ZoomboxViewKind.Absolute )
          throw new InvalidOperationException( ErrorMessages.GetMessage( "ScaleOnlyAccessibleOnAbsolute" ) );

        return _scaleWidth;
      }
      set
      {
        if( this.ViewKind != ZoomboxViewKind.Absolute && this.ViewKind != ZoomboxViewKind.Empty )
          throw new InvalidOperationException( String.Format( ErrorMessages.GetMessage( "ZoomboxViewAlreadyInitialized" ), this.ViewKind.ToString() ) );

        _scaleWidth = value;
        _kindHeight = ( int )ZoomboxViewKind.Absolute;
      }
    }

    private double _scaleWidth = double.NaN;

    #endregion

    #region Region Property

    public Rect Region
    {
      get
      {
        // a region view has a positive _typeHeight value
        if( _kindHeight < 0 )
          throw new InvalidOperationException( ErrorMessages.GetMessage( "RegionOnlyAccessibleOnRegionalView" ) );

        return new( _x, _y, _scaleWidth, _kindHeight );
      }
      set
      {
        if( this.ViewKind != ZoomboxViewKind.Region && this.ViewKind != ZoomboxViewKind.Empty )
          throw new InvalidOperationException( String.Format( ErrorMessages.GetMessage( "ZoomboxViewAlreadyInitialized" ), this.ViewKind.ToString() ) );

        if( !value.IsEmpty )
        {
          _x = value.X;
          _y = value.Y;
          _scaleWidth = value.Width;
          _kindHeight = value.Height;
        }
      }
    }

    #endregion

    public override int GetHashCode()
    {
      return _x.GetHashCode() ^ _y.GetHashCode() ^ _scaleWidth.GetHashCode() ^ _kindHeight.GetHashCode();
    }

    public override bool Equals( object o )
    {
      bool result = false;
      if( o is ZoomboxView other )
      {
        if( this.ViewKind == other.ViewKind )
        {
          switch( this.ViewKind )
          {
            case ZoomboxViewKind.Absolute:
              result = ( DoubleHelper.AreVirtuallyEqual( _scaleWidth, other._scaleWidth ) )
                    && ( DoubleHelper.AreVirtuallyEqual( Position, other.Position ) );
              break;

            case ZoomboxViewKind.Region:
              result = DoubleHelper.AreVirtuallyEqual( Region, other.Region );
              break;

            default:
              result = true;
              break;
          }
        }
      }
      return result;
    }

    public override string ToString()
    {
      switch( ViewKind )
      {
        case ZoomboxViewKind.Empty:
          return "ZoomboxView: Empty";

        case ZoomboxViewKind.Center:
          return "ZoomboxView: Center";

        case ZoomboxViewKind.Fill:
          return "ZoomboxView: Fill";

        case ZoomboxViewKind.Fit:
          return "ZoomboxView: Fit";

        case ZoomboxViewKind.Absolute:
          return $"ZoomboxView: Scale = {_scaleWidth:f}; Position = ({_x:f}, {_y:f})";

        case ZoomboxViewKind.Region:
          return $"ZoomboxView: Region = ({_x:f}, {_y:f}, {_scaleWidth:f}, {_kindHeight:f})";
      }

      return base.ToString();
    }

    private ZoomboxView( ZoomboxViewKind viewType )
    {
      _kindHeight = ( int )viewType;
    }

    #region Operators Methods

    public static bool operator ==( ZoomboxView v1, ZoomboxView v2 )
    {
      if( ( object )v1 == null )
        return ( object )v2 == null;

      if( ( object )v2 == null )
        return false;

      return v1.Equals( v2 );
    }

    public static bool operator !=( ZoomboxView v1, ZoomboxView v2 )
    {
      return !( v1 == v2 );
    }

    #endregion
  }
}
