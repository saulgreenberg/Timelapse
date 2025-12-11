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
using System.Windows.Controls;

namespace TimelapseWpf.Toolkit.Primitives
{
  internal class CachedTextInfo : ICloneable
  {
    private CachedTextInfo( string text, int caretIndex, int selectionStart, int selectionLength )
    {
      this.Text = text;
      this.CaretIndex = caretIndex;
      this.SelectionStart = selectionStart;
      this.SelectionLength = selectionLength;
    }

    public CachedTextInfo( TextBox textBox )
      : this( textBox.Text, textBox.CaretIndex, textBox.SelectionStart, textBox.SelectionLength )
    {
    }

    public string Text { get; }
    public int CaretIndex { get; }
    public int SelectionStart { get; }
    public int SelectionLength { get; }

    #region ICloneable Members

    public object Clone()
    {
      return new CachedTextInfo( this.Text, this.CaretIndex, this.SelectionStart, this.SelectionLength );
    }

    #endregion
  }
}
