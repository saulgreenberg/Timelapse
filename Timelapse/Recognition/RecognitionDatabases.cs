using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Globalization;
using System.IO;
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
                ClearDetectionTables(database, true, true, true, clearDBRecognitionData, clearDBRecognitionData);
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
            List<SchemaColumnDefinition> columnDefinitions = new List<SchemaColumnDefinition>
            {
                new SchemaColumnDefinition(InfoColumns.InfoID, Sql.IntegerType + Sql.PrimaryKey), // Primary Key
                new SchemaColumnDefinition(InfoColumns.Detector,  Sql.StringType),
                new SchemaColumnDefinition(InfoColumns.DetectorVersion,  Sql.StringType, RecognizerValues.MDVersionUnknown),
                new SchemaColumnDefinition(InfoColumns.DetectionCompletionTime,  Sql.StringType),
                new SchemaColumnDefinition(InfoColumns.Classifier,  Sql.StringType),
                new SchemaColumnDefinition(InfoColumns.ClassificationCompletionTime,  Sql.StringType),
                new SchemaColumnDefinition(InfoColumns.TypicalDetectionThreshold, Sql.Real, RecognizerValues.DefaultTypicalDetectionThresholdIfUnknown),
                new SchemaColumnDefinition(InfoColumns.ConservativeDetectionThreshold, Sql.Real, RecognizerValues.DefaultConservativeDetectionThresholdIfUnknown),
                new SchemaColumnDefinition(InfoColumns.TypicalClassificationThreshold, Sql.Real, RecognizerValues.DefaultTypicalClassificationThresholdIfUnknown)
            };
            database.CreateTable(DBTables.Info, columnDefinitions);

            // DetectionCategories
            columnDefinitions = new List<SchemaColumnDefinition>
            {
                new SchemaColumnDefinition(DetectionCategoriesColumns.Category,  Sql.StringType + Sql.PrimaryKey), // Primary Key
                new SchemaColumnDefinition(DetectionCategoriesColumns.Label,  Sql.StringType),
            };
            database.CreateTable(DBTables.DetectionCategories, columnDefinitions);

            // ClassificationCategories: create or clear table 
            columnDefinitions = new List<SchemaColumnDefinition>
            {
                new SchemaColumnDefinition(ClassificationCategoriesColumns.Category,  Sql.StringType + Sql.PrimaryKey), // Primary Key
                new SchemaColumnDefinition(ClassificationCategoriesColumns.Label,  Sql.StringType),
            };
            database.CreateTable(DBTables.ClassificationCategories, columnDefinitions);

            // Detections 
            columnDefinitions = new List<SchemaColumnDefinition>
            {
                new SchemaColumnDefinition(DetectionColumns.DetectionID, Sql.IntegerType + Sql.PrimaryKey),
                new SchemaColumnDefinition(DetectionColumns.Category,  Sql.StringType),
                new SchemaColumnDefinition(DetectionColumns.Conf,  Sql.Real),
                new SchemaColumnDefinition(DetectionColumns.BBox,  Sql.StringType), // Will need to parse it into new new double[4]
                new SchemaColumnDefinition(DetectionColumns.ImageID, Sql.IntegerType), // Foreign key: ImageID
                new SchemaColumnDefinition("FOREIGN KEY ( " + DetectionColumns.ImageID + " )", "REFERENCES " + DBTables.FileData + " ( " + DetectionColumns.ImageID + " ) " + " ON DELETE CASCADE "),
            };
            database.CreateTable(DBTables.Detections, columnDefinitions);

            // Classifications 
            columnDefinitions = new List<SchemaColumnDefinition>
            {
                new SchemaColumnDefinition(ClassificationColumns.ClassificationID, Sql.IntegerType + Sql.PrimaryKey),
                new SchemaColumnDefinition(ClassificationColumns.Category, Sql.StringType),
                new SchemaColumnDefinition(ClassificationColumns.Conf,  Sql.Real),
                new SchemaColumnDefinition(ClassificationColumns.DetectionID, Sql.IntegerType), // Foreign key: ImageID
                new SchemaColumnDefinition("FOREIGN KEY ( " + ClassificationColumns.DetectionID + " )", "REFERENCES " + DBTables.Detections + " ( " + ClassificationColumns.DetectionID + " ) " + " ON DELETE CASCADE "),
            };
            database.CreateTable(DBTables.Classifications, columnDefinitions);
        }
        #endregion

        #region Public: Clear Detection Tables
        public static void ClearDetectionTables(SQLiteWrapper database, bool clearInfo, bool clearDetectionCategories, bool clearClassificationCategories, bool clearDetections, bool clearClassifications)
        {
            // Check the arguments for null 
            ThrowIf.IsNullArgument(database, nameof(database));

            // Create a list of tables to clear
            List<string> detectionTables = new List<string>();
            if (clearInfo) detectionTables.Add(DBTables.Info);
            if (clearDetectionCategories) detectionTables.Add(DBTables.DetectionCategories);
            if (clearClassificationCategories) detectionTables.Add(DBTables.ClassificationCategories);
            if (clearDetections) detectionTables.Add(DBTables.Detections);
            if (clearClassifications) detectionTables.Add(DBTables.Classifications);

            if (detectionTables.Count > 0)
            {
                database.DeleteAllRowsInTables(detectionTables);
            }
        }
        #endregion

        #region Public: Populate Detection Tables
        // Populate the various Detection Database Tables from the detection data structure.
        // The startDetectionID should be greater than any existing detection ID in the detection table. 
        // This is necessary to make sure we don't add duplicate keys if we are merging detections
        public static void PopulateTables(Recognizer recognizer, FileDatabase fileDatabase, SQLiteWrapper detectionDB, string pathPrefixForTruncation, int startDetectionID, int startClassificationID, IProgress<ProgressBarArguments> progress)
        {
            // Check the arguments for null 
            ThrowIf.IsNullArgument(recognizer, nameof(recognizer));
            ThrowIf.IsNullArgument(fileDatabase, nameof(fileDatabase));
            ThrowIf.IsNullArgument(detectionDB, nameof(detectionDB));
            ThrowIf.IsNullArgument(pathPrefixForTruncation, nameof(pathPrefixForTruncation));

            progress.Report(new ProgressBarArguments(0, "Adding new recognitions...", false, true));
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
            List<ColumnTuple> columnsToUpdate = new List<ColumnTuple>
            {
                new ColumnTuple(InfoColumns.InfoID, 1),
                new ColumnTuple(InfoColumns.Detector, recognizer.info.detector),
                new ColumnTuple(InfoColumns.DetectionCompletionTime, recognizer.info.detection_completion_time),
                new ColumnTuple(InfoColumns.Classifier, recognizer.info.classifier),
                new ColumnTuple(InfoColumns.ClassificationCompletionTime, recognizer.info.classification_completion_time),
                new ColumnTuple(InfoColumns.DetectorVersion, recognizer.info.detector_metadata.megadetector_version),
                new ColumnTuple(InfoColumns.TypicalDetectionThreshold, typicalDetectionThreshold),
                new ColumnTuple(InfoColumns.ConservativeDetectionThreshold, typicalConservativeDetectionThreshold),
                new ColumnTuple(InfoColumns.TypicalClassificationThreshold, typicalClassificationThreshold),
            };
            List<List<ColumnTuple>> insertionStatements = new List<List<ColumnTuple>>
            {
                columnsToUpdate
            };
            detectionDB.Insert(DBTables.Info, insertionStatements);

            // DetectionCategories:  Populate
            if (recognizer.detection_categories != null || recognizer.detection_categories?.Count > 0)
            {
                bool emptyCategoryExists = false;
                insertionStatements = new List<List<ColumnTuple>>();
                foreach (KeyValuePair<string, string> detection_category in recognizer.detection_categories)
                {
                    if (detection_category.Key == RecognizerValues.NoDetectionCategory)
                    {
                        emptyCategoryExists = true;
                    }
                    // Populate each detection category row
                    columnsToUpdate = new List<ColumnTuple>
                    {
                        new ColumnTuple(DetectionCategoriesColumns.Category, detection_category.Key),
                        new ColumnTuple(DetectionCategoriesColumns.Label, detection_category.Value)
                    };
                    insertionStatements.Add(columnsToUpdate);
                }
                if (emptyCategoryExists == false)
                {
                    // If its not defined, include the category '0' for Empty i.e., no detections.
                    columnsToUpdate = new List<ColumnTuple>
                    {
                        new ColumnTuple(DetectionCategoriesColumns.Category, RecognizerValues.NoDetectionCategory),
                        new ColumnTuple(DetectionCategoriesColumns.Label, RecognizerValues.NoDetectionLabel)
                    };
                    insertionStatements.Insert(0, columnsToUpdate);
                }
                detectionDB.Insert(DBTables.DetectionCategories, insertionStatements);
            }

            // ClassificationCategories:  Populate
            if (recognizer.classification_categories != null && recognizer.classification_categories.Count > 0)
            {
                insertionStatements = new List<List<ColumnTuple>>();
                foreach (KeyValuePair<string, string> classification_category in recognizer.classification_categories)
                {
                    // Populate each classification category row
                    columnsToUpdate = new List<ColumnTuple>
                    {
                        new ColumnTuple(ClassificationCategoriesColumns.Category, classification_category.Key),
                        new ColumnTuple(ClassificationCategoriesColumns.Label, classification_category.Value)
                    };
                    insertionStatements.Add(columnsToUpdate);
                }
                detectionDB.Insert(DBTables.ClassificationCategories, insertionStatements);
            }

            // Images and Detections:  Populate
            if (recognizer.images != null && recognizer.images.Count > 0)
            {
                int detectionIndex = startDetectionID;
                int classificationIndex = startClassificationID;
                List<List<ColumnTuple>> detectionInsertionStatements = new List<List<ColumnTuple>>();
                List<List<ColumnTuple>> classificationInsertionStatements = new List<List<ColumnTuple>>();

                // Get a data table containing the ID, RelativePath, and File
                // and create primary keys for the fields we will search for (for performance speedup)
                // We will use that to search for the file index.
                string query = Sql.Select + DatabaseColumn.ID + Sql.Comma + DatabaseColumn.RelativePath + Sql.Comma + DatabaseColumn.File + Sql.From + DBTables.FileData;
                DataTable dataTable = detectionDB.GetDataTableFromSelect(query);
                dataTable.PrimaryKey = new[]
                {
                    dataTable.Columns[DatabaseColumn.ID],
                    dataTable.Columns[DatabaseColumn.File],
                    dataTable.Columns[DatabaseColumn.RelativePath],
                };

                int j = 0;
                int totalFiles = recognizer.images.Count;
                foreach (image image in recognizer.images)
                {
                    if (j % 10000 == 0)
                    {
                        progress.Report(new ProgressBarArguments(Convert.ToInt32(j * 100.0 / totalFiles),
                            $"Adding new recognitions ({j:N0}/{totalFiles:N0})...", false, false));
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
                        imageFile = image.file.Substring(pathPrefixForTruncation.Length);
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
                        if (Int32.TryParse(t[0].ToString(), out int id))
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
                                if (detection.conf < RecognizerValues.MinimumDetectionValue)
                                {
                                    // Timelapse enforces a minimum detection confidence. That is, any value less than the MinimumDetectionValue 
                                    // is automatically thrown away
                                    continue;
                                }

                                // Populate each classification category row, making sure the bounding box  will be written with decimal places (in case its a ,-based culture)
                                string bboxAsString = (detection.bbox == null || detection.bbox.Length != 4)
                                    ? string.Empty
                                    : String.Format(CultureInfo.InvariantCulture, "{0}, {1}, {2}, {3}", detection.bbox[0], detection.bbox[1], detection.bbox[2], detection.bbox[3]);
                                detection.detectionID = detectionIndex;
                                noDetectionsIncluded = false;

                                // Note: The ColumnTuple for floats (i.e., detection.conf) takes care of writing
                                // floats in invariant culture format (so ',' decimal separators are avoided)
                                List<ColumnTuple> detectionColumnsToUpdate = new List<ColumnTuple>
                                {
                                    new ColumnTuple(DetectionColumns.DetectionID, detection.detectionID),
                                    new ColumnTuple(DetectionColumns.ImageID, image.imageID),
                                    new ColumnTuple(DetectionColumns.Category, detection.category),
                                    new ColumnTuple(DetectionColumns.Conf, detection.conf),
                                    new ColumnTuple(DetectionColumns.BBox, bboxAsString),
                                };
                                // Debug.Print("id:"+image.imageID.ToString());
                                detectionInsertionStatements.Add(detectionColumnsToUpdate);

                                // If the detection has some classification info, then add that to the classifications data table
                                foreach (Object[] classification in detection.classifications)
                                {
                                    double conf = Double.Parse(classification[1].ToString());
                                    // Timelapse also enforces a minimum recognition confidence. That is, any value less than the MinimumRecognitoinValue 
                                    // is automatically thrown away. Note that this means that the confidence probabilities may not sum to 1
                                    if (conf < RecognizerValues.MinimumRecognitionValue)
                                    {
                                        continue;
                                    }
                                    // Debug.Print(String.Format("{0} {1} {2}", detection.detectionID, category, conf));
                                    List<ColumnTuple> classificationColumnsToUpdate = new List<ColumnTuple>
                                    {
                                        new ColumnTuple(ClassificationColumns.ClassificationID, classificationIndex),
                                        new ColumnTuple(ClassificationColumns.DetectionID, detection.detectionID),
                                        new ColumnTuple(ClassificationColumns.Category, (string)classification[0]),
                                        new ColumnTuple(ClassificationColumns.Conf, String.Format(CultureInfo.InvariantCulture, "{0}", (float)Double.Parse(classification[1].ToString()))),
                                    };
                                    classificationInsertionStatements.Add(classificationColumnsToUpdate);
                                    classificationIndex++;
                                }
                                detectionIndex++;
                            }
                        }
                        // If there are no detections, we populate it with values that indicate that.
                        if (image.detections.Count == 0 || noDetectionsIncluded)
                        {
                            string bboxAsString = string.Empty;
                            List<ColumnTuple> detectionColumnsToUpdate = new List<ColumnTuple>
                            {
                                new ColumnTuple(DetectionColumns.DetectionID, detectionIndex++),
                                new ColumnTuple(DetectionColumns.ImageID, image.imageID),
                                new ColumnTuple(DetectionColumns.Category, RecognizerValues.NoDetectionCategory),
                                new ColumnTuple(DetectionColumns.Conf, 0),
                                new ColumnTuple(DetectionColumns.BBox, bboxAsString),
                            };
                            detectionInsertionStatements.Add(detectionColumnsToUpdate);
                        }
                    }
                }
                detectionDB.Insert(DBTables.Detections, detectionInsertionStatements, progress, "Adding detections");
                detectionDB.Insert(DBTables.Classifications, classificationInsertionStatements, progress, "Adding classifications");
                fileDatabase.IndexCreateForDetectionsAndClassificationsIfNotExists();
                dataTable?.Dispose();
            }
        }
        #endregion
    }
}