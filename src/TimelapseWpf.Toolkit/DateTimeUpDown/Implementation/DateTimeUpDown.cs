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
using TimelapseWpf.Toolkit.Core.Utilities;
using System.Windows.Input;
// ReSharper disable StringCompareIsCultureSpecific.2

namespace TimelapseWpf.Toolkit
{
  public class DateTimeUpDown : DateTimeUpDownBase<DateTime?>
  {
    #region Members

    private DateTime? _lastValidDate; //null
    private bool _setKindInternal;

    #endregion

    #region Properties

    #region AutoClipTimeParts

    public static readonly DependencyProperty AutoClipTimePartsProperty = DependencyProperty.Register( nameof(AutoClipTimeParts), typeof( bool ), typeof( DateTimeUpDown ), new UIPropertyMetadata( false ) );
    public bool AutoClipTimeParts
    {
      get => (bool)GetValue( AutoClipTimePartsProperty );
      set => SetValue( AutoClipTimePartsProperty, value );
    }

    #endregion //AutoClipTimeParts

    #region Format

    public static readonly DependencyProperty FormatProperty = DependencyProperty.Register( nameof(Format), typeof( DateTimeFormat ), typeof( DateTimeUpDown ), new UIPropertyMetadata( DateTimeFormat.FullDateTime, OnFormatChanged ) );
    public DateTimeFormat Format
    {
      get => ( DateTimeFormat )GetValue( FormatProperty );
      set => SetValue( FormatProperty, value );
    }

    private static void OnFormatChanged( DependencyObject o, DependencyPropertyChangedEventArgs e )
    {
      if( o is DateTimeUpDown dateTimeUpDown )
        dateTimeUpDown.OnFormatChanged( ( DateTimeFormat )e.OldValue, ( DateTimeFormat )e.NewValue );
    }

    protected virtual void OnFormatChanged( DateTimeFormat oldValue, DateTimeFormat newValue )
    {
      FormatUpdated();
    }

    #endregion //Format

    #region FormatString

    public static readonly DependencyProperty FormatStringProperty = DependencyProperty.Register( nameof(FormatString), typeof( string ), typeof( DateTimeUpDown ), new UIPropertyMetadata( default( String ), OnFormatStringChanged ), IsFormatStringValid );
    public string FormatString
    {
      get => ( string )GetValue( FormatStringProperty );
      set => SetValue( FormatStringProperty, value );
    }

    internal static bool IsFormatStringValid( object value )
    {
      try
      {
        // Test the format string if it is used.
        _ = CultureInfo.CurrentCulture.DateTimeFormat.Calendar.MinSupportedDateTime.ToString( (string)value, CultureInfo.CurrentCulture );
        return true;
      }
      catch
      {
        return false;
      }
    }

    private static void OnFormatStringChanged( DependencyObject o, DependencyPropertyChangedEventArgs e )
    {
      if( o is DateTimeUpDown dateTimeUpDown )
        dateTimeUpDown.OnFormatStringChanged( ( string )e.OldValue, ( string )e.NewValue );
    }

    protected virtual void OnFormatStringChanged( string oldValue, string newValue )
    {
        FormatUpdated();
    }

    #endregion //FormatString

    #region Kind

    public static readonly DependencyProperty KindProperty = DependencyProperty.Register( nameof(Kind), typeof( DateTimeKind ), typeof( DateTimeUpDown ), 
      new FrameworkPropertyMetadata( DateTimeKind.Unspecified, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnKindChanged ) );
    public DateTimeKind Kind
    {
      get => ( DateTimeKind )GetValue( KindProperty );
      set => SetValue( KindProperty, value );
    }

    private static void OnKindChanged( DependencyObject o, DependencyPropertyChangedEventArgs e )
    {
      if( o is DateTimeUpDown dateTimeUpDown )
        dateTimeUpDown.OnKindChanged( ( DateTimeKind )e.OldValue, ( DateTimeKind )e.NewValue );
    }

    protected virtual void OnKindChanged( DateTimeKind oldValue, DateTimeKind newValue )
    {
      //Upate the value based on kind. (Postpone to EndInit if not yet initialized)
      if( !_setKindInternal
        && this.Value != null 
        && this.IsInitialized )
      {
        this.Value = this.ConvertToKind( this.Value.Value, newValue );
      }
    }

    private void SetKindInternal( DateTimeKind kind )
    {
      _setKindInternal = true;
      try
      {
#if VS2008
        // Warning : Binding could be lost
        this.Kind = kind;
#else
        //We use SetCurrentValue to not erase the possible underlying 
        //OneWay Binding. (This will also update correctly any
        //possible TwoWay bindings).
        this.SetCurrentValue( DateTimeUpDown.KindProperty, kind );
#endif
      }
      finally
      {
        _setKindInternal = false;
      }
    }

    #endregion //Kind

    #region TempValue (Internal)

    internal DateTime? TempValue
    {
      get;
      set;
    }

    #endregion

    #region ContextNow (Private)

    internal DateTime ContextNow => DateTimeUtilities.GetContextNow( this.Kind );

    #endregion

    #endregion //Properties

    #region Constructors

    static DateTimeUpDown()
    {
      DefaultStyleKeyProperty.OverrideMetadata( typeof( DateTimeUpDown ), new FrameworkPropertyMetadata( typeof( DateTimeUpDown ) ) );
      MaximumProperty.OverrideMetadata( typeof( DateTimeUpDown ), new FrameworkPropertyMetadata( CultureInfo.CurrentCulture.DateTimeFormat.Calendar.MaxSupportedDateTime ) );
      MinimumProperty.OverrideMetadata( typeof( DateTimeUpDown ), new FrameworkPropertyMetadata( CultureInfo.CurrentCulture.DateTimeFormat.Calendar.MinSupportedDateTime ) );
      UpdateValueOnEnterKeyProperty.OverrideMetadata( typeof( DateTimeUpDown ), new FrameworkPropertyMetadata( true ) );
    }

    public DateTimeUpDown()
    {
      this.Loaded += this.DateTimeUpDown_Loaded;
    }

    #endregion //Constructors

    #region Base Class Overrides

    public override bool CommitInput()
    {
      bool isSyncValid = this.SyncTextAndValueProperties( true, Text );
      _lastValidDate = this.Value;
      return isSyncValid;
    }

    protected override void OnCultureInfoChanged( CultureInfo oldValue, CultureInfo newValue )
    {
      FormatUpdated();
    }

    protected override void OnIncrement()
    {
      if( this.IsCurrentValueValid() )
      {
        this.Increment( this.Step );
      }
    }    

    protected override void OnDecrement()
    {
      if( this.IsCurrentValueValid() )
      {
        this.Increment( -this.Step );
      }
    }

    protected override void OnTextChanged( string previousValue, string currentValue )
    {
      if( !_processTextChanged )
        return;

      base.OnTextChanged( previousValue, currentValue );
    }

    protected override DateTime? ConvertTextToValue( string text )
    {
      if( string.IsNullOrEmpty( text ) )
        return null;

      this.TryParseDateTime( text, out var result );

      //Do not force "unspecified" to a time-zone specific
      //parsed text value. This would result in a lost of precision and
      //corrupt data. Let the value impose the Kind to the
      //WatermarkDateTimePicker. 
      if( this.Kind != DateTimeKind.Unspecified )
      {

        //Keep the current kind (Local or Utc) 
        //by imposing it to the parsed text value.
        //
        //Note: A parsed UTC text value may be
        //      adjusted with a Local kind and time.
        result = this.ConvertToKind( result, this.Kind );
      }

      if( this.ClipValueToMinMax )
      {
        return this.GetClippedMinMaxValue( result );
      }

      this.ValidateDefaultMinMax( result );

      return result;
    }

    protected override string ConvertValueToText()
    {
      if( Value == null )
        return string.Empty;

      return Value.Value.ToString( GetFormatString( Format ), CultureInfo );
    }

    protected override void SetValidSpinDirection()
    {
      ValidSpinDirections validDirections = ValidSpinDirections.None;

      if( !IsReadOnly )
      {
        if( this.IsLowerThan( this.Value, this.Maximum ) || !this.Value.HasValue || !this.Maximum.HasValue )
          validDirections = validDirections | ValidSpinDirections.Increase;

        if( this.IsGreaterThan( this.Value, this.Minimum ) || !this.Value.HasValue || !this.Minimum.HasValue )
          validDirections = validDirections | ValidSpinDirections.Decrease;
      }

      if( this.Spinner != null )
        this.Spinner.ValidSpinDirection = validDirections;
    }

    protected override object OnCoerceValue( object newValue )
    {
      //Since only changing the "kind" of a date
      //Ex. "2001-01-01 12:00 AM, Kind=Utc" to "2001-01-01 12:00 AM Kind=Local"
      //by setting the "Value" property won't trigger a property changed,
      //but will call this callback (coerce), we update the Kind here.
      DateTime? value = ( DateTime? )base.OnCoerceValue( newValue );

      //Let the initialized determine the final "kind" value.
      if(value != null && this.IsInitialized)
      {
        //Update kind based on value kind
        this.SetKindInternal( value.Value.Kind );
      }

      return value;
    }

    protected override void OnValueChanged( DateTime? oldValue, DateTime? newValue )
    {
      //this only occurs when the user manually type in a value for the Value Property
      DateTimeInfo info = (_selectedDateTimeInfo ?? ((this.CurrentDateTimePart != DateTimePart.Other) ? this.GetDateTimeInfo( this.CurrentDateTimePart ) : _dateTimeInfoList[ 0 ])) ??
                          _dateTimeInfoList[ 0 ];

      //whenever the value changes we need to parse out the value into out DateTimeInfo segments so we can keep track of the individual pieces
      //but only if it is not null
      if( newValue != null )
        ParseValueIntoDateTimeInfo( this.Value );

      base.OnValueChanged( oldValue, newValue );

      if( !_isTextChangedFromUI )
      {
        _lastValidDate = newValue;
      }

      if( TextBox != null )
      {
        //we loose our selection when the Value is set so we need to reselect it without firing the selection changed event
        _fireSelectionChangedEvent = false;
        TextBox.Select( info.StartPosition, info.Length );
        _fireSelectionChangedEvent = true;
      }
    }

    protected override bool IsCurrentValueValid()
    {
      if( string.IsNullOrEmpty( this.TextBox.Text ) )
        return true;

      return this.TryParseDateTime( this.TextBox.Text, out _ );
    }

    protected override void OnInitialized( EventArgs e )
    {
      base.OnInitialized( e );
      if( this.Value != null )
      {
        DateTimeKind valueKind = this.Value.Value.Kind;

        if( valueKind != this.Kind )
        {
          //Conflit between "Kind" property and the "Value.Kind" value.
          //Priority to the one that is not "Unspecified".
          if( this.Kind == DateTimeKind.Unspecified )
          {
            this.SetKindInternal( valueKind );
          }
          else
          {
            this.Value = this.ConvertToKind( this.Value.Value, this.Kind );
          }
        }
      }
    }

    protected override void PerformMouseSelection()
    {
      if( this.UpdateValueOnEnterKey )
      {
        this.ParseValueIntoDateTimeInfo( this.ConvertTextToValue( this.TextBox.Text ) );
      }
      base.PerformMouseSelection();
    }

    protected internal override void PerformKeyboardSelection( int nextSelectionStart )
    {
      if( this.UpdateValueOnEnterKey )
      {
        this.ParseValueIntoDateTimeInfo( this.ConvertTextToValue( this.TextBox.Text ) );
      }
      base.PerformKeyboardSelection( nextSelectionStart );
    }

    protected override void InitializeDateTimeInfoList( DateTime? value )
    {
      _dateTimeInfoList.Clear();
      _selectedDateTimeInfo = null;

      string format = GetFormatString( Format );

      if( string.IsNullOrEmpty( format ) )
        return;

      while( format.Length > 0 )
      {
        int elementLength = GetElementLengthByFormat( format );
        DateTimeInfo info = null;

        switch( format[ 0 ] )
        {
          case '"':
          case '\'':
            {
              int closingQuotePosition = format.IndexOf( format[ 0 ], 1 );
              info = new()
              {
                IsReadOnly = true,
                Type = DateTimePart.Other,
                Length = 1,
                Content = format.Substring( 1, Math.Max( 1, closingQuotePosition - 1 ) )
              };
              elementLength = Math.Max( 1, closingQuotePosition + 1 );
              break;
            }
          case 'D':
          case 'd':
            {
              string d = format.Substring( 0, elementLength );
              if( elementLength == 1 )
                d = "%" + d;

              if( elementLength > 2 )
                info = new()
                {
                  IsReadOnly = true,
                  Type = DateTimePart.DayName,
                  Length = elementLength,
                  Format = d
                };
              else
                info = new()
                {
                  IsReadOnly = false,
                  Type = DateTimePart.Day,
                  Length = elementLength,
                  Format = d
                };
              break;
            }
          case 'F':
          case 'f':
            {
              string f = format.Substring( 0, elementLength );
              if( elementLength == 1 )
                f = "%" + f;

              info = new()
              {
                IsReadOnly = false,
                Type = DateTimePart.Millisecond,
                Length = elementLength,
                Format = f
              };
              break;
            }
          case 'h':
            {
              string h = format.Substring( 0, elementLength );
              if( elementLength == 1 )
                h = "%" + h;

              info = new()
              {
                IsReadOnly = false,
                Type = DateTimePart.Hour12,
                Length = elementLength,
                Format = h
              };
              break;
            }
          case 'H':
            {
              string H = format.Substring( 0, elementLength );
              if( elementLength == 1 )
                H = "%" + H;

              info = new()
              {
                IsReadOnly = false,
                Type = DateTimePart.Hour24,
                Length = elementLength,
                Format = H
              };
              break;
            }
          case 'M':
            {
              string M = format.Substring( 0, elementLength );
              if( elementLength == 1 )
                M = "%" + M;

              if( elementLength >= 3 )
                info = new()
                {
                  IsReadOnly = false,
                  Type = DateTimePart.MonthName,
                  Length = elementLength,
                  Format = M
                };
              else
                info = new()
                {
                  IsReadOnly = false,
                  Type = DateTimePart.Month,
                  Length = elementLength,
                  Format = M
                };
              break;
            }
          case 'S':
          case 's':
            {
              string s = format.Substring( 0, elementLength );
              if( elementLength == 1 )
                s = "%" + s;

              info = new()
              {
                IsReadOnly = false,
                Type = DateTimePart.Second,
                Length = elementLength,
                Format = s
              };
              break;
            }
          case 'T':
          case 't':
            {
              string t = format.Substring( 0, elementLength );
              if( elementLength == 1 )
                t = "%" + t;

              info = new()
              {
                IsReadOnly = false,
                Type = DateTimePart.AmPmDesignator,
                Length = elementLength,
                Format = t
              };
              break;
            }
          case 'Y':
          case 'y':
            {
              string y = format.Substring( 0, elementLength );
              if( elementLength == 1 )
                y = "%" + y;

              info = new()
              {
                IsReadOnly = false,
                Type = DateTimePart.Year,
                Length = elementLength,
                Format = y
              };
              break;
            }
          case '\\':
            {
              if( format.Length >= 2 )
              {
                info = new()
                {
                  IsReadOnly = true,
                  Content = format.Substring( 1, 1 ),
                  Length = 1,
                  Type = DateTimePart.Other
                };
                elementLength = 2;
              }
              break;
            }
          case 'g':
            {
              string g = format.Substring( 0, elementLength );
              if( elementLength == 1 )
                g = "%" + g;

              info = new()
              {
                IsReadOnly = true,
                Type = DateTimePart.Period,
                Length = elementLength,
                Format = format.Substring( 0, elementLength )
              };
              break;
            }
          case 'm':
            {
              string m = format.Substring( 0, elementLength );
              if( elementLength == 1 )
                m = "%" + m;

              info = new()
              {
                IsReadOnly = false,
                Type = DateTimePart.Minute,
                Length = elementLength,
                Format = m
              };
              break;
            }
          case 'z':
            {
              string z = format.Substring( 0, elementLength );
              if( elementLength == 1 )
                z = "%" + z;

              info = new()
              {
                IsReadOnly = true,
                Type = DateTimePart.TimeZone,
                Length = elementLength,
                Format = z
              };
              break;
            }
          default:
            {
              elementLength = 1;
              info = new()
              {
                IsReadOnly = true,
                Length = 1,
                Content = format[ 0 ].ToString(),
                Type = DateTimePart.Other
              };
              break;
            }
        }

        _dateTimeInfoList.Add( info );
        format = format.Substring( elementLength );
      }
    }

    protected override bool IsLowerThan( DateTime? value1, DateTime? value2 )
    {
      if( value1 == null || value2 == null )
        return false;

      return (value1.Value < value2.Value);
    }

    protected override bool IsGreaterThan( DateTime? value1, DateTime? value2 )
    {
      if( value1 == null || value2 == null )
        return false;

      return (value1.Value > value2.Value);
    }

    protected override void OnUpdateValueOnEnterKeyChanged( bool oldValue, bool newValue )
    {
      throw new NotSupportedException( "DateTimeUpDown controls do not support modifying UpdateValueOnEnterKey property." );
    }

    protected override void OnKeyDown( KeyEventArgs e )
    {
      if( e.Key == Key.Escape )
      {
        this.SyncTextAndValueProperties( false, null );
        e.Handled = true;
      }

      base.OnKeyDown( e );
    }


#endregion //Base Class Overrides

    #region Methods

    public void SelectAll()
    {
      _fireSelectionChangedEvent = false;
      TextBox.SelectAll();
      _fireSelectionChangedEvent = true;
    }

    private void FormatUpdated()
    {
      InitializeDateTimeInfoList( this.Value );
      if( Value != null )
        ParseValueIntoDateTimeInfo( this.Value );

      // Update the Text representation of the value.
      _processTextChanged = false;

      this.SyncTextAndValueProperties( false, null );

      _processTextChanged = true;

    }

    private static int GetElementLengthByFormat( string format )
    {
      for( int i = 1; i < format.Length; i++ )
      {
        if( String.Compare( format[ i ].ToString(), format[ 0 ].ToString(), false ) != 0 )
        {
          return i;
        }
      }
      return format.Length;
    }

    private void Increment( int step )
    {
      _fireSelectionChangedEvent = false;

      var currentValue = this.ConvertTextToValue( this.TextBox.Text );
      if( currentValue.HasValue )
      {
        var newValue = this.UpdateDateTime( currentValue, step );
        if( newValue == null )
          return;
        this.TextBox.Text = newValue.Value.ToString( this.GetFormatString( this.Format ), this.CultureInfo );
      }
      else
      {
        this.TextBox.Text = ( this.DefaultValue != null )
                            ? this.DefaultValue.Value.ToString( this.GetFormatString( this.Format ), this.CultureInfo )
                            : this.ContextNow.ToString( this.GetFormatString( this.Format ), this.CultureInfo );
      }

      if( this.TextBox != null )
      {
        //this only occurs when the user manually type in a value for the Value Property
        DateTimeInfo info = (_selectedDateTimeInfo ?? (( this.CurrentDateTimePart != DateTimePart.Other ) 
                              ? this.GetDateTimeInfo( this.CurrentDateTimePart ) 
                              : _dateTimeInfoList[ 0 ])) ??
                            _dateTimeInfoList[ 0 ];

        //whenever the value changes we need to parse out the value into out DateTimeInfo segments so we can keep track of the individual pieces
        this.ParseValueIntoDateTimeInfo( this.ConvertTextToValue( this.TextBox.Text ) );

        //we loose our selection when the Value is set so we need to reselect it without firing the selection changed event
        this.TextBox.Select( info.StartPosition, info.Length );
      }
      _fireSelectionChangedEvent = true;

      this.SyncTextAndValueProperties( true, Text );
    }

    private void ParseValueIntoDateTimeInfo( DateTime? newDate )
    {
      string text = string.Empty;

      _dateTimeInfoList.ForEach( info =>
      {
        if( info.Format == null )
        {
          info.StartPosition = text.Length;
          info.Length = info.Content.Length;
          text += info.Content;
        }
        else if( newDate != null )
        {
          DateTime date = newDate.Value;
          info.StartPosition = text.Length;
          info.Content = date.ToString( info.Format, CultureInfo.DateTimeFormat );
          info.Length = info.Content.Length;
          text += info.Content;
        }
      } );
    }

    internal string GetFormatString( DateTimeFormat dateTimeFormat )
    {
      switch( dateTimeFormat )
      {
        case DateTimeFormat.ShortDate:
          return CultureInfo.DateTimeFormat.ShortDatePattern;
        case DateTimeFormat.LongDate:
          return CultureInfo.DateTimeFormat.LongDatePattern;
        case DateTimeFormat.ShortTime:
          return CultureInfo.DateTimeFormat.ShortTimePattern;
        case DateTimeFormat.LongTime:
          return CultureInfo.DateTimeFormat.LongTimePattern;
        case DateTimeFormat.FullDateTime:
          return CultureInfo.DateTimeFormat.FullDateTimePattern;
        case DateTimeFormat.MonthDay:
          return CultureInfo.DateTimeFormat.MonthDayPattern;
        case DateTimeFormat.RFC1123:
          return CultureInfo.DateTimeFormat.RFC1123Pattern;
        case DateTimeFormat.SortableDateTime:
          return CultureInfo.DateTimeFormat.SortableDateTimePattern;
        case DateTimeFormat.UniversalSortableDateTime:
          return CultureInfo.DateTimeFormat.UniversalSortableDateTimePattern;
        case DateTimeFormat.YearMonth:
          return CultureInfo.DateTimeFormat.YearMonthPattern;
        case DateTimeFormat.Custom:
          {
            switch( this.FormatString )
            {
              case "d":
                return CultureInfo.DateTimeFormat.ShortDatePattern;
              case "t":
                return CultureInfo.DateTimeFormat.ShortTimePattern;
              case "T":
                return CultureInfo.DateTimeFormat.LongTimePattern;
              case "D":
                return CultureInfo.DateTimeFormat.LongDatePattern;
              case "f":
                return CultureInfo.DateTimeFormat.LongDatePattern + " " + CultureInfo.DateTimeFormat.ShortTimePattern;
              case "F":
                return CultureInfo.DateTimeFormat.FullDateTimePattern;
              case "g":
                return CultureInfo.DateTimeFormat.ShortDatePattern + " " + CultureInfo.DateTimeFormat.ShortTimePattern;
              case "G":
                return CultureInfo.DateTimeFormat.ShortDatePattern + " " + CultureInfo.DateTimeFormat.LongTimePattern;
              case "m":
                return CultureInfo.DateTimeFormat.MonthDayPattern;
              case "y":
                return CultureInfo.DateTimeFormat.YearMonthPattern;
              case "r":
                return CultureInfo.DateTimeFormat.RFC1123Pattern;
              case "s":
                return CultureInfo.DateTimeFormat.SortableDateTimePattern;
              case "u":
                return CultureInfo.DateTimeFormat.UniversalSortableDateTimePattern;
              default:
                return FormatString;
            }
          }
        default:
          throw new ArgumentException( "Not a supported format" );
      }
    }

    private DateTime? UpdateDateTime(DateTime? currentDateTime, int value)
    {
      //this only occurs when the user manually type in a value for the Value Property
      DateTimeInfo info = (_selectedDateTimeInfo ?? ((this.CurrentDateTimePart != DateTimePart.Other)
                            ? this.GetDateTimeInfo(this.CurrentDateTimePart)
                            : _dateTimeInfoList[0])) ??
                          _dateTimeInfoList[0];

      DateTime? result = null;

      if (!currentDateTime.HasValue)
        return this.CoerceValueMinMax(null);

      try
      {
        DateTime dateTime = currentDateTime.Value;
        switch (info.Type)
        {
          case DateTimePart.Year:
            {
              result = dateTime.AddYears(value);
              break;
            }
          case DateTimePart.Month:
          case DateTimePart.MonthName:
            {
              result = dateTime.AddMonths(value);
              break;
            }
          case DateTimePart.Day:
          case DateTimePart.DayName:
            {
              result = dateTime.AddDays(value);
              break;
            }
          case DateTimePart.Hour12:
          case DateTimePart.Hour24:
            {
              result = dateTime.AddHours(value);
              break;
            }
          case DateTimePart.Minute:
            {
              result = dateTime.AddMinutes(value);
              break;
            }
          case DateTimePart.Second:
            {
              result = dateTime.AddSeconds(value);
              break;
            }
          case DateTimePart.Millisecond:
            {
              result = dateTime.AddMilliseconds(value);
              break;
            }
          case DateTimePart.AmPmDesignator:
            {
              result = dateTime.AddHours(value * 12);
              break;
            }
        }
      }
      catch
      {
        //this can occur if the date/time = 1/1/0001 12:00:00 AM which is the smallest date allowed.       
        //I could write code that would validate the date each and everytime but I think that it would be more
        //efficient if I just handle the edge case and allow an exeption to occur and swallow it instead.
      }

      return this.CoerceValueMinMax(result);
    }

    private bool TryParseDateTime( string text, out DateTime result )
    {
      bool isValid;
      result = this.ContextNow;

      DateTime current = this.ContextNow;
      try
      {
        // TempValue is used when Manipulating TextBox.Text while Value is not updated yet (used in WatermarkDateTimePicker's TimePicker).
        // ReSharper disable once SpecifyACultureInStringConversionExplicitly
        current = this.TempValue ?? (this.Value ?? DateTime.Parse( this.ContextNow.ToString(), this.CultureInfo.DateTimeFormat ));

        isValid = DateTimeParser.TryParse( text, this.GetFormatString( Format ), current, this.CultureInfo, this.AutoClipTimeParts, out result );
      }
      catch( FormatException )
      {
        isValid = false;
      }

      if( !isValid )
      {
        isValid = DateTime.TryParseExact( text, this.GetFormatString( this.Format ), this.CultureInfo, DateTimeStyles.None, out result );
      }

      if( !isValid )
      {
        result = _lastValidDate ?? current;
      }

      return isValid;
    }

    private DateTime ConvertToKind( DateTime dateTime, DateTimeKind kind )
    {
      //Same kind, just return same value.
      if( kind == dateTime.Kind )
        return dateTime;

      //"ToLocalTime()" from an unspecified will assume
      // That the time was originaly Utc and affect the datetime value. 
      // Just "Force" the "Kind" instead.
      if( dateTime.Kind == DateTimeKind.Unspecified 
        || kind == DateTimeKind.Unspecified )
        return DateTime.SpecifyKind( dateTime, kind );

      return ( kind == DateTimeKind.Local )
         ? dateTime.ToLocalTime()
         : dateTime.ToUniversalTime();
    }

    #endregion //Methods

    #region Event Handlers

    private void DateTimeUpDown_Loaded( object sender, RoutedEventArgs e )
    {
      if( ( this.Format == DateTimeFormat.Custom ) && ( string.IsNullOrEmpty( this.FormatString ) ) )
      {
        throw new InvalidOperationException( "A FormatString is necessary when Format is set to Custom." );
      }
    }

    #endregion
  }
}
