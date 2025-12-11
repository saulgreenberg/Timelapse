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
  public class ShortUpDown() : CommonNumericUpDown<short>(Int16.TryParse, Decimal.ToInt16, (v1, v2) => v1 < v2, (v1, v2) => v1 > v2)
  {
    #region Constructors

    static ShortUpDown()
    {
      UpdateMetadata( typeof( ShortUpDown ), 1, short.MinValue, short.MaxValue );
    }

    #endregion //Constructors

    #region Base Class Overrides

    protected override short IncrementValue( short value, short increment )
    {
      return ( short )( value + increment );
    }

    protected override short DecrementValue( short value, short increment )
    {
      return ( short )( value - increment );
    }

    #endregion //Base Class Overrides
  }
}
