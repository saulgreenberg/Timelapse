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
using System.Windows.Controls;

namespace TimelapseWpf.Toolkit.Primitives
{
  public class SelectorItem : ContentControl
  {
    #region Constructors

    static SelectorItem()
    {
      DefaultStyleKeyProperty.OverrideMetadata( typeof( SelectorItem ), new FrameworkPropertyMetadata( typeof( SelectorItem ) ) );
    }

    #endregion //Constructors

    #region Properties

    public static readonly DependencyProperty IsSelectedProperty = DependencyProperty.Register( nameof(IsSelected), typeof( bool? ), typeof( SelectorItem ), new UIPropertyMetadata( false, OnIsSelectedChanged ) );
    public bool? IsSelected
    {
      get => ( bool? )GetValue( IsSelectedProperty );
      set => SetValue( IsSelectedProperty, value );
    }

    private static void OnIsSelectedChanged( DependencyObject o, DependencyPropertyChangedEventArgs e )
    {
      if( o is SelectorItem selectorItem )
        selectorItem.OnIsSelectedChanged( ( bool? )e.OldValue, ( bool? )e.NewValue );
    }

    protected virtual void OnIsSelectedChanged( bool? oldValue, bool? newValue )
    {
      if( newValue.HasValue )
      {
        this.RaiseEvent(newValue.Value 
          ? new(Selector.SelectedEvent, this) 
          : new RoutedEventArgs(Selector.UnSelectedEvent, this));
      }
    }

    internal Selector ParentSelector => ItemsControl.ItemsControlFromItemContainer( this ) as Selector;

    #endregion //Properties

    #region Events

    public static readonly RoutedEvent SelectedEvent = Selector.SelectedEvent.AddOwner( typeof( SelectorItem ) );
    public static readonly RoutedEvent UnselectedEvent = Selector.UnSelectedEvent.AddOwner( typeof( SelectorItem ) );

    #endregion
  }
}
