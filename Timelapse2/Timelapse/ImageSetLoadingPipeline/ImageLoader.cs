using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using Timelapse.Controls;
using Timelapse.Database;
using Timelapse.DataStructures;
using Timelapse.Enums;

namespace Timelapse.ImageSetLoadingPipeline
{
    /// <summary>
    /// Encapsulates the logic necessary to load an image from disk into the system.
    /// </summary>
    public class ImageLoader
    {
        #region Public Properties
        public string FolderPath => this.dataHandler.FileDatabase.FolderPath;

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
                if (this.bitmapSource == null)
                {
                    // Lazy load
                    var task = this.File.LoadBitmapAsync(this.FolderPath, ImageDisplayIntentEnum.Ephemeral, ImageDimensionEnum.UseWidth);
                    task.Wait();

                    var loadResult = task.Result;
                    this.bitmapSource = loadResult.Item1;
                }

                return this.bitmapSource;
            }
            private set => this.bitmapSource = value;
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
            this.RequiresDatabaseInsert = true;

            // Skip the per-file call to the database
            this.File = this.dataHandler.FileDatabase.FileTable.NewRow(this.fileInfo);
            this.File.RelativePath = this.relativePath;
            this.File.SetDateTimeFromFileInfo(this.FolderPath);

            return Task.Run(() =>
            {
                // Try to set the metadata fields specified in MetadataOnLoad, as well as the date from either the metadata or the file time depending on what is available
                if (GlobalReferences.TimelapseState.MetadataOnLoad != null && GlobalReferences.TimelapseState.MetadataOnLoad.SelectedMetadataDataLabels != null &&
                    GlobalReferences.TimelapseState.MetadataOnLoad.SelectedMetadataDataLabels.Count > 0)
                {
                    this.File.TryReadMetadataAndSetMetadataFields(this.FolderPath, GlobalReferences.TimelapseState.MetadataOnLoad);
                }

                // Try to update the datetime (which is currently recorded as the file's date) with the metadata date time the image was taken instead
                // Note that videos do not have these metadata fields
                // Strategy is to set date from either the metadata or the file time depending on what is available
                this.File.TryReadDateTimeOriginalFromMetadata(this.FolderPath);

                // This completes processing, but it may be some time before the task is checked for completion.
                // for purposes of reporting progress, call the completion delegate provided.
                OnImageLoadComplete?.Invoke();
            });
        }
        #endregion
    }
}
