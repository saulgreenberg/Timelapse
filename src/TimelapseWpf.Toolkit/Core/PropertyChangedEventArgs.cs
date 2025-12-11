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

namespace TimelapseWpf.Toolkit.Core
{
  public class PropertyChangedEventArgs<T> : RoutedEventArgs
  {
    #region Constructors

    public PropertyChangedEventArgs( RoutedEvent Event, T oldValue, T newValue )
    {
      OldValue = oldValue;
      NewValue = newValue;
      this.RoutedEvent = Event;
    }

    #endregion

    #region NewValue Property

    public T NewValue { get; }

    #endregion

    #region OldValue Property

    public T OldValue { get; }

    #endregion

    protected override void InvokeEventHandler( Delegate genericHandler, object genericTarget )
    {
      PropertyChangedEventHandler<T> handler = ( PropertyChangedEventHandler<T> )genericHandler;
      handler( genericTarget, this );
    }
  }
}
