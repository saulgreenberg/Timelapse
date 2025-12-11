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

namespace TimelapseWpf.Toolkit.Panels
{
  public class ChildExitingEventArgs(UIElement child, Rect? exitTo, Rect arrangeRect) : RoutedEventArgs
  {
    #region ArrangeRect Property

    public Rect ArrangeRect => arrangeRect;

    #endregion

    #region Child Property

    public UIElement Child => child;

    #endregion

    #region ExitTo Property

    public Rect? ExitTo
    {
      get => exitTo;
      set => exitTo = value;
    }

    //null

    #endregion
  }
}
