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
using System.Windows.Documents;
using System.Windows.Input;

namespace TimelapseWpf.Toolkit
{
  public class MagnifierManager : DependencyObject
  {
    #region Members

    private MagnifierAdorner _adorner;
    private UIElement _element;

    #endregion //Members

    #region Properties

    public static readonly DependencyProperty CurrentProperty = DependencyProperty.RegisterAttached( "Magnifier", typeof( Magnifier ), typeof( UIElement ), new FrameworkPropertyMetadata( null, OnMagnifierChanged ) );
    public static void SetMagnifier( UIElement element, Magnifier value )
    {
      element.SetValue( CurrentProperty, value );
    }
    public static Magnifier GetMagnifier( UIElement element )
    {
      return ( Magnifier )element.GetValue( CurrentProperty );
    }

    private static void OnMagnifierChanged( DependencyObject d, DependencyPropertyChangedEventArgs e )
    {
      if( d is not UIElement target )
        throw new ArgumentException( "Magnifier can only be attached to a UIElement." );

      MagnifierManager manager = new();
      manager.AttachToMagnifier( target, e.NewValue as Magnifier );
    }

    #endregion //Properties

    #region Event Handlers

    private void Element_MouseLeave( object sender, MouseEventArgs e )
    {
      if( ( MagnifierManager.GetMagnifier( _element ) is { IsFrozen: true }) )
        return;

      HideAdorner();
    }

    private void Element_MouseEnter( object sender, MouseEventArgs e )
    {
      ShowAdorner();
    }

    private void Element_MouseWheel( object sender, MouseWheelEventArgs e )
    {
      if( (MagnifierManager.GetMagnifier( _element ) is { IsUsingZoomOnMouseWheel: true } magnifier) )
      {
        if( e.Delta < 0 )
        {
          var newValue = magnifier.ZoomFactor + magnifier.ZoomFactorOnMouseWheel;
#if VS2008
          magnifier.ZoomFactor = newValue;
#else
          magnifier.SetCurrentValue( Magnifier.ZoomFactorProperty, newValue );
#endif
        }
        else if ( e.Delta > 0 )
        {
          var newValue = (magnifier.ZoomFactor >= magnifier.ZoomFactorOnMouseWheel) ? magnifier.ZoomFactor - magnifier.ZoomFactorOnMouseWheel : 0d;
#if VS2008
          magnifier.ZoomFactor = newValue;
#else
          magnifier.SetCurrentValue( Magnifier.ZoomFactorProperty, newValue );
#endif
        }
        _adorner.UpdateViewBox();
      }
    }

    #endregion //Event Handlers

    #region Methods

    private void AttachToMagnifier( UIElement element, Magnifier magnifier )
    {
      _element = element;
      _element.MouseEnter += this.Element_MouseEnter;
      _element.MouseLeave += this.Element_MouseLeave;
      _element.MouseWheel += this.Element_MouseWheel;

      magnifier.Target = _element;

      _adorner = new( _element, magnifier );
    }

    void ShowAdorner()
    {
      VerifyAdornerLayer();
      _adorner.Visibility = Visibility.Visible;
    }

    void VerifyAdornerLayer()
    {
      if( _adorner.Parent != null ) return;

      AdornerLayer layer = AdornerLayer.GetAdornerLayer( _element );
      if( layer == null ) return;

      layer.Add( _adorner );
    }

    void HideAdorner()
    {
      if( _adorner.Visibility == Visibility.Visible )
      {
        _adorner.Visibility = Visibility.Collapsed;
      }
    }

#endregion //Methods
  }
}
