//using MetadataExtractor;
//using MetadataExtractor.Formats.Exif;
//using MetadataExtractor.Formats.Exif.Makernotes;
using DialogUpgradeFiles.Enums;
//using DialogUpgradeFiles.Images;
using DialogUpgradeFiles.Util;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
//using MetadataDirectory = MetadataExtractor.Directory;

namespace DialogUpgradeFiles.Database
{
    /// <summary>
    /// Represents the data in a row in the file database describing a single image or video.
    /// Also returns the bitmap representing the image
    /// </summary>
    public class ImageRow : DataRowBackedObject
    {
        #region Public Properties - get /set  standard fields from the image row
        public string Date
        {
            get => this.Row.GetStringField(Constant.DatabaseColumn.Date);
            private set => this.Row.SetField(Constant.DatabaseColumn.Date, value);
        }

        // Set/Get the raw datetime value
        public DateTime DateTime
        {
            get => this.Row.GetDateTimeField(Constant.DatabaseColumn.DateTime);
            private set => this.Row.SetField(Constant.DatabaseColumn.DateTime, value);
        }

        // Get a version of the date/time suitable to display to the user 
        public string DateTimeAsDisplayable => DateTimeHandler.ToStringDisplayDateTime(this.DateTimeIncorporatingOffset);

        // Get the date/time with the UTC offset added into it
        public DateTimeOffset DateTimeIncorporatingOffset => DateTimeHandler.FromDatabaseDateTimeIncorporatingOffset(this.DateTime, this.UtcOffset);

        public bool DeleteFlag
        {
            get => this.Row.GetBooleanField(Constant.DatabaseColumn.DeleteFlag);
            set => this.Row.SetField(Constant.DatabaseColumn.DeleteFlag, value);
        }

        public string File
        {
            get => this.Row.GetStringField(Constant.DatabaseColumn.File);
            set => this.Row.SetField(Constant.DatabaseColumn.File, value);
        }

        public FileSelectionEnum ImageQuality
        {
            get => this.Row.GetEnumField<FileSelectionEnum>(Constant.DatabaseColumn.ImageQuality);
            set
            {
                switch (value)
                {
                    case FileSelectionEnum.Corrupted:
                    case FileSelectionEnum.Missing:
                    case FileSelectionEnum.Ok:
                    case FileSelectionEnum.Dark:
                        this.Row.SetField(Constant.DatabaseColumn.ImageQuality, value);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(ParamName,
                            $"{value} is not an ImageQuality.  ImageQuality must be one of CorruptFile, Dark, FileNoLongerAvailable, or Ok.");
                }
            }
        }

        public string Folder
        {
            get => this.Row.GetStringField(Constant.DatabaseColumn.Folder);
            set => this.Row.SetField(Constant.DatabaseColumn.Folder, value);
        }

        public string RelativePath
        {
            get => this.Row.GetStringField(Constant.DatabaseColumn.RelativePath);
            set => this.Row.SetField(Constant.DatabaseColumn.RelativePath, value);
        }

        public string Time
        {
            get => this.Row.GetStringField(Constant.DatabaseColumn.Time);
            private set => this.Row.SetField(Constant.DatabaseColumn.Time, value);
        }

        public TimeSpan UtcOffset
        {
            get => this.Row.GetUtcOffsetField(Constant.DatabaseColumn.UtcOffset);
            private set => this.Row.SetUtcOffsetField(Constant.DatabaseColumn.UtcOffset, value);
        }
        #endregion

        #region Private Constants
        private const string ParamName = "value";
        #endregion

        #region Constructors
        public ImageRow(DataRow row)
            : base(row)
        {
        }
        #endregion

        #region Public Methods - Various boolean tests
        public bool FileExists(string pathToRootFolder)
        {
            return System.IO.File.Exists(Path.Combine(pathToRootFolder, this.RelativePath, this.File));
        }

        //public virtual bool IsDisplayable(string pathToRootFolder)
        //{
        //    return BitmapUtilities.IsBitmapFileDisplayable(Path.Combine(pathToRootFolder, this.RelativePath, this.File));
        //}

        // This will be invoked only on an image file, so always returns false
        // That is, if its a video, an VideoRow would have been created and the IsVideo test method in that would have been invoked
        public virtual bool IsVideo => false;

        // Check if a datalabel is present in the ImageRow
        public bool Contains(string dataLabel)
        {
            return this.Row.Table.Columns.Contains(dataLabel);
        }
        #endregion

        #region Public Methods - Various Gets

        // Return a FileInfo to the full path of the file
        public FileInfo GetFileInfo(string rootFolderPath)
        {
            return new FileInfo(this.GetFilePath(rootFolderPath));
        }

        // Given the root folder path, 
        // return a full path to the file by combining the root folder path, the relative path, and the file name
        public string GetFilePath(string rootFolderPath)
        {
            // see RelativePath remarks in constructor
            return string.IsNullOrEmpty(this.RelativePath)
                ? Path.Combine(rootFolderPath, this.File)
                : Path.Combine(rootFolderPath, this.RelativePath, this.File);
        }

        // Given a data label, get its value as a string as it exists in the database
        public string GetValueDatabaseString(string dataLabel)
        {
            return (dataLabel == Constant.DatabaseColumn.DateTime)
               ? DateTimeHandler.ToStringDatabaseDateTime(this.DateTime)
               : this.GetValueDisplayString(dataLabel);
        }

        // Should be invoked only with the csvDateTimeOptions to one of the DateTime column formats
        public string GetValueCSVDateTimeWithTSeparatorString()
        {
            // Convert this.DateTime (a DateTimeOffset) to a DateTime, where we add in the offset amount
            return DateTimeHandler.ToStringCSVDateTimeWithTSeparator(new DateTime((this.DateTime + this.UtcOffset).Ticks));
        }

        // Should be invoked only with the csvDateTimeOptions to one of the DateTime column formats
        public string GetValueCSVDateTimeWithoutTSeparatorString()
        {
            // Convert this.DateTime (a DateTimeOffset) to a DateTime, where we add in the offset amount
            return DateTimeHandler.ToStringCSVDateTimeWithoutTSeparator(new DateTime((this.DateTime + this.UtcOffset).Ticks));
        }

        // DEFUNCT AS WE NO LONGER EXPORT OR IMPORT A CSV COLUMN IN THIS FOMRAT
        //public string GetValueCSVDateTimeUTCWithOffsetString()
        //{
        //    // calculate the date in zulu time 
        //    DateTime zuluTime = this.DateTime - this.UtcOffset;
        //    return DateTimeHandler.ToStringCSVUtcWithOffset(zuluTime, this.UtcOffset);
        //}

        // Given a data label, get its value as a string to display to the user in the UI
        // This requires a few values to be transformed (e.g., DateTime, UTCOffsets, ImageQuality)
        public string GetValueDisplayString(string dataLabel)
        {
            switch (dataLabel)
            {
                case Constant.DatabaseColumn.DateTime:
                    return this.DateTimeAsDisplayable;
                case Constant.DatabaseColumn.UtcOffset:
                    return DateTimeHandler.ToStringDatabaseUtcOffset(this.UtcOffset);
                //return DateTimeHandler.ToStringDisplayUtcOffset(this.UtcOffset);
                case Constant.DatabaseColumn.ImageQuality:
                    return this.ImageQuality.ToString();
                default:
                    return this.Row.GetStringField(dataLabel);
            }
        }
        #endregion

        #region Public Methods -Set Value from database string
        // Set the value for the column identified by its datalabel. 
        // We don't do this directly, as some values have to be converted
        public void SetValueFromDatabaseString(string dataLabel, string value)
        {
            switch (dataLabel)
            {
                case Constant.DatabaseColumn.DateTime:
                    this.DateTime = DateTimeHandler.ParseDatabaseDateTimeString(value);
                    break;
                case Constant.DatabaseColumn.UtcOffset:
                    this.UtcOffset = DateTimeHandler.ParseDatabaseUtcOffsetString(value);
                    break;
                case Constant.DatabaseColumn.ImageQuality:
                    // The parse succeeded, where the  result is in result
                    this.ImageQuality = Enum.TryParse(value, out FileSelectionEnum result) 
                        ? result 
                        : default; // The parse did not succeeded. The result contains the default enum value, ie, the same as returning default(Enum)
                    break;
                default:
                    this.Row.SetField(dataLabel, value);
                    break;
            }
        }
        #endregion

        #region Public Methods - Duplicate an Image Row object with its core values
        public ImageRow DuplicateRowWithCoreValues(ImageRow duplicate)
        {
            duplicate.File = this.File;
            duplicate.RelativePath = this.RelativePath;
            duplicate.Date = this.Date;
            duplicate.Time = this.Time;
            duplicate.DateTime = this.DateTime;
            duplicate.Folder = this.Folder;
            duplicate.ImageQuality = duplicate.ImageQuality;
            duplicate.UtcOffset = this.UtcOffset;
            return duplicate;
        }
        #endregion

        #region ColumnTuplesWithWhere - Create it based on the stock Image Row values of the current row
        // Build a ColumnTuplesWithWhere containing the stock column values from the current image row  
        // Where identifies the ID of the current image row - note that this is done in the GetDateTimeColumnTuples()
        public override ColumnTuplesWithWhere CreateColumnTuplesWithWhereByID()
        {
            ColumnTuplesWithWhere columnTuples = this.GetDateTimeColumnTuples();
            columnTuples.Columns.Add(new ColumnTuple(Constant.DatabaseColumn.File, this.File));
            columnTuples.Columns.Add(new ColumnTuple(Constant.DatabaseColumn.ImageQuality, this.ImageQuality.ToString()));
            columnTuples.Columns.Add(new ColumnTuple(Constant.DatabaseColumn.Folder, this.Folder));
            columnTuples.Columns.Add(new ColumnTuple(Constant.DatabaseColumn.RelativePath, this.RelativePath));
            return columnTuples;
        }

        // Build a ColumnTuplesWithWhere which will update the various Date / Time / Offset column values 
        // Where identifies the ID of the current image row
        public ColumnTuplesWithWhere GetDateTimeColumnTuples()
        {
            List<ColumnTuple> columnTuples = new List<ColumnTuple>(4)
            {
                new ColumnTuple(Constant.DatabaseColumn.Date, this.Date),
                new ColumnTuple(Constant.DatabaseColumn.DateTime, this.DateTime),
                new ColumnTuple(Constant.DatabaseColumn.Time, this.Time),
                new ColumnTuple(Constant.DatabaseColumn.UtcOffset, this.UtcOffset)
            };
            return new ColumnTuplesWithWhere(columnTuples, this.ID);
        }
        #endregion

        #region DateTime Methods - sets various date-related values, possibly using various transformation 
        public void SetDateTimeOffset(DateTimeOffset dateTime)
        {
            // Convert the dateTime to an offset of zero
            this.Date = DateTimeHandler.ToStringDisplayDate(dateTime);
            DateTimeOffset dto = new DateTimeOffset(dateTime.Ticks, TimeSpan.Zero);
            this.DateTime = dto.UtcDateTime;
            this.UtcOffset = TimeSpan.Zero;
            this.Time = DateTimeHandler.ToStringDisplayTime(dateTime);
        }

        public void SetDateTimeOffsetFromFileInfo(string folderPath)
        {
            // populate new image's default date and time
            // Typically the creation time is the time a file was created in the local file system and the last write time when it was
            // last modified ever in any file system.  So, for example, copying an image from a camera's SD card to a computer results
            // in the image file on the computer having a write time which is before its creation time.  Check both and take the lesser 
            // of the two to provide a best effort default.  In most cases it's desirable to see if a more accurate time can be obtained
            // from the image's EXIF metadata.
            FileInfo fileInfo = this.GetFileInfo(folderPath);
            DateTime earliestTimeLocal = fileInfo.CreationTime < fileInfo.LastWriteTime ? fileInfo.CreationTime : fileInfo.LastWriteTime;
            this.SetDateTimeOffset(new DateTimeOffset(earliestTimeLocal));
        }
        #endregion
    }
}
