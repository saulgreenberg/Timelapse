
namespace Timelapse.Dialog
{
    /// <summary>
    /// Represents a single row of feedback data for file metadata operations.
    /// Used to display results of metadata population operations.
    /// </summary>
    public class FileMetadataFeedbackRow(string fileName, string metadataName, string metadataValue, bool isSuccessRow = false)
    {
        /// <summary>
        /// The name of the file being processed. 
        /// </summary>
        // Technical note. VS2026 show these properites as not having any references, and Resharper says MetadataValue is unused.
        // This is incorrect:  three DataGrids use AutoGenerateColumns="True" where they use these public properties via          
        // reflection at runtime to generate the columns, which is exactly why ReSharper and VS can't detect the usage.
        // So don't delete any of these properties!
        // ReSharper disable UnusedMember.Global
        public string FileName { get; set; } = fileName;

        /// <summary>
        /// The metadata tag name (e.g., "EXIF:DateTimeOriginal")
        /// </summary>
        public string MetadataName { get; set; } = metadataName;

        /// <summary>
        /// The metadata value or error/warning message
        /// </summary>
        public string MetadataValue { get; set; } = metadataValue;

        /// <summary>
        /// True if this row represents a successful operation, false for errors/warnings
        /// </summary>
        public bool IsSuccessRow { get; } = isSuccessRow;
    }
}
