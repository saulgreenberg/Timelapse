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
using System.Windows.Input;

namespace TimelapseWpf.Toolkit
{
  [System.Diagnostics.CodeAnalysis.SuppressMessage( "Microsoft.Design", "CA1003:UseGenericEventHandlerInstances" )]
  public delegate void QueryMoveFocusEventHandler( object sender, QueryMoveFocusEventArgs e );

  public class QueryMoveFocusEventArgs : RoutedEventArgs
  {
    //default CTOR private to prevent its usage.
    private QueryMoveFocusEventArgs()
    {
    }

    //internal to prevent anybody from building this type of event.
    internal QueryMoveFocusEventArgs( FocusNavigationDirection direction, bool reachedMaxLength )
      : base( AutoSelectTextBox.QueryMoveFocusEvent )
    {
      FocusNavigationDirection = direction;
      ReachedMaxLength = reachedMaxLength;
    }

    public FocusNavigationDirection FocusNavigationDirection { get; }

    public bool ReachedMaxLength { get; }

    public bool CanMoveFocus { get; set; } = true;
  }
}
