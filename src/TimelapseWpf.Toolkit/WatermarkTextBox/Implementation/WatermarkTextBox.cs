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

using System.Windows;

namespace TimelapseWpf.Toolkit
{
#pragma warning disable 0618

  public class WatermarkTextBox : AutoSelectTextBox
  {
    #region Properties

    #region KeepWatermarkOnGotFocus

    public static readonly DependencyProperty KeepWatermarkOnGotFocusProperty = DependencyProperty.Register( nameof(KeepWatermarkOnGotFocus), typeof( bool ), typeof( WatermarkTextBox ), new UIPropertyMetadata( false ) );
    public bool KeepWatermarkOnGotFocus
    {
      get => ( bool )GetValue( KeepWatermarkOnGotFocusProperty );
      set => SetValue( KeepWatermarkOnGotFocusProperty, value );
    }

    #endregion //KeepWatermarkOnGotFocus

    #region Watermark

    public static readonly DependencyProperty WatermarkProperty = DependencyProperty.Register( nameof(Watermark), typeof( object ), typeof( WatermarkTextBox ), new UIPropertyMetadata( null ) );
    public object Watermark
    {
      get => GetValue( WatermarkProperty );
      set => SetValue( WatermarkProperty, value );
    }

    #endregion //Watermark

    #region WatermarkTemplate

    public static readonly DependencyProperty WatermarkTemplateProperty = DependencyProperty.Register( nameof(WatermarkTemplate), typeof( DataTemplate ), typeof( WatermarkTextBox ), new UIPropertyMetadata( null ) );
    public DataTemplate WatermarkTemplate
    {
      get => ( DataTemplate )GetValue( WatermarkTemplateProperty );
      set => SetValue( WatermarkTemplateProperty, value );
    }

    #endregion //WatermarkTemplate

    #endregion //Properties

    #region Constructors

    static WatermarkTextBox()
    {
      DefaultStyleKeyProperty.OverrideMetadata( typeof( WatermarkTextBox ), new FrameworkPropertyMetadata( typeof( WatermarkTextBox ) ) );
    }

    #endregion //Constructors
  }

#pragma warning restore 0618
}
