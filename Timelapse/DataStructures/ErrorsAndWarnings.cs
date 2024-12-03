using System.Collections.Generic;

namespace Timelapse.DataStructures
{
    /// <summary>
    /// Holds two lists of strings that will eventually be displayed somewhere:
    /// - error messages
    /// - warning messages
    /// </summary>
    public class ErrorsAndWarnings
    {
        #region Public Properties
        // A list of error messages
        public List<string> Errors { get; set; }

        // A list of warning messages
        public List<string> Warnings { get; set; }

        // A list of messages concerning backup files
        public List<string> BackupMessages { get; set; }

        public List<string> MergedFiles { get; set; }

        public bool BackupMade { get; set; }
        #endregion

        #region Constructor
        /// <summary>
        /// ErrorsAndWarnings: A data structure holding messages indicating Errors and Warnings
        /// </summary>
        public ErrorsAndWarnings()
        {
            Errors = new List<string>();
            Warnings = new List<string>();
            MergedFiles = new List<string>();
            BackupMessages = new List<string>();
            BackupMade = false;
        }
        #endregion
    }
}
