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
using TimelapseWpf.Toolkit.Core;

namespace TimelapseWpf.Toolkit.Zoombox
{
  public class ZoomboxViewChangedEventArgs(
    ZoomboxView oldView,
    ZoomboxView newView,
    int oldViewStackIndex,
    int newViewStackIndex)
    : PropertyChangedEventArgs<ZoomboxView>(Zoombox.CurrentViewChangedEvent, oldView, newView)
  {
    #region NewViewStackIndex Property

    public int NewViewStackIndex => newViewStackIndex;

    #endregion

    #region NewViewStackIndex Property

    public int OldViewStackIndex => oldViewStackIndex;

    #endregion

    #region NewViewStackIndex Property

    public bool IsNewViewFromStack => newViewStackIndex >= 0;

    #endregion

    #region NewViewStackIndex Property

    public bool IsOldViewFromStack => oldViewStackIndex >= 0;

    #endregion

    protected override void InvokeEventHandler( Delegate genericHandler, object genericTarget )
    {
      ( ( ZoomboxViewChangedEventHandler )genericHandler )( genericTarget, this );
    }
  }
}
