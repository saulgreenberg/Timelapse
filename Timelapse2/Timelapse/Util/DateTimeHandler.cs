using MetadataExtractor;
using MetadataExtractor.Formats.Exif;
using MetadataExtractor.Formats.Exif.Makernotes;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
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
        // TODO: DT We can combine these two simply by using constant that has both formats

        // All these forms returm a valid dateTime, even if the try's fail (just in case)
        public static bool TryParseDatabaseDateTime(string dateTimeAsString, out DateTime dateTime)
        {
            // Parse from yyyy-MM-dd HH:mm:ss | 2021-04-05 18:05:01
            if (false == DateTime.TryParseExact(dateTimeAsString, Constant.Time.DateTimeDatabaseFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out dateTime))
            {
                dateTime = DateTime.MinValue;
                return false;
            }
            return true;
        }

        public static bool TryParseDisplayDateTime(string dateTimeAsString, out DateTime dateTime)
        {
            // Parse from dd-MMM-yyyy HH:mm:ss | 05-Apr-2021 18:05:01
            if (false == DateTime.TryParseExact(dateTimeAsString, Constant.Time.DateTimeDisplayFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out dateTime))
            {
                dateTime = DateTime.MinValue;
                return false;
            }
            return true;
        }

        /// <summary>
        /// Parse a Date from its display format string representation "dd-MMM-yyyy". Return false on failure
        /// </summary>
        public static bool TryParseDisplayDateOnlyString(string dateTimeAsString, out DateTime dateTime)
        {
            if (DateTime.TryParseExact(dateTimeAsString, Constant.Time.DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out dateTime))
            {
                return true;
            }
            else
            {
                dateTime = DateTime.MinValue;
                return false;
            }
        }

        public static bool TryParseMetadataDateTaken(string dateTimeAsString, out DateTime dateTime)
        {
            // Various possible (complete) date/time formats
            if (false == DateTime.TryParseExact(dateTimeAsString, Constant.Time.DateTimeMetadataFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out dateTime))
            {
                dateTime = DateTime.MinValue;
                return false;
            }
            return true;
        }
        #endregion

        #region Static ToString DateTime
        // All examples are April 5, 2021 18:05:01
        public static string ToStringDatabaseDateTime(DateTime dateTime)
        {
            // yyyy-MM-dd HH:mm:ss | 2021-04-05 18:05:01
            return dateTime.ToString(Constant.Time.DateTimeDatabaseFormat, CultureInfo.CreateSpecificCulture("en-US"));
        }
        public static string ToStringDisplayDateTime(DateTime dateTime)
        {
            // dd-MMM-yyyy HH:mm:ss | 05-Apr-2021 18:05:01
            return dateTime.ToString(Constant.Time.DateTimeDisplayFormat, CultureInfo.CreateSpecificCulture("en-US"));
        }

        public static string ToStringDisplayDatePortion(DateTime date)
        {
            // dd-MMM-yyyy | 05-Apr-2021
            return date.ToString(Constant.Time.DateFormat, CultureInfo.CreateSpecificCulture("en-US"));
        }
        public static string ToStringDisplayTimePortion(DateTime dateTime)
        {
            // HH:mm:ss | 18:05:01
            return dateTime.ToString(Constant.Time.TimeFormat, CultureInfo.CreateSpecificCulture("en-US"));
        }

        public static string ToStringCSVDateTimeWithTSeparator(DateTime dateTime)
        {
            //  yyyy-MM-dd'T'HH:mm:ss | 2021-04-05'T'18:05:01
            return dateTime.ToString(Constant.Time.DateTimeCSVWithTSeparator, CultureInfo.CreateSpecificCulture("en-US"));
        }

        public static string ToStringCSVDateTimeWithoutTSeparator(DateTime dateTime)
        {
            //  yyyy-MM-dd' 'HH:mm:ss' | 2021-04-05' '18:05:01
            return dateTime.ToString(Constant.Time.DateTimeCSVWithoutTSeparator, CultureInfo.CreateSpecificCulture("en-US"));
        }
        #endregion

        #region Static Try Reading DateTimeOriginalFromMetadata
        // CURRENTLY UNUSED
        // There is an issue with Reconyx cameras in how it stores the original 'Date / ime' metadata tag's value
        // To get around this, we first try to get that tag's value using the standard method. If it doesn't work,
        // we try again using the Reconyx-specific method.
        public static bool TryReadDateTimeOriginalFromMetadata(string filePath, out DateTime dateTime)
        {
            dateTime = DateTime.MinValue;
            // Use only on images, as video files don't contain the desired metadata. 
            try
            {
                IReadOnlyList<MetadataDirectory> metadataDirectories = null;

                // Performance tweaks. Reading in sequential scan, does this speed up? Under the covers, the MetadataExtractor is using a sequential read, allowing skip forward but not random access.
                // Exif is small, do we need a big block?
                using (FileStream fS = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 64, FileOptions.SequentialScan))
                {
                    metadataDirectories = ImageMetadataReader.ReadMetadata(fS);
                }

                ExifSubIfdDirectory exifSubIfd = metadataDirectories.OfType<ExifSubIfdDirectory>().FirstOrDefault();
                if (exifSubIfd == null)
                {

                    return false;
                }

                if (exifSubIfd.TryGetDateTime(ExifSubIfdDirectory.TagDateTimeOriginal, out dateTime) == false)
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
            if (imageDate.Day > Constant.Time.MonthsInYear)
            {
                return false;
            }
            swappedDate = new DateTime(imageDate.Year, imageDate.Day, imageDate.Month, imageDate.Hour, imageDate.Minute, imageDate.Second, imageDate.Millisecond);
            return true;
        }
        #endregion
    }
}
