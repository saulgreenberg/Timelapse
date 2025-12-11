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

using System.ComponentModel;

namespace TimelapseWpf.Toolkit
{
  public class AutoCompletingMaskEventArgs(MaskedTextProvider maskedTextProvider, int startPosition, int selectionLength, string input)
    : CancelEventArgs
  {
    #region MaskedTextProvider PROPERTY

    public MaskedTextProvider MaskedTextProvider => maskedTextProvider;

    #endregion MaskedTextProvider PROPERTY

    #region StartPosition PROPERTY

    public int StartPosition => startPosition;

    #endregion StartPosition PROPERTY

    #region SelectionLength PROPERTY

    public int SelectionLength => selectionLength;

    #endregion SelectionLength PROPERTY

    #region Input PROPERTY

    public string Input => input;

    #endregion Input PROPERTY


    #region AutoCompleteStartPosition PROPERTY

    public int AutoCompleteStartPosition { get; set; } = -1;

    #endregion AutoCompleteStartPosition PROPERTY

    #region AutoCompleteText PROPERTY

    public string AutoCompleteText { get; set; }

    #endregion AutoCompleteText PROPERTY
  }
}
