using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Timelapse.Constant;
using Timelapse.DataStructures;
using Timelapse.DataTables;
using Timelapse.DebuggingSupport;
using Timelapse.Enums;
using Timelapse.Images;
using Task = System.Threading.Tasks.Task;

// ReSharper disable once CheckNamespace
namespace Timelapse
{
    public partial class TimelapseWindow
    {

        #region Duplicate the current record
        // Duplicates are somewhat problematic in terms of how to display them
        // When a user creates a duplicate, they will likely want to see it immediately, where it would be positioned just after the original. 
        // However, the selection and sorting order may not be amenable to this, e.g.,
        // - if the duplicate does not match the current selection (e.g. selection is species=bear, which the original had, but where species is cleared in the duplicate)
        // - if the sorting order does not have the duplicate follow the selection (e.g., sort on species)
        // To get around this, we do the following.
        // - Create a duplicate row representing the current image
        // - Add it to the database 
        // - Insert it in the file table immediately after the original's position.
        // - Note that we do NOT refresh the selection
        // Because of the above, we will always see the duplicates when they are created, and can update their contents.
        // The next selection, done who knows when, will update the view and the sort,
        // where those duplicates may or may not then appear depending on the select and sort criteria
        public async Task DuplicateCurrentRecord(bool useCurrentValues)
        {
            await DuplicateCurrentRecord(useCurrentValues, string.Empty);
        }

        // Duplicate image serves dual purposes.
        // - If we are displaying a single image, it duplicates that image record in the database, and adds it to the file table
        // - If we are displaying an extracted frame from a video, it duplicates that frame, and adds it to the file table.
        //   However, it is not tagged as a duplicate as it has a different file name.
        public async Task<bool> DuplicateCurrentRecord(bool useCurrentValues, string extractedFrameFileName)
        {
            // Get the current image (or the selected image in the thumbnail grid) and duplicate it.
            // Note that this method shouldn't be called as the menueditDuplicate item will be disabled 
            // if the above conditions aren't met, but we check anyways.
            if (IsDisplayingSingleImage() == false && string.IsNullOrEmpty(extractedFrameFileName))
            {
                // We only allow duplication if we are displaying a single image in the main view
                // or if we are duplicating an extracted frame from a video
                return false;
            }

            // Get the current image
            ImageRow row = DataHandler.ImageCache.Current;
            if (row == null)
            {
                //Shouldn't happen
                TracePrint.NullException(nameof(row));
                return false ;
            }
            FileInfo fileInfo = new(row.File);

            // Create a duplicate of the image row

            // CamtrapDP standard: Whenever a new image is loaded, we assign a GUID to its mediaID.
            // Doing it here means that duplicates will have the same GUID
            bool isCamtrapDPStandard = this.DataHandler.FileDatabase.MetadataTablesIsCamtrapDPStandard();

            ImageRow duplicate = row.DuplicateRowWithValues(DataHandler.FileDatabase.FileTable.NewRow(fileInfo), useCurrentValues, isCamtrapDPStandard);
            if (string.IsNullOrEmpty(extractedFrameFileName) == false)
            {
                // If we are duplicating an extracted frame, set the file name to that instead of the original file name
                duplicate.File = extractedFrameFileName;
            }

            // Add the row to the database
            List<ImageRow> imagesToInsert = [duplicate];
            DataHandler.FileDatabase.AddFiles(imagesToInsert, null);

            // Insert the duplicated image into the filedata table. Note that we have to reset the slider to correctly show the new file count.
            long insertedDuplicateID = DataHandler.FileDatabase.Database.ScalarGetMaxValueAsLong(Constant.DBTables.FileData, Constant.DatabaseColumn.ID);
            DataTable table = await DataHandler.FileDatabase.Database.GetDataTableFromSelectAsync($"Select * from DataTable where ID={Sql.Quote(insertedDuplicateID.ToString())}");
            if (table.Rows.Count == 1)
            {
                int fileIndexForInsert = DataHandler.ImageCache.CurrentRow + 1;
                DataHandler.FileDatabase.FileTable.InsertRow(fileIndexForInsert, table.Rows[0]);
                this.FileNavigatorSliderReset();
            }


            // This next section duplicates the detections, if any.
            if (GlobalReferences.DetectionsExists)
            {
                // Get the ID of the duplicate file that was just inserted into the filedata table
                long duplicateFileID = DataHandler.FileDatabase.GetValueFromLastInsertedRow(DBTables.FileData, DatabaseColumn.ID);

                // Get the detections associated with the current row, if any
                DataRow[] detectionRowsUnsorted = await DataHandler.FileDatabase.GetDetectionsFromFileIDAsync(row.ID);
                if (detectionRowsUnsorted.Length > 0)
                {
                    // Create new detections and (if relevant) new detection videos for each detection row,
                    // but using the duplicate's ID
                    List<List<ColumnTuple>> detectionInsertionStatements = [];
                    List<List<ColumnTuple>> detectionVideoInsertionStatements = [];

                    //
                    // Case 1. We are duplicating an extracted frame from a video, so we need to get the bounding boxes for the current video frame
                    //
                    if (row.IsVideo && false == string.IsNullOrEmpty(extractedFrameFileName) && null != this.DataHandler.ImageCache.Current)
                    {
                        // Note that we use bounding boxes instead of the detection rows, but that simply because I had code for finding the desired frames elsewhere
                        // It also repeats (sort of) a bunch of the code found in DrawBoundingBoxesInCanvas (the video version). They could likely be merged, but its not worth the effort.
                        BoundingBoxes bBoxes = GlobalReferences.MainWindow.GetBoundingBoxesForCurrentFile(this.DataHandler.ImageCache.Current.ID, false);
                        int displayedVideoFrame = this.MarkableCanvas.VideoPlayer.FrameToShow;
                        int frameWindow = bBoxes.FrameRate == null
                            ? 0
                            : (int)Math.Floor((decimal)(bBoxes.FrameRate / 2.0));

                        // The from frame / to frame creates the bounding box search window based on the frame window 
                        int fromFrame = displayedVideoFrame - frameWindow;
                        int toFrame = displayedVideoFrame + frameWindow;

                        // Sort the boxes. Note that its likely that the Boxes are already sorted, so this should be fast.
                        List<BoundingBox> sortedBoxes = [.. bBoxes.Boxes.OrderBy(s => s.FrameNumber)];

                        // Find the index of the frame containing bounding boxes that is closest to the displayedVideoFrame
                        // As we do something different if its a frame before vs. after the displayed video frame, we 
                        // colloect that as prevFrameIndex or nextFrameIndex
                        int prevFrameIndex = -1;
                        int nextFrameIndex = -1;
                        int currentIndex = 0;
                        int difference = 10000;
                        foreach (BoundingBox box in sortedBoxes)
                        {
                            if (currentIndex >= sortedBoxes.Count - 1)
                            {
                                // If we are on the last bounding box, enlarge the frame window so that the bounding box lingers
                                // for a bit longer. That is instead of the bounding box disappearing after
                                // a 1/2 second, it will linger for a full second. This seems to be a better heuristic
                                // for visually indicating an entity that is in the process of moving off the video.The only do
                                fromFrame -= frameWindow;
                                toFrame += frameWindow;
                            }
                            if (box.FrameNumber < fromFrame)
                            {
                                // Skip this bounding box, as its below the frame window
                                currentIndex++;
                                continue;
                            }

                            if (box.FrameNumber > toFrame)
                            {
                                // Skip this bounding box and break, as its above the frame window
                                break;
                            }
                            // as we cycle through, we find the prev/next frames with boxes closest to the frame window
                            if (box.FrameNumber <= displayedVideoFrame)
                            {
                                // Incrementally get closer to the displayedVideoFrame
                                // Also, calculate how far the current bbox frame is from displayedVideoFrame 
                                prevFrameIndex = currentIndex;
                                difference = displayedVideoFrame - sortedBoxes[prevFrameIndex].FrameNumber;
                            }
                            else
                            if (box.FrameNumber - displayedVideoFrame < difference)
                            {
                                // We are above the displayedVideoFrame, where the difference is less than the difference found for the previous video frame.
                                // As we have now found the closest bbox frame above the displayedVideoFrame, we can stop searching.
                                nextFrameIndex = currentIndex;
                                prevFrameIndex = -1;
                                break;
                            }
                            currentIndex++;
                        }

                        // Collect the bounding boxes for the desired frame
                        List<BoundingBox> bboxes = [];
                        if (prevFrameIndex != -1)
                        {
                            // The desired bounding box frame is below or equal to the displayedVideoFrame
                            int boxFrameNumber = sortedBoxes[prevFrameIndex].FrameNumber;
                            int i = prevFrameIndex;
                            while (true)
                            {
                                if (i < 0 || sortedBoxes[i].FrameNumber != boxFrameNumber)
                                {
                                    // We reached the beginning or a box with a frame number that differs
                                    break;
                                }
                                // Record the box
                                bboxes.Add(sortedBoxes[i]);
                                i--;
                            }
                        }
                        else if (nextFrameIndex != -1)
                        {
                            // The desired bounding box frame is above the displayedVideoFrame
                            int boxFrameNumber = sortedBoxes[nextFrameIndex].FrameNumber;
                            int i = nextFrameIndex;
                            int count = sortedBoxes.Count;
                            while (true)
                            {
                                if (i > count - 1 || sortedBoxes[i].FrameNumber != boxFrameNumber)
                                {
                                    // We reached the end or a box with a frame number that differs
                                    break;
                                }
                                // Record the box
                                bboxes.Add(sortedBoxes[i]);
                                i++;
                            }
                        }

                        // Now update the database with the relevant bounding box values
                        foreach (BoundingBox bbox in bboxes)
                        {
                            detectionInsertionStatements.Clear();
                            List<ColumnTuple> detectionColumnsToUpdate =
                            [
                                new(DetectionColumns.ImageID, duplicateFileID),
                                new(DetectionColumns.Category, bbox.DetectionCategory),
                                new(DetectionColumns.Conf, bbox.Confidence),
                                new(DetectionColumns.BBox,
                                    $"{Math.Round(bbox.Rectangle.Left, 3)}, {Math.Round(bbox.Rectangle.Top, 3)}, {Math.Round(bbox.Rectangle.Width, 3)}, {Math.Round(bbox.Rectangle.Height, 3)}")
                            ];

                            // Add classification values to the detection row if they exist, otherwise they will be null
                            if (bbox.Classifications.Count != 0)
                            {
                                // We only use the first classification, as we no longer support multiple classifications per bounding box
                                // The bbox.Classifications is a list of KeyValuePairs, where the Key is the classification category (the species name) and the Value is the confidence
                                // So we have to look up the classification category ID
                                string category = this.DataHandler.FileDatabase.classificationCategoriesDictionary.FirstOrDefault(x=> x.Value == bbox.Classifications[0].Key).Key;
                                detectionColumnsToUpdate.Add(new(DetectionColumns.Classification, category));
                                detectionColumnsToUpdate.Add(new(DetectionColumns.ClassificationConf,
                                    (float)Convert.ToDouble(bbox.Classifications[0].Value)));
                            }
                            detectionInsertionStatements.Add(detectionColumnsToUpdate);

                            // Insert the detections into the Detections table
                            DataHandler.FileDatabase.InsertDetection(detectionInsertionStatements);
                        }
                    }

                    //
                    // Case 2. We are duplicating a regular image or video, so we need to use all the current detection values
                    //
                    else
                    {
                       
                        foreach (DataRow detectionRow in detectionRowsUnsorted)
                        {
                            detectionInsertionStatements.Clear();
                            detectionVideoInsertionStatements.Clear();

                            // Fill it in with the current file's detection values
                            List<ColumnTuple> detectionColumnsToUpdate =
                            [
                                new(DetectionColumns.ImageID, duplicateFileID),
                                new(DetectionColumns.Category, (string)detectionRow[DetectionColumns.Category]),
                                new(DetectionColumns.Conf, (float)Convert.ToDouble(detectionRow[DetectionColumns.Conf])),
                                new(DetectionColumns.BBox, (string)detectionRow[DetectionColumns.BBox])

                            ];
                            // Add classification values to the detection row if they exist, otherwise they will be null
                            if (detectionRow[DetectionColumns.Classification] != DBNull.Value)
                            {
                                detectionColumnsToUpdate.Add(new(DetectionColumns.Classification, (string)detectionRow[DetectionColumns.Classification]));
                                detectionColumnsToUpdate.Add(new(DetectionColumns.ClassificationConf,
                                    (float)Convert.ToDouble(detectionRow[DetectionColumns.ClassificationConf])));
                            }

                            detectionInsertionStatements.Add(detectionColumnsToUpdate);

                            // Insert the detections into the Detections table
                            DataHandler.FileDatabase.InsertDetection(detectionInsertionStatements);

                            // Get the ID of the duplicate file that was just inserted into the filedata table
                            long detectionID = DataHandler.FileDatabase.GetValueFromLastInsertedRow(DBTables.Detections, DetectionColumns.DetectionID);

                            // Now get the DetectionsVideo value, if they are present
                            if (row.IsVideo)
                            {
                                List<ColumnTuple> detectionsVideoColumnsToUpdate =
                                [
                                    new(DetectionColumns.FrameNumber, Convert.ToInt32(detectionRow[DetectionColumns.FrameNumber])),
                                    new(DetectionColumns.FrameRate, (float?)Convert.ToDouble(detectionRow[DetectionColumns.FrameRate])),
                                    new(DetectionColumns.DetectionID, detectionID)

                                ];
                                detectionVideoInsertionStatements.Add(detectionsVideoColumnsToUpdate);
                            }

                            // Insert the DetectionsVideo into the DetectionsVideo table
                            if (detectionVideoInsertionStatements.Count > 0)
                            {
                                DataHandler.FileDatabase.InsertDetectionsVideo(detectionVideoInsertionStatements);
                            }
                        }
                    }
                }


                // Regenerate the internal detections and classifications table to include the new detections (which will include the DetectionVideos) and classifications
                await DataHandler.FileDatabase.RefreshDetectionsDataTableAsync();

                // Check if we need this...
                DataHandler.FileDatabase.IndexCreateForDetectionsIfNeeded();
                Episodes.Episodes.Reset();
            }
            TryFileShowWithoutSliderCallback(DirectionEnum.Next);
            return true;
        }
        #endregion


        // Manage the display of the duplicate indicator (text in the form Duplicate: x/y) in the main window.
        public void DuplicateDisplayIndicatorInImageIfWarranted()
        {
            if (IsDisplayingMultipleImagesInOverview())
            {
                DuplicateIndicatorInMainWindow.Visibility = Visibility.Collapsed;
            }
            else
            {
                if (Keyboard.IsKeyDown(Key.H))
                {
                    DuplicateIndicatorInMainWindow.Visibility = Visibility.Collapsed;
                    return;
                }
                // Display the text "Duplicate x/y" if needed
                // The returned point will be 1,1 if there are no duplicates,
                // or position,count if there are duplicates e.g. 2/4 means its the 2nd image in a set of 4 duplicates
                Point duplicateSequence = DuplicatesCheckIfDuplicateAndGetSequenceNumberIfAny();
                if (duplicateSequence.Y > 1)
                {
                    DuplicateIndicatorInMainWindow.Visibility = Visibility.Visible;
                    DuplicateIndicatorInMainWindow.Text =
                        $"Duplicate: {duplicateSequence.X}/{duplicateSequence.Y}";
                }
                else
                {
                    DuplicateIndicatorInMainWindow.Visibility = Visibility.Collapsed;
                }
            }
        }

        // Check if the current image is a duplicate, and if so get its sequence number
        // Starting from the curent position in the file table, check if this image has duplicates
        // We do this simply by comparing the relativePath,File of the surrounding images in the current selection
        // The returned point will beL
        // (0,0) if there is no current image or if for some reason this method blows up
        // (1,1) if there are no duplicates (i.e., image 1 out of a total count of 1)
        // (position,count) if there are duplicates e.g. (2,4) means its the 2nd image in a set of 4 duplicates

        // This version invokes it on the current image (which works fine in the main view, but not in the overview)
        public Point DuplicatesCheckIfDuplicateAndGetSequenceNumberIfAny()
        {
            if (DataHandler?.FileDatabase == null)
            {
                return new(0, 0);
            }

            return DuplicatesCheckIfDuplicateAndGetSequenceNumberIfAny(DataHandler.ImageCache.Current, DataHandler.ImageCache.CurrentRow);
        }

        public Point DuplicatesCheckIfDuplicateAndGetSequenceNumberIfAny(ImageRow selectedImageRow, int selectedRowIndex)
        {
            try
            {


                int currentPosition = 0;
                int lastPosition = 0;
                if (DataHandler?.FileDatabase?.CountAllCurrentlySelectedFiles <= 0 || selectedRowIndex < 0)
                {
                    // There are no images to navigate
                    return new(0, 0);
                }

                if (DataHandler?.FileDatabase == null)
                {
                    // Shouldn't happen
                    TracePrint.NullException(nameof(DataHandler.FileDatabase));
                    return new(0, 0);
                }

                ImageRow previousOrNextImageRow;
                // Get the path of the current image
                string currentPath = Path.Combine(selectedImageRow.RelativePath, selectedImageRow.File);

                // Loop backwards from the current image, counting how many previous images have the same path as the current image, until one differs.
                // The count indicates the duplicate number
                string otherFilesPath;
                for (int previousFileIndex = selectedRowIndex - 1; previousFileIndex >= 0; previousFileIndex--)
                {
                    previousOrNextImageRow = DataHandler.FileDatabase.FileTable[previousFileIndex];
                    otherFilesPath = Path.Combine(previousOrNextImageRow.RelativePath, previousOrNextImageRow.File);
                    if (otherFilesPath == currentPath)
                    {
                        currentPosition++;
                    }
                    else
                    {
                        // We encountered a file with a different RelativePath/File name, so it cannot be a duplicate
                        break;
                    }
                }
                for (int nextFileIndex = selectedRowIndex + 1; nextFileIndex < DataHandler.FileDatabase.CountAllCurrentlySelectedFiles; nextFileIndex++)
                {
                    previousOrNextImageRow = DataHandler.FileDatabase.FileTable[nextFileIndex];
                    otherFilesPath = Path.Combine(previousOrNextImageRow.RelativePath, previousOrNextImageRow.File);
                    if (otherFilesPath == currentPath)
                    {
                        lastPosition++;
                    }
                    else
                    {
                        // We encountered a file with a different RelativePath/File name, so it cannot be a duplicate
                        break;
                    }
                }
                // The current counts are 0-based, so we add 1 to make it all 1-based
                return new(currentPosition + 1, currentPosition + lastPosition + 1);
            }
            catch
            {
                return new(0, 0);
            }
        }
    }
}
