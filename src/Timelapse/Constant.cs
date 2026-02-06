using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using TimelapseWpf.Toolkit;

// ReSharper disable once CheckNamespace
namespace Timelapse.Constant
{
    // Various arguments accepted by Timelapse.
    public static class Arguments
    {
        // Restricts Timelapse to only display and manipulate files in the relative path supplied as the 2nd argument
        public const string RelativePathArgument = "-relativepath";

        // Timelapse opens in view only mode, where no data can be altered
        public const string ViewOnlyArgument = "-viewonly";

        public const string TemplateEditorArgument = "-templateeditor";
    }

    public static class AvalonDockValues
    {
        public const double DefaultTimelapseWindowHeight = 900.0;
        public const double DefaultTimelapseWindowWidth = 1350.0;
        public const double FloatingWindowMinimumHeight = 119;
        public const double FloatingWindowMinimumWidth = 275;
        public const double FloatingWindowLimitSizeHeightCorrection = 50.0;
        public const double FloatingWindowLimitSizeWidthCorrection = 18.0;
        public const string FloatingWindowFloatingWidthProperty = "FloatingWidth";
        public const string FloatingWindowFloatingHeightProperty = "FloatingHeight";
        public const string WindowRegistryKeySuffix = "_window";
        public const string WindowMaximizeStateRegistryKeySuffix = "_windowmaximized";
    }

    public static class AvalonLayoutResourcePaths
    {
        public const string DataEntryOnTop = "pack://application:,,/Resources/AvalonLayout_DataEntryOnTop.config";
        public const string DataEntryOnSide = "pack://application:,,/Resources/AvalonLayout_DataEntryOnSide.config";
        public const string DataEntryFloating = "pack://application:,,/Resources/AvalonLayout_DataEntryFloating.config";
    }
    public static class AvalonLayoutTags
    {
        public const string LastUsed = "AvalonLayout_LastUsed4";
        public const string DataEntryOnTop = "AvalonLayout_DataEntryOnTop";
        public const string DataEntryOnSide = "AvalonLayout_DataEntryOnSide";
        public const string DataEntryFloating = "AvalonLayout_DataEntryFloating";
        public const string Custom1 = "AvalonLayout_Custom01";
        public const string Custom2 = "AvalonLayout_Custom02";
        public const string Custom3 = "AvalonLayout_Custom03";
    }

    // Boolean - preferred string representations 
    public static class BooleanValue
    {
        public const string True = "true";
        public const string False = "false";
    }

    public static class Colours
    {
        public static readonly SolidColorBrush VeryLightBlue = (SolidColorBrush) new BrushConverter().ConvertFrom("#dfecf8");
        public static readonly SolidColorBrush LightBlue = new(Colors.LightBlue);
        public static readonly SolidColorBrush DarkBlue = new(Colors.Blue);
        public static readonly SolidColorBrush MintyGreen = new(Color.FromArgb(255, 200, 251, 200));
        public static readonly SolidColorBrush VeryLightGrey = Brushes.Gainsboro;
        public static readonly SolidColorBrush PaleWhite = Brushes.Ivory;
    }
    public static class Control
    {
        // columns unique to the template control table
        public const string ControlOrder = "ControlOrder";
        public const string Copyable = "Copyable"; // whether the content of this item should be copied from previous values
        public const string DataLabel = "DataLabel"; // if not empty, its used instead of the label as the header for the column when writing the spreadsheet
        public const string DefaultValue = "DefaultValue"; // a default value for that code
        public const string Label = "Label"; // a label used to describe that code
        public const string List = "List"; // indicates a list of items
        public const string SpreadsheetOrder = "SpreadsheetOrder";
        public const string TextBoxWidth = "TXTBOXWIDTH"; // the width of the textbox
        public const string Tooltip = "Tooltip"; // the tooltip text that describes the code
        public const string Type = "Type"; // the data type
        public const string Visible = "Visible"; // whether an item should be visible (used by standard items)
        public const string ExportToCSV = "ExportToCSV";

        // columns unique to the  template metadata control
        public const string Level = "Level";

        // columns unique to the metadata alias table
        public const string Alias = "Alias";
        public const string Guid = "Guid";
        public const string Ignore = "Ignore";

        // control types
        public const string Counter = "Counter"; // a counter
        public const string FixedChoice = "FixedChoice"; // a fixed choice
        public const string MultiChoice = "MultiChoice"; // can choose multiple items from a list
        public const string Flag = "Flag"; // A boolean
        public const string Note = "Note"; // A note
        public const string MultiLine = "MultiLine"; // A multiline Note
        public const string AlphaNumeric = "AlphaNumeric"; // Text allowing only letters, numbers, - and _
        public const string IntegerAny = "IntegerAny"; // A positive or negative integer
        public const string IntegerPositive = "IntegerPositive"; // A positive integer
        public const string DecimalAny = "DecimalAny"; // A decimal
        public const string DecimalPositive = "DecimalPositive"; // A decimal
        public const string DateTime_ = "DateTime_"; // A custom DateTime
        public const string DateTimeCustom = "DateTimeCustom"; // A custom DateTime (legacy)
        public const string Date_ = "Date_"; // A custom DateTime showing only the date
        public const string Time_ = "Time_"; // A custom DateTime showing only the time
        // A list of all control types
        internal static string[] ControlTypes =
        [
            Note, MultiLine, AlphaNumeric,
            Counter, IntegerAny, IntegerPositive, DecimalAny, DecimalPositive,
            FixedChoice, MultiChoice,
            DateTime_, Date_, Time_,
            Flag
        ];

        // default data labels
        public const string Choice = "Choice";         // Label for a fixed choice
        public const string LabelIntegerAny = "Integer";
        public const string LabelDecimalAny = "Decimal";

        // highlight / non-hightlight border thicknesses of a control
        public const double BorderThicknessNormal = 1;
        public const double BorderThicknessHighlight = 3;
        public static readonly SolidColorBrush BorderColorNormal = Colours.LightBlue;
        public static readonly SolidColorBrush BorderColorHighlight = Colours.DarkBlue;

        // a minty green, same as Copyable field
        public static readonly SolidColorBrush QuickPasteFieldHighlightBrush = Colours.MintyGreen; 

        public static readonly ReadOnlyCollection<Type> KeyboardInputTypes = new List<Type>
        {       typeof(ImprintAutoCompleteTextBox), // note or alphanumeric control
                typeof(Calendar),          // date time control
                typeof(CalendarDayButton), // date time control
                typeof(CheckBox),          // flag controls
                typeof(ComboBox),          // choice controls
                typeof(ComboBoxItem),      // choice controls
                typeof(IntegerUpDown),
                typeof(DoubleUpDown),
                typeof(MultiLineText), // multiline editor control
                typeof(WatermarkCheckComboBox),      // multiChoice editor control
                typeof(WatermarkComboBox),  // choice controls with watermark
                typeof(TextBox),           // note controls
                typeof(WatermarkTextBox),   // date time or counter control

        }.AsReadOnly();

        public static readonly ReadOnlyCollection<string> StandardTypes = new List<string>
        {
                DatabaseColumn.DateTime,
                DatabaseColumn.DeleteFlag,
                DatabaseColumn.File,
                DatabaseColumn.RelativePath
        }.AsReadOnly();
    }

    // see also ControlLabelStyle and ControlContentStyle
    public static class ControlStyle
    {
        public const string StackPanelContainerStyle = "StackPanelContainerStyle";
        public const string GridContainerStyle = "GridContainerStyle";
    }

    public static class ControlDefault
    {
        // general defaults
        public const string ControlDefaultTextValue = "";
        public const int DataEntryControlDefaultWidth = 100;
        public const string DataEntryControlDefaultWidthAsString = "100";
        public const double MetadataDataEntryControlDefaultWidth = 250;

        // File control
        public const string FileTooltip = "The file name";
        public const string FileWidth = DataEntryControlDefaultWidthAsString;

        // RelativePath control
        public const string RelativePathTooltip = "Path from the root folder containing the template and image data files to the file";
        public const string RelativePathWidth = DataEntryControlDefaultWidthAsString;

        // DateTime, DateTimeCustom, Date_, Time_ controls
        public static readonly DateTime DateTimeDefaultValue = new(DateTime.Now.Year, 1, 1, 12, 0, 0);

        public const string DateTimeTooltip = "Date and time taken (Time_ in 24 hour format)";
        public const int DateTimeDefaultWidth = 160;

        public const string DateTimeCustomTooltip = "Use the calendar (down arrow) or click a field to update the date and time (24 hour format).";
        public const string DateTimeCustomLabel = "DateTime";
        public static readonly DateTime DateTimeCustomDefaultValue = DateTimeDefaultValue;
        public const int DateTimeCustomDefaultWidth = DateTimeDefaultWidth;

        public const string Date_Tooltip = "Use the calendar (down arrow) or click a field to update the date.";
        public const string Date_Label = "Date";
        public static readonly DateTime Date_DefaultValue = DateTimeDefaultValue;
        public const int Date_DefaultWidth = 120;

        public const string Time_Tooltip = "Use the time control (down arrow) or click a field to update the time (24 hour format). ";
        public const string Time_Label = "Time";
        public static readonly DateTime Time_DefaultValue = DateTimeDefaultValue;
        public const int Time_Width = 95;

        // DeleteFlag control
        public const string DeleteFlagLabel = "Delete?";    // a flag data type for marking deletion
        public const string DeleteFlagTooltip = "Mark a file as one to be deleted. You can then confirm deletion through the Edit Menu";

        // Text-based controls
        public const string AlphaNumericTooltip = "An alphanumeric note comprising only letters, numbers, underscore (_) and/or dashes (-).";
        public const string AlphaNumericDefaultValue = NoteDefaultValue;
        public const int AlphaNumericWidth = NoteDefaultWidth;

        public const string MultiLineTooltip = "A textual note that can have 1 or more lines in it";
        public const string MultiLineDefaultValue = NoteDefaultValue;
        public const int MultiLineWidth = NoteDefaultWidth;

        public const string NoteTooltip = "A (usually short) textual note";
        public const string NoteDefaultValue = "";
        public const int NoteDefaultWidth = 100;
        
        // Number controls
        public const string CounterTooltip = "Click the counter button, then click on the image to count the entity. Or just type in an integer";
        public const int CounterWidth = 30;
        public const string DecimalAnyTooltip = "Type in a decimal number (negative or positive, including zero or blank). Or use the toggle to change it.";
        public const string DecimalPositiveTooltip = "Type in a positive decimal number (includes zero or blanks). Or use the toggle to change it.";
        public const string IntegerAnyTooltip = "Type in a negative or positive integer or blank. Or use the toggle to change it.";
        public const string IntegerPositiveTooltip = "Type in a positive integer (includes zero or blank). Or use the toggle to change it.";

        public const string DecimalFormatString = "###############0.0###############";

        public const string IntegerFormatString = "###############0";
        // These are common to all number controls
        public const int NumberWidth = 60;
        public const string NumberDefaultValue = "0";

        // Choice, MultiChoice
        public const string FixedChoiceTooltip = "Choose an item from the menu";
        public const string FixedChoiceDefaultValue = ControlDefaultTextValue;
        public const int FixedChoiceDefaultWidth = DataEntryControlDefaultWidth;

        public const string MultiChoiceTooltip = "Choose one or more item from the menu";
        public const string MultiChoiceDefaultValue = ControlDefaultTextValue;
        public const int MultiChoiceDefaultWidth = DataEntryControlDefaultWidth;

        // Flag
        public const string FlagTooltip = "Toggle between true and false";
        public const string FlagValue = BooleanValue.False;             // Default for: flags
        public const int FlagWidth = 20;
    }

    public static class ControlDeprecated
    {
        public const string Folder = "Folder"; // Used in Custom Select instead of DateTime
        public const string DateLabel = "Date"; // Used in Custom Select instead of DateTime
        public const string TimeLabel = "Time"; // Used in Custom Select instead of DateTime
        public const string UtcOffsetLabel = "UtcOffset";
        public const string ImageQuality = "ImageQuality";
        // MarkForDeletion data label was split between editor and Timelapse and normalized to DeleteFlag in 2.1.0.4
        public const string MarkForDeletion = "MarkForDeletion";
    }

    public static class DatabaseValues
    {
        // default values
        public const long DefaultFileID = 1;
        //public const int DateTimePosition = 4;
        public const string ImageSetDefaultLog = "";
        public const long ImageSetRowID = 1;
        public const long InvalidID = -1;
        public const int InvalidRow = -1;
        //public const int RelativePathPosition = 2;
        public const int RowsPerInsert = 5000;

        // These constants control incompatible versioning, as indicated below
        public const string VersionNumberMinimum = "2.3.0.0"; // Earlier versions than this requires a special software update to the database files as done via DialogUpgradeFils.dll
        public const string VersionNumberBackwardsCompatible = "2.3.3.0"; // The earliest version known to be backwards compatible with this database
        public const string VersionNumberBackwardsCompatibleForTemplates = "2.3.0.0"; // The earliest version known to be backwards compatible with this database
        public const string VersionPatchNumber = ""; // If this version is patched, the patch number (or Beta indications).. Should be updated whenever new versions are release. Use "." or "-patch #." 
        public const string DefaultSortTerms = "[ { \"DataLabel\":\"RelativePath\", \"DisplayLabel\":\"RelativePath\", \"ControlType\":\"RelativePath\", \"IsAscending\":\"true\" }, { \"DataLabel\":\"DateTime\", \"DisplayLabel\":\"DateTime\", \"ControlType\":\"DateTime\", \"IsAscending\":\"true\" } ]";
        public const string DefaultSearchTerms = "{}";
        public const string DefaultQuickPasteJSON = "[]";
        public const string IndexRelativePath = "IndexRelativePath";
        public const string IndexRelativePathFile = "IndexRelativePathFile";
        public const string IndexRelativePathDateTimeFile = "IndexRelativePathDateTimeFile";
        public const string IndexFile = "IndexFile";
        public const string IndexDetectionID = "IndexDetectionID";
        public const string IndexDetectionVideoID = "IndexDetectionVideoID";
        public const string IndexDetectionsClassificationConfidence = "IndexDetectionsClassificationConfidence";
        public const string IndexEpisodeField = "IndexEpisodeField";

        // Marker values
        public const string DefaultMarkerValue = "[]";    // Default is the empty Json value 
    }

    // Names of standard database columns, always included but not always made visible in the user controls
    public static class DatabaseColumn
    {
        public const string ID = "Id";

        // columns in ImageDataTable
        public const string DateTime = "DateTime";
        public const string File = "File";
        public const string DeleteFlag = "DeleteFlag";
        public const string RelativePath = "RelativePath";

        // columns in MetadataTables
        public const string FolderDataPath = "FolderDataPath";

        // columns in ImageSetTable
        public const string Log = "Log";                    // String holding a user-created text log
        public const string RootFolder = "RootFolder";       // String holding the root folder containing the template
        public const string MostRecentFileID = "Row";       // ID of the last image displayed. It used to hold the current row #, but we repurposed ROW to  hold the MostRecentFileID as it simplifies backwards compatability
        public const string VersionCompatibility = "VersionCompatabily";      // The latest version of Timelapse that opened this database. Useful for cases when we want to check for backwards compatability
        public const string BackwardsCompatibility = "BackwardsCompatibility"; // The earliest version of Timelapse that is backwards compatable with this database.
        public const string SortTerms = "SortTerms";                     // a comma-separated list that indicates the Primary 1st and 2nd sort terms and their attribute
        public const string QuickPasteTerms = "QuickPasteTerms";              // a JSON description that specifies the user's quickpaste entries and values.
        public const string SearchTerms = "SearchTerms";              // a JSON description storing the current search terms
        public const string BoundingBoxDisplayThreshold = "BBDisplayThreshold";
        public const string Standard = "Standard";

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
        public const string MetadataTemplate = "FolderDataTemplateTable"; // the table containing the folder data template data
        public const string MetadataInfo = "FolderDataInfo";         // the table containing info about each level in the folder data template
        public const string TemplateInfo = "TemplateInfo"; // the table containing info about the template
        public const string FileData = "DataTable";         // the table containing the image data
        public const string ImageSet = "ImageSetTable"; // the table containing information common to the entire image set
        public const string Markers = "MarkersTable";         // the table containing the marker data
        public const string Info = "Info";
        public const string DetectionCategories = "DetectionCategories";
        public const string ClassificationCategories = "ClassificationCategories";
        public const string Images = "Images";
        public const string Detections = "Detections";
        public const string DetectionsVideo = "DetectionsVideo";
        public const string Classifications = "Classifications"; // Kepty only for backwards compatability i.e., to update DB to merge old classification tables into the detection table

    }

    // Default Settings
    public static class Defaults
    {
        public const string MainWindowBaseTitle = "Timelapse: Helping You Analyze Images and Videos";
        public const int NumberOfMostRecentDatabasesToTrack = 9;
        public const string StandardColour = "Gold";
        public const string SelectionColour = "MediumBlue";
    }

    public static class EcoAssist
    {
        // Legacy stuff, can remove in later versions
        public const string EcoAssistSubfolderExecutable = @"EcoAssist_files\EcoAssist\open.bat";
    }

    public static class AddaxAI
    {
        public const string AddaxAIDownload = @"https://addaxdatascience.com/addaxai-windows/";
        public const string AddaxAIUninstallDownload = @"https://addaxdatascience.com/windows-uninstall/";
        public const string AddaxAISubfolderExecutable = @"AddaxAI_files\AddaxAI\open.bat";
    }

    public static class EpisodeDefaults
    {
        public static readonly double TimeThresholdDefault = 2; // 2 Minutes - the default
        public static readonly double TimeThresholdMinimum = 0.0166666667; // 1 second miminum
        public static readonly double TimeThresholdMaximum = 30; // 30 minutes max
        public static readonly int DefaultRangeToSearch = 1000; // How many files ahead / behind to search for the episode limits
        public static readonly int MaximumRangeToSearch = 10000; // How many files ahead / behind to search for the episode limits
    }

    // External URLs
    public static class ExternalLinks
    {
        public const string TimelapseHomePage = "https://timelapse.ucalgary.ca";
        public const string TimelapseGuidesPage = TimelapseHomePage + "/Guides";
        public const string TimelapseFAQPage = TimelapseHomePage + "/faq";
        public const string TimelapseVideosPage = TimelapseHomePage + "/Videos";
        public const string TimelapseDownloadPage = TimelapseHomePage + "/Download";
        public const string TimelapseUploadLink = TimelapseHomePage + "/wp-content/uploads/";

        // Guides
        public const string TimelapseGuideUploadLink = TimelapseUploadLink + "Guides";
        public const string TimelapseGuideQuickStart = TimelapseGuideUploadLink + "/TimelapseQuickStartGuide.pdf";
        public const string TimelapseGuideReference = TimelapseGuideUploadLink + "/TimelapseReferenceGuide.pdf";
        public const string TimelapseGuideTemplate = TimelapseGuideUploadLink + "/TimelapseTemplateGuide.pdf";
        public const string TimelapseGuideMetadata = TimelapseGuideUploadLink + "/TimelapseMetadataGuide.pdf";
        public const string TimelapseGuideImageRecognition = TimelapseGuideUploadLink + "/TimelapseImageRecognitionGuide.pdf";
        public const string TimelapseGuideDatabase = TimelapseGuideUploadLink + "/TimelapseDatabaseGuide.pdf";

        // Videos - Companions to the above Guides
        public const string TimelapseVideosUploadLink = TimelapseUploadLink + "Videos";
        public const string TimelapseVideosQuickStart = TimelapseVideosUploadLink + "/QuickStartGuide.mp4";
        public const string TimelapseVideosTemplateEditor = TimelapseVideosUploadLink + "/TemplateGuide.mp4";
        public const string TimelapseVideosImageRecognition = TimelapseVideosUploadLink + "/ImageRecognitionGuide.mp4";

        // Videos - Presentations
        public const string TimelapseVideosWhirlwindTourOfTimelapse = TimelapseVideosUploadLink + "/WhirlwindTourOfTimelapse.mp4";

        public const string TimelapseVideoImageRecognitionPresentation =
            "http://grouplab.cpsc.ucalgary.ca/grouplab/uploads/Publications/Publications/2021-05-ImageRecognition-Video.mp4";
    // Videos - Lessons
    public const string TimelapseVideosInstallingTimelapse = TimelapseVideosUploadLink + "/Lesson-InstallingTimelapse.mp4";
    public const string TimelapseVideosIncrementallyAddingImages = TimelapseVideosUploadLink + "/Lesson-IncrementallyAddingImages.mp4";
    public const string TimelapseVideosViewingVideos = TimelapseVideosUploadLink + "/Lesson-ViewingVideos.mp4";
    public const string TimelapseVideosQuickPaste = TimelapseVideosUploadLink + "/Lesson-QuickPaste.mp4";
    public const string TimelapseVideosDuplicatingRecords = TimelapseVideosUploadLink + "/Lesson-DuplicatingRecords.mp4";
    public const string TimelapseVideosUsingAddaxAI = TimelapseVideosUploadLink + "/Lesson-UsingEcoAssist.mp4";
    public const string TimelapseVideosImageRecognitionForVideos = TimelapseVideosUploadLink + "/Lesson-ImageRecognitionForVideos.mp4";
    public const string TimelapseVideosUsingTheOverview = TimelapseVideosUploadLink + "/Lesson-UsingTheOverview.mp4";
    public const string TimelapseVideosRandomSampling = TimelapseVideosUploadLink + "/Lesson-RandomSampling.mp4";

    public static readonly Uri TimlapseVersionChangesLink = new("https://timelapse.ucalgary.ca/versions");
    public static readonly Uri CreativeCommonsLicenseLink = new("https://creativecommons.org/licenses/by-nc-sa/4.0/");
    public static readonly Uri AdditionalLicenseDetailsLink = new("https://github.com/saulgreenberg/Timelapse/blob/master/LICENSE.md");

    // Videos - Presentations
    public const string TimelapseVideosImageRecognitionTalk = TimelapseVideosUploadLink + "/ImageRecognitionForCameraTraps.mp4";

    public static readonly string EmailAddress = "saul@ucalgary.ca";
    public static readonly string GmailAddress = "saul.greenberg@gmail.com";

    // Camtrap
    public static readonly string CamtrapWebSite = "https://camtrap-dp.tdwg.org";
    public static readonly string CamtrapWebSiteValidation = "https://camtrap-dp.tdwg.org/#validation";
}
    public static class File
    {
        public const string AviFileExtension = ".avi";
        public const string BackupFolder = "Backups"; // Sub-folder that will contain database and csv file backups  
        public const string BackupCheckpointIndicator = ".Checkpoint-"; // string added to the backup file path for special backup files

        public const string CamtrapDPExportFolder = "CamtrapDPExportedFiles";
        public const string CamtrapDPDataPackageJsonFilename = "datapackage.json";
        public const string CamtrapDPDeploymentCSVFilename = "deployments.csv";
        public const string CamtrapDPMediaCSVFilename = "media.csv";
        public const string CamtrapDPObservationsCSVFilename = "observations.csv";
        public const string CsvFileExtension = ".csv";
        public const string CsvExportFolder = "CsvExportedFiles";
        public const string CSVImageDataFileName = "ImageData.csv";
        public const string DeletedFilesFolder = "DeletedFiles"; // Sub-folder that will contain backups of deleted images 
        public const string DefaultFileDatabaseFileName = DefaultFileDatabaseFileNameRoot + FileDatabaseFileExtension;
        public const string DefaultFileDatabaseFileNameRoot = "TimelapseData";
        public const string DefaultTemplateDatabaseFileName = "TimelapseTemplate.tdb";
        public const string FileDatabaseFileExtension = ".ddb";
        public const string JpgFileExtension = ".jpg";
        public const string JpegFileExtension = ".jpeg";
        public const string JsonFileExtension = ".json";
        public const string MovFileExtension = ".mov";
        public const string Mp4FileExtension = ".mp4";
        public const string ASFFileExtension = ".asf";
        public const string MacOSXHiddenFilePrefix = "._";
        public const int NumberOfBackupFilesToKeep = 8; // Maximum number of backup files to keep
        public const string RecognitionJsonDataFileName = "recognitionData.json";
        public const string NetworkRecycleBin = "@Recycle";
        public const string TemplateDatabaseFileExtension = ".tdb";
        public const string VideoThumbnailFolderName = ".vthumb";
        public const int MaxPathLength = 259; // One less than the permissable length of 260, as I'm not sure how the null at the end of as string is counted

        public static readonly TimeSpan BackupInterval = TimeSpan.FromMinutes(30);

        public const string TraceFile = "TimelapseErrorReport.txt"; // File name for file containing debug information. Usually written in the same folder containing the template.
    }

    // Default settings for the FilePlayer
    public static class FilePlayerValues
    {
        public static readonly TimeSpan PlaySlowMinimum = TimeSpan.FromMilliseconds(500.0);
        public static readonly TimeSpan PlaySlowDefault = TimeSpan.FromMilliseconds(500.0);
        public static readonly TimeSpan PlaySlowMaximum = TimeSpan.FromMilliseconds(5000.0);

        public static readonly TimeSpan PlayFastMinimum = TimeSpan.FromMilliseconds(40.0);
        public static readonly TimeSpan PlayFastDefault = TimeSpan.FromMilliseconds(100.0);
        public static readonly TimeSpan PlayFastMaximum = TimeSpan.FromMilliseconds(500.0);
    }

    public static class ImageValues
    {
        public const int BitmapCacheSize = 9;

        // The default threshold where the ratio of pixels below a given darkness in an image is used to determine whether the image is classified as 'dark'
        public const double DarkPixelRatioThresholdDefault = 0.9;
        public const int DarkPixelSampleStrideDefault = 20;
        // The default threshold where a pixel color should be considered as 'dark' when checking image darkness. The Range is 0  (black) - 255 (white)
        public const int DarkPixelThresholdDefault = 60;

        // The threshold to determine differences between images
        public const int DifferenceThresholdDefault = 20;
        public const byte DifferenceThresholdMax = 255;
        public const byte DifferenceThresholdMin = 0;

        // A greyscale image (given the above slop) will typically have about 90% of its pixels as grey scale
        public const double GreyscaleImageThreshold = 0.9;
        // A grey scale pixel has r = g = b. But we will allow some slop in here just in case a bit of color creeps in
        public const int GreyscalePixelThreshold = 40;

        // The number of images that are considered large enough to deserve special treatment on deletion
        public const int LargeNumberOfDeletedImages = 30;

        // Image sizes
        public const int PreviewWidth480 = 480;
        public const int PreviewWidth384 = 384;
        public const int PreviewWidth128 = 128;
        public const int PreviewWidth32 = 32;

        public static readonly Lazy<BitmapImage> Corrupt = Load("Corrupted.jpg");
        public static readonly Lazy<BitmapImage> FileNoLongerAvailable = Load("FileNoLongerAvailable.jpg");
        public static readonly Lazy<BitmapImage> NoFilesAvailable = Load("NoFilesAvailable.jpg");
        public static readonly Lazy<BitmapImage> LoadAnImageSet = Load("LoadAnImageSet.jpg");
        public static readonly Lazy<BitmapImage> BlankVideo = Load("BlankVideo.jpg");
        public static readonly Lazy<BitmapImage> FilePathTooLong = Load("FilePathTooLong.jpg");

        private static Lazy<BitmapImage> Load(string fileName)
        {
            return new(() =>
            {
                // if the requested image is available as an application resource, prefer that
                if (Application.Current != null && Application.Current.Resources.Contains(fileName))
                {
                    return (BitmapImage)Application.Current.Resources[fileName];
                }

                // if it's not (editor, resource not listed in App.xaml) fall back to loading from the resources assembly
                BitmapImage image = new();
                image.BeginInit();
                string location = "pack://application:,,/Resources/" + fileName;
                image.UriSource = new(location);
                image.EndInit();
                image.Freeze();
                return image;
            });
        }
    }


    public static class MarkableCanvas
    {
        public const double ImageZoomMaximum = 10.0;   // User configurable maximum amount of zoom in a display image
        public const double ImageZoomMaximumRangeAllowed = 50.0;   // the highest zoom a user can configure for a display image
        public const double ImageZoomMinimum = 1.0;   // Minimum amount of zoom
        public const double ImageZoomStep = 1.2;   // Amount to scale on each increment

        public const double MagnifyingGlassDefaultZoom = 60;
        public const double MagnifyingGlassMaximumZoom = 15;  // Max is a smaller number
        public const double MagnifyingGlassMinimumZoom = 300; // Min is the larger number
        public const double MagnifyingGlassZoomIncrement = 5;

        public const int MagnifyingGlassDiameter = 250;
        public const int MagnifyingGlassHandleStart = 200;
        public const int MagnifyingGlassHandleEnd = 250;

        public const double OffsetLensDefaultZoom = .4;
        public const double OffsetLensMaximumZoom = 0.9;
        public const double OffsetLensMinimumZoom = 0.2;
        public const double OffsetLensZoomIncrement = .05;

        public const int MarkerDiameter = 10;
        public const int MarkerGlowDiameterIncrease = 14;
        public const double MarkerGlowOpacity = 0.35;
        public const int MarkerGlowStrokeThickness = 7;
        public const int MarkerStrokeThickness = 2;

        public const string BoundingBoxCanvasTag = "BoundingBoxCanvas";

        // Threshold for a double click duration and for differentiating between marking and panning
        public static readonly TimeSpan DoubleClickTimeThreshold = TimeSpan.FromMilliseconds(250.0);
        public static readonly double MarkingVsPanningDistanceThreshold = 2.0;
    }

    public static class RegExExpressions
    {
        public const string NotDecimalCharacters = @"[^0-9\-\.]";
        public const string NotDecimalPositiveCharacters = @"[^0-9\.]";
        public const string NotIntegerCharacters = @"[^0-9\-]";
        public const string NotDigitCharacters = @"[^0-9]";
        public const string NotIntegerPositiveCharacters = NotDigitCharacters;
        public const string NotLetterDigitUnderscoreCharacters = @"[^a-zA-Z0-9_]";
        public const string NotAlphaNumericDashUnderscoresCharacters = @"[^a-zA-Z0-9_\-]";
        public const string NotAlphaNumericDashUnderscoresGlobCharacters = @"[^a-zA-Z0-9_\-\*\?]";
        public const string NotDateTimeCharacters = @"[^0-9:\-]";
        public const string NotDateCharacters = @"[^0-9\-]";
        public const string NotTimeCharacters = @"[^0-9:]";
        public const string NotEpisodeCharacters = @"^[0-9]+:[0-9]+\|[0-9]+$";
    }

    // Various keys used to access and save state in the Windows Registry
    public static class WindowRegistryKeys
    {
        // Defines the KEY path under HKEY_CURRENT_USER where Timelapse registry information is stored
        public const string RootKey = @"Software\Greenberg Consulting\Timelapse\2.0";

        // Magnifying / Offset lens glass
        public const string MagnifyingGlassOffsetLensEnabled = "MagnifyingGlassOffsetLensEnabled";
        public const string MagnifyingGlassZoomFactor = "MagnifyingGlassZoomFactor";
        public const string OffsetLensZoomFactor = "OffsetLensZoomFactor";

        // Various Recognition-related values

        public const string BoundingBoxAnnotate = "BoundingBoxAnnotate";
        public const string BoundingBoxColorBlindFriendlyColors = "BoundingBoxColorBlindFriendlyColors";
        public const string UseDetections = "UseDetections";


        // key containing the size the Template Editor Window, as a point
        // the top left location of the Template Editor Window will be the same as the Timelapse Window
        public const string TemplateEditorWindowSize = "TemplateEditorWindowSize";

        // Whether to automatically update the recognition counts in the CustomSelect dialog, or to wait until the user clicks the update button.
        // This is because updating the recognition counts can be time consuming for large image sets, so some users may prefer to only update them when they choose to.
        public const string AutoUpdateRecognitionCounts = "AutoUpdateRecognitionCounts";

        // key containing the top left location of the Timelapse Window, as a point
        // key containing the size of the Timelapse Window, as a Height
        public const string TimelapseWindowPosition = "TimelapseWindowPosition";

        // key containing the position of the QuickPaste window, as a rect
        public const string QuickPasteWindowPosition = "QuickPasteWindowPosition";

        // most recently used operator for custom selections
        public const string CustomSelectionTermCombiningOperator = "CustomSelectionTermCombiningOperator";

        // Format and column options when writing the date to the CSV file
        public const string CSVDateTimeOptions = "CSVDateTimeOptions2";
        public const string CSVInsertSpaceBeforeDates = "CSVInsertSpaceBeforeDates";
        public const string CSVIncludeFolderColumn = "CSVIncludeFolderColumns";

        // DarkPixelThreshold and Ratio used to determine image darkness
        public const string DarkPixelThreshold = "DarkPixelThreshold";
        public const string DarkPixelRatio = "DarkPixelRatio";

        // How the DeleteFolder is managed (e.g.,manual, by asking, or automatic deletion)
        public const string DeleteFolderManagementValue = "DeleteFolderManagement";

        public const string EpisodeTimeThreshold = "EpisodeTimeThreshold";
        public const string EpisodeMaxRangeToSearch = "EpisodeMaxFilesToSearch";

        // File Player play speeds (slow and fast)
        public const string FilePlayerSlowValue = "FilePlayerSlowValue";
        public const string FilePlayerFastValue = "FilePlayerFastValue";

        // Rendering image speed
        public const string DesiredImageRendersPerSecond = "DesiredImageRendersPerSecond";

        // The date/time the last check for timelapse updates was done (used to decide whether to check for updates)
        public const string MostRecentCheckForUpdates = "MostRecentCheckForUpdates2";

        // Metadata: whether to ask for metadata / datalabel pairing when loading new files
        public const string ImageMetadataAskOnLoad = "ImageMetadataAskOnLoad";

        // list of most recently image sets opened by Timelapse
        public const string RecentlyOpenedTemplateFiles = "RecentlyOpenedTemplateFiles";

        // Set Bookmark scale and transform coordinates
        public const string BookmarkScaleX = "BookmarkScaleX";
        public const string BookmarkScaleY = "BookmarkScaleY";
        public const string BookmarkTranslationX = "BookmarkTransformX";
        public const string BookmarkTranslationY = "BookmarkTransformY";

        // dialog opt outs
        public const string SuppressAmbiguousDatesDialog = "SuppressAmbiguousDatesDialog";
        public const string SuppressCsvExportDialog = "SuppressCsvExportDialog";
        public const string SuppressCsvImportPrompt = "SuppressCsvImportPrompt";
        public const string SuppressHowDuplicatesWorkDialog = "SuppressHowDuplicatesWorkDialog";
        
        public const string SuppressOpeningMessageDialog = "SuppressOpeningMessageDialog";
        public const string SuppressOpeningWithOlderTimelapseVersionDialog = "SuppressOpeningWithOlderTimelapseVersionDialog";
        public const string SuppressPropagateFromLastNonEmptyValuePrompt = "SuppressPropagateFromLastNonEmptyValuePrompt";
        public const string SuppressSelectedAmbiguousDatesPrompt = "SuppressSelectedAmbiguousDatesPrompt";
        public const string SuppressSelectedCsvExportPrompt = "SuppressSelectedCsvExportPrompt";
        public const string SuppressSelectedDarkThresholdPrompt = "SuppressSelectedDarkThresholdPrompt";
        public const string SuppressSelectedDateTimeFixedCorrectionPrompt = "SuppressSelectedDateTimeFixedCorrectionPrompt";
        public const string SuppressSelectedDateTimeLinearCorrectionPrompt = "SuppressSelectedDateTimeLinearCorrectionPrompt";
        public const string SuppressSelectedDaylightSavingsCorrectionPrompt = "SuppressSelectedDaylightSavingsCorrectionPrompt";
        public const string SuppressSelectedPopulateFieldFromMetadataPrompt = "SuppressSelectedPopulateFieldFromMetadataPrompt";
        public const string SuppressSelectedRereadDatesFromFilesPrompt = "SuppressSelectedRereadDatesFromFilesPrompt";
        public const string SuppressShortcutDetectedPrompt = "SuppressShortcutDetectedPrompt";
        public const string SuppressWarningToUpdateDBFilesToSQLPrompt = "SuppressWarningToUpdateDBFilesToSQL";

        // TabOrderInclude
        public const string TabOrderIncludeDateTime = "TabOrderIncludeDateTime";
        public const string TabOrderIncludeDeleteFlag = "TabOrderIncludeDeleteFlag";

        public const string VideoAutoPlay = "VideoAutoPlay";
        public const string VideoSpeed = "VideoSpeed";
        public const string VideoRepeat = "VideoRepeat";
        public const string VideoMute = "VideoMute";

    }

    public static class SearchTermOperator
    {
        public const string Equal = "\u003D";
        public const string Glob = " GLOB ";
        public const string NotGlob = " ^GLOB ";
        public const string Includes = " Includes ";
        public const string Excludes = " Excludes ";
        public const string GreaterThan = "\u003E";
        public const string GreaterThanOrEqual = "\u2265";
        public const string LessThan = "\u003C";
        public const string LessThanOrEqual = "\u2264";
        public const string NotEqual = "\u2260";
    }

    public static class SortTermValues
    {
        public const string NoneDisplayLabel = "-- None --";
        public const string DateDisplayLabel = "Date/Time";
        public const string FileDisplayLabel = "File Path (relative path + file name)";
        public const string RelativePathDisplayLabel = "Relative Path (of image sub-folders)";

        public const string DateStatusBarLabel = "Date/Time{0}";
        public const string FileStatusBarLabel = "File Path{0}";
        public const string IDStatusBarLabel = "Id{0} (the order files were added to Timelapse)";
    }

    public static class Standards
    {
        public const string AlbertaMetadataStandard = "AlbertaMetadataStandard";
        public const string CamtrapDPStandard = "CamtrapDP";
    }
    public static class ThrottleValues
    {
        public const double DesiredMaximumImageRendersPerSecondLowerBound = 3.0;     // Likely a very safe render rate 
        public const double DesiredMaximumImageRendersPerSecondDefault = 7.0;   // Default render rate - could exhibit stalls on poor machines
        public const double DesiredMaximumImageRendersPerSecondUpperBound = 15.0;    // Somewhat riskier render rate that I know works on high end machines without stuttering
        public const int MaximumRenderAttempts = 10;

        public static readonly TimeSpan PollIntervalForVideoLoad = TimeSpan.FromMilliseconds(1.0);
        public static readonly TimeSpan RenderingBackoffTime = TimeSpan.FromMilliseconds(25.0);
        public static readonly TimeSpan ProgressBarSleepInterval = TimeSpan.FromMilliseconds(10.0);
        public static readonly TimeSpan DataGridTimerInterval = TimeSpan.FromMilliseconds(250);
        public static readonly TimeSpan ProgressBarRefreshInterval = TimeSpan.FromMilliseconds(250);
    }

    public static class ThumbnailGrid
    {
        public static readonly int MaxRows = 15;
        public static readonly double AspectRatioDefault = 1.77777777777778;
        public static readonly double MinumumThumbnailHeight = 96;
    }
    public static class Time
    {
        // Dates
        public const string DateDisplayFormat = "dd-MMM-yyyy";
        public const string DateDisplayRelaxedFormat = "d-MMM-yyyy";
        public const string DateDatabaseFormat = "yyyy-MM-dd";
        public static readonly string[] DateDisplayFormats =
        [
            DateDisplayFormat,
            DateDisplayRelaxedFormat
        ];

        public static readonly string[] DateDatabaseAndDisplayFormats =
        [
            DateDisplayFormat,
            DateDatabaseFormat
        ];

        // Time
        public const string TimeInputFormat = "HH:mm:ss"; // format for Time in Select dialog

        // DateTime
        // DateTimes as stored in the database. The 2nd form is so it can read in from the DB both the legacy (UTC) format and current simpler format 
        public const string DateTimeDatabaseLegacyUTCFormat = "yyyy-MM-ddTHH:mm:ss.fffZ";  // legacy DatabaseFormat
        public const string DateTimeDatabaseFormat = "yyyy-MM-dd HH:mm:ss";
        public static readonly string[] DateTimeDatabaseFormats = [DateTimeDatabaseFormat, DateTimeDatabaseLegacyUTCFormat];
        public const string DateTimeDisplayFormat = "dd-MMM-yyyy HH:mm:ss";
        public const string DateTimeCSVWithTSeparator = "yyyy-MM-dd'T'HH:mm:ss";
        public const string DateTimeCSVWithoutTSeparator = "yyyy-MM-dd' 'HH:mm:ss";
        public static readonly string[] DateTimeDatabaseAndDisplayFormats =
        [
            DateTimeDisplayFormat,
            DateTimeDatabaseFormat
        ];

        // This is an SQL format for writing date/time, and is equivalent to the DateTimeDatabaseFormat
        //public static readonly string DateTimeSQLFormatForWritingTimelapseDB = "%Y-%m-%d %H:%M:%S";

        // known formats supported by Metadata Extractor. All these can be read without ambiguity
        public static readonly string[] DateTimeMetadataFormats =
        [
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
            "ddd MMM dd HH:mm:ss K yyyy" // File.File Modified Date_
        ];

        public const int MonthsInYear = 12;
        public const string TimeFormat = "HH:mm:ss";
        public const string RelaxedTimeFormat = "H:mm:ss";
        public static readonly string[] TimeFormats =
        [
            TimeFormat,
            RelaxedTimeFormat
        ];

        public const string VideoPositionFormat = @"mm\:ss";
    }

    public static class Unicode
    {
        public const string DownArrow = "\u2193";
        public const string Ellipsis = "\u2026";
        public const string UpArrow = "\u2191";
    }

    // Update Information, for checking for updates in the timelapse xml file stored on the web site
    public static class VersionUpdates
    {
        public const string ApplicationName = "Timelapse";
        public static readonly Uri LatestVersionBaseAddress = new("https://timelapse.ucalgary.ca/wp-content/uploads/Versions/timelapse_version.xml");
        public static readonly string LatestVersionFileNamePrefix = "timelapse_version_";
        public static readonly string LatestVersionFileNameSuffix = ".rtf";
        public static readonly Uri LatestVersionFileNameXML = new(LatestVersionBaseAddress, "timelapse_version.xml");
    }

    public static class VersionXml
    {
        public const string Changes = "changes";
        public const string Timelapse = "timelapse";
        public const string Url = "url";
        public const string Version = "version";
    }

    public static class XMP
    {
        public const string Namespace = "XMP-TimelapseData";
    }
    // DETECTION Columns and values  
    #region Detection Constants.
    public static class InfoColumns
    {
        public const string InfoID = "infoID";
        public const string Detector = "detector";
        public const string DetectorVersion = "megadetector_version";
        public const string DetectionCompletionTime = "detection_completion_time";
        public const string Classifier = "classifier";
        public const string ClassificationCompletionTime = "classification_completion_time";
        public const string TypicalDetectionThreshold = "typical_detection_threshold";
        public const string ConservativeDetectionThreshold = "conservative_detection_threshold";
        public const string TypicalClassificationThreshold = "typical_classification_threshold";
    }

    public static class DetectionCategoriesColumns
    {
        public const string Category = "category";
        public const string Label = "label";
    }

    public static class ClassificationCategoriesColumns
    {
        public const string Category = "classification";
        public const string Label = "label";
        public const string Description = "description";
    }

    public static class DetectionColumns
    {
        public const string DetectionID = "detectionID";
        public const string ImageID = DatabaseColumn.ID; // Foreign key
        public const string Category = "category";
        public const string Conf = "conf";
        public const string Classification = "classification";
        public const string ClassificationConf = "classification_conf";
        public const string BBox = "bbox";
        public const string FrameNumber = "frame_number";
        public const string FrameRate = "frame_rate";
    }

    public static class RecognizerValues
    {
        public const string NoDetectionCategory = "0";
        public const string AllDetectionCategoryNumber = "-1";
        public const string EmptyDetectionLabel = "Empty";
        public const string EmptyDetectionCategoryNumber = "0";
        public const string AllDetectionLabel = "All";

        // Detection/classifications whose confidence values are below these are removed from an
        // imported recognition file, as these are typically of little use. The determination of the actual value
        // is somewhat empirical. The code also checks the conservative_*_threshold; if its significantly higher,
        // it uses a fraction of that instead.
        public const float MinimumDetectionValue = 0.03F;
        public const float MinimumClassificationValue = 0.03F;
        public const int ConfidenceDecimalPlaces = 3;

        // Recognizer defaults. Different versions of Megadetector produce different confidence values.
        // The values below reflect  Megadetector v4, which is the likely detector if no overrides 
        // were set  in the Detection json file
        public const float DefaultTypicalDetectionThresholdIfUnknown = 0.2f;        // These values are recommend by Dan Morris
        public const float DefaultConservativeDetectionThresholdIfUnknown = 0.05f;   
        public const float DefaultTypicalClassificationThresholdIfUnknown = 0.5f; 
        public const float BoundingBoxDisplayThresholdDefault = Undefined;   // Appropriate for Megadetector v4
        public const string MDVersionUnknown = "vUnknown";
        public const float Undefined = -1F;

        public const string DetectorUnknown = "megadetector_unknown_version";
        public const string DetectionCompletionTimeUnknown = "unknown";
        public const string ClassifierUnknown = "ecosystem1_unknown_version";
        public const string ClassificationCompletionTimeUnknown = "unknown";
        public const string FormatVersionUnknown = "";
    }

    public static class ClassificationColumns
    {
        public const string ClassificationID = "classificationID";
        public const string DetectionID = DetectionColumns.DetectionID; // Foreign key
        public const string Category = "category";
        public const string Conf = "conf";
    }
    #endregion
}
