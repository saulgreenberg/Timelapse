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
using System.Globalization;
using System.Windows;
using TimelapseWpf.Toolkit.Primitives;


namespace TimelapseWpf.Toolkit
{
  public abstract class NumericUpDown<T> : UpDownBase<T>
  {
#pragma warning disable 0618

    #region Properties

    #region AutoMoveFocus

    public bool AutoMoveFocus
    {
      get => ( bool )GetValue( AutoMoveFocusProperty );
      set => SetValue( AutoMoveFocusProperty, value );
    }

    public static readonly DependencyProperty AutoMoveFocusProperty =
        DependencyProperty.Register( nameof(AutoMoveFocus), typeof( bool ), typeof( NumericUpDown<T> ), new UIPropertyMetadata( false ) );

    #endregion AutoMoveFocus

    #region AutoSelectBehavior

    public AutoSelectBehavior AutoSelectBehavior
    {
      get => ( AutoSelectBehavior )GetValue( AutoSelectBehaviorProperty );
      set => SetValue( AutoSelectBehaviorProperty, value );
    }

    public static readonly DependencyProperty AutoSelectBehaviorProperty =
        DependencyProperty.Register( nameof(AutoSelectBehavior), typeof( AutoSelectBehavior ), typeof( NumericUpDown<T> ),
      new UIPropertyMetadata( AutoSelectBehavior.OnFocus ) );

    #endregion AutoSelectBehavior PROPERTY

    #region FormatString

    public static readonly DependencyProperty FormatStringProperty = DependencyProperty.Register( nameof(FormatString), typeof( string ), typeof( NumericUpDown<T> ), new UIPropertyMetadata( String.Empty, OnFormatStringChanged, OnCoerceFormatString ) );
    public string FormatString
    {
      get => ( string )GetValue( FormatStringProperty );
      set => SetValue( FormatStringProperty, value );
    }

    private static object OnCoerceFormatString( DependencyObject o, object baseValue )
    {
      if( o is NumericUpDown<T> numericUpDown )
        return numericUpDown.OnCoerceFormatString( (string)baseValue );

      return baseValue;
    }

    protected virtual string OnCoerceFormatString( string baseValue )
    {
      return baseValue ?? string.Empty;
    }

    private static void OnFormatStringChanged( DependencyObject o, DependencyPropertyChangedEventArgs e )
    {
      if( o is NumericUpDown<T> numericUpDown )
        numericUpDown.OnFormatStringChanged( ( string )e.OldValue, ( string )e.NewValue );
    }

    protected virtual void OnFormatStringChanged( string oldValue, string newValue )
    {
      if( IsInitialized )
      {
        this.SyncTextAndValueProperties( false, null );
      }
    }

    #endregion //FormatString

    #region Increment

    public static readonly DependencyProperty IncrementProperty = DependencyProperty.Register( nameof(Increment), typeof( T ), typeof( NumericUpDown<T> ), new( default( T ), OnIncrementChanged, OnCoerceIncrement ) );
    public T Increment
    {
      get => ( T )GetValue( IncrementProperty );
      set => SetValue( IncrementProperty, value );
    }

    private static void OnIncrementChanged( DependencyObject o, DependencyPropertyChangedEventArgs e )
    {
      if( o is NumericUpDown<T> numericUpDown )
        numericUpDown.OnIncrementChanged( ( T )e.OldValue, ( T )e.NewValue );
    }

    protected virtual void OnIncrementChanged( T oldValue, T newValue )
    {
      if( this.IsInitialized )
      {
        SetValidSpinDirection();
      }
    }

    private static object OnCoerceIncrement( DependencyObject d, object baseValue )
    {
      if( d is NumericUpDown<T> numericUpDown )
        return numericUpDown.OnCoerceIncrement( ( T )baseValue );

      return baseValue;
    }

    protected virtual T OnCoerceIncrement( T baseValue )
    {
      return baseValue;
    }

    #endregion

    #region MaxLength

    public static readonly DependencyProperty MaxLengthProperty = DependencyProperty.Register( nameof(MaxLength), typeof( int ), typeof( NumericUpDown<T> ), new UIPropertyMetadata( 0 ) );
    public int MaxLength
    {
      get => ( int )GetValue( MaxLengthProperty );
      set => SetValue( MaxLengthProperty, value );
    }

    #endregion //MaxLength

    #endregion //Properties

    #region Methods

    protected static decimal ParsePercent( string text, IFormatProvider cultureInfo )
    {
      NumberFormatInfo info = NumberFormatInfo.GetInstance( cultureInfo );

      text = text.Replace( info.PercentSymbol, null );

      decimal result = Decimal.Parse( text, NumberStyles.Any, info );
      result = result / 100;

      return result;
    }

    #endregion //Methods
  }

#pragma warning restore 0618
}
