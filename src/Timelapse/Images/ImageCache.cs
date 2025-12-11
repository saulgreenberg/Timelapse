using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using Timelapse.Constant;
using Timelapse.Database;
using Timelapse.DataStructures;
using Timelapse.DataTables;
using Timelapse.DebuggingSupport;
using Timelapse.Enums;
using Timelapse.Extensions;

namespace Timelapse.Images
{
    // ImageCache holds the current image & differenced images in a cache, as well as the state as to which image is being displayed
    // It also retrieves the images, and moves to other files
    public class ImageCache : FileTableEnumerator
    {
        #region Public Properties
        public ImageDifferenceEnum CurrentDifferenceState { get; private set; }

        public BitmapSource GetCurrentImage => differenceBitmapCache[CurrentDifferenceState];

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
            TryMoveToFile(DatabaseValues.InvalidRow);
            CurrentDifferenceState = ImageDifferenceEnum.Unaltered;
            differenceBitmapCache = [];
            mostRecentlyUsedIDs = new(ImageValues.BitmapCacheSize);
            prefetechesByID = new();
            unalteredBitmapsByID = new();
        }
        #endregion


        #region Public Methods - Navigate between image / differenced images 
        public void MoveToNextStateInCombinedDifferenceCycle()
        {
            // if this method and MoveToNextStateInPreviousNextDifferenceCycle() returned bool they'd be consistent MoveNext() and MovePrevious()
            // however, there's no way for them to fail and there's not value in always returning true
            if (CurrentDifferenceState == ImageDifferenceEnum.Next ||
                CurrentDifferenceState == ImageDifferenceEnum.Previous ||
                CurrentDifferenceState == ImageDifferenceEnum.Combined)
            {
                CurrentDifferenceState = ImageDifferenceEnum.Unaltered;
            }
            else
            {
                CurrentDifferenceState = ImageDifferenceEnum.Combined;
            }
        }

        public void MoveToNextStateInPreviousNextDifferenceCycle()
        {
            // If we are looking at the combined differenced image, then always go to the unaltered image.
            if (CurrentDifferenceState == ImageDifferenceEnum.Combined)
            {
                CurrentDifferenceState = ImageDifferenceEnum.Unaltered;
                return;
            }


            // If the current image is marked as corrupted, we will only show the original (replacement) image
            if (Current == null || !Current.IsDisplayable(Database.RootPathToImages))
            {
                CurrentDifferenceState = ImageDifferenceEnum.Unaltered;
                return;
            }

            // We are going around in a cycle, so go back to the beginning if we are at the end of it.
            CurrentDifferenceState = (CurrentDifferenceState >= ImageDifferenceEnum.Next) ? ImageDifferenceEnum.Previous : ++CurrentDifferenceState;

            // Because we can always display the unaltered image, we don't have to do any more tests if that is the current one in the cyle
            if (CurrentDifferenceState == ImageDifferenceEnum.Unaltered)
            {
                return;
            }

            // We can't actually show the previous or next image differencing if we are on the first or last image in the set respectively
            // Nor can we do it if the next image in the sequence is a corrupted one.
            // If that is the case, skip to the next one in the sequence
            if (CurrentDifferenceState == ImageDifferenceEnum.Previous && CurrentRow == 0)
            {
                // Already at the beginning
                MoveToNextStateInPreviousNextDifferenceCycle();
            }
            else if (CurrentDifferenceState == ImageDifferenceEnum.Next && CurrentRow == Database.CountAllCurrentlySelectedFiles - 1)
            {
                // Already at the end
                MoveToNextStateInPreviousNextDifferenceCycle();
            }
            else if (CurrentDifferenceState == ImageDifferenceEnum.Next && !Database.IsFileDisplayable(CurrentRow + 1))
            {
                // Can't use the next image as its corrupted
                MoveToNextStateInPreviousNextDifferenceCycle();
            }
            else if (CurrentDifferenceState == ImageDifferenceEnum.Previous && !Database.IsFileDisplayable(CurrentRow - 1))
            {
                // Can't use the previous image as its corrupted
                MoveToNextStateInPreviousNextDifferenceCycle();
            }
        }
        #endregion

        #region Public Methods - Calculate difference and get difference availability
        public ImageDifferenceResultEnum TryCalculateDifference()
        {
            if (Current == null || Current.IsVideo || Current.IsDisplayable(Database.RootPathToImages) == false)
            {
                CurrentDifferenceState = ImageDifferenceEnum.Unaltered;
                return ImageDifferenceResultEnum.CurrentImageNotAvailable;
            }

            // determine which image to use for differencing
            WriteableBitmap comparisonBitmap;
            if (CurrentDifferenceState == ImageDifferenceEnum.Previous)
            {
                if (TryGetPreviousBitmapAsWriteable(out comparisonBitmap) == false)
                {
                    return ImageDifferenceResultEnum.PreviousImageNotAvailable;
                }
            }
            else if (CurrentDifferenceState == ImageDifferenceEnum.Next)
            {
                if (TryGetNextBitmapAsWriteable(out comparisonBitmap) == false)
                {
                    return ImageDifferenceResultEnum.NextImageNotAvailable;
                }
            }
            else
            {
                return ImageDifferenceResultEnum.NotCalculable;
            }

            WriteableBitmap unalteredBitmap = differenceBitmapCache[ImageDifferenceEnum.Unaltered].AsWriteable();
            differenceBitmapCache[ImageDifferenceEnum.Unaltered] = unalteredBitmap;

            BitmapSource differenceBitmap = unalteredBitmap.Subtract(comparisonBitmap);
            differenceBitmapCache[CurrentDifferenceState] = differenceBitmap;
            return differenceBitmap != null ? ImageDifferenceResultEnum.Success : ImageDifferenceResultEnum.NotCalculable;
        }

        public ImageDifferenceResultEnum TryCalculateCombinedDifference(byte differenceThreshold)
        {
            if (CurrentDifferenceState != ImageDifferenceEnum.Combined)
            {
                return ImageDifferenceResultEnum.NotCalculable;
            }

            // We need three valid images: the current one, the previous one, and the next one.
            if (Current == null || Current.IsVideo || Current.IsDisplayable(Database.RootPathToImages) == false)
            {
                CurrentDifferenceState = ImageDifferenceEnum.Unaltered;
                return ImageDifferenceResultEnum.CurrentImageNotAvailable;
            }

            if (TryGetPreviousBitmapAsWriteable(out WriteableBitmap previousBitmap) == false)
            {
                return ImageDifferenceResultEnum.PreviousImageNotAvailable;
            }

            if (TryGetNextBitmapAsWriteable(out WriteableBitmap nextBitmap) == false)
            {
                return ImageDifferenceResultEnum.NextImageNotAvailable;
            }

            WriteableBitmap unalteredBitmap = differenceBitmapCache[ImageDifferenceEnum.Unaltered].AsWriteable();
            differenceBitmapCache[ImageDifferenceEnum.Unaltered] = unalteredBitmap;

            // all three images are available, so calculate and cache difference
            BitmapSource differenceBitmap = unalteredBitmap.CombinedDifference(previousBitmap, nextBitmap, differenceThreshold);
            differenceBitmapCache[ImageDifferenceEnum.Combined] = differenceBitmap;
            return differenceBitmap != null ? ImageDifferenceResultEnum.Success : ImageDifferenceResultEnum.NotCalculable;
        }
        #endregion

        #region Public Methods - Move to File
        public sealed override bool TryMoveToFile(int fileIndex)
        {
            return TryMoveToFile(fileIndex, false, out _);
        }

        public bool TryMoveToFile(int fileIndex, bool forceUpdate, out bool newFileToDisplay)
        {
            long oldFileID = -1;
            if (Current != null)
            {
                oldFileID = Current.ID;
            }

            newFileToDisplay = false;
            if (base.TryMoveToFile(fileIndex) == false)
            {
                return false;
            }

            if (Current == null)
            {
                // Shouldn't happen
                TracePrint.NullException(nameof(Current));
                return false;
            }

            if (Current.ID != oldFileID || forceUpdate)
            {
                // if this is an image load it from cache or disk
                BitmapSource unalteredImage = null;
                if (Current.IsVideo == false)
                {
                    if (TryGetBitmap(Current, forceUpdate, out unalteredImage) == false)
                    {
                        return false;
                    }
                }
                // all moves are to display of unaltered images and invalidate any cached differences
                // it is assumed images on disk are not altered while Timelapse is running and hence unaltered bitmaps can safely be cached by their IDs
                ResetDifferenceState(unalteredImage);
                newFileToDisplay = true;
            }
            return true;
        }
        #endregion

        #region  Public Methods - Invalidate Image in cache / Reset
        public bool TryInvalidate(long id)
        {
            if (unalteredBitmapsByID.ContainsKey(id) == false)
            {
                return false;
            }

            if (Current == null || Current.ID == id)
            {
                Reset();
            }

            unalteredBitmapsByID.TryRemove(id, out _);
            lock (mostRecentlyUsedIDs)
            {
                return mostRecentlyUsedIDs.TryRemove(id);
            }
        }

        // reset enumerator state but don't clear caches
        public override void Reset()
        {
            base.Reset();
            ResetDifferenceState(null);
        }
        #endregion

        #region Private - Cache
        // ORIGINAL VERSION, WITH UPDATE BELOW AS RECOMMENDED BY CA2008 and generated by Claude
        // Remove this block if the updated version works well
        //private void CacheBitmap(long id, BitmapSource bitmap)
        //{
        //    lock (mostRecentlyUsedIDs)
        //    {
        //        // cache the bitmap, replacing any existing bitmap with the one passed
        //        unalteredBitmapsByID.AddOrUpdate(id,
        //            newID =>
        //            {
        //                // if the bitmap cache is full make room for the incoming bitmap
        //                if (mostRecentlyUsedIDs.IsFull())
        //                {
        //                    if (mostRecentlyUsedIDs.TryGetLeastRecent(out long fileIDToRemove))
        //                    {
        //                        unalteredBitmapsByID.TryRemove(fileIDToRemove, out _);
        //                    }
        //                }

        //                // indicate to add the bitmap
        //                return bitmap;
        //            },
        //            (_, newBitmap) => newBitmap);
        //        mostRecentlyUsedIDs.SetMostRecent(id);
        //    }
        //}

        private void CacheBitmap(long id, BitmapSource bitmap)
        {
            lock (mostRecentlyUsedIDs)
            {
                // Handle eviction first, outside of AddOrUpdate
                if (!unalteredBitmapsByID.ContainsKey(id) && mostRecentlyUsedIDs.IsFull())
                {
                    if (mostRecentlyUsedIDs.TryGetLeastRecent(out long fileIDToRemove))
                    {
                        unalteredBitmapsByID.TryRemove(fileIDToRemove, out _);
                    }
                }

                // Then add/update
                unalteredBitmapsByID.AddOrUpdate(id, _ => bitmap, (_, newBitmap) => newBitmap);
                mostRecentlyUsedIDs.SetMostRecent(id);
            }
        }
        #endregion

        #region Private Methods - ResetDifferences
        private void ResetDifferenceState(BitmapSource unalteredImage)
        {
            CurrentDifferenceState = ImageDifferenceEnum.Unaltered;
            differenceBitmapCache[ImageDifferenceEnum.Unaltered] = unalteredImage;
            differenceBitmapCache[ImageDifferenceEnum.Previous] = null;
            differenceBitmapCache[ImageDifferenceEnum.Next] = null;
            differenceBitmapCache[ImageDifferenceEnum.Combined] = null;
        }
        #endregion

        #region Private Methods - Try Get or Cache Bitmap / Image / Differences / Next / Previous etc
        private bool TryGetBitmap(ImageRow fileRow, out BitmapSource bitmap)
        {
            return TryGetBitmap(fileRow, false, out bitmap);
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
                    bitmap = fileRow.LoadBitmap(Database.RootPathToImages, out _);
                    prefetechesByID.Clear();
                    unalteredBitmapsByID.Clear();
                    CacheBitmap(fileRow.ID, bitmap);
                    // Debug.Print("Loaded as forceUpdate " + fileRow.FileName);
                }
                else if (unalteredBitmapsByID.TryGetValue(fileRow.ID, out bitmap))
                {
                    // There is a cached bitmap, so we are now using it (in out bitmap)
                    // Debug.Print("Prefetched immediate " + fileRow.FileName);
                }
                else
                {
                    // If the prefetched bitmap is still being processed, wait for it
                    if (prefetechesByID.TryGetValue(fileRow.ID, out Task prefetch))
                    {
                        // bitmap retrieval's already in progress, so wait for it to complete
                        prefetch.Wait();
                        bitmap = unalteredBitmapsByID[fileRow.ID];
                        // Debug.Print("Prefetched wait" + fileRow.FileName);
                    }
                    else
                    {
                        // No cached bitmaps are available.
                        // synchronously load the requested bitmap from disk as it isn't cached, 
                        // doesn't have a prefetch running, and is needed right now by the caller
                        bitmap = fileRow.LoadBitmap(Database.RootPathToImages, out bool isCorruptOrMissing);
                        if (isCorruptOrMissing == false)
                        {
                            CacheBitmap(fileRow.ID, bitmap);
                        }
                        // Debug.Print("Loaded as not prefetched " + fileRow.FileName);
                    }
                }

                // Prefetch both forward and backward for smooth bidirectional navigation
                TryInitiateBitmapPrefetch(CurrentRow + 1);  // Forward
                TryInitiateBitmapPrefetch(CurrentRow - 1);  // Backward
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
            if (TryGetImage(fileRow, out ImageRow file) == false)
            {
                bitmap = null;
                return false;
            }

            if (TryGetBitmap(file, out BitmapSource bitmapSource) == false)
            {
                bitmap = null;
                return false;
            }

            bitmap = bitmapSource.AsWriteable();
            CacheBitmap(file.ID, bitmap);
            return true;
        }

        private bool TryGetImage(int fileRow, out ImageRow file)
        {
            if (fileRow == CurrentRow)
            {
                file = Current;
                return true;
            }

            if (Database.IsFileRowInRange(fileRow) == false)
            {
                file = null;
                return false;
            }

            file = Database.FileTable[fileRow];
            return file.IsDisplayable(Database.RootPathToImages);
        }

        private bool TryGetNextBitmapAsWriteable(out WriteableBitmap nextBitmap)
        {
            return TryGetBitmapAsWriteable(CurrentRow + 1, out nextBitmap);
        }

        private bool TryGetPreviousBitmapAsWriteable(out WriteableBitmap previousBitmap)
        {
            return TryGetBitmapAsWriteable(CurrentRow - 1, out previousBitmap);
        }

        private void TryInitiateBitmapPrefetch(int fileIndex)
        {
            if (Database.IsFileRowInRange(fileIndex) == false)
            {
                return;
            }

            ImageRow nextFile = Database.FileTable[fileIndex];
            if (unalteredBitmapsByID.ContainsKey(nextFile.ID) || prefetechesByID.ContainsKey(nextFile.ID))
            {
                return;
            }

            Task prefetch = Task.Run(() => //Task.Factory.StartNew(() => SEE CA2008. Replacing this with Task.Run is recommended
            {
                BitmapSource nextBitmap = nextFile.LoadBitmap(Database.RootPathToImages, out _);
                CacheBitmap(nextFile.ID, nextBitmap);
                prefetechesByID.TryRemove(nextFile.ID, out _);
            });
            prefetechesByID.AddOrUpdate(nextFile.ID, prefetch, (_, newPrefetch) => newPrefetch);
        }
        #endregion
    }
}
