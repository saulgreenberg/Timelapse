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
using System.Windows;
using System.Windows.Controls;

namespace TimelapseWpf.Toolkit.Panels
{
  public class RandomPanel : AnimationPanel
  {
    #region MinimumWidth Property

    public static readonly DependencyProperty MinimumWidthProperty =
      DependencyProperty.Register( nameof(MinimumWidth), typeof( double ), typeof( RandomPanel ),
        new FrameworkPropertyMetadata(
          10d,
          RandomPanel.OnMinimumWidthChanged,
          RandomPanel.CoerceMinimumWidth ) );

    public double MinimumWidth
    {
      get => ( double )this.GetValue( RandomPanel.MinimumWidthProperty );
      set => this.SetValue( RandomPanel.MinimumWidthProperty, value );
    }

    private static void OnMinimumWidthChanged( DependencyObject d, DependencyPropertyChangedEventArgs e )
    {
      RandomPanel panel = ( RandomPanel )d;

      panel.CoerceValue( RandomPanel.MaximumWidthProperty );
      panel.InvalidateMeasure();
    }

    private static object CoerceMinimumWidth( DependencyObject d, object baseValue )
    {
      RandomPanel panel = ( RandomPanel )d;
      double value = ( double )baseValue;

      if( double.IsNaN( value ) || double.IsInfinity( value ) || ( value < 0d ) )
        return DependencyProperty.UnsetValue;

      double maximum = panel.MaximumWidth;
      if( value > maximum )
        return maximum;

      return baseValue;
    }

    #endregion

    #region MinimumHeight Property

    public static readonly DependencyProperty MinimumHeightProperty =
      DependencyProperty.Register( nameof(MinimumHeight), typeof( double ), typeof( RandomPanel ),
        new FrameworkPropertyMetadata(
          10d,
          RandomPanel.OnMinimumHeightChanged,
          RandomPanel.CoerceMinimumHeight ) );

    public double MinimumHeight
    {
      get => ( double )this.GetValue( RandomPanel.MinimumHeightProperty );
      set => this.SetValue( RandomPanel.MinimumHeightProperty, value );
    }

    private static void OnMinimumHeightChanged( DependencyObject d, DependencyPropertyChangedEventArgs e )
    {
      RandomPanel panel = ( RandomPanel )d;

      panel.CoerceValue( RandomPanel.MaximumHeightProperty );
      panel.InvalidateMeasure();
    }

    private static object CoerceMinimumHeight( DependencyObject d, object baseValue )
    {
      RandomPanel panel = ( RandomPanel )d;
      double value = ( double )baseValue;

      if( double.IsNaN( value ) || double.IsInfinity( value ) || ( value < 0d ) )
        return DependencyProperty.UnsetValue;

      double maximum = panel.MaximumHeight;
      if( value > maximum )
        return maximum;

      return baseValue;
    }

    #endregion

    #region MaximumWidth Property

    public static readonly DependencyProperty MaximumWidthProperty =
      DependencyProperty.Register( nameof(MaximumWidth), typeof( double ), typeof( RandomPanel ),
        new FrameworkPropertyMetadata(
          100d,
          RandomPanel.OnMaximumWidthChanged,
          RandomPanel.CoerceMaximumWidth ) );

    public double MaximumWidth
    {
      get => ( double )this.GetValue( RandomPanel.MaximumWidthProperty );
      set => this.SetValue( RandomPanel.MaximumWidthProperty, value );
    }

    private static void OnMaximumWidthChanged( DependencyObject d, DependencyPropertyChangedEventArgs e )
    {
      RandomPanel panel = ( RandomPanel )d;

      panel.CoerceValue( RandomPanel.MinimumWidthProperty );
      panel.InvalidateMeasure();
    }

    private static object CoerceMaximumWidth( DependencyObject d, object baseValue )
    {
      RandomPanel panel = ( RandomPanel )d;
      double value = ( double )baseValue;

      if( double.IsNaN( value ) || double.IsInfinity( value ) || ( value < 0d ) )
        return DependencyProperty.UnsetValue;

      double minimum = panel.MinimumWidth;
      if( value < minimum )
        return minimum;

      return baseValue;
    }

    #endregion

    #region MaximumHeight Property

    public static readonly DependencyProperty MaximumHeightProperty =
      DependencyProperty.Register( nameof(MaximumHeight), typeof( double ), typeof( RandomPanel ),
        new FrameworkPropertyMetadata(
          100d,
          RandomPanel.OnMaximumHeightChanged,
          RandomPanel.CoerceMaximumHeight ) );

    public double MaximumHeight
    {
      get => ( double )this.GetValue( RandomPanel.MaximumHeightProperty );
      set => this.SetValue( RandomPanel.MaximumHeightProperty, value );
    }

    private static void OnMaximumHeightChanged( DependencyObject d, DependencyPropertyChangedEventArgs e )
    {
      RandomPanel panel = ( RandomPanel )d;

      panel.CoerceValue( RandomPanel.MinimumHeightProperty );
      panel.InvalidateMeasure();
    }

    private static object CoerceMaximumHeight( DependencyObject d, object baseValue )
    {
      RandomPanel panel = ( RandomPanel )d;
      double value = ( double )baseValue;

      if( double.IsNaN( value ) || double.IsInfinity( value ) || ( value < 0d ) )
        return DependencyProperty.UnsetValue;

      double minimum = panel.MinimumHeight;
      if( value < minimum )
        return minimum;

      return baseValue;
    }

    #endregion

    #region Seed Property

    public static readonly DependencyProperty SeedProperty =
      DependencyProperty.Register( nameof(Seed), typeof( int ), typeof( RandomPanel ),
        new FrameworkPropertyMetadata( 0,
          RandomPanel.SeedChanged ) );

    public int Seed
    {
      get => ( int )this.GetValue( RandomPanel.SeedProperty );
      set => this.SetValue( RandomPanel.SeedProperty, value );
    }

    private static void SeedChanged( DependencyObject obj, DependencyPropertyChangedEventArgs args )
    {
      if( obj is RandomPanel owner )
      {
        owner._random = new( ( int )args.NewValue );
        owner.InvalidateArrange();
      }
    }

    #endregion

    #region ActualSize Private Property

    // Using a DependencyProperty as the backing store for ActualSize.  This enables animation, styling, binding, etc...
    private static readonly DependencyProperty ActualSizeProperty =
      DependencyProperty.RegisterAttached( "ActualSize", typeof( Size ), typeof( RandomPanel ),
        new UIPropertyMetadata( new Size() ) );

    private static Size GetActualSize( DependencyObject obj )
    {
      return ( Size )obj.GetValue( RandomPanel.ActualSizeProperty );
    }

    private static void SetActualSize( DependencyObject obj, Size value )
    {
      obj.SetValue( RandomPanel.ActualSizeProperty, value );
    }

    #endregion

    protected override Size MeasureChildrenOverride( UIElementCollection children, Size constraint )
    {
      foreach( UIElement child in children )
      {
        if( child == null )
          continue;

        Size childSize = new( 1d * _random.Next( Convert.ToInt32( MinimumWidth ), Convert.ToInt32( MaximumWidth ) ),
                                   1d * _random.Next( Convert.ToInt32( MinimumHeight ), Convert.ToInt32( MaximumHeight ) ) );

        child.Measure( childSize );
        RandomPanel.SetActualSize( child, childSize );
      }
      return new();
    }

    protected override Size ArrangeChildrenOverride( UIElementCollection children, Size finalSize )
    {
      foreach( UIElement child in children )
      {
        if( child == null )
          continue;

        Size childSize = RandomPanel.GetActualSize( child );

        double x = _random.Next( 0, ( int )( Math.Max( finalSize.Width - childSize.Width, 0 ) ) );
        double y = _random.Next( 0, ( int )( Math.Max( finalSize.Height - childSize.Height, 0 ) ) );

        double width = Math.Min( finalSize.Width, childSize.Width );
        double height = Math.Min( finalSize.Height, childSize.Height );

        this.ArrangeChild( child, new( new( x, y ), new Size( width, height ) ) );
      }
      return finalSize;
    }

    #region Private Fields

    private Random _random = new();

    #endregion
  }
}
