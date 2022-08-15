//using MetadataExtractor;
//using MetadataExtractor.Formats.Exif;
//using MetadataExtractor.Formats.Exif.Makernotes;
using System;
using System.Globalization;
//using MetadataDirectory = MetadataExtractor.Directory;
namespace DialogUpgradeFiles.Util
{
    public static class DateTimeHandler
    {

        #region Public Static Create DateTimeOffset
        /// <summary>
        /// Create a DateTimeOffset given a dateTime and timezoneinfo
        /// </summary>
        public static DateTimeOffset CreateDateTimeOffset(DateTime dateTime, TimeZoneInfo imageSetTimeZone)
        {
            if (imageSetTimeZone != null && dateTime.Kind == DateTimeKind.Unspecified)
            {
                TimeSpan utcOffset = imageSetTimeZone.GetUtcOffset(dateTime);
                return new DateTimeOffset(dateTime, utcOffset);
            }
            return new DateTimeOffset(dateTime);
        }

        /// <summary>
        /// Return a DateTimeOffset from its database string representation
        /// </summary>
        public static DateTimeOffset FromDatabaseDateTimeIncorporatingOffset(DateTime dateTime, TimeSpan utcOffset)
        {
            return new DateTimeOffset((dateTime + utcOffset).AsUnspecifed(), utcOffset);
        }
        #endregion

        #region TryParse on various DateTimes
        /// <summary>
        /// Parse a utcOffsetAsString from its double representation. Return false on failure or on reasonableness checks
        /// </summary>
        public static bool TryParseDatabaseUtcOffsetString(string utcOffsetAsString, out TimeSpan utcOffset)
        {
            if (double.TryParse(utcOffsetAsString, out double utcOffsetAsDouble))
            {
                utcOffset = TimeSpan.FromHours(utcOffsetAsDouble);
                return (utcOffset >= Constant.Time.MinimumUtcOffset) &&
                       (utcOffset <= Constant.Time.MaximumUtcOffset) &&
                       (utcOffset.Ticks % Constant.Time.UtcOffsetGranularity.Ticks == 0);
            }

            utcOffset = TimeSpan.Zero;
            return false;
        }

        /// <summary>
        /// Parse a legacy date and time string into a DateTimeOffset. Return false on failure
        /// </summary>
        public static bool TryParseLegacyDateTime(string date, string time, TimeZoneInfo imageSetTimeZone, out DateTimeOffset dateTimeOffset)
        {
            return DateTimeHandler.TryParseDateTaken(date + " " + time, imageSetTimeZone, out dateTimeOffset);
        }

        /// <summary>
        /// Parse a metadata-formatted date/time string exactly into a DateTimeOffset. 
        /// Note that this only accepts 'standard' date/time forms as described in Constant.Time.DateTimeMetadataFormat
        /// Return false on failure
        /// </summary>
        public static bool TryParseMetadataDateTaken(string dateTimeAsString, TimeZoneInfo imageSetTimeZone, out DateTimeOffset dateTimeOffset)
        {
            if (false == DateTime.TryParseExact(dateTimeAsString, Constant.Time.DateTimeMetadataFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime dateTime))
            {
                // Commented out section skips the general form, which can accept weird incomplete values that are not really useful e.g. 3.5
                //if (false == DateTime.TryParse(dateTimeAsString, out dateTime))
                //{
                dateTimeOffset = DateTimeOffset.MinValue;
                return false;
                //}
                //return false;
            }

            dateTimeOffset = DateTimeHandler.CreateDateTimeOffset(dateTime, imageSetTimeZone);
            return true;
        }

        /// <summary>
        /// Parse a metadata-formatted date/time string into a DateTime. 
        /// Note that this only accepts 'standard' date/time forms as described in Constant.Time.DateTimeMetadataFormats
        /// Return false on failure
        /// </summary>
        public static bool TryParseMetadataDateTaken(string dateTimeAsString, out DateTime dateTime)
        {
            // Commented out section skips the general form, which can accept weird incomplete values that are not really useful e.g. 3.5
            // Try the standard try parse first
            //if (DateTime.TryParse(dateTimeAsString, out dateTime))
            //{
            //    return true;
            //}

            // No luck with the standard TryParse.So lets try our specialized Metadata formats.
            return DateTime.TryParseExact(dateTimeAsString, Constant.Time.DateTimeMetadataFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out dateTime);
        }
        #endregion

        #region TrySwapDayMonth
        /// <summary>
        /// Swap the day and month of a DateTimeOffset if possible. Otherwise use the same DateTimeOffset. Return false if we cannot do the swap.
        /// </summary>
        public static bool TrySwapDayMonth(DateTimeOffset imageDate, out DateTimeOffset swappedDate)
        {
            swappedDate = DateTimeOffset.MinValue;
            if (imageDate.Day > Constant.Time.MonthsInYear)
            {
                return false;
            }
            swappedDate = new DateTimeOffset(imageDate.Year, imageDate.Day, imageDate.Month, imageDate.Hour, imageDate.Minute, imageDate.Second, imageDate.Millisecond, imageDate.Offset);
            return true;
        }
        #endregion

        #region Public Static Parse / Convert Forms to get DateTime
        /// <summary>
        /// Return a DateTime from its DataTime database string representation in the form of "yyyy-MM-ddTHH:mm:ss.fffZ"
        /// </summary>
        public static DateTime ParseDatabaseDateTimeString(string dateTimeAsString)
        {
            return DateTime.ParseExact(dateTimeAsString, Constant.Time.DateTimeDatabaseFormat, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal);
        }

        /// <summary>
        /// Parse a DateTime from its database format string representation "yyyy-MM-ddTHH:mm:ss.fffZ". Return false on failure
        /// </summary>
        public static bool TryParseDatabaseDateTime(string dateTimeAsString, out DateTime dateTime)
        {
            return DateTime.TryParseExact(dateTimeAsString, Constant.Time.DateTimeDatabaseFormat, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out dateTime);
        }

        /// <summary>
        /// Converts a display string to a DateTime of DateTimeKind.Unspecified.
        /// </summary>
        /// <param name="dateTimeAsString">string potentially containing a date time in display format</param>
        /// <param name="dateTime">the date time in the string, if any</param>
        /// <returns>true if string was in the date time display format, false otherwise</returns>
        public static bool TryParseDisplayDateTime(string dateTimeAsString, out DateTime dateTime)
        {
            return DateTime.TryParseExact(dateTimeAsString, Constant.Time.DateTimeDisplayFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out dateTime);
        }

        /// <summary>
        /// Parse a DateTime from its display format string representation "dd-MMM-yyyy HH:mm:ss". Return false on failure
        /// </summary>
        public static bool TryParseDisplayDateTimeString(string dateTimeAsString, out DateTime dateTime)
        {
            if (DateTime.TryParseExact(dateTimeAsString, Constant.Time.DateTimeDisplayFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out dateTime) == true)
            {
                return true;
            }
            else
            {
                dateTime = DateTime.MinValue;
                return false;
            };
        }

        /// <summary>
        /// Parse a Date from its display format string representation "dd-MMM-yyyy". Return false on failure
        /// </summary>
        public static bool TryParseDisplayDateOnlyString(string dateTimeAsString, out DateTime dateTime)
        {
            if (DateTime.TryParseExact(dateTimeAsString, Constant.Time.DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out dateTime) == true)
            {
                return true;
            }
            else
            {
                dateTime = DateTime.MinValue;
                return false;
            };
        }
        #endregion

        #region Public Static Parse to get TimeSpan
        /// <summary>
        /// Return a TimeSpan from its utcOfset string representation (hours) e.g., "-7.0"
        /// </summary>
        public static TimeSpan ParseDatabaseUtcOffsetString(string utcOffsetAsString)
        {
            // This used to fail when the culture allowed , decimal places. It should now be fixed. 
            // Although  we do throw an error if it doesn't work
            // TimeSpan utcOffset = TimeSpan.FromHours(double.Parse(utcOffsetAsString, CultureInfo.InvariantCulture));
            TimeSpan utcOffset;
            NumberStyles style = NumberStyles.Number | NumberStyles.AllowDecimalPoint;
            if (true == Double.TryParse(utcOffsetAsString, style, CultureInfo.InvariantCulture, out double utcOffsetDouble))
            {
                utcOffset = TimeSpan.FromHours(utcOffsetDouble);
            }
            else
            {
                throw new ArgumentOutOfRangeException(nameof(utcOffsetAsString), String.Format("UTC offset could not be parsed from {0}.", utcOffsetAsString));
            }
            //TimeSpan utcOffset = TimeSpan.FromHours();
            if ((utcOffset < Constant.Time.MinimumUtcOffset) ||
                (utcOffset > Constant.Time.MaximumUtcOffset))
            {
                throw new ArgumentOutOfRangeException(nameof(utcOffsetAsString), String.Format("UTC offset must be between {0} and {1}, inclusive.", DateTimeHandler.ToStringDatabaseUtcOffset(Constant.Time.MinimumUtcOffset), DateTimeHandler.ToStringDatabaseUtcOffset(Constant.Time.MinimumUtcOffset)));
            }
            if (utcOffset.Ticks % Constant.Time.UtcOffsetGranularity.Ticks != 0)
            {
                throw new ArgumentOutOfRangeException(nameof(utcOffsetAsString), String.Format("UTC offset must be an exact multiple of {0} ({1}).", DateTimeHandler.ToStringDatabaseUtcOffset(Constant.Time.UtcOffsetGranularity), DateTimeHandler.ToStringDisplayUtcOffset(Constant.Time.UtcOffsetGranularity)));
            }
            return utcOffset;
        }
        #endregion

        #region Try Reading DateTimeOriginalFromMetadata
        // There is an issue with Reconyx cameras in how it stores the original 'Date / ime' metadata tag's value
        // To get around this, we first try to get that tag's value using the standard method. If it doesn't work,
        // we try again using the Reconyx-specific method.
        //public static bool TryReadDateTimeOriginalFromMetadata(string filePath, out DateTime dateTime)
        //{
        //    dateTime = DateTime.MinValue;
        //    // Use only on images, as video files don't contain the desired metadata. 
        //    try
        //    {
        //        IReadOnlyList<MetadataDirectory> metadataDirectories = null;

        //        // Performance tweaks. Reading in sequential scan, does this speed up? Under the covers, the MetadataExtractor is using a sequential read, allowing skip forward but not random access.
        //        // Exif is small, do we need a big block?
        //        using (FileStream fS = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 64, FileOptions.SequentialScan))
        //        {
        //            metadataDirectories = ImageMetadataReader.ReadMetadata(fS);
        //        }

        //        ExifSubIfdDirectory exifSubIfd = metadataDirectories.OfType<ExifSubIfdDirectory>().FirstOrDefault();
        //        if (exifSubIfd == null)
        //        {

        //            return false;
        //        }

        //        if (exifSubIfd.TryGetDateTime(ExifSubIfdDirectory.TagDateTimeOriginal, out dateTime) == false)
        //        { 
        //            // We couldn't read the metadata. In case its a reconyx camera, the fallback is to use the Reconyx-specific metadata using its DateTimeOriginal tag
        //            ReconyxHyperFireMakernoteDirectory reconyxMakernote = metadataDirectories.OfType<ReconyxHyperFireMakernoteDirectory>().FirstOrDefault();
        //            if ((reconyxMakernote == null) || (reconyxMakernote.TryGetDateTime(ReconyxHyperFireMakernoteDirectory.TagDateTimeOriginal, out dateTime) == false))
        //            {
        //                return false;
        //            }
        //        }
        //        return true;
        //    }
        //    catch
        //    {
        //        return false;
        //    }
        //}
        #endregion

        #region Public Static GetNeutralTimeZone
        // Return a custom Neutral Time zone that has an offset of 0.0 and is always on standard time
        public static TimeZoneInfo GetNeutralTimeZone()
        {
            // We now ignore time zone. 
            TimeZoneInfo.AdjustmentRule[] adjustmentRules = new TimeZoneInfo.AdjustmentRule[0];
            return TimeZoneInfo.CreateCustomTimeZone(Constant.Time.NeutralTimeZone, new TimeSpan(0), Constant.Time.NeutralTimeZone, Constant.Time.NeutralTimeZone, Constant.Time.NeutralTimeZone, adjustmentRules, true);
        }
        #endregion

        #region Private Static Convert To String
        /// <summary>
        /// Return "yyyy-MM-ddTHH:mm:ss.fffZ" database string format of DateTimeOffset
        /// </summary>
        public static string ToStringDatabaseDateTime(DateTimeOffset dateTime)
        {
            return dateTime.UtcDateTime.ToString(Constant.Time.DateTimeDatabaseFormat, CultureInfo.CreateSpecificCulture("en-US"));
        }

        /// <summary>
        /// Return "yyyy-MM-dd HH:mm:ss" database string format of DateTimeOffset
        /// </summary>
        public static string ToStringDefaultDateTime(DateTime dateTime)
        {
            return dateTime.ToString(Constant.Time.DateTimeDatabaseFormat, CultureInfo.CreateSpecificCulture("en-US"));
        }

        /// <summary>
        /// Return "0.00" hours format of a timeSpan e.g. "11:30"
        /// </summary>
        public static string ToStringDatabaseUtcOffset(TimeSpan timeSpan)
        {
            // We use Invariant culture, as otherwise in some cultures (e.g., Spanish) a ',' is used to specify a decimal number rather than a '.'
            return timeSpan.TotalHours.ToString(Constant.Time.UtcOffsetDatabaseFormat, CultureInfo.InvariantCulture);
        }
        /// <summary>
        /// Return "dd-MMM-yyyy" format of a DateTimeOffset, e.g., 05-Apr-2016 
        /// </summary>
        public static string ToStringDisplayDate(DateTimeOffset date)
        {
            return date.DateTime.ToString(Constant.Time.DateFormat, CultureInfo.CreateSpecificCulture("en-US"));
        }

        /// <summary>
        /// Return "dd-MMM-yyyy HH:mm:ss" format of a DateTimeOffset  e.g. 05-Apr-2016 12:05:01
        /// </summary>
        public static string ToStringDisplayDateTime(DateTimeOffset dateTime)
        {
            return dateTime.DateTime.ToString(Constant.Time.DateTimeDisplayFormat, CultureInfo.CreateSpecificCulture("en-US"));
        }

        /// <summary>
        /// Return "dd-MMM-yyyy HH:mm:ss+hh:mm" format of a DateTimeOffset  e.g. 05-Apr-2016 12:05:01+5:00
        /// </summary>
        public static string ToStringDisplayDateTimeUtcOffset(DateTimeOffset dateTime)
        {
            return dateTime.DateTime.ToString(Constant.Time.DateTimeDisplayFormat, CultureInfo.CreateSpecificCulture("en-US")) + " " + DateTimeHandler.ToStringDisplayUtcOffset(dateTime.Offset);
        }

        /// <summary>
        /// Return "dd-MMM-yyyyTHH:mm:s" format for local DateTime in the CSV file  e.g. 05-Apr-2016T12:05:01
        /// </summary>
        public static string ToStringCSVDateTimeWithTSeparator(DateTimeOffset dateTime)
        {
            //return dateTime.LocalDateTime.ToString(Constant.Time.DateTimeCSVLocalDateTime, CultureInfo.CreateSpecificCulture("en-US"));
            return dateTime.ToString(Constant.Time.DateTimeCSVWithTSeparator, CultureInfo.CreateSpecificCulture("en-US"));
        }

        /// <summary>
        /// Return "dd-MMM-yyyy HH:mm:s" CSV format for DateTime Column e.g. 05-Apr-2016 12:05:01
        /// </summary>
        public static string ToStringCSVDateTimeWithoutTSeparator(DateTime dateTime)
        {
            return dateTime.ToString(Constant.Time.DateTimeCSVWithoutTSeparator, CultureInfo.CreateSpecificCulture("en-US"));
        }

        /// <summary>
        /// The dateTime should be in ZULU time (i.e., not in local time)
        /// Return "dd-MMM-yyyyTHH:mm:ssZ+hh:mm" format of a DateTimeOffset  e.g. 05-Apr-2016 12:05:01+5:00
        /// </summary>
        public static string ToStringCSVUtcWithOffset(DateTimeOffset dateTime, TimeSpan offset)
        {
            string offsetAsString = String.Format("{0:+00;-00}:{1:00}", offset.Hours, offset.Minutes);
            return String.Format("{0}Z{1}",
                dateTime.UtcDateTime.ToString(Constant.Time.DateTimeCSVWithTSeparator, CultureInfo.CreateSpecificCulture("en-US")),
                offsetAsString);
        }

        /// <summary>
        /// Return display format of a TimeSpan
        /// </summary>
        public static string ToStringDisplayTimeSpan(TimeSpan timeSpan)
        {
            // Pretty print the adjustment time, depending upon how many day(s) were included 
            string sign = (timeSpan < TimeSpan.Zero) ? "-" : null;
            string timeSpanAsString = sign + timeSpan.ToString(Constant.Time.TimeSpanDisplayFormat);

            TimeSpan duration = timeSpan.Duration();
            if (duration.Days == 0)
            {
                return timeSpanAsString;
            }
            if (duration.Days == 1)
            {
                return sign + "1 day " + timeSpanAsString;
            }

            return sign + duration.Days.ToString("D") + " days " + timeSpanAsString;
        }

        /// <summary>
        /// Return "HH:mm:ss" in 24 hour format given a DateTimeOffset
        /// </summary>
        public static string ToStringDisplayTime(DateTimeOffset time)
        {
            return time.DateTime.ToString(Constant.Time.TimeFormat, CultureInfo.CreateSpecificCulture("en-US"));
        }

        /// <summary>
        /// Return "+hh\:mm" given a TimeSpan
        /// </summary>
        public static string ToStringDisplayUtcOffset(TimeSpan utcOffset)
        {
            string displayString = utcOffset.ToString(Constant.Time.UtcOffsetDisplayFormat, CultureInfo.InvariantCulture);
            if (utcOffset < TimeSpan.Zero)
            {
                displayString = "-" + displayString;
            }
            return displayString;
        }
        #endregion

        #region Private Methods
        private static bool TryParseDateTaken(string dateTimeAsString, TimeZoneInfo imageSetTimeZone, out DateTimeOffset dateTimeOffset)
        {
            // use current culture as BitmapMetadata.DateTaken is not invariant
            if (DateTime.TryParse(dateTimeAsString, CultureInfo.CurrentCulture, DateTimeStyles.None, out DateTime dateTime) == false)
            {
                dateTimeOffset = DateTimeOffset.MinValue;
                return false;
            }

            dateTimeOffset = DateTimeHandler.CreateDateTimeOffset(dateTime, imageSetTimeZone);
            return true;
        }
        #endregion
    }
}
