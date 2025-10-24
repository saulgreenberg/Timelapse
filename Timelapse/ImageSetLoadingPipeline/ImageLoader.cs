using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using Timelapse.ControlsDataEntry;
using Timelapse.DataStructures;
using Timelapse.DataTables;
using Timelapse.Enums;
using Timelapse.Standards;

namespace Timelapse.ImageSetLoadingPipeline
{
    /// <summary>
    /// Encapsulates the logic necessary to load an image from disk into the system.
    /// </summary>
    public class ImageLoader(string relativePath, FileInfo fileInfo, DataEntryHandler dataHandler)
    {
        #region Public Properties
        public string RootPathToImages => dataHandler.FileDatabase.RootPathToImages;

        public bool RequiresDatabaseInsert
        {
            get;
            private set;
        }

        public ImageRow File
        {
            get;
            private set;
        }

        private BitmapSource bitmapSource;
        public BitmapSource BitmapSource
        {
            get
            {
                if (bitmapSource == null)
                {
                    // Lazy load
                    var task = File.LoadBitmapAsync(this.RootPathToImages, ImageDisplayIntentEnum.Ephemeral, ImageDimensionEnum.UseWidth);
                    task.Wait();

                    var loadResult = task.Result;
                    bitmapSource = loadResult.Item1;
                }

                return bitmapSource;
            }
            // ReSharper disable once UnusedMember.Local
            private set => bitmapSource = value;
        }
        #endregion

        #region LoadImageAsync
        public Task LoadImageAsync(Action OnImageLoadComplete)
        {
            // Set the loader's file member. 
            RequiresDatabaseInsert = true;

            // Skip the per-file call to the database
            File = dataHandler.FileDatabase.FileTable.NewRow(fileInfo);
            File.RelativePath = relativePath;
            File.SetDateTimeFromFileInfo(this.RootPathToImages);

            return Task.Run(() =>
            {
                // Try to set the metadata fields specified in ImageMetadataOnLoad, as well as the date from either the metadata or the file time depending on what is available
                if (GlobalReferences.TimelapseState.MetadataOnLoad != null && GlobalReferences.TimelapseState.MetadataOnLoad.SelectedImageMetadataDataLabels != null &&
                    GlobalReferences.TimelapseState.MetadataOnLoad.SelectedImageMetadataDataLabels.Count > 0)
                {
                    File.TryReadMetadataAndSetMetadataFields(this.RootPathToImages, GlobalReferences.TimelapseState.MetadataOnLoad);
                }

                // Try to update the datetime (which is currently recorded as the file's date) with the metadata date time the image was taken instead
                // Note that videos do not have these metadata fields
                // Strategy is to set date from either the metadata or the file time depending on what is available
                File.TryReadDateTimeOriginalFromMetadata(this.RootPathToImages);

                // CamtrapDP standard: Whenever a new image is loaded, we assign:
                // - a GUID to its mediaID
                // - a GUID to its observationID.
                // - a file type to its FileMediaType (e.g., image/jpeg, video/mp4, etc)
                // Doing it here means that duplicates, if created, will have the same mediaID
                // When creating the duplicate, we will change the observationID to a different GUID.
                if (dataHandler.FileDatabase.MetadataTablesIsCamtrapDPStandard())
                {
                   File.SetValueFromDatabaseString(CamtrapDPConstants.Media.MediaID, Guid.NewGuid().ToString());
                   File.SetValueFromDatabaseString(CamtrapDPConstants.Observations.ObservationID, Guid.NewGuid().ToString());
                   File.SetValueFromDatabaseString(CamtrapDPConstants.Media.FileMediatype, HelperGetFileMediaType(File.File));
                }

                // This completes processing, but it may be some time before the task is checked for completion.
                // for purposes of reporting progress, call the completion delegate provided.
                OnImageLoadComplete?.Invoke();
            });
        }

        private string HelperGetFileMediaType(string fileName)
        {
            string extension = System.IO.Path.GetExtension(fileName);
            string fileMediaType;
            if (string.Equals(extension, Constant.File.JpgFileExtension, StringComparison.OrdinalIgnoreCase) ||
               (string.Equals(extension, Constant.File.JpegFileExtension, StringComparison.OrdinalIgnoreCase)))
            {
                fileMediaType = "image/jpeg";
            }
            else
            {
                fileMediaType = "video";
                switch (extension.ToLower())
                {
                    case Constant.File.ASFFileExtension:
                        fileMediaType += $"/{Constant.File.ASFFileExtension.TrimStart('.')}";
                        break;
                    case Constant.File.AviFileExtension:
                        fileMediaType += $"/{Constant.File.AviFileExtension.TrimStart('.')}";
                        break;
                    case Constant.File.Mp4FileExtension:
                        fileMediaType += $"/{Constant.File.Mp4FileExtension.TrimStart('.')}";
                        break;
                    case Constant.File.MovFileExtension:
                        fileMediaType += $"/{Constant.File.MovFileExtension.TrimStart('.')}";
                        break;
                }
            }
            return fileMediaType;
        }
        #endregion
    }
}
