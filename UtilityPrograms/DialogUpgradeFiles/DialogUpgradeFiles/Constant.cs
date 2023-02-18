using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

//using Timelapse.Controls;

namespace DialogUpgradeFiles.Constant
{
    public static class BusyState
    {
        public const int SleepTime = 50;
    }

    // Boolean - preferred string representations 
    public static class BooleanValue
    {
        public const string True = "true";
        public const string False = "false";
    }

    public static class Control
    {
        // columns unique to the template table
        public const string ControlOrder = "ControlOrder";
        public const string Copyable = "Copyable";     // whether the content of this item should be copied from previous values
        public const string DataLabel = "DataLabel";   // if not empty, its used instead of the label as the header for the column when writing the spreadsheet
        public const string DefaultValue = "DefaultValue"; // a default value for that code
        public const string Label = "Label";           // a label used to describe that code
        public const string List = "List";             // indicates a list of items
        public const string SpreadsheetOrder = "SpreadsheetOrder";
        public const string TextBoxWidth = "TXTBOXWIDTH";  // the width of the textbox
        public const string Tooltip = "Tooltip";       // the tooltip text that describes the code
        public const string Type = "Type";             // the data type
        public const string Visible = "Visible";       // whether an item should be visible (used by standard items)

        // control types
        public const string Counter = "Counter";       // a counter
        public const string FixedChoice = "FixedChoice";  // a fixed choice
        public const string Flag = "Flag";             // A boolean
        public const string Note = "Note";             // A note

        // default data labels
        public const string Choice = "Choice";         // Label for a fixed choice

        public static readonly ReadOnlyCollection<string> StandardTypes = new List<string>
        {
            DatabaseColumn.Date,
            DatabaseColumn.DateTime,
            DatabaseColumn.DeleteFlag,
            DatabaseColumn.File,
            DatabaseColumn.Folder,
            DatabaseColumn.ImageQuality,
            DatabaseColumn.RelativePath,
            DatabaseColumn.Time,
            DatabaseColumn.UtcOffset
        }.AsReadOnly();
    }

    public static class ControlDefault
    {
        // general defaults
        public const string Value = "";
        public const string FlagValue = BooleanValue.False;             // Default for: flags
        public const int FlagWidth = 20;

        public const string DateTimeTooltip = "Date and time taken (Year-Month-Day Hours:Minutes:Seconds:Milliseconds)";
        public const string DateTimeWidth = "160";
        public const string RelativePathTooltip = "Path from the folder containing the template and image data files to the file";
        public const string RelativePathWidth = "100";

        public const string DeleteFlagLabel = "Delete?";    // a flag data type for marking deletion
        public const string DeleteFlagTooltip = "Mark a file as one to be deleted. You can then confirm deletion through the Edit Menu";

        public const string UtcOffsetTooltip = "Universal Time offset of the time zone for date and time taken";
        public const string UtcOffsetWidth = "60";

        public static readonly DateTimeOffset DateTimeValue = new DateTimeOffset(1900, 1, 1, 12, 0, 0, 0, TimeSpan.Zero);
    }

    public static class ControlMiscellaneous
    {
        public const string EmptyChoiceItem = "<EMPTY>"; // Indicates an empty item included in the choice menu list
    }
    public static class ControlsDeprecated
    {
        // MarkForDeletion data label was split between editor and Timelapse and normalized to DeleteFlag in 2.1.0.4
        public const string MarkForDeletion = "MarkForDeletion";
    }

    public static class DatabaseValues
    {
        // default values
        public const int DateTimePosition = 4;
        public const long ImageSetRowID = 1;
        public const int RelativePathPosition = 2;
        public const int UtcOffsetPosition = 5;
        public const string VersionNumberMinimum = "2.3.0.0";
        public const string DefaultSortTerms = DatabaseColumn.RelativePath + "," + SortTermValues.RelativePathDisplayLabel + "," + DatabaseColumn.RelativePath + "," + BooleanValue.True + ","
            + DatabaseColumn.DateTime + "," + DatabaseColumn.DateTime + "," + DatabaseColumn.DateTime + "," + BooleanValue.True;
        public const string DefaultQuickPasteXML = "<Entries></Entries>";
        public const string IndexRelativePath = "IndexRelativePath";
        public const string IndexRelativePathFile = "IndexRelativePathFile";
        public const string IndexFile = "IndexFile";
        public const string IndexID = "IndexDetectionID";
        public const string IndexDetectionID = "IndexDetectionID";
    }

    // Names of standard database columns, always included but not always made visible in the user controls
    public static class DatabaseColumn
    {
        public const string ID = "Id";

        // columns in ImageDataTable
        public const string Date = "Date";
        public const string DateTime = "DateTime";
        public const string File = "File";
        public const string Folder = "Folder";
        public const string ImageQuality = "ImageQuality";
        public const string Dark = "Dark";
        public const string DeleteFlag = "DeleteFlag";
        public const string RelativePath = "RelativePath";
        public const string Time = "Time";
        public const string UtcOffset = "UtcOffset";

        // columns in ImageSetTable
        public const string Log = "Log";                    // String holding a user-created text log
        public const string RootFolder = "RootFolder";       // String holding the root folder containing the template
        public const string MagnifyingGlass = "Magnifier";        // string holding the true/false state of the magnifying glass (on or off)
        public const string MostRecentFileID = "Row";       // ID of the last image displayed. It used to hold the current row #, but we repurposed ROW to  hold the MostRecentFileID as it simplifies backwards compatability
        public const string Selection = "Filter";           // string holding the currently selected selection. For backwards compatability, leave the actual column name as Filter.
        public const string TimeZone = "TimeZone";
        public const string WhiteSpaceTrimmed = "WhiteSpaceTrimmed";        // string holding the true/false state of whether the white space has been trimmed from the data.
        public const string VersionCompatabily = "VersionCompatabily";      // The latest version of Timelapse that opened this database. Useful for cases when we want to check for backwards compatability
        public const string SortTerms = "SortTerms";                     // a JSON list that indicates the Primary 1st and 2nd sort terms and their attribute
        public const string QuickPasteXML = "QuickPasteXML";              // an XML description that specifies the user's quickpaste entries and values.
        public const string QuickPasteTerms = "QuickPasteTerms";              // a JSON description that specifies the user's quickpaste entries and values.
        public const string SelectedFolder = "SelectedFolder";              // a string identifying the folder selected by a user via the Select|Folders menu. Otherwise empty if another selection was done, or if its all files
        public const string SearchTerms = "SearchTerms";
        public const string BoundingBoxDisplayThreshold = "BBDisplayThreshold";

        // other columns found in Old XML files
        public const string Data = "Data";                 // the data describing the attributes of that control
        public const string Image = "Image";               // A single image and its associated data
        public const string Point = "Point";               // a single point
        public const string X = "X";                       // Every point has an X and Y
        public const string Y = "Y";
    }

    public static class DBTables
    {
        // database table names
        public const string Template = "TemplateTable"; // the table containing the template data
        public const string FileData = "DataTable";         // the table containing the image data
        public const string ImageSet = "ImageSetTable"; // the table containing information common to the entire image set
        public const string TemplateInfo = "TemplateInfo"; // the table containing info about the template
        public const string Markers = "MarkersTable";         // the table containing the marker data
        public const string Info = "Info";
        public const string Images = "Images";
        public const string Detections = "Detections";
        public const string Classifications = "Classifications";
    }

    public static class ExceptionTypes
    {
        public const string TemplateReadWriteException = "TemplateReadWriteException";
    }

    public static class File
    {
        public const string AviFileExtension = ".avi";
        public const string BackupFolder = "Backups"; // Sub-folder that will contain database and csv file backups  
        public const string BackupCheckpointIndicator = ".Checkpoint-"; // string added to the backup file path for special backup files
        public const string BackupPre23Indicator = ".Pre2.3"; // string added to the backup file path for special backup files

        public const string DeletedFilesFolder = "DeletedFiles"; // Sub-folder that will contain backups of deleted images 
        public const string DefaultFileDatabaseFileNameRoot = "TimelapseData";
        public const string FileDatabaseFileExtension = ".ddb";
        public const string JpgFileExtension = ".jpg";
        public const string MovFileExtension = ".mov";
        public const string Mp4FileExtension = ".mp4";
        public const string ASFFileExtension = ".asf";
        public const string MacOSXHiddenFilePrefix = "._";
        public const string NetworkRecycleBin = "@Recycle";
        public const string VideoThumbnailFolderName = ".vthumb";
        public const int MaxPathLength = 259; // One less than the permissable length of 260, as I'm not sure how the null at the end of as string is counted;
        public const string TraceFile = "Trace.txt"; // FIle name for file containing debug information. Usually written in the same folder containing the template.
    }

    // shorthands for FileSelection.<value>.ToString()
    public static class ImageQuality
    {
        public const string Ok = "Ok";
        public const string Dark = "Dark";
        public const string Missing = "Missing";
        public const string ListOfValues = "Ok|Dark|Corrupted|Missing";
    }

    public static class ImageXml
    {
        // standard elements, always included but not always made visible
        public const string Date = "_Date";
        public const string File = "_File";
        public const string Folder = "_Folder";
        public const string Time = "_Time";

        // paths to standard elements, always included but not always made visible
        public const string FilePath = "Codes/_File";
        public const string FolderPath = "Codes/_Folder";

        // elements
        public const string Codes = "Codes";
        public const string Data = "Data";             // the data describing the attributes of that code
        public const string Images = "Images";
        public const string Item = "Item";             // and item in a list
        public const string Slash = "/";
    }

    public static class SortTermValues
    {

        public const string RelativePathDisplayLabel = "Relative Path (folder)";
    }

    public static class Time
    {
        // The standard date format, e.g., 05-Apr-2011
        public static readonly string NeutralTimeZone = TimeZoneInfo.Local.Id; // For backwards compatabity, we use the local time zone. Eventually, this will disapper. 


        public const string DateFormat = "dd-MMM-yyyy";


        // DateTimes as stored in the database. The 2nd form is so it can read in from the DB both the legacy (UTC) format and current simpler format 
        public const string DateTimeDatabaseLegacyUTCFormat = "yyyy-MM-ddTHH:mm:ss.fffZ";  // legacy DatabaseFormat
        public const string DateTimeDatabaseFormat = "yyyy-MM-dd HH:mm:ss";

        public const string DateTimeDisplayFormat = "dd-MMM-yyyy HH:mm:ss";
        public const string DateTimeCSVWithTSeparator = "yyyy-MM-dd'T'HH:mm:ss";
        public const string DateTimeCSVWithoutTSeparator = "yyyy-MM-dd' 'HH:mm:ss";

        // This is an SQL format for writing date/time, and is equivalent to the DateTimeDatabaseFormat
        public static readonly string DateTimeSQLFormatForWritingTimelapseDB = "%Y-%m-%d %H:%M:%S";

        // known formats supported by Metadata Extractor. All these can be read without ambiguity
        public static readonly string[] DateTimeMetadataFormats =
        {
            "yyyy:MM:dd HH:mm:ss.fff",
            "yyyy:MM:dd HH:mm:ss",
            "yyyy:MM:dd HH:mm",
            "yyyy-MM-dd HH:mm:ss",
            "yyyy-MM-dd HH:mm",
            "yyyy.MM.dd HH:mm:ss",
            "yyyy.MM.dd HH:mm",
            "yyyy-MM-ddTHH:mm:ss.fff",
            "yyyy-MM-ddTHH:mm:ss.ff",
            "yyyy-MM-ddTHH:mm:ss.f",
            "yyyy-MM-ddTHH:mm:ss",
            "yyyy-MM-ddTHH:mm.fff",
            "yyyy-MM-ddTHH:mm.ff",
            "yyyy-MM-ddTHH:mm.f",
            "yyyy-MM-ddTHH:mm",
            "yyyy:MM:dd",
            "yyyy-MM-dd",
            "ddd MMM dd HH:mm:ss K yyyy" // File.File Modified Date
        };

        public const int MonthsInYear = 12;
        public const string TimeFormat = "HH:mm:ss";
        public const string TimeSpanDisplayFormat = @"hh\:mm\:ss";

        // Various UTC Formats
        public const string UtcOffsetDatabaseFormat = "0.00";
        public const string UtcOffsetDisplayFormat = @"hh\:mm";

        public static readonly TimeSpan MaximumUtcOffset = TimeSpan.FromHours(14.0);
        public static readonly TimeSpan MinimumUtcOffset = TimeSpan.FromHours(-12.0);
        public static readonly TimeSpan UtcOffsetGranularity = TimeSpan.FromTicks(9000000000); // 15 minutes
    }

    // DETECTION Columns and values  
    #region Detection Constants.
    public static class InfoColumns
    {
        public const string Detector = "detector";
        public const string DetectorVersion = "megadetector_version";
        public const string TypicalDetectionThreshold = "typical_detection_threshold";
        public const string ConservativeDetectionThreshold = "conservative_detection_threshold";
        public const string TypicalClassificationThreshold = "typical_classification_threshold";
    }

    public static class DetectionCategoriesColumns
    {
        public const string Label = "label";
    }

    public static class ClassificationCategoriesColumns
    {
        public const string Label = "label";
    }

    public static class DetectionColumns
    {
        public const string DetectionID = "detectionID";
        public const string Conf = "conf";
        public const string BBox = "bbox";
    }

    public static class DetectionValues
    {
        // Detector defaults. Different versions of Megadetector produce different confidence values.
        // The values below reflect  Megadetector v4, which is the likely detector if no overrides 
        // were set  in the Detection json file
        public const float DefaultTypicalDetectionThresholdIfUnknown = 0.8f;        // Appropriate for Megadetector v4
        public const float DefaultConservativeDetectionThresholdIfUnknown = 0.3f;   // Appropriate for Megadetector v4
        public const float DefaultTypicalClassificationThresholdIfUnknown = 0.75f;  // Appropriate for Megadetector v4
        public const float BoundingBoxDisplayThresholdDefault = Undefined;   // Appropriate for Megadetector v4
        public const string MDVersionUnknown = "vUnknown";
        public const float Undefined = -1F;
    }

    public static class ClassificationColumns
    {
        public const string Conf = "conf";
    }
    #endregion
}