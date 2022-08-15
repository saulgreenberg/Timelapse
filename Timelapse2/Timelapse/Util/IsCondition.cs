using System;

namespace Timelapse.Util
{
    /// <summary>
    /// IsConditonal  - returns true or false depending on what is being tested
    /// All method names start with 'Is'
    /// </summary>

    public static class IsCondition
    {
        #region Public Methods - Digits and Letters
        /// <summary>
        /// Returns true only if every character in the string is a digit
        /// </summary>
        public static bool IsDigits(string value)
        {
            // Check the arguments for null 
            if (value == null)
            {
                // this should not happen
                TracePrint.PrintStackTrace(1);
                // throw new ArgumentNullException(nameof(value));
                return false;
            }

            foreach (char character in value)
            {
                if (!Char.IsDigit(character))
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// Returns true only if every character in the string is a letter or a digit
        /// </summary>
        public static bool IsLetterOrDigit(string str)
        {
            // Check the arguments for null 
            if (str == null)
            {
                // this should not happen
                TracePrint.PrintStackTrace(1);
                return false;
            }

            foreach (char c in str)
            {
                if (!Char.IsLetterOrDigit(c))
                {
                    return false;
                }
            }
            return true;
        }
        #endregion 

        #region  Public Methods - Path lengths
        /// <summary>
        /// Returns true when the path length exceeds the maximum allowed by Windows
        /// </summary>
        public static bool IsPathLengthTooLong(string str)
        {
            // Check the arguments for null 
            if (str == null)
            {
                // this should not happen
                TracePrint.PrintStackTrace(1);
                return false;
            }

            return str.Length > Constant.File.MaxPathLength;
        }

        // Checks the length of the backup path
        public static bool IsBackupPathLengthTooLong(string str)
        {
            // Check the arguments for null 
            if (str == null)
            {
                // this should not happen
                TracePrint.PrintStackTrace(1);
                return false;
            }
            return str.Length + Constant.File.MaxAdditionalLengthOfBackupFiles > Constant.File.MaxPathLength;
        }
        #endregion
    }
}
