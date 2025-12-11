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
  public class SByteUpDown() : CommonNumericUpDown<sbyte>(sbyte.TryParse, Decimal.ToSByte, (v1, v2) => v1 < v2, (v1, v2) => v1 > v2)
  {
    #region Constructors

    static SByteUpDown()
    {
      UpdateMetadata( typeof( SByteUpDown ), 1, sbyte.MinValue, sbyte.MaxValue );
    }

    #endregion //Constructors

    #region Base Class Overrides

    protected override sbyte IncrementValue( sbyte value, sbyte increment )
    {
      return ( sbyte )( value + increment );
    }

    protected override sbyte DecrementValue( sbyte value, sbyte increment )
    {
      return ( sbyte )( value - increment );
    }

    #endregion //Base Class Overrides
  }
}
