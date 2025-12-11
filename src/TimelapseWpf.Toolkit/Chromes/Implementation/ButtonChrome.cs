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

namespace TimelapseWpf.Toolkit.Chromes
{
  public class ButtonChrome : ContentControl
  {
    #region CornerRadius

    public static readonly DependencyProperty CornerRadiusProperty = DependencyProperty.Register( nameof(CornerRadius), typeof( CornerRadius ), typeof( ButtonChrome ), new UIPropertyMetadata( default( CornerRadius ), OnCornerRadiusChanged ) );
    public CornerRadius CornerRadius
    {
      get => ( CornerRadius )GetValue( CornerRadiusProperty );
      set => SetValue( CornerRadiusProperty, value );
    }

    private static void OnCornerRadiusChanged( DependencyObject o, DependencyPropertyChangedEventArgs e )
    {
      if( o is ButtonChrome buttonChrome )
        buttonChrome.OnCornerRadiusChanged( ( CornerRadius )e.OldValue, ( CornerRadius )e.NewValue );
    }

    protected virtual void OnCornerRadiusChanged( CornerRadius oldValue, CornerRadius newValue )
    {
      //we always want the InnerBorderRadius to be one less than the CornerRadius
      CornerRadius newInnerCornerRadius = new( Math.Max( 0, newValue.TopLeft - 1 ),
                                                           Math.Max( 0, newValue.TopRight - 1 ),
                                                           Math.Max( 0, newValue.BottomRight - 1 ),
                                                           Math.Max( 0, newValue.BottomLeft - 1 ) );

      InnerCornerRadius = newInnerCornerRadius;
    }

    #endregion //CornerRadius

    #region InnerCornerRadius

    public static readonly DependencyProperty InnerCornerRadiusProperty = DependencyProperty.Register( nameof(InnerCornerRadius), typeof( CornerRadius ), typeof( ButtonChrome ), new UIPropertyMetadata( default( CornerRadius ), OnInnerCornerRadiusChanged ) );
    public CornerRadius InnerCornerRadius
    {
      get => ( CornerRadius )GetValue( InnerCornerRadiusProperty );
      set => SetValue( InnerCornerRadiusProperty, value );
    }

    private static void OnInnerCornerRadiusChanged( DependencyObject o, DependencyPropertyChangedEventArgs e )
    {
      if( o is ButtonChrome buttonChrome )
        buttonChrome.OnInnerCornerRadiusChanged( ( CornerRadius )e.OldValue, ( CornerRadius )e.NewValue );
    }

    protected virtual void OnInnerCornerRadiusChanged( CornerRadius oldValue, CornerRadius newValue )
    {
      // TODO: Add your property changed side-effects. Descendants can override as well.
    }

    #endregion //InnerCornerRadius

    #region RenderChecked

    public static readonly DependencyProperty RenderCheckedProperty = DependencyProperty.Register( nameof(RenderChecked), typeof( bool ), typeof( ButtonChrome ), new UIPropertyMetadata( false, OnRenderCheckedChanged ) );
    public bool RenderChecked
    {
      get => ( bool )GetValue( RenderCheckedProperty );
      set => SetValue( RenderCheckedProperty, value );
    }

    private static void OnRenderCheckedChanged( DependencyObject o, DependencyPropertyChangedEventArgs e )
    {
      if( o is ButtonChrome buttonChrome )
        buttonChrome.OnRenderCheckedChanged( ( bool )e.OldValue, ( bool )e.NewValue );
    }

    protected virtual void OnRenderCheckedChanged( bool oldValue, bool newValue )
    {
      // TODO: Add your property changed side-effects. Descendants can override as well.
    }

    #endregion //RenderChecked

    #region RenderEnabled

    public static readonly DependencyProperty RenderEnabledProperty = DependencyProperty.Register( nameof(RenderEnabled), typeof( bool ), typeof( ButtonChrome ), new UIPropertyMetadata( true, OnRenderEnabledChanged ) );
    public bool RenderEnabled
    {
      get => ( bool )GetValue( RenderEnabledProperty );
      set => SetValue( RenderEnabledProperty, value );
    }

    private static void OnRenderEnabledChanged( DependencyObject o, DependencyPropertyChangedEventArgs e )
    {
      if( o is ButtonChrome buttonChrome )
        buttonChrome.OnRenderEnabledChanged( ( bool )e.OldValue, ( bool )e.NewValue );
    }

    protected virtual void OnRenderEnabledChanged( bool oldValue, bool newValue )
    {
      // TODO: Add your property changed side-effects. Descendants can override as well.
    }

    #endregion //RenderEnabled

    #region RenderFocused

    public static readonly DependencyProperty RenderFocusedProperty = DependencyProperty.Register( nameof(RenderFocused), typeof( bool ), typeof( ButtonChrome ), new UIPropertyMetadata( false, OnRenderFocusedChanged ) );
    public bool RenderFocused
    {
      get => ( bool )GetValue( RenderFocusedProperty );
      set => SetValue( RenderFocusedProperty, value );
    }

    private static void OnRenderFocusedChanged( DependencyObject o, DependencyPropertyChangedEventArgs e )
    {
      if( o is ButtonChrome buttonChrome )
        buttonChrome.OnRenderFocusedChanged( ( bool )e.OldValue, ( bool )e.NewValue );
    }

    protected virtual void OnRenderFocusedChanged( bool oldValue, bool newValue )
    {
      // TODO: Add your property changed side-effects. Descendants can override as well.
    }

    #endregion //RenderFocused

    #region RenderMouseOver

    public static readonly DependencyProperty RenderMouseOverProperty = DependencyProperty.Register( nameof(RenderMouseOver), typeof( bool ), typeof( ButtonChrome ), new UIPropertyMetadata( false, OnRenderMouseOverChanged ) );
    public bool RenderMouseOver
    {
      get => ( bool )GetValue( RenderMouseOverProperty );
      set => SetValue( RenderMouseOverProperty, value );
    }

    private static void OnRenderMouseOverChanged( DependencyObject o, DependencyPropertyChangedEventArgs e )
    {
      if( o is ButtonChrome buttonChrome )
        buttonChrome.OnRenderMouseOverChanged( ( bool )e.OldValue, ( bool )e.NewValue );
    }

    protected virtual void OnRenderMouseOverChanged( bool oldValue, bool newValue )
    {
      // TODO: Add your property changed side-effects. Descendants can override as well.
    }

    #endregion //RenderMouseOver

    #region RenderNormal

    public static readonly DependencyProperty RenderNormalProperty = DependencyProperty.Register( nameof(RenderNormal), typeof( bool ), typeof( ButtonChrome ), new UIPropertyMetadata( true, OnRenderNormalChanged ) );
    public bool RenderNormal
    {
      get => ( bool )GetValue( RenderNormalProperty );
      set => SetValue( RenderNormalProperty, value );
    }

    private static void OnRenderNormalChanged( DependencyObject o, DependencyPropertyChangedEventArgs e )
    {
      if( o is ButtonChrome buttonChrome )
        buttonChrome.OnRenderNormalChanged( ( bool )e.OldValue, ( bool )e.NewValue );
    }

    protected virtual void OnRenderNormalChanged( bool oldValue, bool newValue )
    {
      // TODO: Add your property changed side-effects. Descendants can override as well.
    }

    #endregion //RenderNormal

    #region RenderPressed

    public static readonly DependencyProperty RenderPressedProperty = DependencyProperty.Register( nameof(RenderPressed), typeof( bool ), typeof( ButtonChrome ), new UIPropertyMetadata( false, OnRenderPressedChanged ) );
    public bool RenderPressed
    {
      get => ( bool )GetValue( RenderPressedProperty );
      set => SetValue( RenderPressedProperty, value );
    }

    private static void OnRenderPressedChanged( DependencyObject o, DependencyPropertyChangedEventArgs e )
    {
      if( o is ButtonChrome buttonChrome )
        buttonChrome.OnRenderPressedChanged( ( bool )e.OldValue, ( bool )e.NewValue );
    }

    protected virtual void OnRenderPressedChanged( bool oldValue, bool newValue )
    {
      // TODO: Add your property changed side-effects. Descendants can override as well.
    }

    #endregion //RenderPressed

    #region Contsructors

    static ButtonChrome()
    {
      DefaultStyleKeyProperty.OverrideMetadata( typeof( ButtonChrome ), new FrameworkPropertyMetadata( typeof( ButtonChrome ) ) );
    }

    #endregion //Contsructors
  }
}
