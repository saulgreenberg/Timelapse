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
  [CLSCompliant( false )]
  public class ULongUpDown() : CommonNumericUpDown<ulong>(ulong.TryParse, Decimal.ToUInt64, (v1, v2) => v1 < v2, (v1, v2) => v1 > v2)
  {
    #region Constructors

    static ULongUpDown()
    {
      UpdateMetadata( typeof( ULongUpDown ), 1, ulong.MinValue, ulong.MaxValue );
    }

    #endregion //Constructors

    #region Base Class Overrides

    protected override ulong IncrementValue( ulong value, ulong increment )
    {
      return value + increment;
    }

    protected override ulong DecrementValue( ulong value, ulong increment )
    {
      return value - increment;
    }

    #endregion //Base Class Overrides
  }
}
