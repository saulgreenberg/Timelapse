using System;
using System.Data;
using System.Diagnostics;
using Timelapse.Constant;
using Timelapse.Util;

namespace Timelapse.Extensions
{
    /// <summary>
    /// Various methods to get / set data row fields by type
    /// </summary>
    public static class DataRowExtensions
    {
        #region Public Methods - Various Gets by type
        public static bool GetBooleanField(this DataRow row, string column)
        {
            string fieldAsString = row.GetStringField(column);
            if (fieldAsString == null)
            {
                return false;
            }
            return string.Equals(Boolean.TrueString, fieldAsString, StringComparison.OrdinalIgnoreCase);
        }

        public static DateTime GetDateTimeField(this DataRow row, string column)
        {
            // Check the arguments for null. 
            ThrowIf.IsNullArgument(row, nameof(row));
            try
            {
                return (DateTime)row[column];
            }
            catch
            {
                // If for some reason we have an invalid date time (e.g., a null entry), always return a valid but improbable date (Jan 1 1900 midnight).
                Debug.Print("GetDateTimeField: Unexpected kind for date time in row with ID " + row.GetID());
                return new(1900, 1, 1, 12, 0, 0, 0);
            }
        }

        // ReSharper disable once UnusedMember.Global
        public static TEnum GetEnumField<TEnum>(this DataRow row, string column) where TEnum : struct, IComparable, IFormattable, IConvertible
        {
            string fieldAsString = row.GetStringField(column);
            if (string.IsNullOrEmpty(fieldAsString))
            {
                // This should not happen
                return default;
            }

            // WHile the code below returns the same result value, it is left as is to help future debugging, if needed.
            if (Enum.TryParse(fieldAsString, out TEnum result))
            {
                // The parse succeeded, where the TEnum result is in result
                return result;
            }
            // The parse did not succeeded. The TEnum result contains the default enum value, ie, the same as returning default(TEnum)
            return result;
            
        }

        public static long GetID(this DataRow row)
        {
            return row.GetLongField(DatabaseColumn.ID);
        }

        public static int GetIntegerField(this DataRow row, string column)
        {
            string fieldAsString = row.GetStringField(column);
            if (fieldAsString == null)
            {
                return -1;
            }
            return Int32.Parse(fieldAsString);
        }
        public static long GetLongStringField(this DataRow row, string column)
        {
            string fieldAsString = row.GetStringField(column);
            if (fieldAsString == null)
            {
                return -1;
            }
            return Int64.Parse(fieldAsString);
        }

        public static long GetLongField(this DataRow row, string column)
        {
            // Check the arguments for null 
            ThrowIf.IsNullArgument(row, nameof(row));
            //var foo = row[column];

            //try
            //{
            //    var result = Convert.ToInt64(foo);
            //    Debug.Print("Success as long:<" + result.ToString() + ">");
            //    return (long)result;
            //}
            //catch (Exception e)
            //{
            //    Debug.Print("Fail as long:<" + foo.ToString() + ">");
            //}

            return (long)row[column];
        }

        public static string GetStringField(this DataRow row, string columnName)
        {
            // Check the arguments for null 
            ThrowIf.IsNullArgument(row, nameof(row));

            // throws ArgumentException if column is not present in table
            object field = row[columnName];

            // SQLite assigns both string.Empty and null to DBNull on input
            if (field is DBNull)
            {
                return null;
            }
            return field.ToString();
            //return (string)field.ToString();
        }
        #endregion

        #region Public Methods - Various Sets by type
        public static void SetField(this DataRow row, string column, bool value)
        {
            // Check the arguments for null 
            ThrowIf.IsNullArgument(row, nameof(row));

            row[column] = $"{value}".ToLowerInvariant();
        }

        public static void SetField(this DataRow row, string column, DateTime value)
        {
            // Check the arguments for null 
            ThrowIf.IsNullArgument(row, nameof(row));
            row[column] = value;
        }

        public static void SetField(this DataRow row, string column, int value)
        {
            // Check the arguments for null 
            ThrowIf.IsNullArgument(row, nameof(row));
            row[column] = value.ToString();
        }

        public static void SetField(this DataRow row, string column, long value)
        {
            // Check the arguments for null 
            ThrowIf.IsNullArgument(row, nameof(row));
            row[column] = value;
        }

        public static void SetField(this DataRow row, string column, string value)
        {
            // Check the arguments for null 
            ThrowIf.IsNullArgument(row, nameof(row));
            row[column] = value;
        }

        // ReSharper disable once UnusedMember.Global
        public static void SetField<TEnum>(this DataRow row, string column, TEnum value) where TEnum : struct, IComparable, IFormattable, IConvertible
        {
            row.SetField(column, value.ToString());
        }
        #endregion
    }
}
