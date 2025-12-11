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

namespace TimelapseWpf.Toolkit
{
  public class DoubleUpDown() : CommonNumericUpDown<double>(Double.TryParse, Decimal.ToDouble, (v1, v2) => v1 < v2, (v1, v2) => v1 > v2)
  {
    #region Constructors

    static DoubleUpDown()
    {
      UpdateMetadata( typeof( DoubleUpDown ), 1d, double.NegativeInfinity, double.PositiveInfinity );
    }

    #endregion //Constructors

    #region Properties


    #region AllowInputSpecialValues

    public static readonly DependencyProperty AllowInputSpecialValuesProperty =
        DependencyProperty.Register( nameof(AllowInputSpecialValues), typeof( AllowedSpecialValues ), typeof( DoubleUpDown ), new UIPropertyMetadata( AllowedSpecialValues.None ) );

    public AllowedSpecialValues AllowInputSpecialValues
    {
      get => ( AllowedSpecialValues )GetValue( AllowInputSpecialValuesProperty );
      set => SetValue( AllowInputSpecialValuesProperty, value );
    }

    #endregion //AllowInputSpecialValues

    #endregion

    #region Base Class Overrides

    protected override double? OnCoerceIncrement( double? baseValue )
    {
      if( baseValue is double.NaN )
        throw new ArgumentException( "NaN is invalid for Increment." );

      return base.OnCoerceIncrement( baseValue );
    }

    protected override double? OnCoerceMaximum( double? baseValue )
    {
      if( baseValue is double.NaN )
        throw new ArgumentException( "NaN is invalid for Maximum." );

      return base.OnCoerceMaximum( baseValue );
    }

    protected override double? OnCoerceMinimum( double? baseValue )
    {
      if( baseValue is double.NaN )
        throw new ArgumentException( "NaN is invalid for Minimum." );

      return base.OnCoerceMinimum( baseValue );
    }

    protected override double IncrementValue( double value, double increment )
    {
      return value + increment;
    }

    protected override double DecrementValue( double value, double increment )
    {
      return value - increment;
    }

    protected override void SetValidSpinDirection()
    {
      if( Value.HasValue && double.IsInfinity( Value.Value ) && ( Spinner != null ) )
      {
        Spinner.ValidSpinDirection = ValidSpinDirections.None;
      }
      else
      {
        base.SetValidSpinDirection();
      }
    }

    protected override double? ConvertTextToValue( string text )
    {
      double? result = base.ConvertTextToValue( text );
      if( result != null )
      {
        if( double.IsNaN( result.Value ) )
          TestInputSpecialValue( this.AllowInputSpecialValues, AllowedSpecialValues.NaN );
        else if( double.IsPositiveInfinity( result.Value ) )
          TestInputSpecialValue( this.AllowInputSpecialValues, AllowedSpecialValues.PositiveInfinity );
        else if( double.IsNegativeInfinity( result.Value ) )
          TestInputSpecialValue( this.AllowInputSpecialValues, AllowedSpecialValues.NegativeInfinity );
      }

      return result;
    }

    #endregion
  }
}
