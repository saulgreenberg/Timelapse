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
using System.Collections;
using System.Windows.Data;

namespace TimelapseWpf.Toolkit.Core.Utilities
{
  /// <summary>
  /// This helper class will raise events when a specific
  /// path value on one or many items changes.
  /// </summary>
  internal class ValueChangeHelper : DependencyObject
  {

    #region Value Property
    /// <summary>
    /// This private property serves as the target of a binding that monitors the value of the binding
    /// of each item in the source.
    /// </summary>
    private static readonly DependencyProperty ValueProperty = DependencyProperty.Register( nameof(Value), typeof( object ), typeof( ValueChangeHelper ), new UIPropertyMetadata( null, OnValueChanged ) );
    private object Value
    {
      get => GetValue( ValueProperty );
      set => SetValue( ValueProperty, value );
    }

    private static void OnValueChanged( DependencyObject sender, DependencyPropertyChangedEventArgs args )
    {
      ( ( ValueChangeHelper )sender ).RaiseValueChanged();
    }
    #endregion

    public event EventHandler ValueChanged;

    #region Constructor

    public ValueChangeHelper(Action changeCallback)
    {
      if( changeCallback == null )
        throw new ArgumentNullException( nameof(changeCallback) );

      this.ValueChanged += ( _, _ ) => changeCallback();
    }

    #endregion

    #region Methods

    public void UpdateValueSource( object sourceItem, string path )
    {
      BindingBase binding = null;
      if( sourceItem != null && path != null )
      {
        binding = new Binding( path ) { Source = sourceItem };
      }

      this.UpdateBinding( binding );
    }

    public void UpdateValueSource( IEnumerable sourceItems, string path )
    {
      BindingBase binding = null;
      if( sourceItems != null && path != null )
      {
        MultiBinding multiBinding = new()
        {
          Converter = new BlankMultiValueConverter()
        };

        foreach( var item in sourceItems )
        {
          multiBinding.Bindings.Add( new Binding( path ) { Source = item } );
        }

        binding = multiBinding;
      }

      this.UpdateBinding( binding );
    }

    private void UpdateBinding( BindingBase binding )
    {
      if( binding != null )
      {
        BindingOperations.SetBinding( this, ValueChangeHelper.ValueProperty, binding );
      }
      else
      {
        this.ClearBinding();
      }
    }

    private void ClearBinding()
    {
      BindingOperations.ClearBinding( this, ValueChangeHelper.ValueProperty );
    }

    private void RaiseValueChanged()
    {
      if( this.ValueChanged != null )
      {
        this.ValueChanged( this, EventArgs.Empty );
      }
    }

    #endregion

    #region BlankMultiValueConverter private class

    private class BlankMultiValueConverter : IMultiValueConverter
    {
      public object Convert( object[] values, Type targetType, object parameter, System.Globalization.CultureInfo culture )
      {
        // We will not use the result anyway. We just want the change notification to kick in.
        // Return a new object to have a different value.
        return new();
      }

      public object[] ConvertBack( object value, Type[] targetTypes, object parameter, System.Globalization.CultureInfo culture )
      {
        throw new InvalidOperationException();
      }
    }

    #endregion
  }
}
