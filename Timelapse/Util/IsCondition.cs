using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows.Input;
using Timelapse.DebuggingSupport;
using Timelapse.Enums;

namespace Timelapse.Util
{
    /// <summary>
    /// IsConditonal  - returns true or false depending on what is being tested
    /// All method names start with 'Is'
    /// </summary>
    public static class IsCondition
    {
        // Methods ending in Characters check to see if every characters matches its conditions.
        // However, the characters may not be in the correct order to determine if it matches a particular data type.
        // These are usually used for checking for characters as they are entered

        // Methods ending in a data type check to see if the string does match the data type.
        // These are usually used for checking if a final string is of a particular type.

        // Some, for example are equivalent e.g., alphanumerics as character order doesn't matter.
        // Others are not, e.g., 12-3 may match expected integer characters, but because of the character order, it is not an integer
        #region Characters - LineFeed
        public static bool IsLineFeedCharacters(string value)
        {
            if (value == null)
            {
                return false;
            }

            return value == Environment.NewLine || value == "\r" || value == "\n";
        }
        #endregion

        #region Characters - Letters, Digits, Underscores etc.
        /// <summary>
        /// Returns true only if every character in the string is a letter or a digit or _
        /// Note that this does NOT include the '-' so its not a test suitablefor the Alphanumeric data type
        /// </summary>
        public static bool IsLetterDigitUnderscoreCharacters(string str)
        {
            if (str == null)
            {
                return false;
            }
            return !Regex.IsMatch(str, Constant.RegExExpressions.NotLetterDigitUnderscoreCharacters);
        }
        
        public static bool IsAlphaNumericIncludingGlobCharacters(string str)
        {
            if (str == null)
            {
                return false;
            }
            // we have to negate it as the regular expression returns true only if any other characters are matched 
            return !Regex.IsMatch(str, Constant.RegExExpressions.NotAlphaNumericDashUnderscoresGlobCharacters);
        }
        #endregion

        #region Number characters
        /// <summary>
        /// Returns true only if every character in the string is a digit
        /// </summary>
        public static bool IsStringDigitCharacters(string str)
        {
            if (str == null)
            {
                return false;
            }
            return !Regex.IsMatch(str, Constant.RegExExpressions.NotDigitCharacters);
        }

        public static bool IsIntegerCharacters(string text)
        {
            // we have to negate it as the regular expression returns true only if any other characters are matched 
            return !Regex.IsMatch(text, Constant.RegExExpressions.NotIntegerCharacters);
        }

        public static bool IsIntegerPositiveCharacters(string text)
        {
            // we have to negate it as the regular expression returns true only if any other characters are matched 
            return !Regex.IsMatch(text, Constant.RegExExpressions.NotIntegerPositiveCharacters);
        }
        public static bool IsDecimalCharacters(string text)
        {
            // we have to negate it as the regular expression returns true only if any other characters are matched 
            return !Regex.IsMatch(text, Constant.RegExExpressions.NotDecimalCharacters);
        }

        public static bool IsDecimalPositiveCharacters(string text)
        {
            // we have to negate it as the regular expression returns true only if any other characters are matched 
            return !Regex.IsMatch(text, Constant.RegExExpressions.NotDecimalPositiveCharacters);
        }
        #endregion

        #region Date/Time Characters
        public static bool IsDateTimeDataBaseFormatCharacters(string str)
        {
            if (str == null)
            {
                return false;
            }
            // we have to negate it as the regular expression returns true only if any other characters are matched 
            return !Regex.IsMatch(str, Constant.RegExExpressions.NotDateTimeCharacters);
        }

        public static bool IsDateDataBaseFormatCharacters(string str)
        {
            if (str == null)
            {
                return false;
            }
            // we have to negate it as the regular expression returns true only if any other characters are matched 
            return !Regex.IsMatch(str, Constant.RegExExpressions.NotDateCharacters);
        }

        public static bool IsTimeCharacters(string str)
        {
            if (str == null)
            {
                return false;
            }
            // we have to negate it as the regular expression returns true only if any other characters are matched 
            return !Regex.IsMatch(str, Constant.RegExExpressions.NotTimeCharacters);
        }
        #endregion

        #region Keys

        public static bool IsShiftKeyDown()
        {
            return Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift);
        }
        #endregion

        #region Type matches - true when the string matches the indicated data type
        public static bool IsAlphaNumeric(string str)
        {
            if (str == null)
            {
                return false;
            }
            // we have to negate it as the regular expression returns true only if any other characters are matched 
            return !Regex.IsMatch(str, Constant.RegExExpressions.NotAlphaNumericDashUnderscoresCharacters);
        }

        public static bool IsInteger(string text)
        {
            return Int32.TryParse(text, out _);
        }

        public static bool IsIntegerPositive(string text)
        {
            return Int32.TryParse(text, out int intNumber) && intNumber >= 0;
        }

        public static bool IsDecimal(string text)
        {
            return Double.TryParse(text, out _);  
        }
        public static bool IsDecimalPositive(string text)
        {
            return Double.TryParse(text, out double doubleNumber) && doubleNumber >= 0; 
        }

        public static bool IsBoolean(string str)
        {
            if (str == null)
            {
                return false;
            }
            return Boolean.TryParse(str, out _);
        }

        public static bool IsDateTime(string str)
        {
            return DateTimeHandler.TryParseDatabaseDateTime(str, out _);
        }

        public static bool IsDate(string str)
        {
            return DateTimeHandler.TryParseDatabaseDate(str, out _);
        }

        public static bool IsTime(string str)
        {
            return DateTimeHandler.TryParseDatabaseTime(str, out _);
        }

        // Type matches
        // Is the type one of the number types?
        public static bool IsNumberType(string type)
        {
            return type == Constant.Control.Counter || type == Constant.Control.IntegerAny || type == Constant.Control.IntegerPositive ||
             type == Constant.Control.DecimalAny || type == Constant.Control.DecimalPositive;
        }

        // Is the type one of the Date types?
        public static bool IsDateTimeType(string type)
        {
            return type == Constant.DatabaseColumn.DateTime || type == Constant.Control.DateTime_ || type == Constant.Control.Date_ || type == Constant.Control.Time_;
        }

        public static bool IsChoicesType(string type)
        {
            return type == Constant.Control.FixedChoice || type == Constant.Control.MultiChoice;
        }
        #endregion

        #region Check for matches to particular control row types
        // True iff the control is a note or multiline.
        public static bool IsControlType_Note_MultiLine(string controlRowType)
        {
            return controlRowType == Constant.Control.Note || controlRowType == Constant.Control.MultiLine;
        }

        // True iff the control is a note or multiline or alphanumeric.
        public static bool IsControlType_Note_Multiline_Alphanumeric(string controlRowType)
        {
            return controlRowType == Constant.Control.Note || controlRowType == Constant.Control.MultiLine || controlRowType == Constant.Control.AlphaNumeric;
        }

        // True iff the control is a standard custom control (i.e., not a required control)
        public static bool IsControlType_AnyNonRequired(string controlRowType)
        {
            return controlRowType == Constant.DatabaseColumn.DeleteFlag ||
                   controlRowType == Constant.Control.Note ||
                   controlRowType == Constant.Control.MultiLine ||
                   controlRowType == Constant.Control.AlphaNumeric ||
                   controlRowType == Constant.Control.Flag ||
                   controlRowType == Constant.Control.Counter ||
                   controlRowType == Constant.Control.IntegerAny ||
                   controlRowType == Constant.Control.IntegerPositive ||
                   controlRowType == Constant.Control.DecimalAny ||
                   controlRowType == Constant.Control.DecimalPositive ||
                   controlRowType == Constant.Control.FixedChoice ||
                   controlRowType == Constant.Control.MultiChoice ||
                   controlRowType == Constant.Control.DateTime_ ||
                   controlRowType == Constant.Control.Date_ ||
                   controlRowType == Constant.Control.Time_;
        }
        #endregion

        #region Path lengths
        /// <summary>
        /// Various forms returns true when the file path length (including the file name and suffix) approaches the maximum allowed by Windows
        /// While that is 260 characters (inclusive a null at the string's end), we have to account for the way SQLITE 
        /// creates a journal file when updating comprised of the file path + '-journal' (8 extra characters)
        /// That is why MaxPathLength is set to 250
        /// These methods also check for the rare case where it is passed a path that seems to be a short file name
        /// (see https://en.wikipedia.org/wiki/8.3_filename)
        /// </summary>

        // Check length of a file
        public static bool IsPathLengthTooLong(string filePath, FilePathTypeEnum filePathType)
        {

            switch (filePathType)
            {
                case FilePathTypeEnum.DDB:
                case FilePathTypeEnum.TDB:
                    // Database manipulation can create a -journal file, we need to account for that
                    filePath += "-journal";
                    break;
                case FilePathTypeEnum.Backup:
                    // Backup files created from a ddb or tdb file have a new folder and time stamp added to its length
                    // Note that the file name is already accounted for in the file path
                    filePath = Path.Combine(filePath, Constant.File.BackupFolder, ".2022-12-11.16-02-14");
                    break;
                case FilePathTypeEnum.Deleted:
                    // Deleted files created from a ddb or tdb file are located in a new folder under the root folder.
                    // A sample deleted file to testwas passed in, so we don't have to do anything
                    break;
                case FilePathTypeEnum.DisplayFile:
                    // The image path to test was passed in so we don't have to do anything
                    break;
            }
            return IsPathLengthTooLong(filePath);
        }


        private static bool IsPathLengthTooLong(string filePath)
        {
            // Check the arguments for null 
            if (string.IsNullOrWhiteSpace(filePath))
            {
                // this should not happen
                TracePrint.StackTrace(1);
                return false;
            }
            return IsPathEndingWithAShortFileName(filePath) || filePath.Length > Constant.File.MaxPathLength;
        }

        // Checks if the path or file name (excluding extension) ends with a short file name,
        // which is normally of the form xxxxxxx~d, where the first 6 chars are upper case letters, then a ~, then a number
        // THis is not guaranteed to work. For example, a long file name could exactly match this (e.g., Templa~1.tdb),
        // and sometimes short file names don't match this pattern (see https://en.wikipedia.org/wiki/8.3_filename)
        private static bool IsPathEndingWithAShortFileName(string filePath)
        {
            return new Regex(@"^[A-Z]{6}~\d$").IsMatch(Path.GetFileNameWithoutExtension(filePath));
        }
        #endregion
    }
}
