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
  public class IntegerUpDown() : CommonNumericUpDown<int>(Int32.TryParse, Decimal.ToInt32, (v1, v2) => v1 < v2, (v1, v2) => v1 > v2)
  {
    #region Constructors

    static IntegerUpDown()
    {
      UpdateMetadata( typeof( IntegerUpDown ), 1, int.MinValue, int.MaxValue );
    }

    #endregion //Constructors

    #region Base Class Overrides

    protected override int IncrementValue( int value, int increment )
    {
      return value + increment;
    }

    protected override int DecrementValue( int value, int increment )
    {
      return value - increment;
    }

    #endregion //Base Class Overrides
  }
}
