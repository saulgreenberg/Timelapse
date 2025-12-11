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

namespace TimelapseWpf.Toolkit
{
  public class ByteUpDown() : CommonNumericUpDown<byte>(Byte.TryParse, Decimal.ToByte, (v1, v2) => v1 < v2, (v1, v2) => v1 > v2)
  {
    #region Constructors

    static ByteUpDown()
    {
      UpdateMetadata( typeof( ByteUpDown ), 1, byte.MinValue, byte.MaxValue );
      MaxLengthProperty.OverrideMetadata( typeof(ByteUpDown), new FrameworkPropertyMetadata( 3 ) );
    }

    #endregion //Constructors

    #region Base Class Overrides

    protected override byte IncrementValue( byte value, byte increment )
    {
      return ( byte )( value + increment );
    }

    protected override byte DecrementValue( byte value, byte increment )
    {
      return ( byte )( value - increment );
    }

    #endregion //Base Class Overrides
  }
}
