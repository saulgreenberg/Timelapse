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

namespace TimelapseWpf.Toolkit.Core.Utilities
{
  internal static class PointHelper
  {
    public static double DistanceBetween( Point p1, Point p2 )
    {
      return Math.Sqrt( Math.Pow( p1.X - p2.X, 2 ) + Math.Pow( p1.Y - p2.Y, 2 ) );
    }

    public static Point Empty => new( double.NaN, double.NaN );

    public static bool IsEmpty( Point point )
    {
      return DoubleHelper.IsNaN( point.X ) && DoubleHelper.IsNaN( point.Y );
    }
  }
}
