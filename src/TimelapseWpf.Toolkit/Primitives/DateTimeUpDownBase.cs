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
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Threading;

namespace TimelapseWpf.Toolkit.Primitives
{
  public abstract class DateTimeUpDownBase<T> : UpDownBase<T>
  {
    #region Members

    internal List<DateTimeInfo> _dateTimeInfoList = [];
    internal DateTimeInfo _selectedDateTimeInfo;
    internal bool _fireSelectionChangedEvent = true;
    internal bool _processTextChanged = true;

    #endregion //Members

    #region Properties

    #region CurrentDateTimePart

    public static readonly DependencyProperty CurrentDateTimePartProperty = DependencyProperty.Register( nameof(CurrentDateTimePart), typeof( DateTimePart )
      , typeof( DateTimeUpDownBase<T> ), new UIPropertyMetadata( DateTimePart.Other, OnCurrentDateTimePartChanged ) );
    public DateTimePart CurrentDateTimePart
    {
      get => (DateTimePart)GetValue( CurrentDateTimePartProperty );
      set => SetValue( CurrentDateTimePartProperty, value );
    }

    private static void OnCurrentDateTimePartChanged( DependencyObject o, DependencyPropertyChangedEventArgs e )
    {
      if( o is DateTimeUpDownBase<T> dateTimeUpDownBase )
        dateTimeUpDownBase.OnCurrentDateTimePartChanged( (DateTimePart)e.OldValue, (DateTimePart)e.NewValue );
    }

    protected virtual void OnCurrentDateTimePartChanged( DateTimePart oldValue, DateTimePart newValue )
    {
      this.Select( this.GetDateTimeInfo( newValue ) );
    }

    #endregion //CurrentDateTimePart

    #region Step

    public static readonly DependencyProperty StepProperty = DependencyProperty.Register( nameof(Step), typeof( int )
      , typeof( DateTimeUpDownBase<T> ), new UIPropertyMetadata( 1, OnStepChanged ) );
    public int Step
    {
      get => (int)GetValue( StepProperty );
      set => SetValue( StepProperty, value );
    }

    private static void OnStepChanged( DependencyObject o, DependencyPropertyChangedEventArgs e )
    {
      if( o is DateTimeUpDownBase<T> dateTimeUpDownBase )
        dateTimeUpDownBase.OnStepChanged( (int)e.OldValue, (int)e.NewValue );
    }

    protected virtual void OnStepChanged( int oldValue, int newValue )
    {
    }

    #endregion //Step

    #endregion

    #region Constructors

    internal DateTimeUpDownBase()
    {
#pragma warning disable CS8618, CA2214
      // We need to initialize the DateTimeInfo list here because during OnValueChanged the derived class members are not yet initialized
      // However, we also want to guard against a null value, but not quite sure if we can.
      // so we just supress the warning for now.
      this.InitializeDateTimeInfoList( this.Value );
#pragma warning restore CS8618, CA2214
      this.Loaded += this.DateTimeUpDownBase_Loaded;
    }

    #endregion

    #region BaseClass Overrides

    public override void OnApplyTemplate()
    {
      if( this.TextBox != null )
      {
        this.TextBox.SelectionChanged -= this.TextBox_SelectionChanged;
      }

      base.OnApplyTemplate();

      if( this.TextBox != null )
      {
        this.TextBox.SelectionChanged += this.TextBox_SelectionChanged;
      }
    }

    protected override void OnPreviewKeyDown( KeyEventArgs e )
    {
      int selectionStart = _selectedDateTimeInfo?.StartPosition ?? 0;
      int selectionLength = _selectedDateTimeInfo?.Length ?? 0;

      switch( e.Key )
      {
        case Key.Enter:
          {
            if (!IsReadOnly)
            {
              _fireSelectionChangedEvent = false;
              if (BindingOperations.GetBindingExpression(TextBox, System.Windows.Controls.TextBox.TextProperty
                  ) is { } binding)
              {
                binding.UpdateSource();
              }
              _fireSelectionChangedEvent = true;
            }
            return;
          }
        case Key.Add:
          if( this.AllowSpin && !this.IsReadOnly )
          {
            this.DoIncrement();
            e.Handled = true;
          }
          _fireSelectionChangedEvent = false;
          break;
        case Key.Subtract:
          if( this.AllowSpin && !this.IsReadOnly )
          {
            this.DoDecrement();
            e.Handled = true;
          }
          _fireSelectionChangedEvent = false;
          break;
        case Key.Right:
          if( this.IsCurrentValueValid() )
          {
            this.PerformKeyboardSelection( selectionStart + selectionLength );
            e.Handled = true;
          }
          _fireSelectionChangedEvent = false;
          break;
        case Key.Left:
          if( this.IsCurrentValueValid() )
          {
            this.PerformKeyboardSelection( selectionStart > 0 ? selectionStart - 1 : 0 );
            e.Handled = true;
          }
          _fireSelectionChangedEvent = false;
          break;
        default:
          {
            _fireSelectionChangedEvent = false;
            break;
          }
      }

      base.OnPreviewKeyDown( e );
    }

    #endregion

    #region Event Hanlders

    private void TextBox_SelectionChanged( object sender, RoutedEventArgs e )
    {
      if( _fireSelectionChangedEvent )
        this.PerformMouseSelection();
      else
        _fireSelectionChangedEvent = true;
    }

    private void DateTimeUpDownBase_Loaded( object sender, RoutedEventArgs e )
    {
      this.InitSelection();
    }

    #endregion

    #region Methods

    protected virtual void InitializeDateTimeInfoList( T value )
    {
    }

    protected virtual bool IsCurrentValueValid()
    {
      return true;
    }

    protected virtual void PerformMouseSelection()
    {
      var dateTimeInfo = this.GetDateTimeInfo( TextBox.SelectionStart );
      if( dateTimeInfo is { Type: DateTimePart.Other } )
      {
        this.Dispatcher.BeginInvoke( DispatcherPriority.Background, new Action( () =>
        {
          // Select the next dateTime part
          this.Select( this.GetDateTimeInfo( dateTimeInfo.StartPosition + dateTimeInfo.Length ) );
        }
        ) );
        return;
      }     

      this.Select( dateTimeInfo );
    }

    protected virtual bool IsLowerThan( T value1, T value2 )
    {
      return false;
    }

    protected virtual bool IsGreaterThan( T value1, T value2 )
    {
      return false;
    }

    internal DateTimeInfo GetDateTimeInfo( int selectionStart )
    {
      return _dateTimeInfoList.FirstOrDefault( ( info ) =>
                              ( info.StartPosition <= selectionStart ) && ( selectionStart < ( info.StartPosition + info.Length ) ) );
    }

    internal DateTimeInfo GetDateTimeInfo( DateTimePart part )
    {
      return _dateTimeInfoList.FirstOrDefault( ( info ) =>info.Type == part );
    }

    internal virtual void Select( DateTimeInfo info )
    {
      if( (info != null) && !info.Equals( _selectedDateTimeInfo ) && ( this.TextBox != null) && !string.IsNullOrEmpty( this.TextBox.Text ) )
      {
        _fireSelectionChangedEvent = false;
        this.TextBox.Select( info.StartPosition, info.Length );
        _fireSelectionChangedEvent = true;
        _selectedDateTimeInfo = info;
#if VS2008
        this.CurrentDateTimePart = info.Type;
#else
        this.SetCurrentValue( DateTimeUpDownBase<T>.CurrentDateTimePartProperty, info.Type );
#endif
      }
    }

    internal T CoerceValueMinMax( T value )
    {
      if( this.IsLowerThan( value, this.Minimum ) )
        return this.Minimum;
      else if( this.IsGreaterThan( value, this.Maximum ) )
        return this.Maximum;
      else
        return value;
    }

    internal void ValidateDefaultMinMax( T value )
    {
      // DefaultValue is always accepted.
      if( object.Equals( value, this.DefaultValue ) )
        return;

      if( this.IsLowerThan( value, this.Minimum ) )
        throw new ArgumentOutOfRangeException( nameof(value), $"Value must be greater than MinValue of {this.Minimum}");
      else if( this.IsGreaterThan( value, this.Maximum ) )
        throw new ArgumentOutOfRangeException( nameof(value), $"Value must be less than MaxValue of {this.Maximum}");
    }

    internal T GetClippedMinMaxValue( T value )
    {
      if( this.IsGreaterThan( value, this.Maximum ) )
        return this.Maximum;
      else if( this.IsLowerThan( value, this.Minimum ) )
        return this.Minimum;
      return value;
    }

    protected internal virtual void PerformKeyboardSelection( int nextSelectionStart )
    {
      this.TextBox.Focus();

      if( !this.UpdateValueOnEnterKey )
      {
        this.CommitInput();
      }

      int selectedDateStartPosition = _selectedDateTimeInfo?.StartPosition ?? 0;
      int direction = nextSelectionStart - selectedDateStartPosition;
      this.Select(direction > 0 
        ? this.GetNextDateTimeInfo(nextSelectionStart) 
        : this.GetPreviousDateTimeInfo(nextSelectionStart - 1));
    }    

    private DateTimeInfo GetNextDateTimeInfo( int nextSelectionStart )
    {
      DateTimeInfo nextDateTimeInfo = this.GetDateTimeInfo( nextSelectionStart ) ?? _dateTimeInfoList.First();

      DateTimeInfo initialDateTimeInfo = nextDateTimeInfo;

      while( nextDateTimeInfo.Type == DateTimePart.Other )
      {
        nextDateTimeInfo = this.GetDateTimeInfo( nextDateTimeInfo.StartPosition + nextDateTimeInfo.Length ) ?? _dateTimeInfoList.First();
        if( object.Equals( nextDateTimeInfo, initialDateTimeInfo ) )
          throw new InvalidOperationException( "Couldn't find a valid DateTimeInfo." );
      }
      return nextDateTimeInfo;
    }

    private DateTimeInfo GetPreviousDateTimeInfo( int previousSelectionStart )
    {
      DateTimeInfo previousDateTimeInfo = this.GetDateTimeInfo( previousSelectionStart );
      if( previousDateTimeInfo == null )
      {
        if( _dateTimeInfoList.Count > 0 )
        {
          previousDateTimeInfo = _dateTimeInfoList.Last();
        }
      }

      DateTimeInfo initialDateTimeInfo = previousDateTimeInfo;

      while( previousDateTimeInfo is { Type: DateTimePart.Other } )
      {
        previousDateTimeInfo = this.GetDateTimeInfo( previousDateTimeInfo.StartPosition - 1 ) ?? _dateTimeInfoList.Last();
        if( object.Equals( previousDateTimeInfo, initialDateTimeInfo ) )
          throw new InvalidOperationException( "Couldn't find a valid DateTimeInfo." );
      }
      return previousDateTimeInfo;
    }

    private void InitSelection()
    {
      if( _selectedDateTimeInfo == null )
      {
        this.Select( (this.CurrentDateTimePart != DateTimePart.Other) ? this.GetDateTimeInfo( this.CurrentDateTimePart ) : this.GetDateTimeInfo( 0 ) );
      }
    }

    #endregion
  }
}
