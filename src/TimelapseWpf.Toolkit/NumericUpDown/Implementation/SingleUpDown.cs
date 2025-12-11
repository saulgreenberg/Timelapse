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
  public class SingleUpDown() : CommonNumericUpDown<float>(Single.TryParse, Decimal.ToSingle, (v1, v2) => v1 < v2, (v1, v2) => v1 > v2)
  {
    #region Constructors

    static SingleUpDown()
    {
      UpdateMetadata( typeof( SingleUpDown ), 1f, float.NegativeInfinity, float.PositiveInfinity );
    }

    #endregion //Constructors

    #region Properties

    #region AllowInputSpecialValues

    public static readonly DependencyProperty AllowInputSpecialValuesProperty =
        DependencyProperty.Register( nameof(AllowInputSpecialValues), typeof( AllowedSpecialValues ), typeof( SingleUpDown ), new UIPropertyMetadata( AllowedSpecialValues.None ) );

    public AllowedSpecialValues AllowInputSpecialValues
    {
      get => ( AllowedSpecialValues )GetValue( AllowInputSpecialValuesProperty );
      set => SetValue( AllowInputSpecialValuesProperty, value );
    }

    #endregion //AllowInputSpecialValues

    #endregion

    #region Base Class Overrides

    protected override float? OnCoerceIncrement( float? baseValue )
    {
      if( baseValue is float.NaN )
        throw new ArgumentException( "NaN is invalid for Increment." );

      return base.OnCoerceIncrement( baseValue );
    }

    protected override float? OnCoerceMaximum( float? baseValue )
    {
      if( baseValue is float.NaN)
        throw new ArgumentException( "NaN is invalid for Maximum." );

      return base.OnCoerceMaximum( baseValue );
    }

    protected override float? OnCoerceMinimum( float? baseValue )
    {
      if( baseValue is float.NaN )
        throw new ArgumentException( "NaN is invalid for Minimum." );

      return base.OnCoerceMinimum( baseValue );
    }

    protected override float IncrementValue( float value, float increment )
    {
      return value + increment;
    }

    protected override float DecrementValue( float value, float increment )
    {
      return value - increment;
    }

    protected override void SetValidSpinDirection()
    {
      if( Value.HasValue && float.IsInfinity( Value.Value ) && ( Spinner != null ) )
      {
        Spinner.ValidSpinDirection = ValidSpinDirections.None;
      }
      else
      {
        base.SetValidSpinDirection();
      }
    }

    protected override float? ConvertTextToValue( string text )
    {
      float? result = base.ConvertTextToValue( text );

      if( result != null )
      {
        if( float.IsNaN( result.Value ) )
          TestInputSpecialValue( this.AllowInputSpecialValues, AllowedSpecialValues.NaN );
        else if( float.IsPositiveInfinity( result.Value ) )
          TestInputSpecialValue( this.AllowInputSpecialValues, AllowedSpecialValues.PositiveInfinity );
        else if( float.IsNegativeInfinity( result.Value ) )
          TestInputSpecialValue( this.AllowInputSpecialValues, AllowedSpecialValues.NegativeInfinity );
      }

      return result;
    }

    #endregion
  }
}
