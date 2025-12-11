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

using TimelapseWpf.Toolkit.Media.Animation;

namespace TimelapseWpf.Toolkit.Panels
{
  public static class Animators
  {
    #region BackEaseIn Static Property

    public static DoubleAnimator BackEaseIn => _backEaseIn ??= new(PennerEquations.BackEaseIn);

    private static DoubleAnimator _backEaseIn;

    #endregion

    #region BackEaseInOut Static Property

    public static DoubleAnimator BackEaseInOut => _backEaseInOut ??= new(PennerEquations.BackEaseInOut);

    private static DoubleAnimator _backEaseInOut;

    #endregion

    #region BackEaseOut Static Property

    public static DoubleAnimator BackEaseOut => _backEaseOut ??= new(PennerEquations.BackEaseOut);

    private static DoubleAnimator _backEaseOut;

    #endregion

    #region BounceEaseIn Static Property

    public static DoubleAnimator BounceEaseIn => _bounceEaseIn ??= new(PennerEquations.BounceEaseIn);

    private static DoubleAnimator _bounceEaseIn;

    #endregion

    #region BounceEaseInOut Static Property

    public static DoubleAnimator BounceEaseInOut => _bounceEaseInOut ??= new(PennerEquations.BounceEaseInOut);

    private static DoubleAnimator _bounceEaseInOut;

    #endregion

    #region BounceEaseOut Static Property

    public static DoubleAnimator BounceEaseOut => _bounceEaseOut ??= new(PennerEquations.BounceEaseOut);

    private static DoubleAnimator _bounceEaseOut;

    #endregion

    #region CircEaseIn Static Property

    public static DoubleAnimator CircEaseIn => _circEaseIn ??= new(PennerEquations.CircEaseIn);

    private static DoubleAnimator _circEaseIn;

    #endregion

    #region CircEaseInOut Static Property

    public static DoubleAnimator CircEaseInOut => _circEaseInOut ??= new(PennerEquations.CircEaseInOut);

    private static DoubleAnimator _circEaseInOut;

    #endregion

    #region CircEaseOut Static Property

    public static DoubleAnimator CircEaseOut => _circEaseOut ??= new(PennerEquations.CircEaseOut);

    private static DoubleAnimator _circEaseOut;

    #endregion

    #region CubicEaseIn Static Property

    public static DoubleAnimator CubicEaseIn => _cubicEaseIn ??= new(PennerEquations.CubicEaseIn);

    private static DoubleAnimator _cubicEaseIn;

    #endregion

    #region CubicEaseInOut Static Property

    public static DoubleAnimator CubicEaseInOut => _cubicEaseInOut ??= new(PennerEquations.CubicEaseInOut);

    private static DoubleAnimator _cubicEaseInOut;

    #endregion

    #region CubicEaseOut Static Property

    public static DoubleAnimator CubicEaseOut => _cubicEaseOut ??= new(PennerEquations.CubicEaseOut);

    private static DoubleAnimator _cubicEaseOut;

    #endregion

    #region ElasticEaseIn Static Property

    public static DoubleAnimator ElasticEaseIn => _elasticEaseIn ??= new(PennerEquations.ElasticEaseIn);

    private static DoubleAnimator _elasticEaseIn;

    #endregion

    #region ElasticEaseInOut Static Property

    public static DoubleAnimator ElasticEaseInOut => _elasticEaseInOut ??= new(PennerEquations.ElasticEaseInOut);

    private static DoubleAnimator _elasticEaseInOut;

    #endregion

    #region ElasticEaseOut Static Property

    public static DoubleAnimator ElasticEaseOut => _elasticEaseOut ??= new(PennerEquations.ElasticEaseOut);

    private static DoubleAnimator _elasticEaseOut;

    #endregion

    #region ExpoEaseIn Static Property

    public static DoubleAnimator ExpoEaseIn => _expoEaseIn ??= new(PennerEquations.ExpoEaseIn);

    private static DoubleAnimator _expoEaseIn;

    #endregion

    #region ExpoEaseInOut Static Property

    public static DoubleAnimator ExpoEaseInOut => _expoEaseInOut ??= new(PennerEquations.ExpoEaseInOut);

    private static DoubleAnimator _expoEaseInOut;

    #endregion

    #region ExpoEaseOut Static Property

    public static DoubleAnimator ExpoEaseOut => _expoEaseOut ??= new(PennerEquations.ExpoEaseOut);

    private static DoubleAnimator _expoEaseOut;

    #endregion

    #region Linear Static Property

    public static DoubleAnimator Linear => _linear ??= new(PennerEquations.Linear);

    private static DoubleAnimator _linear;

    #endregion

    #region QuadEaseIn Static Property

    public static DoubleAnimator QuadEaseIn => _quadEaseIn ??= new(PennerEquations.QuadEaseIn);

    private static DoubleAnimator _quadEaseIn;

    #endregion

    #region QuadEaseInOut Static Property

    public static DoubleAnimator QuadEaseInOut => _quadEaseInOut ??= new(PennerEquations.QuadEaseInOut);

    private static DoubleAnimator _quadEaseInOut;

    #endregion

    #region QuadEaseOut Static Property

    public static DoubleAnimator QuadEaseOut => _quadEaseOut ??= new(PennerEquations.QuadEaseOut);

    private static DoubleAnimator _quadEaseOut;

    #endregion

    #region QuartEaseIn Static Property

    public static DoubleAnimator QuartEaseIn => _quartEaseIn ??= new(PennerEquations.QuartEaseIn);

    private static DoubleAnimator _quartEaseIn;

    #endregion

    #region QuartEaseInOut Static Property

    public static DoubleAnimator QuartEaseInOut => _quartEaseInOut ??= new(PennerEquations.QuartEaseInOut);

    private static DoubleAnimator _quartEaseInOut;

    #endregion

    #region QuartEaseOut Static Property

    public static DoubleAnimator QuartEaseOut => _quartEaseOut ??= new(PennerEquations.QuartEaseOut);

    private static DoubleAnimator _quartEaseOut;

    #endregion

    #region QuintEaseIn Static Property

    public static DoubleAnimator QuintEaseIn => _quintEaseIn ??= new(PennerEquations.QuintEaseIn);

    private static DoubleAnimator _quintEaseIn;

    #endregion

    #region QuintEaseInOut Static Property

    public static DoubleAnimator QuintEaseInOut => _quintEaseInOut ??= new(PennerEquations.QuintEaseInOut);

    private static DoubleAnimator _quintEaseInOut;

    #endregion

    #region QuintEaseOut Static Property

    public static DoubleAnimator QuintEaseOut => _quintEaseOut ??= new(PennerEquations.QuintEaseOut);

    private static DoubleAnimator _quintEaseOut;

    #endregion

    #region SineEaseIn Static Property

    public static DoubleAnimator SineEaseIn => _sineEaseIn ??= new(PennerEquations.SineEaseIn);

    private static DoubleAnimator _sineEaseIn;

    #endregion

    #region SineEaseInOut Static Property

    public static DoubleAnimator SineEaseInOut => _sineEaseInOut ??= new(PennerEquations.SineEaseInOut);

    private static DoubleAnimator _sineEaseInOut;

    #endregion

    #region SineEaseOut Static Property

    public static DoubleAnimator SineEaseOut => _sineEaseOut ??= new(PennerEquations.SineEaseOut);

    private static DoubleAnimator _sineEaseOut;

    #endregion
  }
}
