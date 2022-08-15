using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Timelapse.Database;
using Timelapse.Enums;
using Timelapse.Util;

namespace Timelapse
{
    public partial class TimelapseWindow : Window, IDisposable
    {

        #region Duplicate the current record
        public async Task DuplicateCurrentRecord()
        {
            // Get the current image (or the selected image in the thumbnail grid) and duplicate it.
            // Note that this method shouldn't be called as the menueditDuplicate item will be disabled 
            // if the above conditions aren't met, but we check anyways.
            if (this.IsDisplayingSingleImage() == false)
            {
                // We only allow duplication if we are displaying a single image in the main view
                return;
            }

            // Get the current image
            ImageRow row = this.DataHandler.ImageCache.Current;
            FileInfo fileInfo = new FileInfo(row.File);

            // Create a duplicate of it
            ImageRow duplicate = row.DuplicateRowWithCoreValues(this.DataHandler.FileDatabase.FileTable.NewRow(fileInfo));

            // Insert the duplicated image into the filedata table
            List<ImageRow> imagesToInsert = new List<ImageRow> { duplicate };
            this.DataHandler.FileDatabase.AddFiles(imagesToInsert, null);

            if (GlobalReferences.DetectionsExists)
            {
                // Get the ID of the duplicate file that was just inserted into the filedata table
                int duplicateFileID = this.DataHandler.FileDatabase.GetLastInsertedRow(Constant.DBTables.FileData, Constant.DatabaseColumn.ID);

                // Get the detections associated with the current row, if any
                DataRow[] detectionRows = this.DataHandler.FileDatabase.GetDetectionsFromFileID(row.ID);
                if (detectionRows.Length > 0)
                {
                    // Create a new detection for each detection row, but using the duplicate's ID
                    List<List<ColumnTuple>> detectionInsertionStatements = new List<List<ColumnTuple>>();
                    List<List<ColumnTuple>> classificationInsertionStatements = new List<List<ColumnTuple>>();
                    foreach (DataRow detectionRow in detectionRows)
                    {
                        detectionInsertionStatements.Clear();

                        // Fill it in with the current file's detection values
                        List<ColumnTuple> detectionColumnsToUpdate = new List<ColumnTuple>()
                        {
                            new ColumnTuple(Constant.DetectionColumns.ImageID, duplicateFileID),
                            new ColumnTuple(Constant.DetectionColumns.Category, (string) detectionRow[1]),
                            new ColumnTuple(Constant.DetectionColumns.Conf, (float) Convert.ToDouble(detectionRow[2])),
                            new ColumnTuple(Constant.DetectionColumns.BBox, (string) detectionRow[3]),
                        };
                        detectionInsertionStatements.Add(detectionColumnsToUpdate);

                        // Insert the detections into the Detections table
                        this.DataHandler.FileDatabase.InsertDetection(detectionInsertionStatements);

                        // Get the ID of the duplicate file that was just inserted into the filedata table
                        int detectionID = this.DataHandler.FileDatabase.GetLastInsertedRow(Constant.DBTables.Detections, Constant.DetectionColumns.DetectionID);

                        // Now get the classifications associated with each detection, if any
                        DataRow[] classificationDataTableRows = this.DataHandler.FileDatabase.GetClassificationsFromDetectionID((long)detectionRow[0]);
                        if (classificationDataTableRows.Length > 0)
                        {
                            // Fill it in with the current file's classification values
                            classificationInsertionStatements.Clear();
                            foreach (DataRow classificationRow in classificationDataTableRows)
                            {
                                List<ColumnTuple> classificationColumnsToUpdate = new List<ColumnTuple>()
                                {
                                    new ColumnTuple(Constant.ClassificationColumns.DetectionID, detectionID),
                                    new ColumnTuple(Constant.ClassificationColumns.Category, (string)classificationRow[1]),
                                    new ColumnTuple(Constant.ClassificationColumns.Conf, (float)Convert.ToDouble(classificationRow[2]))
                                };
                                classificationInsertionStatements.Add(classificationColumnsToUpdate);
                            }
                            // Instert the classifications into the Classifications table
                            this.DataHandler.FileDatabase.InsertClassifications(classificationInsertionStatements);
                        }
                    }
                }

                // Regenerate the internal detections and classifications table to include the new detections andclassifications
                this.DataHandler.FileDatabase.RefreshDetectionsDataTable();
                this.DataHandler.FileDatabase.RefreshClassificationsDataTable();

                // Check if we need this...
                this.DataHandler.FileDatabase.IndexCreateForDetectionsAndClassificationsIfNotExists();
            }
            await this.FilesSelectAndShowAsync();
            this.TryFileShowWithoutSliderCallback(DirectionEnum.Next);
        }
        #endregion


        // Manage the display of the duplicate indicator (text in the form Duplicate: x/y) in the main window.
        public void DuplicateDisplayIndicatorInImageIfWarranted()
        {
            if (this.IsDisplayingMultipleImagesInOverview())
            {
                this.DuplicateIndicatorInMainWindow.Visibility = Visibility.Collapsed;
            }
            else
            {
                if (Keyboard.IsKeyDown(Key.H))
                {
                    this.DuplicateIndicatorInMainWindow.Visibility = Visibility.Collapsed;
                    return;
                }
                // Display the text "Duplicate x/y" if needed
                // The returned point will be 1,1 if there are no duplicates,
                // or position,count if there are duplicates e.g. 2/4 means its the 2nd image in a set of 4 duplicates
                Point duplicateSequence = DuplicatesCheckIfDuplicateAndGetSequenceNumberIfAny();
                if (duplicateSequence.Y > 1)
                {
                    this.DuplicateIndicatorInMainWindow.Visibility = Visibility.Visible;
                    this.DuplicateIndicatorInMainWindow.Text = String.Format("Duplicate: {0}/{1}", duplicateSequence.X, duplicateSequence.Y);
                }
                else
                {
                    this.DuplicateIndicatorInMainWindow.Visibility = Visibility.Collapsed;
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
            if (this.DataHandler?.FileDatabase == null)
            {
                return new Point(0, 0);
            }

            return this.DuplicatesCheckIfDuplicateAndGetSequenceNumberIfAny(this.DataHandler.ImageCache.Current, this.DataHandler.ImageCache.CurrentRow);
        }

        public Point DuplicatesCheckIfDuplicateAndGetSequenceNumberIfAny(ImageRow selectedImageRow, int selectedRowIndex)
        {
            try
            {
                int currentPosition = 0;
                int lastPosition = 0;
                if (this.DataHandler?.FileDatabase?.CountAllCurrentlySelectedFiles <= 0 || selectedRowIndex < 0)
                {
                    // There are no images to navigate
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
                    previousOrNextImageRow = this.DataHandler.FileDatabase.FileTable[previousFileIndex];
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
                for (int nextFileIndex = selectedRowIndex + 1; nextFileIndex < this.DataHandler.FileDatabase.CountAllCurrentlySelectedFiles; nextFileIndex++)
                {
                    previousOrNextImageRow = this.DataHandler.FileDatabase.FileTable[nextFileIndex];
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
