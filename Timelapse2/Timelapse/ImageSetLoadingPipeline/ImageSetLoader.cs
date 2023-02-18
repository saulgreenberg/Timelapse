using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Timelapse.Controls;
using Timelapse.DataStructures;
using Timelapse.DataTables;
using Timelapse.Util;

namespace Timelapse.ImageSetLoadingPipeline
{
    /// <summary>
    /// Encapsulates logic to load a set of images into the system.
    /// </summary>
    public class ImageSetLoader : IDisposable
    {
        #region Public Properties
        public ImageLoader LastLoadComplete
        {
            get;
            private set;
        }

        public ImageRow LastInsertComplete
        {
            get;
            private set;
        }

        public int LastIndexInsertComplete
        {
            get;
            private set;
        }

        public int ImagesLoaded => this.imagesLoaded;

        public int ImagesToLoad
        {
            get;
        }

        public List<string> ImagesSkippedAsFilePathTooLong { get; set; }
        #endregion

        #region Private Variables
        private int imagesLoaded;
        private int imagesToInsert;

        private readonly Task pass1;

        private readonly Task pass2;
        #endregion

        #region Construtor
        public ImageSetLoader(string imageSetFolderPath, IEnumerable<FileInfo> fileInfos, DataEntryHandler dataHandler)
        {
            // Check the arguments for null 
            ThrowIf.IsNullArgument(dataHandler, nameof(dataHandler));

            ImagesSkippedAsFilePathTooLong = new List<string>();

            // Don't add a file if it already exists in the database.
            // Rather than check every file one by one to see if it exists in the database 
            // - get all the current files in the database (as existing full paths) in a single database call,
            // - create a new file list (fileInfoArray) that only adds files (as fileInfos) that are NOT present in the database. 
            HashSet<string> existingPaths;
            using (FileTable filetable = dataHandler.FileDatabase.SelectAllFiles())
            {
                existingPaths = new HashSet<string>(from file in filetable
                                                    select Path.Combine(imageSetFolderPath, Path.Combine(file.RelativePath, file.File)).ToLowerInvariant());
            }
            FileInfo[] filesToAddInfoArray;
            filesToAddInfoArray = (from fileInfo in fileInfos
                                   where existingPaths.Contains(fileInfo.FullName.ToLowerInvariant()) == false
                                   select fileInfo).OrderBy(f => f.FullName).ToArray();

            this.ImagesToLoad = filesToAddInfoArray.Length;

            // The queue will take image rows ready for insertion to the second pass
            // The eventindicates explicitly when the first pass is done.
            ConcurrentQueue<ImageRow> databaseInsertionQueue = new ConcurrentQueue<ImageRow>();
            ConcurrentQueue<ImageRow> capturedDatabaseInsertionQueue = databaseInsertionQueue;
            // We trim as well, as it handles both the case where it already has a trailing \ and when it is missing it.
            // For example, if we opened a template in a top level drive, it would have a following '\'
            string absolutePathPart = imageSetFolderPath.TrimEnd(Path.DirectorySeparatorChar) + @"\";

            // Pass 1
            this.pass1 = new Task(() =>
            {

                List<Task> loadTasks = new List<Task>();

                // Fan out the loader tasks
                foreach (FileInfo fileInfo in filesToAddInfoArray)
                {
                    // Parse the relative path from the full name. 
                    // As GetDirectoryName does not end with a \ on a file name, we add the' '\' as needed

                    if (GlobalReferences.CancelTokenSource.IsCancellationRequested)
                    {
                        // User requested we cancel the task. So stop going through files and adding any further tasks.
                        // Although I am unsure about this, clearing the task list may help inhibit additional tasks being added
                        // The caller will check the cancellation token again, and will stop further actions and clean up as needed.
                        // Debug.Print("Cancelled from ImageSetLoader: this.pass1");
                        loadTasks = new List<Task>();
                        return;
                    }
                    string directoryName = Path.GetDirectoryName(fileInfo.FullName);
                    try
                    {
                        if (directoryName != null && directoryName.EndsWith(@"\") == false)
                        {
                            directoryName += @"\";
                        }
                    }
                    catch (PathTooLongException)
                    {
                        // If the file path is too long, skip the file.
                        // Also, add its folder name (if it isn't already there) to a list so we can
                        // later show a meaningful error message to the user that these files were skipped.
                        // We do the folder name as otherwise the number of images could be overwhelming.
                        string path = fileInfo.FullName.Substring(0, fileInfo.FullName.LastIndexOf(("\\"), StringComparison.Ordinal));
                        if (ImagesSkippedAsFilePathTooLong.Contains(path) == false)
                        {
                            ImagesSkippedAsFilePathTooLong.Add(path);
                        }
                        continue;
                    }
                    // The null portion shouldn't happen
                    string relativePath = directoryName != null
                        ? directoryName.Replace(absolutePathPart, string.Empty).TrimEnd(Path.DirectorySeparatorChar)
                        : string.Empty;

                    ImageLoader loader = new ImageLoader(relativePath, fileInfo, dataHandler);

                    Task loaderTask = loader.LoadImageAsync(() =>
                    {
                        // Both of these operations are atomic, the specific number and the specific loader at any given
                        // time may not correspond.
                        Interlocked.Increment(ref this.imagesLoaded);
                        this.LastLoadComplete = loader;
                        if (GlobalReferences.CancelTokenSource.IsCancellationRequested)
                        {
                            // User requested we cancel the task. So stop going through files.
                            // The caller will check the cancellation token again, and will stop further actions and clean up as needed.
                            // Debug.Print("Cancelled from Loader.Task");
                            return;
                        }
                        if (loader.RequiresDatabaseInsert)
                        {
                            // This requires database insertion. Enqueue for pass 2
                            // Note that there is no strict ordering here, anything may finish and insert in
                            // any order. By sorting the file infos above, things that sort first in the database should
                            // be done first, BUT THIS MAY REQUIRE ADDITIONAL FINESSE TO KEEP THE EXPLICIT ORDER CORRECT.
                            capturedDatabaseInsertionQueue.Enqueue(loader.File);
                            Interlocked.Increment(ref this.imagesToInsert);
                        }
                    });
                    loadTasks.Add(loaderTask);
                }

                try
                {
                    Task.WaitAll(loadTasks.ToArray(), GlobalReferences.CancelTokenSource.Token);
                }
                catch //(OperationCanceledException e)
                {
                    // If one of the tasks is cancelled, it raises an OperationCanceled Exception.
                    // We catch it to make it silent.
                    // Debug.Print("Caught Cancellation in ImageSetLoader: Task.WaitAll");
                }
            });
            // End Pass 1


            // Pass 2
            if (GlobalReferences.CancelTokenSource.IsCancellationRequested)
            {
                // Don't start the second pass if things have been cancelled.
                // Not sure if we really need to clear the queue, but it likely doesn't hurt.
                Debug.Print("Cancellation in ImageSetLoader: Just before pass 2");
                // ReSharper disable once RedundantAssignment
                databaseInsertionQueue = new ConcurrentQueue<ImageRow> ();
                return;
            }
            this.pass2 = new Task(() =>
            {
                // This pass2 starts after pass1 is fully complete
                List<ImageRow> imagesToInsert = capturedDatabaseInsertionQueue.OrderBy(f => Path.Combine(f.RelativePath, f.File)).ToList();
                dataHandler.FileDatabase.AddFiles(imagesToInsert,
                                                  (file, fileIndex) =>
                                                  {
                                                      this.LastInsertComplete = file;
                                                      this.LastIndexInsertComplete = fileIndex;
                                                  });
            });
            // End Pass 2 
        }
        #endregion

        #region Internal Task LoadAsync
        internal async Task LoadAsync(Action<int, FolderLoadProgress> reportProgress, FolderLoadProgress folderLoadProgress, int progressIntervalMilliseconds)
        {
            this.pass1.Start();

            Timer t = new Timer((state) =>
            {
                if (GlobalReferences.CancelTokenSource.IsCancellationRequested)
                {
                    // If we received a cancellation notice in Step 1, don't bother reporting any progress.
                    // Debug.Print("Cancellation at beginning of Timer t");
                    return;
                }
                if (this.LastLoadComplete != null)
                {
                    folderLoadProgress.BitmapSource = this.LastLoadComplete.File.IsVideo 
                        ? Constant.ImageValues.BlankVideo.Value 
                        : this.LastLoadComplete.BitmapSource;
                }
                else
                {
                    folderLoadProgress.BitmapSource = null;
                }
                
                folderLoadProgress.CurrentFile = this.ImagesLoaded;
                folderLoadProgress.CurrentFileName = this.LastLoadComplete?.File.File;
                int percentProgress = (int)(100.0 * this.ImagesLoaded / this.ImagesToLoad);
                try
                {
                    reportProgress(percentProgress, folderLoadProgress);
                }
                catch 
                {
                    // If you cancel, it may still try to invoke reportProgress even thought the operation is marked as completed. 
                    // This catch at least stops it from generating an error message
                    // Debug.Print("Caught when trying ReportProgress");
                }

            }, null, 0, progressIntervalMilliseconds);

            await this.pass1.ConfigureAwait(false);

            t.Change(-1, -1);
            t.Dispose();

            folderLoadProgress.CurrentPass = 2;

            if (GlobalReferences.CancelTokenSource.IsCancellationRequested)
            {
                // If we received a cancellation notice in Step 1, don't proceed to Step 2
                // Debug.Print("Cancellation in ImageSetLoader: Before invoking this.pass2.Start");
                return;
            }

            this.pass2.Start();

            t = new Timer((state) =>
            {
                folderLoadProgress.BitmapSource = null;
                folderLoadProgress.CurrentFile = this.LastIndexInsertComplete;
                folderLoadProgress.CurrentFileName = this.LastInsertComplete?.File;
                int percentProgress = (int)(100.0 * folderLoadProgress.CurrentFile / this.imagesToInsert);
                reportProgress(percentProgress, folderLoadProgress);
            }, null, 0, progressIntervalMilliseconds);

            await this.pass2.ConfigureAwait(false);

            t.Change(-1, -1);
            t.Dispose();
        }
        #endregion

        #region Dispose
        // To follow design pattern in  CA1001 Types that own disposable fields should be disposable
        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (pass1 != null)
                {
                    pass1.Dispose();
                }
                if (pass2 != null)
                {
                    pass2.Dispose();
                }
            }
        }
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        #endregion
    }
}
