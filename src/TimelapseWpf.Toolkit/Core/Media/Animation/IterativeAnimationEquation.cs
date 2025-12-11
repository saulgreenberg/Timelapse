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

namespace TimelapseWpf.Toolkit.Media.Animation
{
  [TypeConverter( typeof( IterativeEquationConverter ) )]
  public class IterativeEquation<T>
  {
    #region Constructors

    public IterativeEquation( IterativeAnimationEquationDelegate<T> equation )
    {
      _equation = equation;
    }

    internal IterativeEquation()
    {
    }

    #endregion

    public virtual T Evaluate( TimeSpan currentTime, T from, T to, TimeSpan duration )
    {
      return _equation( currentTime, from, to, duration );
    }

    #region Private Fields

    private readonly IterativeAnimationEquationDelegate<T> _equation;

    #endregion
  }
}
