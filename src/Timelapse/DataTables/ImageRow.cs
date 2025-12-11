using MetadataExtractor;
using MetadataExtractor.Formats.Exif;
using MetadataExtractor.Formats.Exif.Makernotes;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using Timelapse.Constant;
using Timelapse.DataStructures;
using Timelapse.DebuggingSupport;
using Timelapse.Enums;
using Timelapse.Extensions;
using Timelapse.Images;
using Timelapse.Standards;
using Timelapse.Util;
using Directory = System.IO.Directory;
using MetadataDirectory = MetadataExtractor.Directory;

namespace Timelapse.DataTables
{
    /// <summary>
    /// Represents the data in a row in the file database describing a single image or video.
    /// Also returns the bitmap representing the image
    /// </summary>
    public class ImageRow(DataRow row) : DataRowBackedObject(row)
    {
        #region Public Properties - get /set  standard fields from the image row

        // Set/Get the raw datetime value
        public DateTime DateTime
        {
            // There was still a UTCOffset conversion issues, so this kinda fixes it.
            // Original code is in comments
            //get { return (this.Row.GetDateTimeField(Constant.DatabaseColumn.DateTime)); }
            //private set { this.Row.SetField(Constant.DatabaseColumn.DateTime, value); }
            get => DateTime.SpecifyKind(Row.GetDateTimeField(DatabaseColumn.DateTime), DateTimeKind.Unspecified);
            private set => Row.SetField(DatabaseColumn.DateTime, DateTime.SpecifyKind(value, DateTimeKind.Unspecified));
        }

        // Get a version of the date/time suitable to display to the user 
        public string DateTimeAsDisplayable => DateTimeHandler.ToStringDisplayDateTime(DateTime);

        // Get the date/time  - This version is a null op!
        public DateTime DateTimeIncorporatingOffsetPLAINVERSION => DateTime;

        public bool DeleteFlag
        {
            get => Row.GetBooleanField(DatabaseColumn.DeleteFlag);
            set => Row.SetField(DatabaseColumn.DeleteFlag, value);
        }

        public string File
        {
            get => Row.GetStringField(DatabaseColumn.File);
            set => Row.SetField(DatabaseColumn.File, value);
        }

        public string RelativePath
        {
            get => Row.GetStringField(DatabaseColumn.RelativePath);
            set => Row.SetField(DatabaseColumn.RelativePath, value);
        }
        #endregion

        #region Public Methods - Various boolean tests
        public bool FileExists(string pathToRootFolder)
        {
            return System.IO.File.Exists(Path.Combine(pathToRootFolder, RelativePath, File));
        }

        public virtual bool IsDisplayable(string pathToRootFolder)
        {
            return BitmapUtilities.IsBitmapFileDisplayable(Path.Combine(pathToRootFolder, RelativePath, File));
        }

        // This will be invoked only on an image file, so always returns false
        // That is, if its a video, an VideoRow would have been created and the IsVideo test method in that would have been invoked
        public virtual bool IsVideo => false;

        // Check if a datalabel is present in the ImageRow
        public bool Contains(string dataLabel)
        {
            return Row.Table.Columns.Contains(dataLabel);
        }
        #endregion

        #region Public Methods - Various Gets

        // Return a FileInfo to the full path of the file
        public FileInfo GetFileInfo(string rootFolderPath)
        {
            return new(GetFilePath(rootFolderPath));
        }

        // Given the root folder path, 
        // return a full path to the file by combining the root folder path, the relative path, and the file name
        public string GetFilePath(string rootFolderPath)
        {
            // see RelativePath remarks in constructor
            return string.IsNullOrEmpty(RelativePath)
                ? Path.Combine(rootFolderPath, File)
                : Path.Combine(rootFolderPath, RelativePath, File);
        }

        // Given a data label, get its value as a string as it exists in the database
        public string GetValueDatabaseString(string dataLabel)
        {
            return (dataLabel == DatabaseColumn.DateTime)
               ? DateTimeHandler.ToStringDatabaseDateTime(DateTime)
               : GetValueDisplayString(dataLabel);
        }

        // Should be invoked only with the csvDateTimeOptions to one of the DateTime column formats
        public string GetValueCSVDateTimeWithTSeparatorString(bool insertSpaceBefore)
        {
            // Convert this.DateTime (a DateTimeOffset) to a DateTime, where we add in the offset amount
            string prefix = insertSpaceBefore ? " " : string.Empty;
            return $"{prefix}{DateTimeHandler.ToStringCSVDateTimeWithTSeparator(DateTime)}";
        }

        // Should be invoked only with the csvDateTimeOptions to one of the DateTime column formats
        public string GetValueCSVDateTimeWithoutTSeparatorString(bool insertSpaceBefore)
        {
            // Convert this.DateTime (a DateTimeOffset) to a DateTime
            string prefix = insertSpaceBefore ? " " : string.Empty;
            return $"{prefix}{DateTimeHandler.ToStringCSVDateTimeWithoutTSeparator(DateTime)}";
        }

      public string GetValueCSVDateString(bool insertSpaceBefore)
        {
            string prefix = insertSpaceBefore ? " " : string.Empty;
            // Convert this.DateTime to a displayable Date for the CSV file
            return $"{prefix}{DateTimeHandler.ToStringDisplayDatePortion(DateTime)}";
        }

        public string GetValueCSVTimeString(bool insertSpaceBefore)
        {
            string prefix = insertSpaceBefore ? " " : string.Empty;
            // Convert this.DateTime to a displayable Date for the CSV file
            return $"{prefix}{DateTimeHandler.ToStringTime(DateTime)}";
        }

        // Given a control and its data label, get its value as a string to display to the user in the UI
        // This requires DateTime to be transformed
        // Note that converting the various other Date control values are handled by its SetContentAndTooltip method
        public string GetValueDisplayString(string dataLabel)
        {
            return dataLabel switch
            {
                DatabaseColumn.DateTime => DateTimeAsDisplayable,
                _ => Row.GetStringField(dataLabel)
            };
        }
        #endregion

        #region Public Methods -Set Value from database string
        // Set the value for the column identified by its datalabel. 
        // We don't do this directly, as some values have to be converted
        public void SetValueFromDatabaseString(string dataLabel, string value)
        {
            switch (dataLabel)
            {
                case DatabaseColumn.DateTime:
                    DateTime = DateTimeHandler.TryParseDatabaseDateTime(value, out DateTime dateTime)
                        ? dateTime
                        : DateTime.MinValue;
                    break;
                default:
                    Row.SetField(dataLabel, value);
                    break;
            }
        }
        #endregion

        #region Public Methods - Duplicate an Image Row object with its core values and optionally with all other values
        public ImageRow DuplicateRowWithValues(ImageRow duplicate, bool retainValues, bool isCamtrapDPStandard)
        {
            // We should always copy the core standard values
            duplicate.File = File;
            duplicate.RelativePath = RelativePath;
            duplicate.DateTime = DateTime;

            // CamtrapDP standard: Whenever a duplicate image is created,
            // - its mediaID and fileMediaType should be identical to the original.
            // We only need to do it if retainValues is false, as if its true, we copy those values anyways
            if (isCamtrapDPStandard && retainValues == false)
            {
                duplicate.SetValueFromDatabaseString(CamtrapDPConstants.Media.MediaID, this.GetValueDatabaseString(CamtrapDPConstants.Media.MediaID));
                duplicate.SetValueFromDatabaseString(CamtrapDPConstants.Media.FileMediatype, this.GetValueDatabaseString(CamtrapDPConstants.Media.FileMediatype));
            }

            // We optionally copy all other values depending on the state of retainValues
            if (retainValues && null != Row?.ItemArray && Row.ItemArray.Clone() is object[] obj)
            {
                duplicate.Row.ItemArray = obj;

            }

            // CamtrapDP standard: Whenever a duplicate image is created, its obsservationID should always differ from the original.
            if (isCamtrapDPStandard)
            {
                duplicate.SetValueFromDatabaseString(CamtrapDPConstants.Observations.ObservationID, Guid.NewGuid().ToString());
            }

            return duplicate;
        }
        #endregion

        #region ColumnTuplesWithWhere - Create it based on the stock Image Row values of the current row
        // Build a ColumnTuplesWithWhere containing the stock column values from the current image row  
        // Where identifies the ID of the current image row - note that this is done in the GetDateTimeColumnTuples()
        public override ColumnTuplesWithWhere CreateColumnTuplesWithWhereByID()
        {
            ColumnTuplesWithWhere columnTuples = GetDateTimeColumnTuples();
            columnTuples.Columns.Add(new(DatabaseColumn.File, File));
            columnTuples.Columns.Add(new(DatabaseColumn.RelativePath, RelativePath));
            return columnTuples;
        }

        // Build a ColumnTuplesWithWhere which will update the various Date / Time column values 
        // Where identifies the ID of the current image row
        public ColumnTuplesWithWhere GetDateTimeColumnTuples()
        {
            List<ColumnTuple> columnTuples = new(4)
            {
                new(DatabaseColumn.DateTime, DateTime),
            };
            return new(columnTuples, ID);
        }
        #endregion

        #region DateTime Methods - sets various date-related values, possibly using various transformation 

        public void SetDateTime(DateTime dateTime)
        {
            // There was still a UTCOffset conversionissues, so this kinda fixes it.
            //this.DateTime = dateTime;
            DateTime = DateTime.SpecifyKind(dateTime, DateTimeKind.Unspecified);
        }

        public void SetDateTimeFromFileInfo(string folderPath)
        {
            // populate new image's default date and time
            // Typically the creation time is the time a file was created in the local file system and the last write time when it was
            // last modified ever in any file system.  So, for example, copying an image from a camera's SD card to a computer results
            // in the image file on the computer having a write time which is before its creation time.  Check both and take the lesser 
            // of the two to provide a best effort default.  In most cases it's desirable to see if a more accurate time can be obtained
            // from the image's EXIF metadata.
            FileInfo fileInfo = GetFileInfo(folderPath);
            DateTime earliestTime = fileInfo.CreationTime < fileInfo.LastWriteTime
                ? fileInfo.CreationTime
                : fileInfo.LastWriteTime;
            earliestTime = earliestTime.ToLocalTime();
            SetDateTime(earliestTime);
        }
        #endregion

        #region Public Methods - Metadata Reading
        // Try to populate the metadata/data fields specified in metadataOnLoad for the given file, for those metadata fields that exist 
        public void TryReadMetadataAndSetMetadataFields(string folderPath, ImageMetadataOnLoad metadataOnLoad)
        {
            try
            {
                Dictionary<string, ImageMetadata> metadata = [];

                if (metadataOnLoad.MetadataToolSelected == MetadataToolEnum.MetadataExtractor)
                {
                    // MetadataExtractor - specific code
                    metadata = ImageMetadataDictionary.LoadMetadata(GetFilePath(folderPath));
                }
                else // if metadataToolSelected == MetadataToolEnum.ExifTool
                {
                    // ExifTool specific code - we transform the ExifTool results into the same dictionary structure used by the MetadataExtractor
                    metadata.Clear();
                    Dictionary<string, string> exifData = GlobalReferences.TimelapseState.ExifToolManager.FetchExifFrom(GetFilePath(folderPath), metadataOnLoad.Tags);

                    foreach (KeyValuePair<string, string> kvp in exifData)
                    {
                        metadata.Add(kvp.Key, new(string.Empty, kvp.Key, kvp.Value));
                    }
                }

                // At this point, regardless of which metadata tool was used, we have all the information we need
                // to add the metadata to a datafield.
                foreach (KeyValuePair<string, string> kvp in metadataOnLoad.SelectedImageMetadataDataLabels)
                {
                    // Key is the metadata tag, Value is the data label
                    if (metadata.TryGetValue(kvp.Key, out var value))
                    {
                        Row.SetField(kvp.Value, value.Value);
                    }
                }
            }
            catch
            {
                // If the above fails, we just keep on going.
            }
        }

        public DateTimeAdjustmentEnum TryReadDateTimeOriginalFromMetadata(string folderPath)
        {
            // Use only on images, as video files don't contain the desired metadata. 
            try
            {
                IReadOnlyList<MetadataDirectory> metadataDirectories;

                // Performance tweaks. Reading in sequential scan, does this speed up? Under the covers, the MetadataExtractor is using a sequential read, allowing skip forward but not random access.
                // Exif is small, do we need a big block?
                using (FileStream fS = new(GetFilePath(folderPath), FileMode.Open, FileAccess.Read, FileShare.Read, 64, FileOptions.SequentialScan))
                {
                    metadataDirectories = ImageMetadataReader.ReadMetadata(fS);
                }

                ExifSubIfdDirectory exifSubIfd = metadataDirectories.OfType<ExifSubIfdDirectory>().FirstOrDefault();
                if (exifSubIfd == null)
                {
                    return DateTimeAdjustmentEnum.MetadataNotUsed;
                }

                if (exifSubIfd.TryGetDateTime(ExifDirectoryBase.TagDateTimeOriginal, out DateTime dateTimeOriginal) == false)
                {
                    // We couldn't read the metadata. In case its a reconyx camera, the fallback is to use the Reconyx-specific metadata 
                    ReconyxHyperFireMakernoteDirectory reconyxMakernote = metadataDirectories.OfType<ReconyxHyperFireMakernoteDirectory>().FirstOrDefault();
                    if ((reconyxMakernote == null) || (reconyxMakernote.TryGetDateTime(ReconyxHyperFireMakernoteDirectory.TagDateTimeOriginal, out dateTimeOriginal) == false))
                    {
                        return DateTimeAdjustmentEnum.MetadataNotUsed;
                    }
                }
                DateTime exifDateTime = dateTimeOriginal;

                // get the current date time
                DateTime currentDateTime = DateTime;
                // measure the extent to which the file time and 'image taken' metadata are consistent
                bool dateAdjusted = currentDateTime.Date != exifDateTime.Date;
                bool timeAdjusted = currentDateTime.TimeOfDay != exifDateTime.TimeOfDay;
                if (dateAdjusted || timeAdjusted)
                {
                    SetDateTime(exifDateTime);
                }

                // At least with several Bushnell Trophy HD and Aggressor models (119677C, 119775C, 119777C) file times are sometimes
                // indicated an hour before the image taken time during standard time.  This is not known to occur during daylight 
                // savings time and does not occur consistently during standard time.  It is problematic in the sense time becomes
                // scrambled, meaning there's no way to detect and correct cases where an image taken time is incorrect because a
                // daylight-standard transition occurred but the camera hadn't yet been serviced to put its clock on the new time,
                // and needs to be reported separately as the change of day in images taken just after midnight is not an indicator
                // of day-month ordering ambiguity in the image taken metadata.
                // NOTE: I Don't know if this is needed since we did the UTC eliminations
                bool standardTimeAdjustment = exifDateTime - currentDateTime == TimeSpan.FromHours(1);

                // snap to metadata time and return the extent of the time adjustment
                if (standardTimeAdjustment)
                {
                    return DateTimeAdjustmentEnum.MetadataDateAndTimeOneHourLater;
                }
                if (dateAdjusted && timeAdjusted)
                {
                    return DateTimeAdjustmentEnum.MetadataDateAndTimeUsed;
                }
                if (dateAdjusted)
                {
                    return DateTimeAdjustmentEnum.MetadataDateUsed;
                }
                if (timeAdjusted)
                {
                    return DateTimeAdjustmentEnum.MetadataTimeUsed;
                }
                return DateTimeAdjustmentEnum.SameFileAndMetadataTime;
            }
            catch
            {
                return DateTimeAdjustmentEnum.MetadataNotUsed;
            }
        }
        #endregion

        #region Delete File
        // Delete the file, where we also try to back it up by moving it into the Deleted folder
        // TODO File deletion backups is problematic as files in different relative paths could have the same file name (overwritting possible, ambiguity). Perhaps mirror the file structure as otherwise a previously deleted file could be overwritten
        // CODECLEANUP Should this method really be part of an image row? 
        public bool TryMoveFileToDeletedFilesFolder(string folderPath, bool backupDeletedFile)
        {
            string sourceFilePath = GetFilePath(folderPath);
            if (!System.IO.File.Exists(sourceFilePath))
            {
                return false;  // If there is no source file, its a missing file so we can't back it up
            }

            if (false == backupDeletedFile)
            {
                // just delete the file, as we aren't backing it up.
                try
                {
                    // delete the Destination file if it already exists.
                    FilesFolders.TryDeleteFileIfExists(sourceFilePath);
                    return true;
                }
                catch (UnauthorizedAccessException exception)
                {
                    TracePrint.PrintMessage("Could not delete " + sourceFilePath + Environment.NewLine + exception.Message + ": " + exception);
                    return false;
                }
            }

            // Create a new target folder, if necessary.
            string deletedFilesFolderPath = Path.Combine(folderPath, Constant.File.DeletedFilesFolder);
            if (!Directory.Exists(deletedFilesFolderPath))
            {
                Directory.CreateDirectory(deletedFilesFolderPath);
            }

            // Get the sub-folder path from the root folder to the image, so we can reconstruct its path
            string destinationFolderPath = Path.Combine(deletedFilesFolderPath, RelativePath);

            // If we can't create the destinaton folder sub-folder, we use the root delete folder
            string destinationFilePath = Path.Combine(deletedFilesFolderPath, destinationFolderPath, File);
            if (!Directory.Exists(destinationFolderPath))
            {
                try
                {
                    // If we can create the destinaton folder / sub-folder, the path is to that subfolder
                    Directory.CreateDirectory(destinationFolderPath);

                }
                catch (Exception exception)
                {
                    // If we can't create that folder, fall back to just putting the image into the main Deleted folder
                    TracePrint.PrintMessage("Could not create " + destinationFilePath + Environment.NewLine + exception.Message + ": " + exception);
                    destinationFilePath = Path.Combine(deletedFilesFolderPath, File);
                }
            }

            // Move the file to the backup location.           
            if (System.IO.File.Exists(destinationFilePath))
            {
                return FilesFolders.TryDeleteFileIfExists(destinationFilePath);
            }

            // A failure may occur if for some reason we could not move the file, for example, if we have loaded the image in a way that it locks the file.
            // I've changed image loading to avoid this, but its something to watch out for.
            return FilesFolders.TryMoveFileIfExists(sourceFilePath, destinationFilePath);
        }
        #endregion

        #region LoadBitmap - Various Forms
        // LoadBitmap Wrapper: defaults to full size image, Persistent. 
        public BitmapSource LoadBitmap(string baseFolderPath, out bool isCorruptOrMissing)
        {
            // ImageDimension doesn't do anything in this context, as the full size image is returned
            return LoadBitmap(baseFolderPath, null, ImageDisplayIntentEnum.Persistent, ImageDimensionEnum.UseWidth, out isCorruptOrMissing);
        }

        // LoadBitmap Wrapper: defaults to Persistent, Decode to the given width
        public virtual BitmapSource LoadBitmap(string baseFolderPath, int? desiredWidth, out bool isCorruptOrMissing)
        {
            return LoadBitmap(baseFolderPath, desiredWidth, ImageDisplayIntentEnum.Persistent, ImageDimensionEnum.UseWidth, out isCorruptOrMissing);
        }

        // LoadBitmap Wrapper: If Ephemeral, generate a low-res thumbnail suitable for previewing. Otherwise full size
        public virtual BitmapSource LoadBitmap(string baseFolderPath, ImageDisplayIntentEnum imageExpectedUsage, out bool isCorruptOrMissing)
        {
            return LoadBitmap(baseFolderPath,
                     imageExpectedUsage == ImageDisplayIntentEnum.Ephemeral ? ImageValues.PreviewWidth128 : null,
                     imageExpectedUsage,
                     ImageDimensionEnum.UseWidth,
                     out isCorruptOrMissing);
        }

        /// <summary>
        /// Async Wrapper for LoadBitmap
        /// </summary>
        /// <returns>Tuple of the BitmapSource and boolean isCorruptOrMissing output of the underlying load logic</returns>
        public virtual Task<Tuple<BitmapSource, bool>> LoadBitmapAsync(string baseFolderPath, ImageDisplayIntentEnum imageExpectedUsage, ImageDimensionEnum imageDimension)
        {
            // 'out' arguments not allowed in tasks, so it returns a tuple containg the bitmap and the isCorruptOrMissingflag flag indicating bitmap retrieval state 
            return Task.Run(() =>
            {
                BitmapSource bitmap = LoadBitmap(baseFolderPath, imageExpectedUsage == ImageDisplayIntentEnum.Ephemeral ? ImageValues.PreviewWidth128 : null,
                                               imageExpectedUsage,
                                               ImageDimensionEnum.UseWidth,
                                               out bool isCorruptOrMissing);
                return Tuple.Create(bitmap, isCorruptOrMissing);
            });
        }

        // Load: Full form
        // Get a bitmap of the desired width. If its not there or something is wrong it will return a placeholder bitmap displaying the 'error'.
        // Also sets a flag (isCorruptOrMissing) indicating if the bitmap wasn't retrieved (signalling a placeholder bitmap was returned)
        public virtual BitmapSource LoadBitmap(string rootFolderPath, int? desiredWidthOrHeight, ImageDisplayIntentEnum displayIntent, ImageDimensionEnum imageDimension, out bool isCorruptOrMissing)
        {
            // Invoke the static version. The only change is that we get the full file path and pass that as a parameter
            return BitmapUtilities.GetBitmapFromImageFile(GetFilePath(rootFolderPath), desiredWidthOrHeight, displayIntent, imageDimension, out isCorruptOrMissing);
        }

        // Return the aspect ratio (as Width/Height) of a bitmap or its placeholder as efficiently as possible
        // Timing tests suggests this can be done very quickly i.e., 0 - 10 msecs
        // While this is marked as virtual, there is currently no over-ride for getting it from a video.
        // So it should only be invoked if we know the file is an image
        public virtual double GetBitmapAspectRatioFromFile(string rootFolderPath)
        {
            return BitmapUtilities.GetBitmapAspectRatioFromImageFile(GetFilePath(rootFolderPath));
        }
        #endregion
    }
}
