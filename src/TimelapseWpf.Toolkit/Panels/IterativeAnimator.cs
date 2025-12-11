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
using System.ComponentModel;
using System.Windows;
using TimelapseWpf.Toolkit.Media.Animation;
using TimelapseWpf.Toolkit.Core;

namespace TimelapseWpf.Toolkit.Panels
{
  [TypeConverter( typeof( AnimatorConverter ) )]
  public abstract class IterativeAnimator
  {
    #region Default Static Property

    public static IterativeAnimator Default { get; } = new DefaultAnimator();

    #endregion

    public abstract Rect GetInitialChildPlacement(
      UIElement child,
      Rect currentPlacement,
      Rect targetPlacement,
      AnimationPanel activeLayout,
      ref AnimationRate animationRate,
      out object placementArgs,
      out bool isDone );

    public abstract Rect GetNextChildPlacement(
      UIElement child,
      TimeSpan currentTime,
      Rect currentPlacement,
      Rect targetPlacement,
      AnimationPanel activeLayout,
      AnimationRate animationRate,
      ref object placementArgs,
      out bool isDone );

    #region DefaultAnimator Nested Type

    private sealed class DefaultAnimator : IterativeAnimator
    {
      public override Rect GetInitialChildPlacement( UIElement child, Rect currentPlacement, Rect targetPlacement, AnimationPanel activeLayout, ref AnimationRate animationRate, out object placementArgs, out bool isDone )
      {
        throw new InvalidOperationException( ErrorMessages.GetMessage( ErrorMessages.DefaultAnimatorCantAnimate ) );
      }

      public override Rect GetNextChildPlacement( UIElement child, TimeSpan currentTime, Rect currentPlacement, Rect targetPlacement, AnimationPanel activeLayout, AnimationRate animationRate, ref object placementArgs, out bool isDone )
      {
        throw new InvalidOperationException( ErrorMessages.GetMessage( ErrorMessages.DefaultAnimatorCantAnimate ) );
      }
    }

    #endregion
  }
}
