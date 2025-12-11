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

using System.Windows;
using System.Windows.Data;

namespace TimelapseWpf.Toolkit.Core.Utilities
{
  internal sealed class GeneralUtilities : DependencyObject
  {
    private GeneralUtilities() { }

    #region StubValue attached property

    internal static readonly DependencyProperty StubValueProperty = DependencyProperty.RegisterAttached(
      "StubValue",
      typeof( object ),
      typeof( GeneralUtilities ),
      new FrameworkPropertyMetadata( null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault ) );

    internal static object GetStubValue( DependencyObject obj )
    {
      return obj.GetValue( GeneralUtilities.StubValueProperty );
    }

    internal static void SetStubValue( DependencyObject obj, object value )
    {
      obj.SetValue( GeneralUtilities.StubValueProperty, value );
    }

    #endregion StubValue attached property

    public static object GetPathValue( object sourceObject, string path )
    {
      var targetObj = new GeneralUtilities();
      BindingOperations.SetBinding( targetObj, GeneralUtilities.StubValueProperty, new Binding( path ) { Source = sourceObject } );
      object value = GeneralUtilities.GetStubValue( targetObj );
      BindingOperations.ClearBinding( targetObj, GeneralUtilities.StubValueProperty );
      return value;
    }

    public static object GetBindingValue( object sourceObject, Binding binding )
    {
      Binding bindingClone = new()
      {
        BindsDirectlyToSource = binding.BindsDirectlyToSource,
        Converter = binding.Converter,
        ConverterCulture = binding.ConverterCulture,
        ConverterParameter = binding.ConverterParameter,
        FallbackValue = binding.FallbackValue,
        Mode = BindingMode.OneTime, 
        Path = binding.Path,
        StringFormat = binding.StringFormat,
        TargetNullValue = binding.TargetNullValue,
        XPath = binding.XPath,
        Source = sourceObject
      };

      var targetObj = new GeneralUtilities();
      BindingOperations.SetBinding( targetObj, GeneralUtilities.StubValueProperty, bindingClone );
      object value = GeneralUtilities.GetStubValue( targetObj );
      BindingOperations.ClearBinding( targetObj, GeneralUtilities.StubValueProperty );
      return value;
    }

    internal static bool CanConvertValue( object value, object targetType )
    {
      return ( ( value != null )
              && ( !object.Equals( value.GetType(), targetType ) )
              && ( !object.Equals( targetType, typeof( object ) ) ) );
    }
  }
}
