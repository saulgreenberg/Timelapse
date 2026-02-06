using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading;
using Timelapse.Constant;
using Timelapse.Controls;
using Timelapse.Database;
using Timelapse.DataStructures;
using Timelapse.Util;

namespace Timelapse.Recognition
{
    public static class RecognitionDatabases
    {
        #region Public: Prepare  all recognition-related Database Tables
        // Prepare all recognition-related database tables by creating them, or updating them as needed.
        public static void PrepareRecognitionTablesAndColumns(SQLiteWrapper database, bool existsDBRecognitionTables)
        {
            PrepareRecognitionTablesAndColumns(database, existsDBRecognitionTables, false);
        }
        public static void PrepareRecognitionTablesAndColumns(SQLiteWrapper database, bool existsDBRecognitionTables, bool clearDBRecognitionData)
        {
            // Check the arguments for null 
            ThrowIf.IsNullArgument(database, nameof(database));

            if (existsDBRecognitionTables)
            {
                // Case 1. Tables already exist, so clear their contents as indicated.
                // Always clear info and category tables, but
                // Only clear detections and recognitions as indicated by the clearDBRecognitionData argument
                ClearRecognitionTables(database, true, true, true, clearDBRecognitionData);
            }
            else
            {
                // Case 2. No recognition tables are present, so create them
                CreateRecognitionTables(database);
            }
        }
        #endregion

        #region Public: Create the various recognition tables
        // Create the various recognition tables in the database provided.
        // Assumes that the database exists and that the tables aren't already in them.
        public static void CreateRecognitionTables(SQLiteWrapper database)
        {
            // Info table
            List<SchemaColumnDefinition> columnDefinitions =
            [
                new(InfoColumns.InfoID, Sql.IntegerType + Sql.PrimaryKey), // Primary Key
                new(InfoColumns.Detector, Sql.StringType),
                new(InfoColumns.DetectorVersion, Sql.StringType, RecognizerValues.MDVersionUnknown),
                new(InfoColumns.DetectionCompletionTime, Sql.StringType),
                new(InfoColumns.Classifier, Sql.StringType),
                new(InfoColumns.ClassificationCompletionTime, Sql.StringType),
                new(InfoColumns.TypicalDetectionThreshold, Sql.Real, RecognizerValues.DefaultTypicalDetectionThresholdIfUnknown),
                new(InfoColumns.ConservativeDetectionThreshold, Sql.Real, RecognizerValues.DefaultConservativeDetectionThresholdIfUnknown),
                new(InfoColumns.TypicalClassificationThreshold, Sql.Real, RecognizerValues.DefaultTypicalClassificationThresholdIfUnknown)
            ];
            database.CreateTable(DBTables.Info, columnDefinitions);

            // DetectionCategories
            columnDefinitions =
            [
                new(DetectionCategoriesColumns.Category, Sql.StringType + Sql.PrimaryKey), // Primary Key
                new(DetectionCategoriesColumns.Label, Sql.StringType)
            ];
            database.CreateTable(DBTables.DetectionCategories, columnDefinitions);

            // ClassificationCategories: create or clear table 
            columnDefinitions =
            [
                new(ClassificationCategoriesColumns.Category, Sql.StringType + Sql.PrimaryKey), // Primary Key
                new(ClassificationCategoriesColumns.Label, Sql.StringType),
                new(ClassificationCategoriesColumns.Description, Sql.StringType, string.Empty)
            ];
            database.CreateTable(DBTables.ClassificationCategories, columnDefinitions);

            //// ClassificationCategoryDescriptions: create or clear table 
            //// The column names are identical to the ClassificationCategories table, but the table is used to store descriptions of the categories
            //columnDefinitions = new List<SchemaColumnDefinition>
            //{
            //    new SchemaColumnDefinition(ClassificationCategoriesColumns.Category,  Sql.StringType + Sql.PrimaryKey), // Primary Key
            //    new SchemaColumnDefinition(ClassificationCategoriesColumns.Label,  Sql.StringType),
            //};
            //database.CreateTable(DBTables.ClassificationDescriptions, columnDefinitions);

            // Detections 
            columnDefinitions =
            [
                new(DetectionColumns.DetectionID, Sql.IntegerType + Sql.PrimaryKey),
                new(DetectionColumns.Category, Sql.StringType),
                new(DetectionColumns.Conf, Sql.Real),
                new(DetectionColumns.BBox, Sql.StringType), // Will need to parse it into new new double[4]
                new(DetectionColumns.Classification, Sql.StringType),
                new(DetectionColumns.ClassificationConf, Sql.Real),
                new(DetectionColumns.ImageID, Sql.IntegerType), // Foreign key: ImageID
                new("FOREIGN KEY ( " + DetectionColumns.ImageID + " )",
                    "REFERENCES " + DBTables.FileData + " ( " + DetectionColumns.ImageID + " ) " + " ON DELETE CASCADE ")
            ];
            database.CreateTable(DBTables.Detections, columnDefinitions);

            // Detections Video
            RecognitionDatabases.CreateDetectionsVideoTable(database);

            // Classifications - only used for backwards compatability
            columnDefinitions =
            [
                new(ClassificationColumns.ClassificationID, Sql.IntegerType + Sql.PrimaryKey),
                new(ClassificationColumns.Category, Sql.StringType),
                new(ClassificationColumns.Conf, Sql.Real),
                new(ClassificationColumns.DetectionID, Sql.IntegerType) // Foreign key: ImageID
                //new SchemaColumnDefinition("FOREIGN KEY ( " + ClassificationColumns.DetectionID + " )", "REFERENCES " + DBTables.Detections + " ( " + ClassificationColumns.DetectionID + " ) " + " ON DELETE CASCADE "),
            ];
            database.CreateTable(DBTables.Classifications, columnDefinitions);

        }

        // This is its own method as we also invoke it elsewhere
        public static void CreateDetectionsVideoTable(SQLiteWrapper database)
        {
            // Detections Video
            List<SchemaColumnDefinition> columnDefinitions =
            [
                new(DetectionColumns.FrameNumber, Sql.IntegerType),
                new(DetectionColumns.FrameRate, Sql.RealType),
                new(DetectionColumns.DetectionID, Sql.IntegerType), // Foreign key: DetectionID
                new("FOREIGN KEY ( " + DetectionColumns.DetectionID + " )",
                    "REFERENCES " + DBTables.Detections + " ( " + DetectionColumns.DetectionID + " ) " + " ON DELETE CASCADE ")
            ];
            database.CreateTable(DBTables.DetectionsVideo, columnDefinitions);
        }
        #endregion

        #region Public: Clear Recognition Tables
        public static void ClearRecognitionTables(SQLiteWrapper database, bool clearInfo, bool clearDetectionCategories, bool clearClassificationCategories, bool clearDetections)
        {
            // Check the arguments for null 
            ThrowIf.IsNullArgument(database, nameof(database));

            // Create a list of tables to clear
            List<string> recognitionTables = [];
            if (clearInfo) recognitionTables.Add(DBTables.Info);
            if (clearDetectionCategories) recognitionTables.Add(DBTables.DetectionCategories);
            if (clearClassificationCategories)
            {
                recognitionTables.Add(DBTables.ClassificationCategories);
                // recognitionTables.Add(DBTables.ClassificationDescriptions);
            }

            if (clearDetections)
            {
                // When we clear Detections, DetectionsVideo should also be cleared.
                // Even though it would be done anyways as it maintains a foreign key to the DetectionsID,I think this is more efficient.
                recognitionTables.Add(DBTables.DetectionsVideo);
                recognitionTables.Add(DBTables.Detections);

            }
            if (recognitionTables.Count > 0)
            {
                database.DeleteAllRowsInTables(recognitionTables);
            }
        }
        #endregion

        #region Public: Populate Recognition Tables
        // Populate the various Recognition Database Tables from the detection data structure.
        // The startDetectionID should be greater than any existing detection ID in the detection table. 
        // This is necessary to make sure we don't add duplicate keys if we are merging detections
        public static void PopulateTables(Recognizer recognizer, FileDatabase fileDatabase, SQLiteWrapper detectionDB, string pathPrefixForTruncation, long startDetectionID, IProgress<ProgressBarArguments> progress, int progressFrequency)
        {
            // Check the arguments for null 
            ThrowIf.IsNullArgument(recognizer, nameof(recognizer));
            ThrowIf.IsNullArgument(fileDatabase, nameof(fileDatabase));
            ThrowIf.IsNullArgument(detectionDB, nameof(detectionDB));
            ThrowIf.IsNullArgument(pathPrefixForTruncation, nameof(pathPrefixForTruncation));

            progress.Report(new(0, $"Adding {recognizer.images.Count} new recognitions. Please wait...", false, true));
            Thread.Sleep(ThrottleValues.RenderingBackoffTime);  // Allows the UI thread to update every now and then
            // Updating many rows is made hugely more efficient if we create an index for File and Relative Path
            // as otherwise each update is in linear time to the table rows vs log time. 
            // Because we will not need these indexes later, we will drop them after the updates are done

            // Info Table: Populate
            float typicalDetectionThreshold = recognizer.info.detector_metadata.typical_detection_threshold
                                              ?? RecognizerValues.DefaultTypicalDetectionThresholdIfUnknown;
            float typicalConservativeDetectionThreshold = recognizer.info.detector_metadata.conservative_detection_threshold
                                                          ?? RecognizerValues.DefaultConservativeDetectionThresholdIfUnknown;
            float typicalClassificationThreshold = recognizer.info.classifier_metadata.typical_classification_threshold
                                                   ?? RecognizerValues.DefaultTypicalClassificationThresholdIfUnknown;
            List<ColumnTuple> columnsToUpdate =
            [
                new(InfoColumns.InfoID, 1),
                new(InfoColumns.Detector, recognizer.info.detector),
                new(InfoColumns.DetectionCompletionTime, recognizer.info.detection_completion_time),
                new(InfoColumns.Classifier, recognizer.info.classifier),
                new(InfoColumns.ClassificationCompletionTime, recognizer.info.classification_completion_time),
                new(InfoColumns.DetectorVersion, recognizer.info.detector_metadata.megadetector_version),
                new(InfoColumns.TypicalDetectionThreshold, typicalDetectionThreshold),
                new(InfoColumns.ConservativeDetectionThreshold, typicalConservativeDetectionThreshold),
                new(InfoColumns.TypicalClassificationThreshold, typicalClassificationThreshold)
            ];
            List<List<ColumnTuple>> insertionStatements = [columnsToUpdate];
            detectionDB.Insert(DBTables.Info, insertionStatements);

            // DetectionCategories:  Populate
            if (recognizer.detection_categories != null || recognizer.detection_categories?.Count > 0)
            {
                bool emptyCategoryExists = false;
                insertionStatements = [];
                foreach (KeyValuePair<string, string> detection_category in recognizer.detection_categories)
                {
                    if (detection_category.Key == RecognizerValues.NoDetectionCategory)
                    {
                        emptyCategoryExists = true;
                    }
                    // Populate each detection category row
                    columnsToUpdate =
                    [
                        new(DetectionCategoriesColumns.Category, detection_category.Key),
                        new(DetectionCategoriesColumns.Label, detection_category.Value)
                    ];
                    insertionStatements.Add(columnsToUpdate);
                }
                if (emptyCategoryExists == false)
                {
                    // If its not defined, include the category '0' for Empty i.e., no detections.
                    columnsToUpdate =
                    [
                        new(DetectionCategoriesColumns.Category, RecognizerValues.NoDetectionCategory),
                        new(DetectionCategoriesColumns.Label, RecognizerValues.EmptyDetectionLabel)
                    ];
                    insertionStatements.Insert(0, columnsToUpdate);
                }
                detectionDB.Insert(DBTables.DetectionCategories, insertionStatements);
            }

            // ClassificationCategories:  Populate
            if (recognizer.classification_categories is { Count: > 0 })
            {
                insertionStatements = [];
                foreach (KeyValuePair<string, string> classification_category in recognizer.classification_categories)
                {
                    // Populate each classification category row
                    columnsToUpdate =
                    [
                        new(ClassificationCategoriesColumns.Category, classification_category.Key),
                        new(ClassificationCategoriesColumns.Label, classification_category.Value)
                    ];
                    // Check if there is a matching description. If so, add it to the Descriptions column
                    if (recognizer.classification_category_descriptions is { Count: > 0 })
                    {
                        // Add the description if it exists
                        if (recognizer.classification_category_descriptions.TryGetValue(classification_category.Key, out string description))
                        {
                            columnsToUpdate.Add(new(ClassificationCategoriesColumns.Description, description));
                        }
                    }

                    insertionStatements.Add(columnsToUpdate);
                }
                detectionDB.Insert(DBTables.ClassificationCategories, insertionStatements);
            }

            //// ClassificationDescriptions:  Populate
            //if (recognizer.classification_category_descriptions != null && recognizer.classification_category_descriptions.Count > 0)
            //{
            //    insertionStatements = new List<List<ColumnTuple>>();
            //    foreach (KeyValuePair<string, string> description in recognizer.classification_category_descriptions)
            //    {
            //        // Populate each classification category row
            //        columnsToUpdate = new List<ColumnTuple>
            //        {
            //            new ColumnTuple(ClassificationCategoriesColumns.Category, description.Key),
            //            new ColumnTuple(ClassificationCategoriesColumns.Label, description.Value)
            //        };
            //        insertionStatements.Add(columnsToUpdate);
            //    }
            //    detectionDB.Insert(DBTables.ClassificationDescriptions, insertionStatements);
            //}

            // Images and Detections:  Populate
            if (recognizer.images is { Count: > 0 })
            {
                long detectionIndex = startDetectionID;
                List<List<ColumnTuple>> detectionInsertionStatements = [];
                List<List<ColumnTuple>> detectionVideoInsertionStatements = [];

                // Get a data table containing the ID, RelativePath, and File
                // and create primary keys for the fields we will search for (for performance speedup)
                // We will use that to search for the file index.
                string query = Sql.Select + DatabaseColumn.ID + Sql.Comma + DatabaseColumn.RelativePath + Sql.Comma + DatabaseColumn.File + Sql.From + DBTables.FileData;
                DataTable dataTable = detectionDB.GetDataTableFromSelect(query);
                dataTable.PrimaryKey =
                [
                    dataTable.Columns[DatabaseColumn.ID],
                    dataTable.Columns[DatabaseColumn.File],
                    dataTable.Columns[DatabaseColumn.RelativePath]
                ];

                int j = 1; // Delay progress reporting until after the first operation (which takes a long time) is completed, 
                int totalFiles = recognizer.images.Count;
                foreach (image image in recognizer.images)
                {
                    if (j % progressFrequency == 0)
                    {
                        progress.Report(new(Convert.ToInt32(j * 100.0 / totalFiles),
                            $"Adding new recognitions ({j:N0}/{totalFiles:N0})...", false, false));
                        Thread.Sleep(ThrottleValues.RenderingBackoffTime);  // Allows the UI thread to update every now and then
                    }
                    j++;

                    if (image.detections == null)
                    {
                        // The json file may actualy report some detections as null rather than an empty list, in which case we just skip it.
                        continue;
                    }
                    // The truncation prefix is a prefix of the folder path that should be removed from the file path (unless its empty, of course)
                    // As well, detections whose path is in the prefix should not be read in, as they are outside of this sub-folder
                    // It occurs when the actual images were in a subfolder, where that subfolder was read in separately as a datafile
                    // That is, the .tdb file was located in an image subfolder, rather than in the root folder where the detections were done
                    string imageFile;
                    if (string.IsNullOrEmpty(pathPrefixForTruncation))
                    {
                        imageFile = image.file;
                    }
                    else
                    {
                        if (image.file.StartsWith(pathPrefixForTruncation) == false)
                        {
                            // Skip images that start with the truncation string, as these are outside of the image set
                            // Debug.Print("Skipping: " + image.file);
                            continue;
                        }
                        // Remove the truncation prefex from the file path 
                        imageFile = image.file[pathPrefixForTruncation.Length..];
                        // Debug.Print("Using: " + image.file + " as " + imageFile);
                    }

                    // Form: FILE = Filename AND RELATIVEPATH = RelativePath
                    string queryFileRelativePath =
                         DatabaseColumn.File + Sql.Equal + Sql.Quote(Path.GetFileName(imageFile)) +
                         Sql.And +
                         DatabaseColumn.RelativePath + Sql.Equal + Sql.Quote(Path.GetDirectoryName(imageFile));

                    DataRow[] rows = dataTable.Select(queryFileRelativePath);
                    if (rows.Length == 0)
                    {
                        // Couldn't find the image. This could happen if that image and its data was deleted.
                        // This isn't a bug, as all we would do is skip that image.
                        // Debug.Print("Could not find: " + image.file);
                        continue;
                    }
                    foreach (DataRow t in rows)
                    {
                        // Get the image id from the image
                        // If we can't, just skip it (this should not happen)
                        if (long.TryParse(t[0].ToString(), out long id))
                        {
                            image.imageID = id;
                        }
                        else
                        {
                            Debug.Print("Invalid index: " + rows[0][0]);
                            continue;
                        }

                        // Populate the detections table per image.
                        bool noDetectionsIncluded = true;
                        if (image.detections.Count > 0)
                        {
                            foreach (detection detection in image.detections)
                            {
                                // Note that detections and classifications below a threshold were already removed from the Recognizer data structure
                                // when it was read in from the recognition file.

                                // Populate each classification category row, making sure the bounding box  will be written with decimal places (in case its a ,-based culture)
                                string bboxAsString = (detection.bbox == null || detection.bbox.Length != 4)
                                    ? string.Empty
                                    : String.Format(CultureInfo.InvariantCulture, "{0}, {1}, {2}, {3}", detection.bbox[0], detection.bbox[1], detection.bbox[2], detection.bbox[3]);
                                detection.detectionID = detectionIndex;
                                noDetectionsIncluded = false;

                                // Note: The ColumnTuple for floats (i.e., detection.conf) takes care of writing
                                // floats in invariant culture format (so ',' decimal separators are avoided)
                                if (Util.IsCondition.IsJPGExtension(image.file))
                                {
                                    // Its an image. Always add the detection
                                    List<ColumnTuple> detectionColumnsToUpdate =
                                    [
                                        new(DetectionColumns.DetectionID, detection.detectionID),
                                        new(DetectionColumns.ImageID, image.imageID),
                                        new(DetectionColumns.Category, detection.category),
                                        new(DetectionColumns.Conf, detection.conf),
                                        new(DetectionColumns.BBox, bboxAsString)
                                    ];

                                    // Add classification values to the detection row if they exist, otherwise they will be null
                                    if (detection.classifications?.Count > 0)
                                    {
                                        detectionColumnsToUpdate.Add(new(DetectionColumns.Classification, (string)detection.classifications[0][0]));
                                        detectionColumnsToUpdate.Add(new(DetectionColumns.ClassificationConf, String.Format(CultureInfo.InvariantCulture, "{0}", Double.Parse(detection.classifications[0][1].ToString()))));
                                    }
                                    detectionInsertionStatements.Add(detectionColumnsToUpdate);
                                }
                                else if (detection.frame_number >= 0 && image.frame_rate is > 0)
                                {
                                    // Its an video with a valid frame_rate/frame_number.
                                    // Note that the detection and its corresponding detectionVideo is omitted if the frame_rate/frame_number is not valid
                                    List<ColumnTuple> detectionColumnsToUpdate =
                                    [
                                        new(DetectionColumns.DetectionID, detection.detectionID),
                                        new(DetectionColumns.ImageID, image.imageID),
                                        new(DetectionColumns.Category, detection.category),
                                        new(DetectionColumns.Conf, detection.conf),
                                        new(DetectionColumns.BBox, bboxAsString)
                                    ];

                                    // Add classification values to the detection row if they exist, otherwise they will be null
                                    if (detection.classifications?.Count > 0)
                                    {
                                        // Add classification to the detection table as it exists
                                        detectionColumnsToUpdate.Add(new(DetectionColumns.Classification, (string)detection.classifications[0][0]));
                                        detectionColumnsToUpdate.Add(new(DetectionColumns.ClassificationConf, String.Format(CultureInfo.InvariantCulture, "{0}", Double.Parse(detection.classifications[0][1].ToString()))));
                                    }
                                    detectionInsertionStatements.Add(detectionColumnsToUpdate);

                                    // Now add the frame rate/frame number to the DetectionsVideo table
                                    List<ColumnTuple> detectionVideoColumnsToUpdate =
                                    [
                                        new(DetectionColumns.FrameNumber, detection.frame_number),
                                        new(DetectionColumns.FrameRate, image.frame_rate),
                                        new(DetectionColumns.DetectionID, detection.detectionID)
                                    ];
                                    detectionVideoInsertionStatements.Add(detectionVideoColumnsToUpdate);
                                }
                                detectionIndex++;
                            }
                        }
                        // If there are no detections, we populate it with values that indicate that.
                        // The classification columns will be null as they are not specified
                        if (image.detections.Count == 0 || noDetectionsIncluded)
                        {
                            string bboxAsString = string.Empty;
                            List<ColumnTuple> detectionColumnsToUpdate =
                            [
                                new(DetectionColumns.DetectionID, detectionIndex++),
                                new(DetectionColumns.ImageID, image.imageID),
                                new(DetectionColumns.Category, RecognizerValues.NoDetectionCategory),
                                new(DetectionColumns.Conf, 0),
                                new(DetectionColumns.BBox, bboxAsString)
                            ];
                            detectionInsertionStatements.Add(detectionColumnsToUpdate);
                        }
                    }
                }
                detectionDB.Insert(DBTables.Detections, detectionInsertionStatements, progress, "Adding detections", 1000);
                detectionDB.Insert(DBTables.DetectionsVideo, detectionVideoInsertionStatements, progress, "Adding detections for Video", 1000);
                fileDatabase.IndexCreateForDetectionsIfNeeded();
                dataTable?.Dispose();
            }
        }
        #endregion
    }
}