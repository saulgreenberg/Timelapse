using System;
using System.IO;
using System.Text.RegularExpressions;
using Timelapse.Enums;

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
                TracePrint.StackTrace(1);
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
                TracePrint.StackTrace(1);
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
            if (String.IsNullOrWhiteSpace(filePath))
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
