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
using System.Collections;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Reflection;
using TimelapseWpf.Toolkit.Core.Utilities;

namespace TimelapseWpf.Toolkit.Primitives
{
  public class Selector : ItemsControl, IWeakEventListener //should probably make this control an ICommandSource
  {
    #region Members

    private bool _surpressItemSelectionChanged;
    private bool _ignoreSelectedItemChanged;
    private bool _ignoreSelectedValueChanged;
    private int _ignoreSelectedItemsCollectionChanged;
    private int _ignoreSelectedMemberPathValuesChanged;
    private IList _selectedItems;
    private readonly IList _removedItems = new ObservableCollection<object>();
    private object[] _internalSelectedItems;

    private readonly ValueChangeHelper _selectedMemberPathValuesHelper;
    private readonly ValueChangeHelper _valueMemberPathValuesHelper;



    #endregion //Members

    #region Constructors

    public Selector()
    {
      this.SelectedItems = new ObservableCollection<object>();
      AddHandler( Selector.SelectedEvent, new RoutedEventHandler( ( _, args ) => this.OnItemSelectionChangedCore( args, false ) ) );
      AddHandler( Selector.UnSelectedEvent, new RoutedEventHandler( ( _, args ) => this.OnItemSelectionChangedCore( args, true ) ) );
      _selectedMemberPathValuesHelper = new( this.OnSelectedMemberPathValuesChanged );
      _valueMemberPathValuesHelper = new( this.OnValueMemberPathValuesChanged );
    }

    #endregion //Constructors

    #region Properties

    public static readonly DependencyProperty CommandProperty = DependencyProperty.Register( nameof(Command), typeof( ICommand ), typeof( Selector ), new( ( ICommand )null ) );
    [TypeConverter( typeof( CommandConverter ) )]
    public ICommand Command
    {
      get => ( ICommand )GetValue( CommandProperty );
      set => SetValue( CommandProperty, value );
    }

    #region Delimiter

    public static readonly DependencyProperty DelimiterProperty = DependencyProperty.Register( nameof(Delimiter), typeof( string ), typeof( Selector ), new UIPropertyMetadata( ",", OnDelimiterChanged ) );
    public string Delimiter
    {
      get => ( string )GetValue( DelimiterProperty );
      set => SetValue( DelimiterProperty, value );
    }

    private static void OnDelimiterChanged( DependencyObject o, DependencyPropertyChangedEventArgs e )
    {
      ( (Selector)o ).OnSelectedItemChanged( (string)e.OldValue, (string)e.NewValue );
    }

    protected virtual void OnSelectedItemChanged( string oldValue, string newValue )
    {
      if( !this.IsInitialized )
        return;

      this.UpdateSelectedValue();
    }

    #endregion

    #region SelectedItem property

    public static readonly DependencyProperty SelectedItemProperty = DependencyProperty.Register( nameof(SelectedItem), typeof( object ), typeof( Selector ), new UIPropertyMetadata( null, OnSelectedItemChanged ) );
    public object SelectedItem
    {
      get => GetValue( SelectedItemProperty );
      set => SetValue( SelectedItemProperty, value );
    }

    private static void OnSelectedItemChanged( DependencyObject sender, DependencyPropertyChangedEventArgs args )
    {
      ( ( Selector )sender ).OnSelectedItemChanged( args.OldValue, args.NewValue );
    }

    protected virtual void OnSelectedItemChanged( object oldValue, object newValue )
    {
      if( !this.IsInitialized || _ignoreSelectedItemChanged )
        return;

      _ignoreSelectedItemsCollectionChanged++;
      this.SelectedItems.Clear();
      if( newValue != null )
      {
        this.SelectedItems.Add( newValue );
      }
      this.UpdateFromSelectedItems();
      _ignoreSelectedItemsCollectionChanged--;
    }

    #endregion

    #region SelectedItems Property

    public IList SelectedItems
    {
      get => _selectedItems;
      private set
      {
        if( value == null )
          throw new ArgumentNullException( nameof(value) );

        INotifyCollectionChanged newCollection = value as INotifyCollectionChanged;

        if( _selectedItems is INotifyCollectionChanged oldCollection )
        {
          CollectionChangedEventManager.RemoveListener( oldCollection, this );
        }

        if( newCollection != null )
        {
          CollectionChangedEventManager.AddListener( newCollection, this );
        }

        var newValue = value;
        var oldValue = _selectedItems;
        if( oldValue != null )
        {
          foreach( var item in oldValue )
          {
            if( ( !newValue.Contains( item ) ) )
            {
              this.OnItemSelectionChanged( new( Selector.ItemSelectionChangedEvent, this, item, false ) );

              if( Command != null )
              {
                this.Command.Execute( item );
              }
            }
          }
        }

        foreach( var item in newValue )
        {
          this.OnItemSelectionChanged( new( Selector.ItemSelectionChangedEvent, this, item, true ) );

          if( ( ( oldValue != null ) && !oldValue.Contains( item ) ) || ( oldValue == null ) )
          {
            if( Command != null )
            {
              this.Command.Execute( item );
            }
          }
        }


        _selectedItems = value;
      }
    }

    #endregion SelectedItems


    #region SelectedItemsOverride property

    public static readonly DependencyProperty SelectedItemsOverrideProperty = DependencyProperty.Register( nameof(SelectedItemsOverride), typeof( IList ), typeof( Selector ), new UIPropertyMetadata( null, SelectedItemsOverrideChanged ) );
    public IList SelectedItemsOverride
    {
      get => ( IList )GetValue( SelectedItemsOverrideProperty );
      set => SetValue( SelectedItemsOverrideProperty, value );
    }

    private static void SelectedItemsOverrideChanged( DependencyObject sender, DependencyPropertyChangedEventArgs args )
    {
      ( ( Selector )sender ).OnSelectedItemsOverrideChanged( ( IList )args.OldValue, ( IList )args.NewValue );
    }

    protected virtual void OnSelectedItemsOverrideChanged( IList oldValue, IList newValue )
    {
      if( !this.IsInitialized )
        return;

      this.SelectedItems = newValue ?? new ObservableCollection<object>();
      this.UpdateFromSelectedItems();
    }

    #endregion


    #region SelectedMemberPath Property

    public static readonly DependencyProperty SelectedMemberPathProperty = DependencyProperty.Register( nameof(SelectedMemberPath), typeof( string ), typeof( Selector ), new UIPropertyMetadata( null, OnSelectedMemberPathChanged ) );
    public string SelectedMemberPath
    {
      get => ( string )GetValue( SelectedMemberPathProperty );
      set => SetValue( SelectedMemberPathProperty, value );
    }

    private static void OnSelectedMemberPathChanged( DependencyObject o, DependencyPropertyChangedEventArgs e )
    {
      Selector sel = ( ( Selector )o );
      sel.OnSelectedMemberPathChanged( (string)e.OldValue, (string)e.NewValue );
    }

    protected virtual void OnSelectedMemberPathChanged( string oldValue, string newValue )
    {
      if( !this.IsInitialized )
        return;

      this.UpdateSelectedMemberPathValuesBindings();
    }

    #endregion //SelectedMemberPath

    #region SelectedValue

    public static readonly DependencyProperty SelectedValueProperty = DependencyProperty.Register( nameof(SelectedValue), typeof( string ), typeof( Selector ), new FrameworkPropertyMetadata( null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnSelectedValueChanged ) );
    public string SelectedValue
    {
      get => ( string )GetValue( SelectedValueProperty );
      set => SetValue( SelectedValueProperty, value );
    }

    private static void OnSelectedValueChanged( DependencyObject o, DependencyPropertyChangedEventArgs e )
    {
      if( o is Selector selector )
        selector.OnSelectedValueChanged( ( string )e.OldValue, ( string )e.NewValue );
    }

    protected virtual void OnSelectedValueChanged( string oldValue, string newValue )
    {
      if( !this.IsInitialized || _ignoreSelectedValueChanged )
        return;

      UpdateFromSelectedValue();
    }

    #endregion //SelectedValue

    #region ValueMemberPath

    public static readonly DependencyProperty ValueMemberPathProperty = DependencyProperty.Register( nameof(ValueMemberPath), typeof( string ), typeof( Selector ), new UIPropertyMetadata( OnValueMemberPathChanged ) );
    public string ValueMemberPath
    {
      get => ( string )GetValue( ValueMemberPathProperty );
      set => SetValue( ValueMemberPathProperty, value );
    }

    private static void OnValueMemberPathChanged( DependencyObject o, DependencyPropertyChangedEventArgs e )
    {
      Selector sel = ( ( Selector )o );
      sel.OnValueMemberPathChanged( ( string )e.OldValue, ( string )e.NewValue );
    }

    protected virtual void OnValueMemberPathChanged( string oldValue, string newValue )
    {
      if( !this.IsInitialized )
        return;

      this.UpdateValueMemberPathValuesBindings();
    }

    #endregion

    #region ItemsCollection Property

    protected IEnumerable ItemsCollection => ItemsSource ?? Items;

    #endregion

    #endregion //Properties

    #region Base Class Overrides

    protected override bool IsItemItsOwnContainerOverride( object item )
    {
      return item is SelectorItem;
    }

    protected override DependencyObject GetContainerForItemOverride()
    {
      return new SelectorItem();
    }

    protected override void PrepareContainerForItemOverride(DependencyObject element, object item)
    {
      base.PrepareContainerForItemOverride(element, item);

      _surpressItemSelectionChanged = true;

      if (element is FrameworkElement selectorItem)
      {
        selectorItem.SetValue(SelectorItem.IsSelectedProperty, SelectedItems.Contains(item));
      }

      _surpressItemSelectionChanged = false;
    }

    protected override void OnItemsSourceChanged( IEnumerable oldValue, IEnumerable newValue )
    {
      base.OnItemsSourceChanged( oldValue, newValue );

      var newCollection = newValue as INotifyCollectionChanged;

      if( oldValue is INotifyCollectionChanged oldCollection )
      {
        CollectionChangedEventManager.RemoveListener( oldCollection, this );
      }

      if( newCollection != null )
      {
        CollectionChangedEventManager.AddListener( newCollection, this );
      }

      if( !this.IsInitialized )
        return;

      if( !VirtualizingPanel.GetIsVirtualizing( this )
        || (VirtualizingPanel.GetIsVirtualizing( this ) && (newValue != null)) )
      {
        this.RemoveUnavailableSelectedItems();
      }

      this.UpdateSelectedMemberPathValuesBindings();
      this.UpdateValueMemberPathValuesBindings();
    }

    protected override void OnItemsChanged( NotifyCollectionChangedEventArgs e )
    {
      base.OnItemsChanged( e );

      this.RemoveUnavailableSelectedItems();
    }

    // When a DataTemplate includes a CheckComboBox, some bindings are
    // not working, like SelectedValue.
    // We use a priority system to select the good items after initialization.
    public override void EndInit()
    {
      base.EndInit();

      if( this.SelectedItemsOverride != null )
      {
        this.OnSelectedItemsOverrideChanged( null, this.SelectedItemsOverride );
      }
      else if( this.SelectedMemberPath != null )
      {
        this.OnSelectedMemberPathChanged( null, this.SelectedMemberPath );
      }
      else if( this.SelectedValue != null )
      {
        this.OnSelectedValueChanged( null, this.SelectedValue );
      }
      else if( this.SelectedItem != null )
      {
        this.OnSelectedItemChanged( null, this.SelectedItem );
      }

      if( this.ValueMemberPath != null )
      {
        this.OnValueMemberPathChanged( null, this.ValueMemberPath );
      }
    }

    #endregion //Base Class Overrides

    #region Events

    public static readonly RoutedEvent SelectedEvent = EventManager.RegisterRoutedEvent( "SelectedEvent", RoutingStrategy.Bubble, typeof( RoutedEventHandler ), typeof( Selector ) );
    public static readonly RoutedEvent UnSelectedEvent = EventManager.RegisterRoutedEvent( "UnSelectedEvent", RoutingStrategy.Bubble, typeof( RoutedEventHandler ), typeof( Selector ) );

    public static readonly RoutedEvent ItemSelectionChangedEvent = EventManager.RegisterRoutedEvent( "ItemSelectionChanged", RoutingStrategy.Bubble, typeof( ItemSelectionChangedEventHandler ), typeof( Selector ) );
    public event ItemSelectionChangedEventHandler ItemSelectionChanged
    {
      add => AddHandler( ItemSelectionChangedEvent, value );
      remove => RemoveHandler( ItemSelectionChangedEvent, value );
    }

    #endregion //Events

    #region Methods

    protected object GetPathValue( object item, string propertyPath )
    {
      if( item == null )
        throw new ArgumentNullException( nameof(item) );

      if( String.IsNullOrEmpty( propertyPath )
        || propertyPath == "." )
        return item;


      PropertyInfo prop = item.GetType().GetProperty( propertyPath );
      return ( prop != null )
        ? prop.GetValue( item, null )
        : null;
    }

    protected object GetItemValue( object item )
    {
      return ( item != null )
        ? this.GetPathValue( item, this.ValueMemberPath )
        : null;
    }


    protected object ResolveItemByValue(string value)
    {
      if (!String.IsNullOrEmpty(ValueMemberPath))
      {
        foreach (object item in ItemsCollection)
        {
          var property = item.GetType().GetProperty(ValueMemberPath);
          if (property != null)
          {
            var propertyValue = property.GetValue(item, null);
            string propertyValueString = propertyValue?.ToString();
            if (propertyValueString != null && value.Equals(propertyValueString,
                  StringComparison.InvariantCultureIgnoreCase))
              return item;
          }
        }
      }

      return value;
    }

    internal void UpdateFromList( List<string> selectedValues, Func<object, object> GetItemfunction )
    {
      _ignoreSelectedItemsCollectionChanged++;
      // Just update the SelectedItems collection content 
      // and let the synchronization be made from UpdateFromSelectedItems();
      SelectedItems.Clear();

      if( selectedValues is { Count: > 0 } )
      {
        ValueEqualityComparer comparer = new();

        foreach( object item in ItemsCollection )
        {
          object itemValue = GetItemfunction( item );

          bool isSelected = (itemValue != null)
            && selectedValues.Contains( itemValue.ToString(), comparer );

          if( isSelected )
          {
            SelectedItems.Add( item );
          }
        }
      }
      _ignoreSelectedItemsCollectionChanged--;

      this.UpdateFromSelectedItems();
    }

    private bool? GetSelectedMemberPathValue( object item )
    {
      if( String.IsNullOrEmpty( this.SelectedMemberPath ) )
        return null;
      if( item == null )
        return null;

      string[] nameParts = this.SelectedMemberPath.Split( '.' );
      if( nameParts.Length == 1 )
      {
        var property = item.GetType().GetProperty( this.SelectedMemberPath );
        if( (property != null) && (property.PropertyType == typeof( bool )) )
          return property.GetValue( item, null ) as bool?;
        return null;
      }

      for( int i = 0; i < nameParts.Length; ++i )
      {
        if (item?.GetType() == null)
        {
          return null;
        }
        var type = item.GetType();
        var info = type.GetProperty( nameParts[ i ] );
        if( info == null )
        {
          return null;
        }

        if( i == nameParts.Length - 1 )
        {
          if( info.PropertyType == typeof( bool ) )
            return info.GetValue( item, null ) as bool?;
        }
        else
        {
          item = info.GetValue( item, null );
        }
      }
      return null;
    }

    private void SetSelectedMemberPathValue( object item, bool value )
    {
      if( String.IsNullOrEmpty( this.SelectedMemberPath ) )
        return;
      if( item == null )
        return;

      string[] nameParts = this.SelectedMemberPath.Split( '.' );
      if( nameParts.Length == 1 )
      {
        var property = item.GetType().GetProperty( this.SelectedMemberPath );
        if( (property != null) && (property.PropertyType == typeof( bool )) )
        {
          property.SetValue( item, value, null );
        }
        return;
      }

      for( int i = 0; i < nameParts.Length; ++i )
      {
        if (item?.GetType() == null)
        {
          return;
        }

        var type = item.GetType();
        var info = type.GetProperty( nameParts[ i ] );
        if( info == null )
          return;

        if( i == nameParts.Length - 1 )
        {
          if( info.PropertyType == typeof( bool ) )
          {
            info.SetValue( item, value, null );
          }
        }
        else
        {
          item = info.GetValue( item, null );
        }
      }
    }

    /// <summary>
    /// When SelectedItems collection implements INotifyPropertyChanged, this is the callback.
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    protected virtual void OnSelectedItemsCollectionChanged( object sender, NotifyCollectionChangedEventArgs e )
    {
      if( _ignoreSelectedItemsCollectionChanged > 0 )
        return;

      if( e.Action == NotifyCollectionChangedAction.Reset )
      {
        if( _internalSelectedItems != null )
        {
          foreach( var item in _internalSelectedItems )
          {
            this.OnItemSelectionChanged( new( Selector.ItemSelectionChangedEvent, this, item, false ) );

            if( Command != null )
            {
              this.Command.Execute( item );
            }
          }
        }
      }
      if( e.OldItems != null )
      {
        foreach( var item in e.OldItems )
        {
          this.OnItemSelectionChanged( new( Selector.ItemSelectionChangedEvent, this, item, false ) );

          if( Command != null )
          {
            this.Command.Execute( item );
          }
        }
      }
      if( e.NewItems != null )
      {
        foreach( var item in e.NewItems )
        {
          this.OnItemSelectionChanged( new( Selector.ItemSelectionChangedEvent, this, item, true ) );

          if( Command != null )
          {
            this.Command.Execute( item );
          }
        }
      }

      // Keep it simple for now. Just update all
      this.UpdateFromSelectedItems();
    }

    private void OnItemSelectionChangedCore( RoutedEventArgs args, bool unselected )
    {
      object item = this.ItemContainerGenerator.ItemFromContainer( ( DependencyObject )args.OriginalSource );

      // When the item is it's own container, "UnsetValue" will be returned.
      if( item == DependencyProperty.UnsetValue )
      {
        item = args.OriginalSource;
      }

      if( unselected )
      {
        while( SelectedItems.Contains( item ) )
          SelectedItems.Remove( item );
      }
      else
      {
        if( !SelectedItems.Contains( item ) )
          SelectedItems.Add( item );
      }
    }

    /// <summary>
    /// When the ItemsSource implements INotifyPropertyChanged, this is the change callback.
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="args"></param>
    private void OnItemsSourceCollectionChanged( object sender, NotifyCollectionChangedEventArgs args )
    {
      this.RemoveUnavailableSelectedItems();
      this.AddAvailableRemovedItems();
      this.UpdateSelectedMemberPathValuesBindings();
      this.UpdateValueMemberPathValuesBindings();
    }

    /// <summary>
    /// This is called when any value of any item referenced by SelectedMemberPath
    /// is modified. This may affect the SelectedItems collection.
    /// </summary>
    private void OnSelectedMemberPathValuesChanged()
    {
      if( _ignoreSelectedMemberPathValuesChanged > 0 )
        return;

      this.UpdateFromSelectedMemberPathValues();
    }

    /// <summary>
    /// This is called when any value of any item referenced by ValueMemberPath
    /// is modified. This will affect the SelectedValue property
    /// </summary>
    private void OnValueMemberPathValuesChanged()
    {
      this.UpdateSelectedValue();
    }

    private void UpdateSelectedMemberPathValuesBindings()
    {
      _selectedMemberPathValuesHelper.UpdateValueSource( ItemsCollection, SelectedMemberPath );
      this.UpdateFromSelectedMemberPathValues();
    }

    private void UpdateValueMemberPathValuesBindings()
    {
      _valueMemberPathValuesHelper.UpdateValueSource( ItemsCollection, ValueMemberPath );
    }

    /// <summary>
    /// This method will be called when the "IsSelected" property of an SelectorItem
    /// has been modified.
    /// </summary>
    /// <param name="args"></param>
    protected virtual void OnItemSelectionChanged( ItemSelectionChangedEventArgs args )
    {
      if( _surpressItemSelectionChanged )
        return;

      RaiseEvent( args );
    }

    /// <summary>
    /// Updates the SelectedValue property based on what is present in the SelectedItems property.
    /// </summary>
    private void UpdateSelectedValue()
    {
#if VS2008
      string newValue = String.Join( Delimiter, SelectedItems.Cast<object>().Select( x => GetItemValue( x ).ToString() ).ToArray() );
#else
      string newValue = String.Join( Delimiter, SelectedItems.Cast<object>().Select( GetItemValue ) );
#endif
      if( String.IsNullOrEmpty( SelectedValue ) || !SelectedValue.Equals( newValue ) )
      {
        _ignoreSelectedValueChanged = true;
        SelectedValue = newValue;
        _ignoreSelectedValueChanged = false;
      }
    }

    /// <summary>
    /// Updates the SelectedItem property based on what is present in the SelectedItems property.
    /// </summary>
    private void UpdateSelectedItem()
    {
      if( !SelectedItems.Contains( SelectedItem ) )
      {
        _ignoreSelectedItemChanged = true;
        SelectedItem = ( SelectedItems.Count > 0 ) ? SelectedItems[ 0 ] : null;
        _ignoreSelectedItemChanged = false;
      }
    }

    /// <summary>
    /// Update the SelectedItems collection based on the values 
    /// refered to by the SelectedMemberPath property.
    /// </summary>
    private void UpdateFromSelectedMemberPathValues()
    {
      _ignoreSelectedItemsCollectionChanged++;
      foreach( var item in ItemsCollection )
      {
        bool? isSelected = this.GetSelectedMemberPathValue( item );
        if( isSelected != null )
        {
          if( isSelected.Value )
          {
            if( !SelectedItems.Contains( item ) )
            {
              SelectedItems.Add( item );
            }
          }
          else
          {
            if( SelectedItems.Contains( item ) )
            {
              SelectedItems.Remove( item );
            }
          }
        }
      }
      _ignoreSelectedItemsCollectionChanged--;
      this.UpdateFromSelectedItems();
    }

    internal void UpdateSelectedItems( IList selectedItems )
    {
      if( selectedItems == null )
        throw new ArgumentNullException( nameof(selectedItems) );

      // Just check if the collection is the same..
      if( selectedItems.Count == this.SelectedItems.Count
        && selectedItems.Cast<object>().SequenceEqual( this.SelectedItems.Cast<object>() ) )
        return;

      _ignoreSelectedItemsCollectionChanged++;
      this.SelectedItems.Clear();
      foreach( object newItem in selectedItems )
      {
        this.SelectedItems.Add( newItem );
      }
      _ignoreSelectedItemsCollectionChanged--;
      this.UpdateFromSelectedItems();
    }

    /// <summary>
    /// Updates the following based on the content of SelectedItems:
    /// - All SelectorItems "IsSelected" properties
    /// - Values refered to by SelectedMemberPath
    /// - SelectedItem property
    /// - SelectedValue property
    /// Refered to by the SelectedMemberPath property.
    /// </summary>
    private void UpdateFromSelectedItems()
    {
      foreach( object o in ItemsCollection )
      {
        bool isSelected = SelectedItems.Contains( o );

        _ignoreSelectedMemberPathValuesChanged++;
        this.SetSelectedMemberPathValue(o, isSelected);
        _ignoreSelectedMemberPathValuesChanged--;

        if( ItemContainerGenerator.ContainerFromItem( o ) is SelectorItem selectorItem )
        {
          selectorItem.IsSelected = isSelected;
        }
      }

      UpdateSelectedItem();
      UpdateSelectedValue();

      _internalSelectedItems = new object[ this.SelectedItems.Count ];
      this.SelectedItems.CopyTo( _internalSelectedItems, 0 );
    }

    /// <summary>
    /// Removes all items from SelectedItems that are no longer in ItemsSource.
    /// </summary>
    private void RemoveUnavailableSelectedItems()
    {
      _ignoreSelectedItemsCollectionChanged++;
      HashSet<object> hash = [..ItemsCollection.Cast<object>()];

      for( int i = 0; i < SelectedItems.Count; i++ )
      {
        if( !hash.Contains( SelectedItems[ i ] ) )
        {
          _removedItems.Add( SelectedItems[ i ] );
          SelectedItems.RemoveAt( i );
          i--;
        }
      }
      _ignoreSelectedItemsCollectionChanged--;

      UpdateSelectedItem();
      UpdateSelectedValue();
    }

    private void AddAvailableRemovedItems()
    {
      HashSet<object> hash = [..ItemsCollection.Cast<object>()];

      for( int i = 0; i < _removedItems.Count; i++ )
      {
        if( hash.Contains( _removedItems[ i ] ) )
        {
          SelectedItems.Add( _removedItems[ i ] );
          _removedItems.RemoveAt( i );          
          i--;
        }
      }
    }

    /// <summary>
    /// Updates the SelectedItems collection based on the content of
    /// the SelectedValue property.
    /// </summary>
    private void UpdateFromSelectedValue()
    {
      List<string> selectedValues = null;
      if( !String.IsNullOrEmpty( SelectedValue ) )
      {
        selectedValues = SelectedValue.Split([Delimiter], StringSplitOptions.RemoveEmptyEntries ).ToList();
      }

      this.UpdateFromList( selectedValues, this.GetItemValue );
    }

    #endregion //Methods

    #region IWeakEventListener Members

    public bool ReceiveWeakEvent( Type managerType, object sender, EventArgs e )
    {
      if( managerType == typeof( CollectionChangedEventManager ) )
      {
        if( object.ReferenceEquals( _selectedItems, sender ) )
        {
          this.OnSelectedItemsCollectionChanged( sender, ( NotifyCollectionChangedEventArgs )e );
          return true;
        }
        else if( object.ReferenceEquals( ItemsCollection, sender ) )
        {
          this.OnItemsSourceCollectionChanged( sender, ( NotifyCollectionChangedEventArgs )e );
          return true;
        }
      }

      return false;
    }

    #endregion

    #region ValueEqualityComparer private class

    private class ValueEqualityComparer : IEqualityComparer<string>
    {
      public bool Equals( string x, string y )
      {
        return string.Equals( x, y, StringComparison.InvariantCultureIgnoreCase );
      }

      public int GetHashCode( string obj )
      {
        return 1;
      }
    }

    #endregion
  }


  public delegate void ItemSelectionChangedEventHandler( object sender, ItemSelectionChangedEventArgs e );
  public class ItemSelectionChangedEventArgs(RoutedEvent routedEvent, object source, object item, bool isSelected)
    : RoutedEventArgs(routedEvent, source)
  {
    public bool IsSelected
    {
      get;
      private set;
    } = isSelected;

    public object Item
    {
      get;
      private set;
    } = item;
  }
}
