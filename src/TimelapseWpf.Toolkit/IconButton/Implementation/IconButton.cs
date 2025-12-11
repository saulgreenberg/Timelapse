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
using System.Windows.Controls;
using System.Windows.Media;

namespace TimelapseWpf.Toolkit
{
  public class IconButton : Button
  {
    #region Constructors

    static IconButton()
    {
      DefaultStyleKeyProperty.OverrideMetadata( typeof( IconButton ), new FrameworkPropertyMetadata( typeof( IconButton ) ) );
    }

    #endregion //Constructors

    #region Properties

    #region Icon

    public static readonly DependencyProperty IconProperty = DependencyProperty.Register( nameof(Icon), typeof( Image ), typeof( IconButton ), new FrameworkPropertyMetadata( null ) );
    public Image Icon
    {
      get => ( Image )this.GetValue( IconButton.IconProperty );
      set => this.SetValue( IconButton.IconProperty, value );
    }

    #endregion //Icon

    #region IconLocation

    public static readonly DependencyProperty IconLocationProperty = DependencyProperty.Register( nameof(IconLocation), typeof( Location ),
      typeof( IconButton ), new FrameworkPropertyMetadata( Location.Left ) );
    public Location IconLocation
    {
      get => ( Location )this.GetValue( IconButton.IconLocationProperty );
      set => this.SetValue( IconButton.IconLocationProperty, value );
    }

    #endregion //IconLocation

    #region MouseOverBackground

    public static readonly DependencyProperty MouseOverBackgroundProperty = DependencyProperty.Register( nameof(MouseOverBackground), typeof( Brush ), typeof( IconButton ), new FrameworkPropertyMetadata( null ) );

    public Brush MouseOverBackground
    {
      get => ( Brush )this.GetValue( IconButton.MouseOverBackgroundProperty );
      set => this.SetValue( IconButton.MouseOverBackgroundProperty, value );
    }

    #endregion //MouseOverBackground

    #region MouseOverBorderBrush

    public static readonly DependencyProperty MouseOverBorderBrushProperty = DependencyProperty.Register( nameof(MouseOverBorderBrush), typeof( Brush ), typeof( IconButton ), new FrameworkPropertyMetadata( null ) );

    public Brush MouseOverBorderBrush
    {
      get => ( Brush )this.GetValue( IconButton.MouseOverBorderBrushProperty );
      set => this.SetValue( IconButton.MouseOverBorderBrushProperty, value );
    }

    #endregion //MouseOverBorderBrush

    #region MouseOverForeground

    public static readonly DependencyProperty MouseOverForegroundProperty = DependencyProperty.Register( nameof(MouseOverForeground), typeof( Brush ), typeof( IconButton ), new FrameworkPropertyMetadata( null ) );

    public Brush MouseOverForeground
    {
      get => ( Brush )this.GetValue( IconButton.MouseOverForegroundProperty );
      set => this.SetValue( IconButton.MouseOverForegroundProperty, value );
    }

    #endregion //MouseOverForeground

    #region MousePressedBackground

    public static readonly DependencyProperty MousePressedBackgroundProperty = DependencyProperty.Register( nameof(MousePressedBackground), typeof( Brush ), typeof( IconButton ), new FrameworkPropertyMetadata( null ) );

    public Brush MousePressedBackground
    {
      get => ( Brush )this.GetValue( IconButton.MousePressedBackgroundProperty );
      set => this.SetValue( IconButton.MousePressedBackgroundProperty, value );
    }

    #endregion  //MousePressedBackground

    #region MousePressedBorderBrush

    public static readonly DependencyProperty MousePressedBorderBrushProperty = DependencyProperty.Register( nameof(MousePressedBorderBrush), typeof( Brush ), typeof( IconButton ), new FrameworkPropertyMetadata( null ) );

    public Brush MousePressedBorderBrush
    {
      get => ( Brush )this.GetValue( IconButton.MousePressedBorderBrushProperty );
      set => this.SetValue( IconButton.MousePressedBorderBrushProperty, value );
    }

    #endregion  //MousePressedBorderBrush

    #region MousePressedForeground

    public static readonly DependencyProperty MousePressedForegroundProperty = DependencyProperty.Register( nameof(MousePressedForeground), typeof( Brush ), typeof( IconButton ), new FrameworkPropertyMetadata( null ) );

    public Brush MousePressedForeground
    {
      get => ( Brush )this.GetValue( IconButton.MousePressedForegroundProperty );
      set => this.SetValue( IconButton.MousePressedForegroundProperty, value );
    }

    #endregion  //MousePressedForeground

    #endregion
  }
}
