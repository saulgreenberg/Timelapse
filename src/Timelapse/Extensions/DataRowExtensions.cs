using System;
using System.Data;
using System.Diagnostics;
using Timelapse.Constant;
using Timelapse.DataStructures;
using Timelapse.DebuggingSupport;
using Timelapse.Util;

namespace Timelapse.Extensions
{
    /// <summary>
    /// Various methods to get / set data row fields by type
    /// </summary>
    public static class DataRowExtensions
    {
        #region Public Methods - Various Gets by type
        extension(DataRow row)
        {
            public bool GetBooleanField(string column)
            {
                string fieldAsString = row.GetStringField(column);
                if (fieldAsString == null)
                {
                    return false;
                }
                return string.Equals(Boolean.TrueString, fieldAsString, StringComparison.OrdinalIgnoreCase);
            }

            public DateTime GetDateTimeField(string column)
            {
                // Check the arguments for null.
                ThrowIf.IsNullArgument(row, nameof(row));
                DateTime fieldValue;
                try
                {
                    fieldValue = (DateTime)row[column];
                }
                catch
                {
                    // The stored value isn't a DateTime at all (e.g. a null entry) - fall through
                    // to the same out-of-range handling below via an obviously-invalid sentinel.
                    fieldValue = DateTime.MinValue;
                }

                // A successfully-cast DateTime can still be nonsensical for a photo/video (e.g. a
                // corrupted date string that happens to parse, such as one landing on year 9999 -
                // which has previously crashed the date-entry control, since decomposing and
                // recombining a date that close to DateTime.MaxValue can overflow). Treat anything
                // outside a generous sane range the same way as an outright cast failure.
                if (fieldValue.Year < 1900 || fieldValue.Year > 2100)
                {
                    DateTime fallback = new(1900, 1, 1, 0, 0, 0);
                    long id = row.GetID();
                    AppLog.Warning($"GetDateTimeField: invalid DateTime ({fieldValue}) in row Id {id}, column '{column}'. Replacing with {fallback} and correcting the database.");

                    // Correct the in-memory value too, so repeated reads of this row later in the
                    // session see the already-fixed value instead of re-detecting the same error.
                    row[column] = fallback;
                    GlobalReferences.MainWindow?.DataHandler?.FileDatabase?.UpdateFile(id, column, DateTimeHandler.ToStringDatabaseDateTime(fallback));

                    return fallback;
                }

                return fieldValue;
            }

            // ReSharper disable once UnusedMember.Global
            // Currently unused, but leave for potential future use?
            public TEnum GetEnumField<TEnum>(string column) where TEnum : struct, IComparable, IFormattable, IConvertible
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

            public long GetID()
            {
                return row.GetLongField(DatabaseColumn.ID);
            }

            public int GetIntegerField(string column)
            {
                string fieldAsString = row.GetStringField(column);
                if (fieldAsString == null)
                {
                    return -1;
                }
                return Int32.Parse(fieldAsString);
            }

            public long GetLongStringField(string column)
            {
                string fieldAsString = row.GetStringField(column);
                if (fieldAsString == null)
                {
                    return -1;
                }
                return Int64.Parse(fieldAsString);
            }

            public long GetLongField(string column)
            {
                // Check the arguments for null 
                ThrowIf.IsNullArgument(row, nameof(row));
                return (long)row[column];
            }

            public string GetStringField(string columnName)
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
            }
        }

        #endregion

        #region Public Methods - Various Sets by type
        extension(DataRow row)
        {
            public void SetField(string column, bool value)
            {
                // Check the arguments for null 
                ThrowIf.IsNullArgument(row, nameof(row));

                row[column] = $"{value}".ToLowerInvariant();
            }

            public void SetField(string column, DateTime value)
            {
                // Check the arguments for null 
                ThrowIf.IsNullArgument(row, nameof(row));
                row[column] = value;
            }

            public void SetField(string column, int value)
            {
                // Check the arguments for null 
                ThrowIf.IsNullArgument(row, nameof(row));
                row[column] = value.ToString();
            }

            public void SetField(string column, long value)
            {
                // Check the arguments for null 
                ThrowIf.IsNullArgument(row, nameof(row));
                row[column] = value;
            }

            public void SetField(string column, string value)
            {
                // Check the arguments for null 
                ThrowIf.IsNullArgument(row, nameof(row));
                row[column] = value;
            }

            // ReSharper disable once UnusedMember.Global
            public void SetField<TEnum>(string column, TEnum value) where TEnum : struct, IComparable, IFormattable, IConvertible
            {
                row.SetField(column, value.ToString());
            }
        }

        // ReSharper disable once UnusedMember.Global

        #endregion
    }
}
