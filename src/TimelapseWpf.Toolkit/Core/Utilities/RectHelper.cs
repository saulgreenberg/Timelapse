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
  internal static class RectHelper
  {
    public static Point Center( Rect rect )
    {
      return new( rect.Left + rect.Width / 2, rect.Top + rect.Height / 2 );
    }

    public static Point? GetNearestPointOfIntersectionBetweenRectAndSegment( Rect rect, Segment segment, Point point )
    {
      Point? result = null;
      double distance = double.PositiveInfinity;

      Segment leftIntersection = segment.Intersection( new( rect.BottomLeft, rect.TopLeft ) );
      Segment topIntersection = segment.Intersection( new( rect.TopLeft, rect.TopRight ) );
      Segment rightIntersection = segment.Intersection( new( rect.TopRight, rect.BottomRight ) );
      Segment bottomIntersection = segment.Intersection( new( rect.BottomRight, rect.BottomLeft ) );

      RectHelper.AdjustResultForIntersectionWithSide( ref result, ref distance, leftIntersection, point );
      RectHelper.AdjustResultForIntersectionWithSide( ref result, ref distance, topIntersection, point );
      RectHelper.AdjustResultForIntersectionWithSide( ref result, ref distance, rightIntersection, point );
      RectHelper.AdjustResultForIntersectionWithSide( ref result, ref distance, bottomIntersection, point );

      return result;
    }

    public static Rect GetRectCenteredOnPoint( Point center, Size size )
    {
      return new( new( center.X - size.Width / 2, center.Y - size.Height / 2 ), size );
    }

    private static void AdjustResultForIntersectionWithSide( ref Point? result, ref double distance, Segment intersection, Point point )
    {
      if( !intersection.IsEmpty )
      {
        if( intersection.Contains( point ) )
        {
          distance = 0;
          result = point;
          return;
        }

        double p1Distance = PointHelper.DistanceBetween( point, intersection.P1 );
        double p2Distance = double.PositiveInfinity;
        if( !intersection.IsPoint )
        {
          p2Distance = PointHelper.DistanceBetween( point, intersection.P2 );
        }

        if( Math.Min( p1Distance, p2Distance ) < distance )
        {
          if( p1Distance < p2Distance )
          {
            distance = p1Distance;
            result = intersection.P1;
          }
          else
          {
            distance = p2Distance;
            result = intersection.P2;
          }
        }
      }
    }
  }
}
