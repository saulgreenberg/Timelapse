using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Windows;
using System.Windows.Input;
using Timelapse.Constant;
using Timelapse.DataStructures;
using Timelapse.DataTables;
using Timelapse.DebuggingSupport;
using Timelapse.Enums;

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
        public void DuplicateCurrentRecord()
        {
            // Get the current image (or the selected image in the thumbnail grid) and duplicate it.
            // Note that this method shouldn't be called as the menueditDuplicate item will be disabled 
            // if the above conditions aren't met, but we check anyways.
            if (IsDisplayingSingleImage() == false)
            {
                // We only allow duplication if we are displaying a single image in the main view
                return;
            }

            // Get the current image
            ImageRow row = DataHandler.ImageCache.Current;
            if (row == null)
            {
                //Shouldn't happen
                TracePrint.NullException(nameof(row));
                return;
            }
            FileInfo fileInfo = new FileInfo(row.File);

            // Create a duplicate of it
            ImageRow duplicate = row.DuplicateRowWithCoreValues(DataHandler.FileDatabase.FileTable.NewRow(fileInfo));

            // Add the row to the database
            List<ImageRow> imagesToInsert = new List<ImageRow> { duplicate };
            DataHandler.FileDatabase.AddFiles(imagesToInsert, null);

            // Insert the duplicated image into the filedata table. Note that we have to reset the slider to correctly show the new file count.
            long insertedDuplicateID = DataHandler.FileDatabase.Database.ScalarGetMaxValueAsLong(Constant.DBTables.FileData, Constant.DatabaseColumn.ID);
            DataTable table = DataHandler.FileDatabase.Database.GetDataTableFromSelect($"Select * from DataTable where ID={Sql.Quote(insertedDuplicateID.ToString())}");
            if (table.Rows.Count == 1)
            {
                int fileIndexForInsert = DataHandler.ImageCache.CurrentRow + 1;
                DataHandler.FileDatabase.FileTable.InsertRow(fileIndexForInsert, table.Rows[0]);
                this.FileNavigatorSliderReset();
            }
            
            // DELETE THIS AS NEW STRATE
            // We want the select to display this duplicate (and all its companion duplicates for this image). So we create a search term
            // that specifies the RelativePath and File. Later, the FilesSelectAndShowAsync will then include it as an exception,
            // where it will be added to the select criteria within a WHERE.
            //DataHandler.FileDatabase.CustomSelection.DuplicatesRelativePathAndFileTuple = new Tuple<string, string>(duplicate.RelativePath, duplicate.File);

            if (GlobalReferences.DetectionsExists)
            {
                // Get the ID of the duplicate file that was just inserted into the filedata table
                long duplicateFileID = DataHandler.FileDatabase.GetLastInsertedRow(DBTables.FileData, DatabaseColumn.ID);

                // Get the detections associated with the current row, if any
                DataRow[] detectionRows = DataHandler.FileDatabase.GetDetectionsFromFileID(row.ID);
                if (detectionRows.Length > 0)
                {
                    // Create a new detection for each detection row, but using the duplicate's ID
                    List<List<ColumnTuple>> detectionInsertionStatements = new List<List<ColumnTuple>>();
                    List<List<ColumnTuple>> classificationInsertionStatements = new List<List<ColumnTuple>>();
                    foreach (DataRow detectionRow in detectionRows)
                    {
                        detectionInsertionStatements.Clear();

                        // Fill it in with the current file's detection values
                        List<ColumnTuple> detectionColumnsToUpdate = new List<ColumnTuple>
                        {
                            new ColumnTuple(DetectionColumns.ImageID, duplicateFileID),
                            new ColumnTuple(DetectionColumns.Category, (string) detectionRow[1]),
                            new ColumnTuple(DetectionColumns.Conf, (float) Convert.ToDouble(detectionRow[2])),
                            new ColumnTuple(DetectionColumns.BBox, (string) detectionRow[3]),
                        };
                        detectionInsertionStatements.Add(detectionColumnsToUpdate);

                        // Insert the detections into the Detections table
                        DataHandler.FileDatabase.InsertDetection(detectionInsertionStatements);

                        // Get the ID of the duplicate file that was just inserted into the filedata table
                        long detectionID = DataHandler.FileDatabase.GetLastInsertedRow(DBTables.Detections, DetectionColumns.DetectionID);

                        // Now get the classifications associated with each detection, if any
                        DataRow[] classificationDataTableRows = DataHandler.FileDatabase.GetClassificationsFromDetectionID((long)detectionRow[0]);
                        if (classificationDataTableRows.Length > 0)
                        {
                            // Fill it in with the current file's classification values
                            classificationInsertionStatements.Clear();
                            foreach (DataRow classificationRow in classificationDataTableRows)
                            {
                                List<ColumnTuple> classificationColumnsToUpdate = new List<ColumnTuple>
                                {
                                    new ColumnTuple(ClassificationColumns.DetectionID, detectionID),
                                    new ColumnTuple(ClassificationColumns.Category, (string)classificationRow[1]),
                                    new ColumnTuple(ClassificationColumns.Conf, (float)Convert.ToDouble(classificationRow[2]))
                                };
                                classificationInsertionStatements.Add(classificationColumnsToUpdate);
                            }
                            // Instert the classifications into the Classifications table
                            DataHandler.FileDatabase.InsertClassifications(classificationInsertionStatements);
                        }
                    }
                }

                // Regenerate the internal detections and classifications table to include the new detections andclassifications
                DataHandler.FileDatabase.RefreshDetectionsDataTable();
                DataHandler.FileDatabase.RefreshClassificationsDataTable();

                // Check if we need this...
                DataHandler.FileDatabase.IndexCreateForDetectionsAndClassificationsIfNotExists();
            }
            TryFileShowWithoutSliderCallback(DirectionEnum.Next);
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
                return new Point(0, 0);
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
                    return new Point(0, 0);
                }

                if (DataHandler?.FileDatabase == null)
                {
                    // Shouldn't happen
                    TracePrint.NullException(nameof(DataHandler.FileDatabase));
                    return new Point(0, 0);
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
                return new Point(currentPosition + 1, currentPosition + lastPosition + 1);
            }
            catch
            {
                return new Point(0, 0);
            }
        }
    }
}
