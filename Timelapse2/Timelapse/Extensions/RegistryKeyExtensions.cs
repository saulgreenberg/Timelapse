using Microsoft.Win32;
using System;
using System.Globalization;
using System.Windows;

namespace Timelapse.Util
{
    /// <summary>
    /// Read and Write particular data types into the registry
    /// </summary>
    public static class RegistryKeyExtensions
    {
        #region Get (Read) values from the registry, returned as a particular type
        /// <summary>
        /// Get a boolean value from the registry
        /// </summary>
        public static bool GetBoolean(this RegistryKey registryKey, string subKeyPath, bool defaultValue)
        {
            string valueAsString = registryKey.GetString(subKeyPath);
            if (valueAsString != null)
            {
                if (Boolean.TryParse(valueAsString, out bool value))
                {
                    return value;
                }
            }
            return defaultValue;
        }

        /// <summary>
        /// Get a DateTime value from the registry. 
        /// </summary>
        public static DateTime GetDateTime(this RegistryKey registryKey, string subKeyPath, DateTime defaultValue)
        {
            string value = registryKey.GetString(subKeyPath);
            if (value == null)
            {
                return defaultValue;
            }
            return DateTime.TryParseExact(value, Constant.Time.DateTimeDatabaseFormats, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out DateTime dateTime)
                ? dateTime
                : defaultValue;
        }

        /// <summary>
        /// Get a TimeSpan value as Seconds from the registry
        /// </summary>
        public static TimeSpan GetTimeSpanAsSeconds(this RegistryKey registryKey, string subKeyPath, TimeSpan defaultValue)
        {
            string value = registryKey.GetString(subKeyPath);
            if (value == null)
            {
                return defaultValue;
            }
            return int.TryParse(value, out int seconds)
                ? TimeSpan.FromSeconds(seconds)
                : defaultValue;
        }

        /// <summary>
        /// Get a Double  from the registry
        /// </summary>
        public static double GetDouble(this RegistryKey registryKey, string subKeyPath, double defaultValue)
        {
            string valueAsString = registryKey.GetString(subKeyPath);
            if (valueAsString != null)
            {
                if (Double.TryParse(valueAsString, out double value))
                {
                    return value;
                }
            }
            return defaultValue;
        }

        /// <summary>
        /// Get an enum from the registry
        /// </summary>
        public static TEnum GetEnum<TEnum>(this RegistryKey registryKey, string subKeyPath, TEnum defaultValue) where TEnum : struct, IComparable, IConvertible, IFormattable
        {
            string valueAsString = registryKey.GetString(subKeyPath);
            try
            {
                if (valueAsString != null)
                {
                    return (TEnum)Enum.Parse(typeof(TEnum), valueAsString);
                }
            }
            catch
            {
                // This will drop through to return default value
            }
            return defaultValue;
        }

        /// <summary>
        /// Get an Int from the registry
        /// </summary>
        public static int GetInteger(this RegistryKey registryKey, string subKeyPath, int defaultValue)
        {
            // Check the arguments for null 
            if (registryKey == null)
            {
                // this should not happen
                TracePrint.PrintStackTrace(1);
                // throw new ArgumentNullException(nameof(registryKey));
                return defaultValue;
            }

            object value = registryKey.GetValue(subKeyPath);
            if (value == null)
            {
                return defaultValue;
            }

            if (value is Int32)
            {
                return (int)value;
            }

            if (value is string @string)
            {
                return Int32.Parse(@string);
            }
            return defaultValue;
        }

        /// <summary>
        /// Get a rect from the registry. If there are issues, just return the default value.
        /// </summary>
        public static Rect GetRect(this RegistryKey registryKey, string subKeyPath, Rect defaultValue)
        {
            string rectAsString = registryKey.GetString(subKeyPath);

            if (rectAsString == null)
            {
                return defaultValue;
            }
            try
            {
                Rect rectangle = Rect.Parse(rectAsString);
                return rectangle;
            }
            catch
            {
                // The parse can fail if the number format was saved as a non-American number, eg, Portugese uses , vs. as the decimal place.
                // This shouldn't happen as I have used an invarient to save numbers, but just in case... this will drop through to return default value
            }
            return defaultValue;
        }

        /// <summary>
        /// Get a size from the registry
        /// </summary>
        public static Size GetSize(this RegistryKey registryKey, string subKeyPath, Size defaultValue)
        {
            string sizeAsString = registryKey.GetString(subKeyPath);
            if (sizeAsString == null)
            {
                return defaultValue;
            }
            try
            {
                Size size = Size.Parse(sizeAsString);
                return size;
            }
            catch
            {
                // This will drop through to return default value.

            }
            return defaultValue;
        }

        /// <summary>
        /// Get a string from the registry
        /// </summary>
        public static string GetString(this RegistryKey registryKey, string subKeyPath, string defaultValue)
        {
            string valueAsString = registryKey.GetString(subKeyPath);
            if (valueAsString == null)
            {
                return defaultValue;
            }
            return valueAsString;
        }

        /// <summary>
        /// Get a REG_SZ key's value from the registry
        /// </summary>
        public static string GetString(this RegistryKey registryKey, string subKeyPath)
        {
            // Check the arguments for null 
            ThrowIf.IsNullArgument(registryKey, nameof(registryKey));
            return (string)registryKey.GetValue(subKeyPath);
        }

        /// <summary>
        /// Get a RecencyOrderedList from the registry
        /// </summary>
        public static RecencyOrderedList<string> GetRecencyOrderedList(this RegistryKey registryKey, string subKeyPath)
        {
            // Check the arguments for null 
            ThrowIf.IsNullArgument(registryKey, nameof(registryKey));

            RegistryKey subKey = registryKey.OpenSubKey(subKeyPath);
            RecencyOrderedList<string> values = new RecencyOrderedList<string>(Constant.Defaults.NumberOfMostRecentDatabasesToTrack);

            if (subKey != null)
            {
                for (int index = subKey.ValueCount - 1; index >= 0; --index)
                {
                    string listItem = (string)subKey.GetValue(index.ToString());
                    if (listItem != null)
                    {
                        values.SetMostRecent(listItem);
                    }
                }
            }
            return values;
        }
        #endregion

        #region Write a particular type of value to the registry, depending on its type

        /// <summary>
        /// Write a boolean value to registry
        /// </summary>
        public static void Write(this RegistryKey registryKey, string subKeyPath, bool value)
        {
            registryKey.Write(subKeyPath, value.ToString().ToLowerInvariant());
        }

        /// <summary>
        /// Write a DateTime value to registry
        /// </summary>
        public static void Write(this RegistryKey registryKey, string subKeyPath, DateTime value)
        {
            // For backwards compatability, we use the UTC format as otherwise older versions of Timelapse will not open
            registryKey.Write(subKeyPath, value.ToString(Constant.Time.DateTimeDatabaseFormat));
        }

        /// <summary>
        /// Write a Double value to registry
        /// </summary>
        public static void Write(this RegistryKey registryKey, string subKeyPath, double value)
        {
            registryKey.Write(subKeyPath, value.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }

        /// <summary>
        /// Write a RecencyOrderedList value to registry
        /// </summary>
        public static void Write(this RegistryKey registryKey, string subKeyPath, RecencyOrderedList<string> values)
        {
            // Check the arguments for null 
            ThrowIf.IsNullArgument(registryKey, nameof(registryKey));

            if (values != null)
            {
                // create the key whose values represent elements of the list
                RegistryKey subKey = registryKey.OpenSubKey(subKeyPath, true);
                if (subKey == null)
                {
                    subKey = registryKey.CreateSubKey(subKeyPath);
                }

                // write the values
                int index = 0;
                foreach (string value in values)
                {
                    subKey.SetValue(index.ToString(), value);
                    ++index;
                }

                // remove any additional values when the new list is shorter than the old one
                int maximumValueName = subKey.ValueCount;
                for (; index < maximumValueName; ++index)
                {
                    subKey.DeleteValue(index.ToString());
                }
            }
        }

        /// <summary>
        /// Write an int value to registry
        /// </summary>
        public static void Write(this RegistryKey registryKey, string subKeyPath, int value)
        {
            // Check the arguments for null 
            ThrowIf.IsNullArgument(registryKey, nameof(registryKey));

            registryKey.SetValue(subKeyPath, value, RegistryValueKind.DWord);
        }

        /// <summary>
        /// Write a Rect value to registry
        /// </summary>
        public static void Write(this RegistryKey registryKey, string subKeyPath, Rect value)
        {
            registryKey.Write(subKeyPath, value.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }

        /// <summary>
        /// Write a Size value to registry
        /// </summary>
        public static void Write(this RegistryKey registryKey, string subKeyPath, Size value)
        {
            registryKey.Write(subKeyPath, value.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }

        /// <summary>
        /// Write a string value to registry
        /// </summary>
        public static void Write(this RegistryKey registryKey, string subKeyPath, string value)
        {
            // Check the arguments for null 
            ThrowIf.IsNullArgument(registryKey, nameof(registryKey));
            registryKey.SetValue(subKeyPath, value, RegistryValueKind.String);
        }

        /// <summary>
        /// Write a TimeSpan value as Seconds to registry
        /// </summary>
        public static void Write(this RegistryKey registryKey, string subKeyPath, TimeSpan value)
        {
            // Check the arguments for null 
            ThrowIf.IsNullArgument(registryKey, nameof(registryKey));
            registryKey.SetValue(subKeyPath, value.TotalSeconds.ToString(), RegistryValueKind.String);
        }
        #endregion
    }
}
