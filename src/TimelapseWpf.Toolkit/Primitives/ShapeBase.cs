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
using System.Diagnostics;
using System.Windows;
using System.Windows.Media;
using System.Windows.Shapes;
using TimelapseWpf.Toolkit.Core.Utilities;

namespace TimelapseWpf.Toolkit.Primitives
{
  public abstract class ShapeBase : Shape
  {
    #region Constructors

    static ShapeBase()
    {
      ShapeBase.StrokeDashArrayProperty.OverrideMetadata( typeof( ShapeBase ), new FrameworkPropertyMetadata( ShapeBase.OnStrokeChanged ) );
      ShapeBase.StrokeDashCapProperty.OverrideMetadata( typeof( ShapeBase ), new FrameworkPropertyMetadata( ShapeBase.OnStrokeChanged ) );
      ShapeBase.StrokeDashOffsetProperty.OverrideMetadata( typeof( ShapeBase ), new FrameworkPropertyMetadata( ShapeBase.OnStrokeChanged ) );
      ShapeBase.StrokeEndLineCapProperty.OverrideMetadata( typeof( ShapeBase ), new FrameworkPropertyMetadata( ShapeBase.OnStrokeChanged ) );
      ShapeBase.StrokeLineJoinProperty.OverrideMetadata( typeof( ShapeBase ), new FrameworkPropertyMetadata( ShapeBase.OnStrokeChanged ) );
      ShapeBase.StrokeMiterLimitProperty.OverrideMetadata( typeof( ShapeBase ), new FrameworkPropertyMetadata( ShapeBase.OnStrokeChanged ) );
      ShapeBase.StrokeProperty.OverrideMetadata( typeof( ShapeBase ), new FrameworkPropertyMetadata( ShapeBase.OnStrokeChanged ) );
      ShapeBase.StrokeStartLineCapProperty.OverrideMetadata( typeof( ShapeBase ), new FrameworkPropertyMetadata( ShapeBase.OnStrokeChanged ) );
      ShapeBase.StrokeThicknessProperty.OverrideMetadata( typeof( ShapeBase ), new FrameworkPropertyMetadata( ShapeBase.OnStrokeChanged ) );
    }

    #endregion

    #region IsPenEmptyOrUndefined Internal Property

    internal bool IsPenEmptyOrUndefined
    {
      get
      {
        double strokeThickness = this.StrokeThickness;
        return ( this.Stroke == null ) || DoubleHelper.IsNaN( strokeThickness ) || DoubleHelper.AreVirtuallyEqual( 0, strokeThickness );
      }
    }

    #endregion

    #region DefiningGeometry Protected Property

    protected abstract override Geometry DefiningGeometry
    {
      get;
    }

    #endregion

    internal virtual Rect GetDefiningGeometryBounds()
    {
      Geometry geometry = this.DefiningGeometry;

      Debug.Assert( geometry != null );

      return geometry.Bounds;
    }

    internal virtual Size GetNaturalSize()
    {
      Geometry geometry = this.DefiningGeometry;

      Debug.Assert( geometry != null );

      Rect bounds = geometry.GetRenderBounds( GetPen() );

      return new( Math.Max( bounds.Right, 0 ), Math.Max( bounds.Bottom, 0 ) );
    }

    internal Pen GetPen()
    {
      if( this.IsPenEmptyOrUndefined )
        return null;

      return _pen ??= this.MakePen();
    }

    internal double GetStrokeThickness()
    {
      if( this.IsPenEmptyOrUndefined )
        return 0d;

      return Math.Abs( this.StrokeThickness );
    }

    internal bool IsSizeEmptyOrUndefined( Size size )
    {
      return ( DoubleHelper.IsNaN( size.Width ) || DoubleHelper.IsNaN( size.Height ) || size.IsEmpty );
    }

    private static void OnStrokeChanged( DependencyObject d, DependencyPropertyChangedEventArgs e )
    {
      ( ( ShapeBase )d )._pen = null;
    }

    private Pen MakePen()
    {
      Pen pen = new()
      {
        Brush = this.Stroke,
        DashCap = this.StrokeDashCap
      };
      if( this.StrokeDashArray != null || this.StrokeDashOffset != 0.0 )
      {
        pen.DashStyle = new( this.StrokeDashArray, this.StrokeDashOffset );
      }
      pen.EndLineCap = this.StrokeEndLineCap;
      pen.LineJoin = this.StrokeLineJoin;
      pen.MiterLimit = this.StrokeMiterLimit;
      pen.StartLineCap = this.StrokeStartLineCap;
      pen.Thickness = Math.Abs( this.StrokeThickness );

      return pen;
    }

    #region Private Fields

    private Pen _pen;

    #endregion
  }
}
