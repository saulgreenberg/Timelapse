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
using System.Diagnostics;
using System.Linq.Expressions;
using System.Reflection;

namespace TimelapseWpf.Toolkit.Core.Utilities
{
  internal static class ReflectionHelper
  {
    /// <summary>
    /// Check the existence of the specified public instance (i.e. non static) property against
    /// the type of the specified source object. If the property is not defined by the type,
    /// a debug assertion will fail. Typically used to validate the parameter of a 
    /// RaisePropertyChanged method.
    /// </summary>
    /// <param name="sourceObject">The object for which the type will be checked.</param>
    /// <param name="propertyName">The name of the property.</param>
    [System.Diagnostics.Conditional( "DEBUG" )]
    internal static void ValidatePublicPropertyName( object sourceObject, string propertyName )
    {
      if( sourceObject == null )
        throw new ArgumentNullException( nameof(sourceObject) );

      if( propertyName == null )
        throw new ArgumentNullException( nameof(propertyName) );

      System.Diagnostics.Debug.Assert( sourceObject.GetType().GetProperty( propertyName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.FlattenHierarchy | System.Reflection.BindingFlags.Public ) != null,
        $"Public property {propertyName} not found on object of type {sourceObject.GetType().FullName}.");
    }

    /// <summary>
    /// Check the existence of the specified instance (i.e. non static) property against
    /// the type of the specified source object. If the property is not defined by the type,
    /// a debug assertion will fail. Typically used to validate the parameter of a 
    /// RaisePropertyChanged method.
    /// </summary>
    /// <param name="sourceObject">The object for which the type will be checked.</param>
    /// <param name="propertyName">The name of the property.</param>
    [System.Diagnostics.Conditional( "DEBUG" )]
    internal static void ValidatePropertyName( object sourceObject, string propertyName )
    {
      if( sourceObject == null )
        throw new ArgumentNullException( nameof(sourceObject) );

      if( propertyName == null )
        throw new ArgumentNullException( nameof(propertyName) );

      System.Diagnostics.Debug.Assert( sourceObject.GetType().GetProperty( propertyName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.FlattenHierarchy | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic ) != null,
        $"Public property {propertyName} not found on object of type {sourceObject.GetType().FullName}.");
    }

    internal static bool TryGetEnumDescriptionAttributeValue(Enum enumeration, out string description
    )
    {
      try
      {
        FieldInfo fieldInfo = enumeration.GetType().GetField(enumeration.ToString());
        if (fieldInfo != null && (fieldInfo.GetCustomAttributes(typeof(DescriptionAttribute), true
            ) is DescriptionAttribute[] { Length: > 0 } attributes))
        {
          description = attributes[0].Description;
          return true;
        }
      }
      catch
      {
        // ignored
      }

      description = String.Empty;
      return false;
    }

    [DebuggerStepThrough]
    internal static string GetPropertyOrFieldName( MemberExpression expression )
    {
      if( !ReflectionHelper.TryGetPropertyOrFieldName( expression, out var propertyOrFieldName ) )
        throw new InvalidOperationException( "Unable to retrieve the property or field name." );

      return propertyOrFieldName;
    }

    [DebuggerStepThrough]
    internal static string GetPropertyOrFieldName<TMember>( Expression<Func<TMember>> expression )
    {
      if( !ReflectionHelper.TryGetPropertyOrFieldName( expression, out var propertyOrFieldName ) )
        throw new InvalidOperationException( "Unable to retrieve the property or field name." );

      return propertyOrFieldName;
    }

    [DebuggerStepThrough]
    internal static bool TryGetPropertyOrFieldName( MemberExpression expression, out string propertyOrFieldName )
    {
      propertyOrFieldName = null;

      if( expression == null )
        return false;

      propertyOrFieldName = expression.Member.Name;

      return true;
    }

    [DebuggerStepThrough]
    internal static bool TryGetPropertyOrFieldName<TMember>( Expression<Func<TMember>> expression, out string propertyOrFieldName )
    {
      propertyOrFieldName = null;

      if( expression == null )
        return false;

      return ReflectionHelper.TryGetPropertyOrFieldName( expression.Body as MemberExpression, out propertyOrFieldName );
    }

    public static bool IsPublicInstanceProperty( Type type, string propertyName )
    {
      BindingFlags flags = ( System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.FlattenHierarchy | System.Reflection.BindingFlags.Public );
      return type.GetProperty( propertyName, flags ) != null;
    }
  }
}
