using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using Timelapse.Database;
using Timelapse.Enums;
using Timelapse.Extensions;
using Timelapse.Util;

namespace Timelapse.Images
{
    // ImageCache holds the current image & differenced images in a cache, as well as the state as to which image is being displayed
    // It also retrieves the images, and moves to other files
    public class ImageCache : FileTableEnumerator
    {
        #region Public Properties
        public ImageDifferenceEnum CurrentDifferenceState { get; private set; }

        public BitmapSource GetCurrentImage => this.differenceBitmapCache[this.CurrentDifferenceState];

        #endregion

        #region Private Variables
        private readonly Dictionary<ImageDifferenceEnum, BitmapSource> differenceBitmapCache;
        private readonly RecencyOrderedList<long> mostRecentlyUsedIDs;
        private readonly ConcurrentDictionary<long, Task> prefetechesByID;
        private readonly ConcurrentDictionary<long, BitmapSource> unalteredBitmapsByID;
        #endregion

        #region Constructor
        public ImageCache(FileDatabase fileDatabase) :
            base(fileDatabase)
        {
            this.TryMoveToFile(Constant.DatabaseValues.InvalidRow);
            this.CurrentDifferenceState = ImageDifferenceEnum.Unaltered;
            this.differenceBitmapCache = new Dictionary<ImageDifferenceEnum, BitmapSource>();
            this.mostRecentlyUsedIDs = new RecencyOrderedList<long>(Constant.ImageValues.BitmapCacheSize);
            this.prefetechesByID = new ConcurrentDictionary<long, Task>();
            this.unalteredBitmapsByID = new ConcurrentDictionary<long, BitmapSource>();
        }
        #endregion


        #region Public Methods - Navigate between image / differenced images 
        public void MoveToNextStateInCombinedDifferenceCycle()
        {
            // if this method and MoveToNextStateInPreviousNextDifferenceCycle() returned bool they'd be consistent MoveNext() and MovePrevious()
            // however, there's no way for them to fail and there's not value in always returning true
            if (this.CurrentDifferenceState == ImageDifferenceEnum.Next ||
                this.CurrentDifferenceState == ImageDifferenceEnum.Previous ||
                this.CurrentDifferenceState == ImageDifferenceEnum.Combined)
            {
                this.CurrentDifferenceState = ImageDifferenceEnum.Unaltered;
            }
            else
            {
                this.CurrentDifferenceState = ImageDifferenceEnum.Combined;
            }
        }

        public void MoveToNextStateInPreviousNextDifferenceCycle()
        {
            // If we are looking at the combined differenced image, then always go to the unaltered image.
            if (this.CurrentDifferenceState == ImageDifferenceEnum.Combined)
            {
                this.CurrentDifferenceState = ImageDifferenceEnum.Unaltered;
                return;
            }

            // If the current image is marked as corrupted, we will only show the original (replacement) image
            if (!this.Current.IsDisplayable(this.Database.FolderPath))
            {
                this.CurrentDifferenceState = ImageDifferenceEnum.Unaltered;
                return;
            }
            else
            {
                // We are going around in a cycle, so go back to the beginning if we are at the end of it.
                this.CurrentDifferenceState = (this.CurrentDifferenceState >= ImageDifferenceEnum.Next) ? ImageDifferenceEnum.Previous : ++this.CurrentDifferenceState;
            }

            // Because we can always display the unaltered image, we don't have to do any more tests if that is the current one in the cyle
            if (this.CurrentDifferenceState == ImageDifferenceEnum.Unaltered)
            {
                return;
            }

            // We can't actually show the previous or next image differencing if we are on the first or last image in the set respectively
            // Nor can we do it if the next image in the sequence is a corrupted one.
            // If that is the case, skip to the next one in the sequence
            if (this.CurrentDifferenceState == ImageDifferenceEnum.Previous && this.CurrentRow == 0)
            {
                // Already at the beginning
                this.MoveToNextStateInPreviousNextDifferenceCycle();
            }
            else if (this.CurrentDifferenceState == ImageDifferenceEnum.Next && this.CurrentRow == this.Database.CountAllCurrentlySelectedFiles - 1)
            {
                // Already at the end
                this.MoveToNextStateInPreviousNextDifferenceCycle();
            }
            else if (this.CurrentDifferenceState == ImageDifferenceEnum.Next && !this.Database.IsFileDisplayable(this.CurrentRow + 1))
            {
                // Can't use the next image as its corrupted
                this.MoveToNextStateInPreviousNextDifferenceCycle();
            }
            else if (this.CurrentDifferenceState == ImageDifferenceEnum.Previous && !this.Database.IsFileDisplayable(this.CurrentRow - 1))
            {
                // Can't use the previous image as its corrupted
                this.MoveToNextStateInPreviousNextDifferenceCycle();
            }
        }
        #endregion

        #region Public Methods - Calculate difference and get difference availability
        public ImageDifferenceResultEnum TryCalculateDifference()
        {
            if (this.Current == null || this.Current.IsVideo || this.Current.IsDisplayable(this.Database.FolderPath) == false)
            {
                this.CurrentDifferenceState = ImageDifferenceEnum.Unaltered;
                return ImageDifferenceResultEnum.CurrentImageNotAvailable;
            }

            // determine which image to use for differencing
            WriteableBitmap comparisonBitmap;
            if (this.CurrentDifferenceState == ImageDifferenceEnum.Previous)
            {
                if (this.TryGetPreviousBitmapAsWriteable(out comparisonBitmap) == false)
                {
                    return ImageDifferenceResultEnum.PreviousImageNotAvailable;
                }
            }
            else if (this.CurrentDifferenceState == ImageDifferenceEnum.Next)
            {
                if (this.TryGetNextBitmapAsWriteable(out comparisonBitmap) == false)
                {
                    return ImageDifferenceResultEnum.NextImageNotAvailable;
                }
            }
            else
            {
                return ImageDifferenceResultEnum.NotCalculable;
            }

            WriteableBitmap unalteredBitmap = this.differenceBitmapCache[ImageDifferenceEnum.Unaltered].AsWriteable();
            this.differenceBitmapCache[ImageDifferenceEnum.Unaltered] = unalteredBitmap;

            BitmapSource differenceBitmap = unalteredBitmap.Subtract(comparisonBitmap);
            this.differenceBitmapCache[this.CurrentDifferenceState] = differenceBitmap;
            return differenceBitmap != null ? ImageDifferenceResultEnum.Success : ImageDifferenceResultEnum.NotCalculable;
        }

        public ImageDifferenceResultEnum TryCalculateCombinedDifference(byte differenceThreshold)
        {
            if (this.CurrentDifferenceState != ImageDifferenceEnum.Combined)
            {
                return ImageDifferenceResultEnum.NotCalculable;
            }

            // We need three valid images: the current one, the previous one, and the next one.
            if (this.Current == null || this.Current.IsVideo || this.Current.IsDisplayable(this.Database.FolderPath) == false)
            {
                this.CurrentDifferenceState = ImageDifferenceEnum.Unaltered;
                return ImageDifferenceResultEnum.CurrentImageNotAvailable;
            }

            if (this.TryGetPreviousBitmapAsWriteable(out WriteableBitmap previousBitmap) == false)
            {
                return ImageDifferenceResultEnum.PreviousImageNotAvailable;
            }

            if (this.TryGetNextBitmapAsWriteable(out WriteableBitmap nextBitmap) == false)
            {
                return ImageDifferenceResultEnum.NextImageNotAvailable;
            }

            WriteableBitmap unalteredBitmap = this.differenceBitmapCache[ImageDifferenceEnum.Unaltered].AsWriteable();
            this.differenceBitmapCache[ImageDifferenceEnum.Unaltered] = unalteredBitmap;

            // all three images are available, so calculate and cache difference
            BitmapSource differenceBitmap = unalteredBitmap.CombinedDifference(previousBitmap, nextBitmap, differenceThreshold);
            this.differenceBitmapCache[ImageDifferenceEnum.Combined] = differenceBitmap;
            return differenceBitmap != null ? ImageDifferenceResultEnum.Success : ImageDifferenceResultEnum.NotCalculable;
        }
        #endregion

        #region Public Methods - Move to File
        public override bool TryMoveToFile(int fileIndex)
        {
            return this.TryMoveToFile(fileIndex, false, out _);
        }

        public bool TryMoveToFile(int fileIndex, bool forceUpdate, out bool newFileToDisplay)
        {
            long oldFileID = -1;
            if (this.Current != null)
            {
                oldFileID = this.Current.ID;
            }

            newFileToDisplay = false;
            if (base.TryMoveToFile(fileIndex) == false)
            {
                return false;
            }

            if (this.Current.ID != oldFileID || forceUpdate)
            {
                // if this is an image load it from cache or disk
                BitmapSource unalteredImage = null;
                if (this.Current.IsVideo == false)
                {
                    if (this.TryGetBitmap(this.Current, forceUpdate, out unalteredImage) == false)
                    {
                        return false;
                    }
                }
                // all moves are to display of unaltered images and invalidate any cached differences
                // it is assumed images on disk are not altered while Timelapse is running and hence unaltered bitmaps can safely be cached by their IDs
                this.ResetDifferenceState(unalteredImage);
                newFileToDisplay = true;
            }
            return true;
        }
        #endregion

        #region  Public Methods - Invalidate Image in cache / Reset
        public bool TryInvalidate(long id)
        {
            if (this.unalteredBitmapsByID.ContainsKey(id) == false)
            {
                return false;
            }

            if (this.Current == null || this.Current.ID == id)
            {
                this.Reset();
            }

            this.unalteredBitmapsByID.TryRemove(id, out _);
            lock (this.mostRecentlyUsedIDs)
            {
                return this.mostRecentlyUsedIDs.TryRemove(id);
            }
        }

        // reset enumerator state but don't clear caches
        public override void Reset()
        {
            base.Reset();
            this.ResetDifferenceState(null);
        }
        #endregion

        #region Private - Cache
        private void CacheBitmap(long id, BitmapSource bitmap)
        {
            lock (this.mostRecentlyUsedIDs)
            {
                // cache the bitmap, replacing any existing bitmap with the one passed
                this.unalteredBitmapsByID.AddOrUpdate(id,
                    (long newID) =>
                    {
                        // if the bitmap cache is full make room for the incoming bitmap
                        if (this.mostRecentlyUsedIDs.IsFull())
                        {
                            if (this.mostRecentlyUsedIDs.TryGetLeastRecent(out long fileIDToRemove))
                            {
                                this.unalteredBitmapsByID.TryRemove(fileIDToRemove, out BitmapSource ignored);
                            }
                        }

                        // indicate to add the bitmap
                        return bitmap;
                    },
                    (long existingID, BitmapSource newBitmap) => newBitmap);
                this.mostRecentlyUsedIDs.SetMostRecent(id);
            }
        }
        #endregion

        #region Private Methods - ResetDifferences
        private void ResetDifferenceState(BitmapSource unalteredImage)
        {
            this.CurrentDifferenceState = ImageDifferenceEnum.Unaltered;
            this.differenceBitmapCache[ImageDifferenceEnum.Unaltered] = unalteredImage;
            this.differenceBitmapCache[ImageDifferenceEnum.Previous] = null;
            this.differenceBitmapCache[ImageDifferenceEnum.Next] = null;
            this.differenceBitmapCache[ImageDifferenceEnum.Combined] = null;
        }
        #endregion

        #region Private Methods - Try Get or Cache Bitmap / Image / Differences / Next / Previous etc
        private bool TryGetBitmap(ImageRow fileRow, out BitmapSource bitmap)
        {
            return this.TryGetBitmap(fileRow, false, out bitmap);
        }

        private bool TryGetBitmap(ImageRow fileRow, bool forceUpdate, out BitmapSource bitmap)
        {
            // Its in a try/catch because one user was getting a GenericKeyNotFoundException: The given kye was not present in the dictionary", 
            // invoked from 'System.Collections.Concurrent.ConcurrentDictionary;2.get_Item(TKey key) somewhere in here.
            // However, I could not replicate the error. So I am not sure if the catch actually works properly, especially if the
            // calling routines don't check the boolean return value
            try
            {
                if (forceUpdate)
                {
                    // Force update clears the caches, which in turn always forces synchronous loading of the requested bitmap 
                    // from disk as it cannot cached. This is necessary, in case (for example)  a 'missing' placeholder image was used and the image
                    // was later restored. If we don't clear the cache, the placeholder image would be used instead.
                    bitmap = fileRow.LoadBitmap(this.Database.FolderPath, out bool isCorruptOrMissing);
                    this.prefetechesByID.Clear();
                    this.unalteredBitmapsByID.Clear();
                    this.CacheBitmap(fileRow.ID, bitmap);
                    // Debug.Print("Loaded as forceUpdate " + fileRow.FileName);
                }
                else if (this.unalteredBitmapsByID.TryGetValue(fileRow.ID, out bitmap))
                {
                    // There is a cached bitmap, so we are now using it (in out bitmap)
                    // Debug.Print("Prefetched immediate " + fileRow.FileName);
                }
                else
                {
                    // If the prefetched bitmap is still being processed, wait for it
                    if (this.prefetechesByID.TryGetValue(fileRow.ID, out Task prefetch))
                    {
                        // bitmap retrieval's already in progress, so wait for it to complete
                        prefetch.Wait();
                        bitmap = this.unalteredBitmapsByID[fileRow.ID];
                        // Debug.Print("Prefetched wait" + fileRow.FileName);
                    }
                    else
                    {
                        // No cached bitmaps are available.
                        // synchronously load the requested bitmap from disk as it isn't cached, 
                        // doesn't have a prefetch running, and is needed right now by the caller
                        bitmap = fileRow.LoadBitmap(this.Database.FolderPath, out bool isCorruptOrMissing);
                        if (isCorruptOrMissing == false)
                        {
                            this.CacheBitmap(fileRow.ID, bitmap);
                        }
                        // Debug.Print("Loaded as not prefetched " + fileRow.FileName);
                    }
                }

                // assuming a sequential forward scan order, start on the next bitmap
                this.TryInitiateBitmapPrefetch(this.CurrentRow + 1);
                return true;
            }
            catch (ArgumentException e)
            {
                bitmap = null;
                TracePrint.PrintMessage(String.Format("TryGetBitmap ArgumentException failure in ImageCache: " + e.Message));
                // System.Windows.MessageBoxResult result = System.Windows.MessageBox.Show (e.Message);
                return false;
            }
            catch (KeyNotFoundException e)
            {
                bitmap = null;
                TracePrint.PrintMessage(String.Format("TryGetBitmap KeyNotFound Exception failure in ImageCache: " + e.Message));
                // System.Windows.MessageBoxResult result = System.Windows.MessageBox.Show (e.Message);
                return false;
            }
        }

        private bool TryGetBitmapAsWriteable(int fileRow, out WriteableBitmap bitmap)
        {
            if (this.TryGetImage(fileRow, out ImageRow file) == false)
            {
                bitmap = null;
                return false;
            }

            if (this.TryGetBitmap(file, out BitmapSource bitmapSource) == false)
            {
                bitmap = null;
                return false;
            }

            bitmap = bitmapSource.AsWriteable();
            this.CacheBitmap(file.ID, bitmap);
            return true;
        }

        private bool TryGetImage(int fileRow, out ImageRow file)
        {
            if (fileRow == this.CurrentRow)
            {
                file = this.Current;
                return true;
            }

            if (this.Database.IsFileRowInRange(fileRow) == false)
            {
                file = null;
                return false;
            }

            file = this.Database.FileTable[fileRow];
            return file.IsDisplayable(this.Database.FolderPath);
        }

        private bool TryGetNextBitmapAsWriteable(out WriteableBitmap nextBitmap)
        {
            return this.TryGetBitmapAsWriteable(this.CurrentRow + 1, out nextBitmap);
        }

        private bool TryGetPreviousBitmapAsWriteable(out WriteableBitmap previousBitmap)
        {
            return this.TryGetBitmapAsWriteable(this.CurrentRow - 1, out previousBitmap);
        }

        private bool TryInitiateBitmapPrefetch(int fileIndex)
        {
            if (this.Database.IsFileRowInRange(fileIndex) == false)
            {
                return false;
            }

            ImageRow nextFile = this.Database.FileTable[fileIndex];
            if (this.unalteredBitmapsByID.ContainsKey(nextFile.ID) || this.prefetechesByID.ContainsKey(nextFile.ID))
            {
                return false;
            }

            Task prefetch = Task.Run(() => //Task.Factory.StartNew(() => SEE CA2008. Replacing this with Task.Run is recommended
            {
                BitmapSource nextBitmap = nextFile.LoadBitmap(this.Database.FolderPath, out bool isCorruptOrMissing);
                this.CacheBitmap(nextFile.ID, nextBitmap);
                this.prefetechesByID.TryRemove(nextFile.ID, out Task ignored);
            });
            this.prefetechesByID.AddOrUpdate(nextFile.ID, prefetch, (long id, Task newPrefetch) => newPrefetch);
            return true;
        }
        #endregion
    }
}
