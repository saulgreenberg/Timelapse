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

namespace TimelapseWpf.Toolkit
{
  [CLSCompliant(false)]
  public class UIntegerUpDown() : CommonNumericUpDown<uint>(uint.TryParse, Decimal.ToUInt32, (v1, v2) => v1 < v2, (v1, v2) => v1 > v2)
  {
    #region Constructors

    static UIntegerUpDown()
    {
      UpdateMetadata( typeof( UIntegerUpDown ), 1, uint.MinValue, uint.MaxValue );
    }

    #endregion //Constructors

    #region Base Class Overrides

    protected override uint IncrementValue( uint value, uint increment )
    {
      return value + increment;
    }

    protected override uint DecrementValue( uint value, uint increment )
    {
      return value - increment;
    }

    #endregion //Base Class Overrides
  }
}
