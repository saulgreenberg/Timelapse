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
using System.Globalization;
using System.Linq;

namespace TimelapseWpf.Toolkit
{
  internal class DateTimeParser
  {
    public static bool TryParse( string value, string format, DateTime currentDate, CultureInfo cultureInfo, bool autoClipTimeParts, out DateTime result )
    {
      bool success = false;
      result = currentDate;

      if( string.IsNullOrEmpty( value ) || string.IsNullOrEmpty( format ) )
        return false;

      DateTimeParser.UpdateValueFormatForQuotes( ref value, ref format );      

      var dateTimeString = ComputeDateTimeString( value, format, currentDate, cultureInfo, autoClipTimeParts ).Trim();

      if( !String.IsNullOrEmpty( dateTimeString ) )
        success = DateTime.TryParse( dateTimeString, cultureInfo.DateTimeFormat, DateTimeStyles.None, out result );

      if( !success )
        result = currentDate;

      return success;
    }

    private static void UpdateValueFormatForQuotes( ref string value, ref string format )
    {
      // ReSharper disable once StringIndexOfIsCultureSpecific.1
      var quoteStart = format.IndexOf( "'" );
      if( quoteStart > -1 )
      {
        // ReSharper disable once StringIndexOfIsCultureSpecific.2
        var quoteEnd = format.IndexOf( "'", quoteStart + 1 );
        if( quoteEnd > -1 )
        {
          var quoteContent = format.Substring( quoteStart + 1, quoteEnd - quoteStart - 1 );
          value = value.Replace( quoteContent, "" );
          format = format.Remove( quoteStart, quoteEnd - quoteStart + 1 );

          // Use recursive calls for many quote text. 
          DateTimeParser.UpdateValueFormatForQuotes( ref value, ref format );
        }
      }
    }

    private static string ComputeDateTimeString( string dateTime, string format, DateTime currentDate, CultureInfo cultureInfo, bool autoClipTimeParts )
    {
      Dictionary<string, string> dateParts = GetDateParts( currentDate, cultureInfo );
      string[] timeParts = [currentDate.Hour.ToString(), currentDate.Minute.ToString(), currentDate.Second.ToString()];
      string millisecondsPart = currentDate.Millisecond.ToString();
      string designator = "";
      string[] dateTimeSeparators = [",", " ", "-", ".", "/", cultureInfo.DateTimeFormat.DateSeparator, cultureInfo.DateTimeFormat.TimeSeparator];
      var forcePMDesignator = false;

      UpdateSortableDateTimeString( ref dateTime, ref format, cultureInfo );

      var dateTimeParts = new List<string>();
      var formats = new List<string>();
      var isContainingDateTimeSeparators = dateTimeSeparators.Any( s => dateTime.Contains( s ) );
      if( isContainingDateTimeSeparators )
      {
        dateTimeParts = dateTime.Split( dateTimeSeparators, StringSplitOptions.RemoveEmptyEntries ).ToList();
        formats = format.Split( dateTimeSeparators, StringSplitOptions.RemoveEmptyEntries ).ToList();
      }
      else
      {
        string currentformat = "";
        string currentString = "";
        var formatArray = format.ToCharArray();
        for( int i = 0; i < formatArray.Length; ++i )
        {
          var c = formatArray[ i ];
          if( !currentformat.Contains( c ) )
          {
            if( !string.IsNullOrEmpty( currentformat ) )
            {
              formats.Add( currentformat );
              dateTimeParts.Add( currentString );
            }
            currentformat = c.ToString();
            currentString = (i < dateTime.Length) ? dateTime[ i ].ToString() : "";
          }
          else
          {
            currentformat = string.Concat( currentformat, c );
            currentString = string.Concat( currentString, (i < dateTime.Length) ? dateTime[ i ] : '\0' );
          }
        }
        if( !string.IsNullOrEmpty( currentformat ) )
        {
          formats.Add( currentformat );
          dateTimeParts.Add( currentString );
        }
      }

      //Auto-complete missing date parts
      if( dateTimeParts.Count < formats.Count )
      {
        while( dateTimeParts.Count != formats.Count  )
        {
          dateTimeParts.Add( "0" );
        }
      }

      //something went wrong
      if( dateTimeParts.Count != formats.Count )
        return string.Empty;

      for( int i = 0; i < formats.Count; i++ )
      {
        var f = formats[ i ];
        if( !f.Contains( "ddd" ) && !f.Contains( "GMT" ) )
        {
          if( f.Contains( 'M' ) )
            dateParts[ "Month" ] = dateTimeParts[ i ];
          else if( f.Contains('d') )
            dateParts[ "Day" ] = dateTimeParts[ i ];
          else if( f.Contains('y') )
          {
            dateParts[ "Year" ] = dateTimeParts[ i ] != "0" ? dateTimeParts[ i ] : "0000";

            if( dateParts[ "Year" ].Length == 2 )
            {
              var yearDigits = int.Parse( dateParts[ "Year" ] );
              var twoDigitYearMax = cultureInfo.Calendar.TwoDigitYearMax;
              var hundredDigits = ( yearDigits <= twoDigitYearMax % 100 ) ? twoDigitYearMax / 100 : ( twoDigitYearMax / 100 ) - 1;

              dateParts[ "Year" ] = $"{hundredDigits}{dateParts["Year"]}";
            }
          }
          else if( f.Contains( "hh" ) || f.Contains( "HH" ) )
          {
            var hourValue = Convert.ToInt32( dateTimeParts[ i ] ) % 24;
            timeParts[ 0 ] = autoClipTimeParts ? hourValue.ToString() : dateTimeParts[ i ];
          }
          else if( f.Contains( "h" ) || f.Contains('H') )
          {
            if( autoClipTimeParts )
            {
              var hourValue = Convert.ToInt32( dateTimeParts[ i ] ) % 24;
              if( hourValue > 11 )
              {
                hourValue -= 12;
                forcePMDesignator = true;
              }
              timeParts[ 0 ] = hourValue.ToString();
            }
            else
            {
              timeParts[ 0 ] = dateTimeParts[ i ];
            }
          }
          else if( f.Contains('m') )
          {
            var minuteValue = Convert.ToInt32( dateTimeParts[ i ] ) % 60;
            timeParts[ 1 ] = autoClipTimeParts ? minuteValue.ToString() : dateTimeParts[ i ];
          }
          else if( f.Contains('s') )
          {
            var secondValue = Convert.ToInt32( dateTimeParts[ i ] ) % 60;
            timeParts[ 2 ] = autoClipTimeParts ? secondValue.ToString() : dateTimeParts[ i ];
          }
          else if( f.Contains('f') )
            millisecondsPart = dateTimeParts[ i ];
          else if( f.Contains('t') )
            designator = forcePMDesignator ? "PM" : dateTimeParts[ i ];
        }
      }

      var date = string.Join( cultureInfo.DateTimeFormat.DateSeparator, dateParts.Select( x => x.Value ).ToArray() );
      var time = string.Join( cultureInfo.DateTimeFormat.TimeSeparator, timeParts );
      time += "." + millisecondsPart; 

      return $"{date} {time} {designator}";
    }

    private static void UpdateSortableDateTimeString( ref string dateTime, ref string format, CultureInfo cultureInfo )
    {
      if( format == cultureInfo.DateTimeFormat.SortableDateTimePattern )
      {
        format = format.Replace( "'", "" ).Replace( "T", " " );
        dateTime = dateTime.Replace( "'", "" ).Replace( "T", " " );
      }
      else if( format == cultureInfo.DateTimeFormat.UniversalSortableDateTimePattern )
      {
        format = format.Replace( "'", "" ).Replace( "Z", "" );
        dateTime = dateTime.Replace( "'", "" ).Replace( "Z", "" );
      }
    }

    private static Dictionary<string, string> GetDateParts( DateTime currentDate, CultureInfo cultureInfo )
    {
      Dictionary<string, string> dateParts = new();
      var dateTimeSeparators = new[] { ",", " ", "-", ".", "/", cultureInfo.DateTimeFormat.DateSeparator, cultureInfo.DateTimeFormat.TimeSeparator };
      var dateFormatParts = cultureInfo.DateTimeFormat.ShortDatePattern.Split( dateTimeSeparators, StringSplitOptions.RemoveEmptyEntries ).ToList();
      dateFormatParts.ForEach( item =>
      {
        string key = string.Empty;
        string value = string.Empty;

        if( item.Contains('M') )
        {
          key = "Month";
          value = currentDate.Month.ToString();
        }
        else if( item.Contains('d') )
        {
          key = "Day";
          value = currentDate.Day.ToString();
        }
        else if( item.Contains('y') )
        {
          key = "Year";
          value = currentDate.Year.ToString("D4");
        }
        dateParts.TryAdd(key, value);
      } );
      return dateParts;
    }
  }
}
