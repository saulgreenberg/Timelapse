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
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using TimelapseWpf.Toolkit.Core.Utilities;

namespace TimelapseWpf.Toolkit.Panels
{
  public class WrapPanel : AnimationPanel
  {
    #region Orientation Property

    public static readonly DependencyProperty OrientationProperty =
      StackPanel.OrientationProperty.AddOwner( typeof( WrapPanel ),
        new FrameworkPropertyMetadata( Orientation.Horizontal, 
          WrapPanel.OnOrientationChanged ) );

    public Orientation Orientation
    {
      get => _orientation;
      set => base.SetValue( WrapPanel.OrientationProperty, value );
    }

    private static void OnOrientationChanged( DependencyObject d, DependencyPropertyChangedEventArgs e )
    {
      WrapPanel panel = ( WrapPanel )d;
      panel._orientation = ( Orientation )e.NewValue;
      panel.InvalidateMeasure();
    }

    private Orientation _orientation;

    #endregion

    #region ItemWidth Property

    public static readonly DependencyProperty ItemWidthProperty =
      DependencyProperty.Register( nameof(ItemWidth), typeof( double ), typeof( WrapPanel ),
        new FrameworkPropertyMetadata( double.NaN,
          WrapPanel.OnInvalidateMeasure ), WrapPanel.IsWidthHeightValid );

    [TypeConverter( typeof( LengthConverter ) )]
    public double ItemWidth
    {
      get => ( double )base.GetValue( WrapPanel.ItemWidthProperty );
      set => base.SetValue( WrapPanel.ItemWidthProperty, value );
    }

    #endregion

    #region ItemHeight Property

    public static readonly DependencyProperty ItemHeightProperty =
      DependencyProperty.Register( nameof(ItemHeight), typeof( double ), typeof( WrapPanel ),
        new FrameworkPropertyMetadata( double.NaN,
          WrapPanel.OnInvalidateMeasure ), WrapPanel.IsWidthHeightValid );

    [TypeConverter( typeof( LengthConverter ) )]
    public double ItemHeight
    {
      get => ( double )base.GetValue( WrapPanel.ItemHeightProperty );
      set => base.SetValue( WrapPanel.ItemHeightProperty, value );
    }

    #endregion

    #region IsChildOrderReversed Property

    public static readonly DependencyProperty IsStackReversedProperty =
      DependencyProperty.Register( nameof(IsChildOrderReversed), typeof( bool ), typeof( WrapPanel ),
        new FrameworkPropertyMetadata( false, 
          WrapPanel.OnInvalidateMeasure ) );

    public bool IsChildOrderReversed
    {
      get => ( bool )this.GetValue( WrapPanel.IsStackReversedProperty );
      set => this.SetValue( WrapPanel.IsStackReversedProperty, value );
    }

    #endregion

    protected override Size MeasureChildrenOverride( UIElementCollection children, Size constraint )
    {
      double desiredExtent = 0;
      double desiredStack = 0;

      bool isHorizontal = ( this.Orientation == Orientation.Horizontal );
      double constraintExtent = ( isHorizontal ? constraint.Width : constraint.Height );

      double itemWidth = ItemWidth;
      double itemHeight = ItemHeight;

      bool hasExplicitItemWidth = !double.IsNaN( itemWidth );
      bool hasExplicitItemHeight = !double.IsNaN( itemHeight );

      double lineExtent = 0;
      double lineStack = 0;

      Size childConstraint = new( ( hasExplicitItemWidth ? itemWidth : constraint.Width ),
          ( hasExplicitItemHeight ? itemHeight : constraint.Height ) );

      bool isReversed = this.IsChildOrderReversed;
      int from = isReversed ? children.Count - 1 : 0;
      int step = isReversed ? -1 : 1;

      for( int i = from, pass = 0; pass < children.Count; i += step, pass++ )
      {
        UIElement child = children[ i ];

        child.Measure( childConstraint );

        double childExtent = isHorizontal
            ? ( hasExplicitItemWidth ? itemWidth : child.DesiredSize.Width )
            : ( hasExplicitItemHeight ? itemHeight : child.DesiredSize.Height );
        double childStack = isHorizontal
            ? ( hasExplicitItemHeight ? itemHeight : child.DesiredSize.Height )
            : ( hasExplicitItemWidth ? itemWidth : child.DesiredSize.Width );

        if( lineExtent + childExtent > constraintExtent )
        {
          desiredExtent = Math.Max( lineExtent, desiredExtent );
          desiredStack += lineStack;
          lineExtent = childExtent;
          lineStack = childStack;

          if( childExtent > constraintExtent )
          {
            desiredExtent = Math.Max( childExtent, desiredExtent );
            desiredStack += childStack;
            lineExtent = 0;
            lineStack = 0;
          }
        }
        else
        {
          lineExtent += childExtent;
          lineStack = Math.Max( childStack, lineStack );
        }
      }

      desiredExtent = Math.Max( lineExtent, desiredExtent );
      desiredStack += lineStack;

      return isHorizontal
        ? new( desiredExtent, desiredStack )
        : new Size( desiredStack, desiredExtent );
    }

    protected override Size ArrangeChildrenOverride( UIElementCollection children, Size finalSize )
    {
      bool isHorizontal = ( this.Orientation == Orientation.Horizontal );
      double finalExtent = ( isHorizontal ? finalSize.Width : finalSize.Height );

      double itemWidth = this.ItemWidth;
      double itemHeight = this.ItemHeight;
      double itemExtent = ( isHorizontal ? itemWidth : itemHeight );

      bool hasExplicitItemWidth = !double.IsNaN( itemWidth );
      bool hasExplicitItemHeight = !double.IsNaN( itemHeight );
      bool useItemExtent = ( isHorizontal ? hasExplicitItemWidth : hasExplicitItemHeight );

      double lineExtent = 0;
      double lineStack = 0;
      double lineStackSum = 0;

      int from = this.IsChildOrderReversed ? children.Count - 1 : 0;
      int step = this.IsChildOrderReversed ? -1 : 1;

      Collection<UIElement> childrenInLine = [];

      for( int i = from, pass = 0; pass < children.Count; i += step, pass++ )
      {
        UIElement child = children[ i ];

        double childExtent = isHorizontal
            ? ( hasExplicitItemWidth ? itemWidth : child.DesiredSize.Width )
            : ( hasExplicitItemHeight ? itemHeight : child.DesiredSize.Height );
        double childStack = isHorizontal
            ? ( hasExplicitItemHeight ? itemHeight : child.DesiredSize.Height )
            : ( hasExplicitItemWidth ? itemWidth : child.DesiredSize.Width );

        if( lineExtent + childExtent > finalExtent )
        {
          this.ArrangeLineOfChildren( childrenInLine, isHorizontal, lineStack, lineStackSum, itemExtent, useItemExtent );

          lineStackSum += lineStack;
          lineExtent = childExtent;

          if( childExtent > finalExtent )
          {
            childrenInLine.Add( child );
            this.ArrangeLineOfChildren( childrenInLine, isHorizontal, childStack, lineStackSum, itemExtent, useItemExtent );
            lineStackSum += childStack;
            lineExtent = 0;
          }
          childrenInLine.Add( child );
        }
        else
        {
          childrenInLine.Add( child );
          lineExtent += childExtent;
          lineStack = Math.Max( childStack, lineStack );
        }
      }

      if( childrenInLine.Count > 0 )
      {
        this.ArrangeLineOfChildren( childrenInLine, isHorizontal, lineStack, lineStackSum, itemExtent, useItemExtent );
      }

      return finalSize;
    }

    private void ArrangeLineOfChildren( Collection<UIElement> children, bool isHorizontal, double lineStack, double lineStackSum, double itemExtent, bool useItemExtent )
    {
      double extent = 0;
      foreach( UIElement child in children )
      {
        double childExtent = ( isHorizontal ? child.DesiredSize.Width : child.DesiredSize.Height );
        double elementExtent = ( useItemExtent ? itemExtent : childExtent );
        this.ArrangeChild( child, isHorizontal ? new( extent, lineStackSum, elementExtent, lineStack )
          : new Rect( lineStackSum, extent, lineStack, elementExtent ) );
        extent += elementExtent;
      }
      children.Clear();
    }

    private static void OnInvalidateMeasure( DependencyObject d, DependencyPropertyChangedEventArgs e )
    {
      ( ( AnimationPanel )d ).InvalidateMeasure();
    }

    private static bool IsWidthHeightValid( object value )
    {
      double num = ( double )value;
      return ( DoubleHelper.IsNaN( num ) || ( ( num >= 0d ) && !double.IsPositiveInfinity( num ) ) );
    }
  }
}
