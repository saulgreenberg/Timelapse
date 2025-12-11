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

namespace TimelapseWpf.Toolkit.Media.Animation
{
  public class PennerEquation : IterativeEquation<double>
  {
    #region Constructors

    internal PennerEquation( PennerEquationDelegate pennerImpl )
    {
      _pennerImpl = pennerImpl;
    }

    #endregion

    public override double Evaluate( TimeSpan currentTime, double from, double to, TimeSpan duration )
    {
      double t = currentTime.TotalSeconds;
      double b = from;
      double c = to - from;
      double d = duration.TotalSeconds;

      return _pennerImpl( t, b, c, d );
    }

    #region Private Fields

    private readonly PennerEquationDelegate _pennerImpl;

    #endregion

    #region PennerEquationDelegate Delegate

    internal delegate double PennerEquationDelegate( double t, double b, double c, double d );

    #endregion
  }
}
