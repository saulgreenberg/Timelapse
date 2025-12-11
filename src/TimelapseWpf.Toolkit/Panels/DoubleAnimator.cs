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
using TimelapseWpf.Toolkit.Media.Animation;

namespace TimelapseWpf.Toolkit.Panels
{
  public class DoubleAnimator(IterativeEquation<double> equation) : IterativeAnimator
  {
    public override Rect GetInitialChildPlacement( UIElement child, Rect currentPlacement,
        Rect targetPlacement, AnimationPanel activeLayout, ref AnimationRate animationRate,
        out object placementArgs, out bool isDone )
    {
      isDone = animationRate is { HasSpeed: true, Speed: <= 0 } || animationRate is { HasDuration: true, Duration.Ticks: 0 };
      if( !isDone )
      {
        Vector startVector = new( currentPlacement.Left + ( currentPlacement.Width / 2 ), currentPlacement.Top + ( currentPlacement.Height / 2 ) );
        Vector finalVector = new( targetPlacement.Left + ( targetPlacement.Width / 2 ), targetPlacement.Top + ( targetPlacement.Height / 2 ) );
        Vector distanceVector = startVector - finalVector;
        animationRate = new( animationRate.HasDuration ? animationRate.Duration
            : TimeSpan.FromMilliseconds( distanceVector.Length / animationRate.Speed ) );
      }
      placementArgs = currentPlacement;
      return currentPlacement;
    }

    public override Rect GetNextChildPlacement( UIElement child, TimeSpan currentTime,
        Rect currentPlacement, Rect targetPlacement, AnimationPanel activeLayout,
        AnimationRate animationRate, ref object placementArgs, out bool isDone )
    {
      Rect result = targetPlacement;
      isDone = true;
      if( equation != null )
      {
        Rect from = ( Rect )placementArgs;
        TimeSpan duration = animationRate.Duration;
        isDone = currentTime >= duration;
        if( !isDone )
        {
          double x = equation.Evaluate( currentTime, from.Left, targetPlacement.Left, duration );
          double y = equation.Evaluate( currentTime, from.Top, targetPlacement.Top, duration );
          double width = Math.Max( 0, equation.Evaluate( currentTime, from.Width, targetPlacement.Width, duration ) );
          double height = Math.Max( 0, equation.Evaluate( currentTime, from.Height, targetPlacement.Height, duration ) );
          result = new( x, y, width, height );
        }
      }
      return result;
    }

    #region Private Fields

    //null

    #endregion
  }
}
