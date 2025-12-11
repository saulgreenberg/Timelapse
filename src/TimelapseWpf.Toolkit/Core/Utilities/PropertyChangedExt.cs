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
using System.Linq.Expressions;

namespace TimelapseWpf.Toolkit.Core.Utilities
{
  internal static class PropertyChangedExt
  {
    #region Notify Methods

    public static void Notify<TMember>( 
      this INotifyPropertyChanged sender,
      PropertyChangedEventHandler handler, 
      Expression<Func<TMember>> expression )
    {
      if( sender == null )
        throw new ArgumentNullException( nameof(sender) );

      if( expression == null )
        throw new ArgumentNullException( nameof(expression) );

      if( expression.Body is not MemberExpression body )
        throw new ArgumentException( "The expression must target a property or field.", nameof(expression) );

      string propertyName = PropertyChangedExt.GetPropertyName( body, sender.GetType() );

      PropertyChangedExt.NotifyCore( sender, handler, propertyName );
    }

    public static void Notify( this INotifyPropertyChanged sender, PropertyChangedEventHandler handler, string propertyName )
    {
      if( sender == null )
        throw new ArgumentNullException( nameof(sender) );

      if( propertyName == null )
        throw new ArgumentNullException( nameof(propertyName) );

      ReflectionHelper.ValidatePropertyName( sender, propertyName );

      PropertyChangedExt.NotifyCore( sender, handler, propertyName );
    }

    private static void NotifyCore( INotifyPropertyChanged sender, PropertyChangedEventHandler handler, string propertyName )
    {
      if( handler != null )
      {
        handler( sender, new( propertyName ) );
      }
    }

    #endregion

    #region PropertyChanged Verification Methods

    internal static bool PropertyChanged( string propertyName, PropertyChangedEventArgs e, bool targetPropertyOnly )
    {
      string target = e.PropertyName;
      if( target == propertyName )
        return true;

      return ( !targetPropertyOnly )
          && ( string.IsNullOrEmpty( target ) );
    }

    internal static bool PropertyChanged<TOwner, TMember>(
      Expression<Func<TMember>> expression,
      PropertyChangedEventArgs e,
      bool targetPropertyOnly )
    {
      if( expression.Body is not MemberExpression body )
        throw new ArgumentException( "The expression must target a property or field.", nameof(expression) );

      return PropertyChangedExt.PropertyChanged( body, typeof( TOwner ), e, targetPropertyOnly );
    }

    internal static bool PropertyChanged<TOwner, TMember>(
      Expression<Func<TOwner, TMember>> expression,
      PropertyChangedEventArgs e,
      bool targetPropertyOnly )
    {
      if( expression.Body is not MemberExpression body )
        throw new ArgumentException( "The expression must target a property or field.", nameof(expression) );

      return PropertyChangedExt.PropertyChanged( body, typeof( TOwner ), e, targetPropertyOnly );
    }

    private static bool PropertyChanged( MemberExpression expression, Type ownerType, PropertyChangedEventArgs e, bool targetPropertyOnly )
    {
      var propertyName = PropertyChangedExt.GetPropertyName( expression, ownerType );

      return PropertyChangedExt.PropertyChanged( propertyName, e, targetPropertyOnly );
    }

    #endregion

    private static string GetPropertyName(MemberExpression expression, Type ownerType)
    {
      var targetType = expression.Expression?.Type;
      if (targetType == null || !targetType.IsAssignableFrom(ownerType))
        throw new ArgumentException("The expression must target a property or field on the appropriate owner.", nameof(expression) );
  
      return ReflectionHelper.GetPropertyOrFieldName(expression);
    }
  }
}
