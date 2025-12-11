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

namespace TimelapseWpf.Toolkit.Core
{
  public class QueryValueFromTextEventArgs(string text, object mValue) : EventArgs
  {
    #region Text Property

    public string Text => text;

    #endregion Text Property

    #region Value Property

    public object Value
    {
      get => mValue;
      set => mValue = value;
    }

    #endregion Value Property

    #region HasParsingError Property

    public bool HasParsingError { get; set; }

    #endregion HasParsingError Property

  }
}
