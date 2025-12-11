using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using MetadataExtractor;
using MetadataExtractor.Formats.Exif;
using MetadataExtractor.Formats.Exif.Makernotes;
using Timelapse.Constant;
using MetadataDirectory = MetadataExtractor.Directory;
namespace Timelapse.Util
{
    public static class DateTimeHandler
    {
        // Various static methods for
        // - parsing dateTime from string, 
        // - converting string to dateTime
        // - manipulating datetime (e.g., swapping the day/month

        #region Static TryParse from string to get DateTime
        // TODO: Replace these with a single call, where we create an ENUM as an argument (e.g. DateTimeFormat.DatabaseDateTime)
        // All these forms returm a valid dateTime, even if the try's fail (just in case)
        public static bool TryParseDatabaseOrDisplayDateTime(string dateTimeAsString, out DateTime dateTime)
        {
            // Try the Database format
            if (false == TryParseDatabaseDateTime(dateTimeAsString, out DateTime dateTimeDatabase))
            {
                // Try the Display format
                if (false == TryParseDisplayDateTime(dateTimeAsString, out DateTime dateTimeDisplay))
                {
                    // Can't parse the DateTime, so return a minimum value
                    dateTime = DateTime.MinValue;
                    return false;
                }
                // We parsed it as a Display format, so return that date
                dateTime = dateTimeDisplay;
                return true;
            }
            // We parsed it as a Database format, so return that date
            dateTime = dateTimeDatabase;
            return true;
        }

        public static bool TryParseDatabaseDateTime(string dateTimeAsString, out DateTime dateTime)
        {
            // Parse from yyyy-MM-dd HH:mm:ss | 2021-04-05 18:05:01
            if (false == DateTime.TryParseExact(dateTimeAsString, Time.DateTimeDatabaseFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out dateTime))
            {
                dateTime = DateTime.MinValue;
                return false;
            }
            return true;
        }

        public static bool TryParseDatabaseOrDisplayDate(string dateTimeAsString, out DateTime dateTime)
        {
            // Try the Database format
            if (false == TryParseDatabaseDate(dateTimeAsString, out DateTime dateTimeDatabase))
            {
                // Try the Display format
                if (false == TryParseDisplayDate(dateTimeAsString, out DateTime dateTimeDisplay))
                {
                    // Can't parse the DateTime, so return a minimum value
                    dateTime = DateTime.MinValue;
                    return false;
                }
                // We parsed it as a Display format, so return that date
                dateTime = dateTimeDisplay;
                return true;
            }
            // We parsed it as a Database format, so return that date
            dateTime = dateTimeDatabase;
            return true;
        }


        public static bool TryParseDatabaseDate(string dateAsString, out DateTime dateTime)
        {
            // Parse from yyyy-MM-dd | 2021-04-05
            if (false == DateTime.TryParseExact(dateAsString, Time.DateDatabaseFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out dateTime))
            {
                dateTime = DateTime.MinValue;
                return false;
            }
            return true;
        }

        public static bool TryParseDatabaseTime(string timeAsString, out DateTime dateTime)
        {
            // Parse from HH:mm:ss | 18:05:01
            if (false == DateTime.TryParseExact(timeAsString, Time.TimeFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out dateTime))
            {
                dateTime = DateTime.MinValue;
                return false;
            }
            return true;
        }

        public static bool TryParseDisplayDateTime(string dateTimeAsString, out DateTime dateTime)
        {
            // Parse from dd-MMM-yyyy HH:mm:ss | 05-Apr-2021 18:05:01
            if (false == DateTime.TryParseExact(dateTimeAsString, Time.DateTimeDisplayFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out dateTime))
            {
                dateTime = DateTime.MinValue;
                return false;
            }
            return true;
        }

        /// <summary>
        /// Parse a Date from its display format string representation "dd-MMM-yyyy". Return false on failure
        /// </summary>
        // ReSharper disable once UnusedMember.Global
        public static bool TryParseDisplayDate(string dateTimeAsString, out DateTime dateTime)
        {
            if (DateTime.TryParseExact(dateTimeAsString, Time.DateDisplayFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out dateTime))
            {
                return true;
            }

            dateTime = DateTime.MinValue;
            return false;
        }

        public static bool TryParseDateTimeDatabaseAndDisplayFormats(string dateTimeAsString, out DateTime dateTime)
        {
            // Various possible (complete) date/time formats
            if (false == DateTime.TryParseExact(dateTimeAsString, Time.DateTimeDatabaseAndDisplayFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out dateTime))
            {
                dateTime = DateTime.MinValue;
                return false;
            }
            return true;
        }

        public static bool TryParseDateDatabaseAndDisplayFormats(string dateTimeAsString, out DateTime dateTime)
        {
            // Various possible (complete) date/time formats
            if (false == DateTime.TryParseExact(dateTimeAsString, Time.DateDatabaseAndDisplayFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out dateTime))
            {
                dateTime = DateTime.MinValue;
                return false;
            }
            return true;
        }

        public static bool TryParseMetadataDateTaken(string dateTimeAsString, out DateTime dateTime)
        {
            // Various possible (complete) date/time formats
            if (false == DateTime.TryParseExact(dateTimeAsString, Time.DateTimeMetadataFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out dateTime))
            {
                dateTime = DateTime.MinValue;
                return false;
            }
            return true;
        }
        #endregion

        #region Static convert string date format to another string date format

        public static string DateTimeDatabaseStringToDisplayString(string databaseString)
        {
            return TryParseDatabaseDateTime(databaseString, out DateTime dateTime)
                ? ToStringDisplayDateTime(dateTime)
                : string.Empty;
        }

        public static string DateTimeDisplayStringToDataBaseString(string displayString)
        {
            return TryParseDisplayDateTime(displayString, out DateTime dateTime)
                ? ToStringDatabaseDateTime(dateTime)
                : string.Empty;
        }

        public static string DateDisplayStringToDataBaseString(string displayString)
        {
            return TryParseDisplayDate(displayString, out DateTime dateTime)
                ? ToStringDatabaseDate(dateTime)
                : string.Empty;
        }

        public static string DateDatabaseStringToDisplayString(string databaseString)
        {
            return TryParseDatabaseDate(databaseString, out DateTime dateTime)
                ? ToStringDisplayDatePortion(dateTime)
                : string.Empty;
        }

        public static string TimeDatabaseStringToDisplayString(string databaseString)
        {
            return TryParseDatabaseTime(databaseString, out DateTime dateTime)
                ? ToStringTime(dateTime)
                : string.Empty;
        }


        #endregion

        #region Static ToString DateTime
        // All examples are April 5, 2021 18:05:01
        public static string ToStringDatabaseDateTime(DateTime dateTime)
        {
            // yyyy-MM-dd HH:mm:ss | 2021-04-05 18:05:01
            return dateTime.ToString(Time.DateTimeDatabaseFormat, CultureInfo.CreateSpecificCulture("en-US"));
        }

        public static string ToStringDatabaseDate(DateTime dateTime, bool insertSpaceBefore = false)
        {
            // yyyy-MM-dd | 2021-04-05
            string prefix = insertSpaceBefore ? " " : string.Empty;
            return $"{prefix}{dateTime.ToString(Time.DateDatabaseFormat, CultureInfo.CreateSpecificCulture("en-US"))}";
        }

        public static string ToStringDisplayDateTime(DateTime dateTime, bool insertSpaceBefore = false)
        {
            // dd-MMM-yyyy HH:mm:ss | 05-Apr-2021 18:05:01
            string prefix = insertSpaceBefore ? " " : string.Empty;
            return $"{prefix}{dateTime.ToString(Time.DateTimeDisplayFormat, CultureInfo.CreateSpecificCulture("en-US"))}";
        }

        public static string ToStringDisplayDatePortion(DateTime date, bool insertSpaceBefore = false)
        {
            // dd-MMM-yyyy | 05-Apr-2021
            string prefix = insertSpaceBefore ? " " : string.Empty;
            return $"{prefix}{date.ToString(Time.DateDisplayFormat, CultureInfo.CreateSpecificCulture("en-US"))}";
        }
        public static string ToStringTime(DateTime dateTime, bool insertSpaceBefore = false)
        {
            // HH:mm:ss | 18:05:01
            string prefix = insertSpaceBefore ? " " : string.Empty;
            return $"{prefix}{dateTime.ToString(Time.TimeFormat, CultureInfo.CreateSpecificCulture("en-US"))}";
        }

        public static string ToStringCSVDateTimeWithTSeparator(DateTime dateTime, bool insertSpaceBefore = false)
        {
            //  yyyy-MM-dd'T'HH:mm:ss | 2021-04-05'T'18:05:01
            string prefix = insertSpaceBefore ? " " : string.Empty;
            return $"{prefix}{dateTime.ToString(Time.DateTimeCSVWithTSeparator, CultureInfo.CreateSpecificCulture("en-US"))}";
        }

        public static string ToStringCSVDateTimeWithoutTSeparator(DateTime dateTime, bool insertSpaceBefore = false)
        {
            //  yyyy-MM-dd' 'HH:mm:ss' | 2021-04-05' '18:05:01
            string prefix = insertSpaceBefore ? " " : string.Empty;
            return $"{prefix}{dateTime.ToString(Time.DateTimeCSVWithoutTSeparator, CultureInfo.CreateSpecificCulture("en-US"))}";
        }
        #endregion

        #region Static Try Reading DateTimeOriginalFromMetadata
        // CURRENTLY UNUSED
        // There is an issue with Reconyx cameras in how it stores the original 'Date / Time' metadata tag's value
        // To get around this, we first try to get that tag's value using the standard method. If it doesn't work,
        // we try again using the Reconyx-specific method.
        // ReSharper disable once UnusedMember.Global
        public static bool TryReadDateTimeOriginalFromMetadata(string filePath, out DateTime dateTime)
        {
            dateTime = DateTime.MinValue;
            // Use only on images, as video files don't contain the desired metadata. 
            try
            {
                IReadOnlyList<MetadataDirectory> metadataDirectories;

                // Performance tweaks. Reading in sequential scan, does this speed up? Under the covers, the MetadataExtractor is using a sequential read, allowing skip forward but not random access.
                // Exif is small, do we need a big block?
                using (FileStream fS = new(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 64, FileOptions.SequentialScan))
                {
                    metadataDirectories = ImageMetadataReader.ReadMetadata(fS);
                }

                ExifSubIfdDirectory exifSubIfd = metadataDirectories.OfType<ExifSubIfdDirectory>().FirstOrDefault();
                if (exifSubIfd == null)
                {

                    return false;
                }

                if (exifSubIfd.TryGetDateTime(ExifDirectoryBase.TagDateTimeOriginal, out dateTime) == false)
                {
                    // We couldn't read the metadata. In case its a reconyx camera, the fallback is to use the Reconyx-specific metadata using its DateTimeOriginal tag
                    ReconyxHyperFireMakernoteDirectory reconyxMakernote = metadataDirectories.OfType<ReconyxHyperFireMakernoteDirectory>().FirstOrDefault();
                    if ((reconyxMakernote == null) || (reconyxMakernote.TryGetDateTime(ReconyxHyperFireMakernoteDirectory.TagDateTimeOriginal, out dateTime) == false))
                    {
                        return false;
                    }
                }
                return true;
            }
            catch
            {
                return false;
            }
        }
        #endregion

        #region Static TrySwapDayMonth
        /// <summary>
        /// Swap the day and month of a DateTime if possible. Otherwise use the same DateTime. Return false if we cannot do the swap.
        /// </summary>
        public static bool TrySwapDayMonth(DateTime imageDate, out DateTime swappedDate)
        {
            swappedDate = DateTime.MinValue;
            if (imageDate.Day > Time.MonthsInYear)
            {
                return false;
            }
            swappedDate = new(imageDate.Year, imageDate.Day, imageDate.Month, imageDate.Hour, imageDate.Minute, imageDate.Second, imageDate.Millisecond);
            return true;
        }
        #endregion
    }
}
