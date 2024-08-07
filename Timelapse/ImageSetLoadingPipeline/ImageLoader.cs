using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using Timelapse.ControlsDataEntry;
using Timelapse.DataStructures;
using Timelapse.DataTables;
using Timelapse.Enums;

namespace Timelapse.ImageSetLoadingPipeline
{
    /// <summary>
    /// Encapsulates the logic necessary to load an image from disk into the system.
    /// </summary>
    public class ImageLoader
    {
        #region Public Properties
        public string FolderPath => dataHandler.FileDatabase.FolderPath;

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
                    var task = File.LoadBitmapAsync(FolderPath, ImageDisplayIntentEnum.Ephemeral, ImageDimensionEnum.UseWidth);
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

        #region Private Variables
        private readonly FileInfo fileInfo;
        private readonly DataEntryHandler dataHandler;
        private readonly string relativePath;
        #endregion

        #region Constructor
        public ImageLoader(string relativePath, FileInfo fileInfo, DataEntryHandler dataHandler)
        {
            this.fileInfo = fileInfo;
            this.dataHandler = dataHandler;
            this.relativePath = relativePath;
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
            File.SetDateTimeFromFileInfo(FolderPath);

            return Task.Run(() =>
            {
                // Try to set the metadata fields specified in ImageMetadataOnLoad, as well as the date from either the metadata or the file time depending on what is available
                if (GlobalReferences.TimelapseState.MetadataOnLoad != null && GlobalReferences.TimelapseState.MetadataOnLoad.SelectedImageMetadataDataLabels != null &&
                    GlobalReferences.TimelapseState.MetadataOnLoad.SelectedImageMetadataDataLabels.Count > 0)
                {
                    File.TryReadMetadataAndSetMetadataFields(FolderPath, GlobalReferences.TimelapseState.MetadataOnLoad);
                }

                // Try to update the datetime (which is currently recorded as the file's date) with the metadata date time the image was taken instead
                // Note that videos do not have these metadata fields
                // Strategy is to set date from either the metadata or the file time depending on what is available
                File.TryReadDateTimeOriginalFromMetadata(FolderPath);

                // This completes processing, but it may be some time before the task is checked for completion.
                // for purposes of reporting progress, call the completion delegate provided.
                OnImageLoadComplete?.Invoke();
            });
        }
        #endregion
    }
}
