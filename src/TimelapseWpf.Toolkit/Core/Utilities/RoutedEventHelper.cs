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

namespace TimelapseWpf.Toolkit.Core.Utilities
{
  internal static class RoutedEventHelper
  {
    internal static void RaiseEvent( DependencyObject target, RoutedEventArgs args )
    {
      if( target is UIElement uiElement )
      {
        uiElement.RaiseEvent( args );
      }
      else if( target is ContentElement element )
      {
        element.RaiseEvent( args );
      }
    }

    internal static void AddHandler( DependencyObject element, RoutedEvent routedEvent, Delegate handler )
    {
      if( element is UIElement uie )
      {
        uie.AddHandler( routedEvent, handler );
      }
      else
      {
        if( element is ContentElement ce )
        {
          ce.AddHandler( routedEvent, handler );
        }
      }
    }

    internal static void RemoveHandler( DependencyObject element, RoutedEvent routedEvent, Delegate handler )
    {
      if( element is UIElement uie )
      {
        uie.RemoveHandler( routedEvent, handler );
      }
      else
      {
        if( element is ContentElement ce )
        {
          ce.RemoveHandler( routedEvent, handler );
        }
      }
    }
  }
}
