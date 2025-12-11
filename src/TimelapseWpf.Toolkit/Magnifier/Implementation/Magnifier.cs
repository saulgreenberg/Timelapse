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
using TimelapseWpf.Toolkit.Core.Utilities;

namespace TimelapseWpf.Toolkit
{
  [TemplatePart( Name = PART_VisualBrush, Type = typeof( VisualBrush ) )]
  public class Magnifier : Control
  {
    private const double DEFAULT_SIZE = 100d;
    private const string PART_VisualBrush = "PART_VisualBrush";

    #region Private Members

    private VisualBrush _visualBrush = new();

    #endregion //Private Members

    #region Properties

    #region FrameType

    public static readonly DependencyProperty FrameTypeProperty = DependencyProperty.Register( nameof(FrameType), typeof( FrameType ), typeof( Magnifier ), new UIPropertyMetadata( FrameType.Circle, OnFrameTypeChanged ) );
    public FrameType FrameType
    {
      get => ( FrameType )GetValue( FrameTypeProperty );
      set => SetValue( FrameTypeProperty, value );
    }

    private static void OnFrameTypeChanged( DependencyObject d, DependencyPropertyChangedEventArgs e )
    {
      Magnifier m = ( Magnifier )d;
      m.OnFrameTypeChanged( ( FrameType )e.OldValue, ( FrameType )e.NewValue );
    }

    protected virtual void OnFrameTypeChanged( FrameType oldValue, FrameType newValue )
    {
      this.UpdateSizeFromRadius();
    }

    #endregion //FrameType

    #region IsUsingZoomOnMouseWheel

    public static readonly DependencyProperty IsUsingZoomOnMouseWheelProperty = DependencyProperty.Register( nameof(IsUsingZoomOnMouseWheel), typeof( bool )
      , typeof( Magnifier ), new UIPropertyMetadata( true ) );
    public bool IsUsingZoomOnMouseWheel
    {
      get => (bool)GetValue( IsUsingZoomOnMouseWheelProperty );
      set => SetValue( IsUsingZoomOnMouseWheelProperty, value );
    }

    #endregion //IsUsingZoomOnMouseWheel

    #region IsFrozen

    public bool IsFrozen
    {
      get;
      private set;
    }

    #endregion

    #region Radius

    public static readonly DependencyProperty RadiusProperty = DependencyProperty.Register( nameof(Radius), typeof( double ), typeof( Magnifier ), new FrameworkPropertyMetadata( ( Magnifier.DEFAULT_SIZE / 2 ), OnRadiusPropertyChanged ) );
    public double Radius
    {
      get => ( double )GetValue( RadiusProperty );
      set => SetValue( RadiusProperty, value );
    }

    private static void OnRadiusPropertyChanged( DependencyObject d, DependencyPropertyChangedEventArgs e )
    {
      Magnifier m = ( Magnifier )d;
      m.OnRadiusChanged( e );
    }

    protected virtual void OnRadiusChanged( DependencyPropertyChangedEventArgs e )
    {
      this.UpdateSizeFromRadius();
    }

    #endregion

    #region Target

    public static readonly DependencyProperty TargetProperty = DependencyProperty.Register( nameof(Target), typeof( UIElement ), typeof( Magnifier ) );
    public UIElement Target
    {
      get => ( UIElement )GetValue( TargetProperty );
      set => SetValue( TargetProperty, value );
    }

    #endregion //Target

    #region ViewBox

    internal Rect ViewBox
    {
      get => _visualBrush.Viewbox;
      set => _visualBrush.Viewbox = value;
    }

    #endregion

    #region ZoomFactor

    public static readonly DependencyProperty ZoomFactorProperty = DependencyProperty.Register( nameof(ZoomFactor), typeof( double ), typeof( Magnifier ), new FrameworkPropertyMetadata( 0.5, OnZoomFactorPropertyChanged), OnValidationCallback );
    public double ZoomFactor
    {
      get => ( double )GetValue( ZoomFactorProperty );
      set => SetValue( ZoomFactorProperty, value );
    }

    private static bool OnValidationCallback( object baseValue )
    {
      double zoomFactor = ( double )baseValue;
      return ( zoomFactor >= 0 );
    }

    private static void OnZoomFactorPropertyChanged( DependencyObject d, DependencyPropertyChangedEventArgs e )
    {
      Magnifier m = ( Magnifier )d;
      m.OnZoomFactorChanged( e );
    }

    protected virtual void OnZoomFactorChanged( DependencyPropertyChangedEventArgs e )
    {
      UpdateViewBox();
    }

    #endregion //ZoomFactor

    #region ZoomFactorOnMouseWheel

    public static readonly DependencyProperty ZoomFactorOnMouseWheelProperty = DependencyProperty.Register( nameof(ZoomFactorOnMouseWheel), typeof( double )
      , typeof( Magnifier ), new FrameworkPropertyMetadata( 0.1d, OnZoomFactorOnMouseWheelPropertyChanged ), OnZoomFactorOnMouseWheelValidationCallback );
    public double ZoomFactorOnMouseWheel
    {
      get => (double)GetValue( ZoomFactorOnMouseWheelProperty );
      set => SetValue( ZoomFactorOnMouseWheelProperty, value );
    }

    private static bool OnZoomFactorOnMouseWheelValidationCallback( object baseValue )
    {
      double zoomFactorOnMouseWheel = (double)baseValue;
      return (zoomFactorOnMouseWheel >= 0);
    }

    private static void OnZoomFactorOnMouseWheelPropertyChanged( DependencyObject d, DependencyPropertyChangedEventArgs e )
    {
      Magnifier m = (Magnifier)d;
      m.OnZoomFactorOnMouseWheelChanged( e );
    }

    protected virtual void OnZoomFactorOnMouseWheelChanged( DependencyPropertyChangedEventArgs e )
    {
    }

    #endregion //ZoomFactorOnMouseWheel

    #endregion //Properties

    #region Constructors

    /// <summary>
    /// Initializes static members of the <see cref="Magnifier"/> class.
    /// </summary>
    static Magnifier()
    {
      DefaultStyleKeyProperty.OverrideMetadata( typeof( Magnifier ), new FrameworkPropertyMetadata( typeof( Magnifier ) ) );
      HeightProperty.OverrideMetadata( typeof( Magnifier ), new FrameworkPropertyMetadata( Magnifier.DEFAULT_SIZE ) );
      WidthProperty.OverrideMetadata( typeof( Magnifier ), new FrameworkPropertyMetadata( Magnifier.DEFAULT_SIZE ) );
    }

    public Magnifier()
    {
      this.SizeChanged += OnSizeChangedEvent;
    }

    private void OnSizeChangedEvent( object sender, SizeChangedEventArgs e )
    {
      UpdateViewBox();
    }

    private void UpdateSizeFromRadius()
    {
      // Update size for both Circle and Rectangle frame types
      if( this.FrameType == Toolkit.FrameType.Circle || this.FrameType == Toolkit.FrameType.Rectangle )
      {
        double newSize = Radius * 2;
        if(!DoubleHelper.AreVirtuallyEqual( Width, newSize ))
        {
          Width = newSize;
        }

        if(!DoubleHelper.AreVirtuallyEqual( Height, newSize ))
        {
          Height = newSize;
        }
      }
    }

    #endregion

    #region Base Class Overrides

    public override void OnApplyTemplate()
    {
      base.OnApplyTemplate();

      // Just create a brush as placeholder even if there is no such brush.
      // This avoids having to "if" each access to the _visualBrush member.
      // Do not keep the current _visualBrush whatsoever to avoid memory leaks.
      if( GetTemplateChild( PART_VisualBrush ) is not VisualBrush newBrush )
      {
        newBrush = new();
      }

      newBrush.Viewbox = _visualBrush.Viewbox;
      _visualBrush = newBrush;
    }

    #endregion // Base Class Overrides

    #region Public Methods

    public void Freeze( bool freeze )
    {
      this.IsFrozen = freeze;
    }

    #endregion

    #region Private Methods

    private void UpdateViewBox()
    {
      if( !IsInitialized )
        return;

      ViewBox = new( 
        ViewBox.Location, 
        new Size( ActualWidth * ZoomFactor , ActualHeight * ZoomFactor ) );
    }

    #endregion //Methods
  }
}
