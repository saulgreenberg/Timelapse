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

namespace TimelapseWpf.Toolkit.Core
{
  public delegate void CancelRoutedEventHandler( object sender, CancelRoutedEventArgs e );

  /// <summary>
  /// An event data class that allows to inform the sender that the handler wants to cancel
  /// the ongoing action.
  /// 
  /// The handler can set the "Cancel" property to false to cancel the action.
  /// </summary>
  public class CancelRoutedEventArgs : RoutedEventArgs
  {
    public CancelRoutedEventArgs()
    {
    }

    public CancelRoutedEventArgs( RoutedEvent routedEvent )
      : base( routedEvent )
    {
    }

    public CancelRoutedEventArgs( RoutedEvent routedEvent, object source )
      : base( routedEvent, source )
    {
    }

    #region Cancel Property

    public bool Cancel
    {
      get;
      set;
    }

    #endregion Cancel Property
  }
}
