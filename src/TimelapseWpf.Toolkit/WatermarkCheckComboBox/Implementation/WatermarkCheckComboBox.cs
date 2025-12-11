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
  public class WatermarkCheckComboBox : CheckComboBox
  {
    #region Properties

    #region Watermark

    public static readonly DependencyProperty WatermarkProperty = DependencyProperty.Register( nameof(Watermark), typeof( object ), typeof( WatermarkCheckComboBox ), new UIPropertyMetadata( null ) );
    public object Watermark
    {
      get => GetValue( WatermarkProperty );
      set => SetValue( WatermarkProperty, value );
    }

    #endregion //Watermark

    #region WatermarkTemplate

    public static readonly DependencyProperty WatermarkTemplateProperty = DependencyProperty.Register( nameof(WatermarkTemplate), typeof( DataTemplate ), typeof( WatermarkCheckComboBox ), new UIPropertyMetadata( null ) );
    public DataTemplate WatermarkTemplate
    {
      get => ( DataTemplate )GetValue( WatermarkTemplateProperty );
      set => SetValue( WatermarkTemplateProperty, value );
    }

    #endregion //WatermarkTemplate

    #region ForceWatermark

    public static readonly DependencyProperty ForceWatermarkProperty = DependencyProperty.Register( nameof(ForceWatermark), typeof( bool ), typeof( WatermarkCheckComboBox ), new UIPropertyMetadata( false ) );
    public bool ForceWatermark
    {
      get => ( bool )GetValue( ForceWatermarkProperty );
      set => SetValue( ForceWatermarkProperty, value );
    }

    #endregion //ForceWatermark

    #endregion //Properties

    #region Constructors

    static WatermarkCheckComboBox()
    {
      DefaultStyleKeyProperty.OverrideMetadata( typeof( WatermarkCheckComboBox ), new FrameworkPropertyMetadata( typeof( WatermarkCheckComboBox ) ) );
    }

    #endregion //Constructors
  }
}
