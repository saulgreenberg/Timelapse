using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Timelapse.Controls;
using Timelapse.Database;
using Timelapse.Enums;

namespace Timelapse.Recognition
{
    public static class RecognitionUtilities
    {
        #region Get detection info and various category tables from DB as dictionaries
        public static void GenerateRecognitionDictionariesFromDB(string ddbPath, Dictionary<string, object> infoDictionary, Dictionary<string, string> detectionCategoriesDictionary, Dictionary<string, string> classificationCategoriesDictionary)
        {
            SQLiteWrapper db = new SQLiteWrapper(ddbPath);
            RecognitionUtilities.GenerateRecognitionDictionariesFromDB(db, infoDictionary, detectionCategoriesDictionary, classificationCategoriesDictionary);
        }

        public static void GenerateRecognitionDictionariesFromDB(SQLiteWrapper db, Dictionary<string, object> infoDictionary, Dictionary<string, string> detectionCategoriesDictionary, Dictionary<string, string> classificationCategoriesDictionary)
        {
            if (false == db.TableExists(Constant.DBTables.Info))
            {
                // There are no detection-based tables in this database
                return;
            }
            using (DataTable dataTable = db.GetDataTableFromSelect(Sql.SelectStarFrom + Constant.DBTables.Info))
            {
                Dictionary<string, object> tmpDict = new Dictionary<string, object>();
                if (dataTable.Rows.Count != 0)
                {
                    DataRow row = dataTable.Rows[0];
                    tmpDict = row.Table.Columns
                            .Cast<DataColumn>()
                            .ToDictionary(c => c.ColumnName, c => row[c.ColumnName]);
                }
                foreach (KeyValuePair<string, object> item in tmpDict)
                {
                    infoDictionary.Add(item.Key, item.Value);
                }
            }

            using (DataTable dataTable = db.GetDataTableFromSelect(Sql.SelectStarFrom + Constant.DBTables.DetectionCategories))
            {
                int dataTableRowCount = dataTable.Rows.Count;
                for (int i = 0; i < dataTableRowCount; i++)
                {
                    DataRow row = dataTable.Rows[i];
                    detectionCategoriesDictionary.Add((string)row[Constant.DetectionCategoriesColumns.Category], (string)row[Constant.DetectionCategoriesColumns.Label]);
                }
            }
            using (DataTable dataTable = db.GetDataTableFromSelect(Sql.SelectStarFrom + Constant.DBTables.ClassificationCategories))
            {
                int dataTableRowCount = dataTable.Rows.Count;
                for (int i = 0; i < dataTableRowCount; i++)
                {
                    DataRow row = dataTable.Rows[i];
                    classificationCategoriesDictionary.Add((string)row[Constant.ClassificationCategoriesColumns.Category], (string)row[Constant.ClassificationCategoriesColumns.Label]);
                }
            }
        }
        #endregion

        #region Info-related utilities: Compare megadetector strings
        // Given two strings representing megadetector versions, return true if the destination appears to be the higher version number
        public static bool IsMegadetectorVersionHigherInDestination(string source, string destination)
        {
            if (source == destination)
            {
                return false;
            }
            if (source == Constant.RecognizerValues.MDVersionUnknown)
            {
                return true;
            }
            return string.Compare(source, destination) == -1;
        }

        // Given two strings representing megadetector versions, return true if the destination appears to be the same or the higher version number
        public static bool IsMegadetectorVersionSameHigherInDestination(string source, string destination)
        {
            if (source == destination)
            {
                return true;
            }
            if (source == Constant.RecognizerValues.MDVersionUnknown || String.IsNullOrWhiteSpace(source))
            {
                return true;
            }

            if (destination == Constant.RecognizerValues.MDVersionUnknown || String.IsNullOrWhiteSpace(destination))
            {
                return false;
            }
            return string.Compare(source, destination) == -1;
        }
        #endregion

        #region Generate info data structure from two sources
        // Given two dictionaries that contain info values, merge them in a way that combines the best of both into one.
        // Note: this form converts from a dictionary to an info structure and then invokes the 2nd form which actually generates the best recognition structure
        public static Dictionary<string, object> GenerateBestRecognitionInfoFromTwoInfos(Dictionary<string, object> infoDict1, Dictionary<string, object> infoDict2)
        {
            if (infoDict1 == null || infoDict1.Count == 0)
            {
                // As the first dictionary doesn't exist, return the second dictionary
                return infoDict2;
            }
            info info = new info();
            info.SetInfoDefaults();
            info.detector = (string)infoDict2[Constant.InfoColumns.Detector];
            info.detection_completion_time = (string)infoDict2[Constant.InfoColumns.DetectionCompletionTime];
            info.detector_metadata.megadetector_version = (string)infoDict2[Constant.InfoColumns.DetectorVersion];
            info.detector_metadata.typical_detection_threshold = (float)Convert.ToDouble(infoDict2[Constant.InfoColumns.TypicalDetectionThreshold]);
            info.detector_metadata.conservative_detection_threshold = (float)Convert.ToDouble(infoDict2[Constant.InfoColumns.ConservativeDetectionThreshold]);
            info.classifier = (string)infoDict2[Constant.InfoColumns.Classifier];
            info.classification_completion_time = (string)infoDict2[Constant.InfoColumns.ClassificationCompletionTime];
            info.classifier_metadata.typical_classification_threshold = (float)Convert.ToDouble(infoDict2[Constant.InfoColumns.TypicalClassificationThreshold]);
            return RecognitionUtilities.GenerateBestRecognitionInfoFromTwoInfos(infoDict1, info);
        }

        // Given a dictionary and a Recognizer info structure, merge them in a way that combines the best of both into one.
        public static Dictionary<string, object> GenerateBestRecognitionInfoFromTwoInfos(Dictionary<string, object> infoDict1, info info)
        {
            Dictionary<string, object> infoDictFromJsonInfo = new Dictionary<string, object>
            {
                {Constant.InfoColumns.InfoID, 1},
                {Constant.InfoColumns.Detector, info.detector ?? Constant.RecognizerValues.DetectorUnknown},
                {Constant.InfoColumns.DetectionCompletionTime, info.detection_completion_time ?? Constant.RecognizerValues.DetectionCompletionTimeUnknown},
                {Constant.InfoColumns.DetectorVersion, info.detector_metadata.megadetector_version ?? Constant.RecognizerValues.MDVersionUnknown},
                {Constant.InfoColumns.TypicalDetectionThreshold, info.detector_metadata.typical_detection_threshold ?? Constant.RecognizerValues.DefaultTypicalDetectionThresholdIfUnknown},
                {Constant.InfoColumns.ConservativeDetectionThreshold, info.detector_metadata.conservative_detection_threshold ?? Constant.RecognizerValues.DefaultConservativeDetectionThresholdIfUnknown},
                {Constant.InfoColumns.Classifier, String.IsNullOrEmpty(info.classifier)
                    ? String.Empty
                    : info.classifier},
                {Constant.InfoColumns.ClassificationCompletionTime,
                    String.IsNullOrEmpty(info.classification_completion_time)
                    ? String.Empty
                    : info.classification_completion_time},
                {Constant.InfoColumns.TypicalClassificationThreshold, info.classifier_metadata.typical_classification_threshold ?? Constant.RecognizerValues.DefaultTypicalClassificationThresholdIfUnknown},
            };
            return DetermineRecognitionInfoToUse(infoDict1, infoDictFromJsonInfo);
        }

        // This utility actually does the work. When there are two recognizer info structures, return a new detector structure that 
        // - fills in defaults as needed
        // - tries to populate the values with ones that make the most sense in terms of versions and confidence values to use
        private static Dictionary<string, object> DetermineRecognitionInfoToUse(Dictionary<string, object> infoDict1, Dictionary<string, object> infoDict2)
        {
            Dictionary<string, object> updatedDict = new Dictionary<string, object>
            {
                // Use the same InfoID, which should always be 1
                { Constant.InfoColumns.InfoID, infoDict1[Constant.InfoColumns.InfoID] }
            };

            // Populate the dict with the latest detector version, if known.If they are the same, prefer the 2nd dictionary
            string i1DetectorVersion = infoDict1.TryGetValue(Constant.InfoColumns.DetectorVersion, out object i1dv)
                ? i1dv == null
                    ? Constant.RecognizerValues.MDVersionUnknown : i1dv.ToString()
                    : Constant.RecognizerValues.MDVersionUnknown;
            string i2DetectorVersion = infoDict2.TryGetValue(Constant.InfoColumns.DetectorVersion, out object i2dv)
                    ? i2dv == null
                        ? Constant.RecognizerValues.MDVersionUnknown
                        : i2dv.ToString()
                    : Constant.RecognizerValues.MDVersionUnknown;
            bool i2Preferred = IsMegadetectorVersionSameHigherInDestination(i1DetectorVersion, i2DetectorVersion);
            updatedDict.Add(Constant.InfoColumns.DetectorVersion, i2Preferred ? i2DetectorVersion : i1DetectorVersion);

            // Populate the dict with the preferred detector name
            string i1Detector = infoDict1.TryGetValue(Constant.InfoColumns.Detector, out object i1d)
                ? i1d == null
                    ? Constant.RecognizerValues.DetectorUnknown
                    : i1d.ToString()
                : Constant.RecognizerValues.DetectorUnknown;
            string i2Detector = infoDict2.TryGetValue(Constant.InfoColumns.Detector, out object i2d)
                 ? i2d == null
                    ? Constant.RecognizerValues.DetectorUnknown
                    : i2d.ToString()
                 : Constant.RecognizerValues.DetectorUnknown;
            updatedDict.Add(Constant.InfoColumns.Detector, i2Preferred ? i2Detector : i1Detector);

            // Populate the dict with the preferred detection completion time
            string i1DetectionCompletionTime = infoDict1.TryGetValue(Constant.InfoColumns.DetectionCompletionTime, out object i1dct)
                ? i1dct == null
                    ? Constant.RecognizerValues.DetectionCompletionTimeUnknown
                    : i1dct.ToString()
                : Constant.RecognizerValues.DetectionCompletionTimeUnknown;
            string i2DetectionCompletionTime = infoDict2.TryGetValue(Constant.InfoColumns.DetectionCompletionTime, out object i2dct)
                ? i2dct == null
                    ? Constant.RecognizerValues.DetectionCompletionTimeUnknown
                    : i2dct.ToString()
                : Constant.RecognizerValues.DetectionCompletionTimeUnknown;
            updatedDict.Add(Constant.InfoColumns.DetectionCompletionTime, i2Preferred ? i2DetectionCompletionTime : i1DetectionCompletionTime);

            // Populate the dict with the preferred classifier 
            string i1Classifier = infoDict1.TryGetValue(Constant.InfoColumns.Classifier, out object i1c)
                ? i1c == null
                    ? Constant.RecognizerValues.ClassifierUnknown
                    : i1c.ToString()
                : Constant.RecognizerValues.ClassifierUnknown;
            string i2Classifier = infoDict2.TryGetValue(Constant.InfoColumns.Classifier, out object i2c)
                ? i2c == null
                    ? Constant.RecognizerValues.ClassifierUnknown
                    : i2c.ToString()
                : Constant.RecognizerValues.ClassifierUnknown;
            updatedDict.Add(Constant.InfoColumns.Classifier, i2Preferred ? i2Classifier : i1Classifier);

            // Populate the dict with the preferred classification completion time
            string i1ClassificationCompletionTime = infoDict1.TryGetValue(Constant.InfoColumns.ClassificationCompletionTime, out object i1cct)
                ? i1cct == null
                    ? Constant.RecognizerValues.DetectionCompletionTimeUnknown
                    : i1cct.ToString()
                : Constant.RecognizerValues.DetectionCompletionTimeUnknown;
            string i2ClassificationCompletionTime = infoDict2.TryGetValue(Constant.InfoColumns.ClassificationCompletionTime, out object i2cct)
                ? i2cct == null
                    ? Constant.RecognizerValues.ClassificationCompletionTimeUnknown
                    : i2cct.ToString()
                : Constant.RecognizerValues.ClassificationCompletionTimeUnknown;
            updatedDict.Add(Constant.InfoColumns.ClassificationCompletionTime, i2Preferred ? i2ClassificationCompletionTime : i1ClassificationCompletionTime);

            // Populate the dict with the smaller of the two typical detection thresholds
            float i1TypicalDetectionThreshold = infoDict1.TryGetValue(Constant.InfoColumns.TypicalDetectionThreshold, out object i1tdt)
                ? i1tdt == null
                    ? Constant.RecognizerValues.DefaultTypicalDetectionThresholdIfUnknown
                    : Convert.ToSingle(i1tdt)
                : Constant.RecognizerValues.DefaultTypicalDetectionThresholdIfUnknown;
            float i2TypicalDetectionThreshold = infoDict2.TryGetValue(Constant.InfoColumns.TypicalDetectionThreshold, out object i2tdt)
                ? i2tdt == null
                    ? Constant.RecognizerValues.DefaultTypicalDetectionThresholdIfUnknown
                    : Convert.ToSingle(i2tdt)
                : Constant.RecognizerValues.DefaultTypicalDetectionThresholdIfUnknown;
            updatedDict.Add(Constant.InfoColumns.TypicalDetectionThreshold, Math.Min(i1TypicalDetectionThreshold, i2TypicalDetectionThreshold));

            // Populate the dict with the smaller of the two typical classification thresholds
            float i1TypicalClassificationThreshold = infoDict1.TryGetValue(Constant.InfoColumns.TypicalClassificationThreshold, out object i1tct)
                ? i1tct == null
                    ? Constant.RecognizerValues.DefaultTypicalClassificationThresholdIfUnknown
                    : Convert.ToSingle(i1tct)
                : Constant.RecognizerValues.DefaultTypicalClassificationThresholdIfUnknown;
            float i2TypicalClassificationThreshold = infoDict2.TryGetValue(Constant.InfoColumns.TypicalClassificationThreshold, out object i2tct)
                ? i2tct == null
                    ? Constant.RecognizerValues.DefaultTypicalClassificationThresholdIfUnknown
                    : Convert.ToSingle(i2tct)
                : Constant.RecognizerValues.DefaultTypicalClassificationThresholdIfUnknown;
            updatedDict.Add(Constant.InfoColumns.TypicalClassificationThreshold, Math.Min(i1TypicalClassificationThreshold, i2TypicalClassificationThreshold));

            // Populate the dict with the smaller of the two conservative classification thresholds
            float i1ConservativeDetectionThreshold = infoDict1.TryGetValue(Constant.InfoColumns.ConservativeDetectionThreshold, out object i1cdt)
                ? i1cdt == null
                    ? Constant.RecognizerValues.DefaultConservativeDetectionThresholdIfUnknown
                    : Convert.ToSingle(i1cdt)
                : Constant.RecognizerValues.DefaultConservativeDetectionThresholdIfUnknown;
            float i2TConservativeDetectionThreshold = infoDict2.TryGetValue(Constant.InfoColumns.ConservativeDetectionThreshold, out object i2cdt)
                ? i2cdt == null
                    ? Constant.RecognizerValues.DefaultConservativeDetectionThresholdIfUnknown
                    : Convert.ToSingle(i2cdt)
                : Constant.RecognizerValues.DefaultConservativeDetectionThresholdIfUnknown;
            updatedDict.Add(Constant.InfoColumns.ConservativeDetectionThreshold, Math.Min(i1ConservativeDetectionThreshold, i2TConservativeDetectionThreshold));

            return updatedDict;
        }
        #endregion

        #region Compare the root folder vs json file paths and return the difference between the two
        // Compare the jsonFilePath to the rootFolderPath and return the difference
        //(i.e., the subfolder to the jsonFilePath from the rootFolderPath if any
        // If the JsonFilePath is the same as or outside of the rootFolderPath, return an empty string
        static public string GetRecognizersFileSubfolderPathIfAny(string rootFolderPath, string jsonFilePath)
        {
            Tuple<string, string, string> splitPath = Util.FilesFolders.SplitFullPath(rootFolderPath, jsonFilePath);
            if (splitPath == null)
            {
                // file is outside of root folder and its subfolders
                return String.Empty;
            }
            else if (String.IsNullOrEmpty(splitPath.Item2))
            {
                // Don't add prefix: file is in the root folder
                return String.Empty;
            }
            return splitPath.Item2;
        }
        #endregion

        #region Check/repair recognizer image paths if relative to root folder or subfolder
        // Try to read the recognition data from the Json file into the Recognizer structure
        // A progress bar is displayed
        // Success: returns a filled in Recognizer structure
        static public async Task<RecognizerPathTestResults> IsRecognizersFilePathsLikelyRelativeToTheSubfolder(Recognizer jsonRecognizer, string rootFolderPath, string subFolderPrefix, IProgress<ProgressBarArguments> progress, CancellationTokenSource cancelToken)
        {
            RecognizerPathTestResults results = RecognizerPathTestResults.NoMatchToExistingFiles;
            await Task.Run(() =>
            {
                int totalImages = jsonRecognizer.images.Count;
                string sTotalImages = String.Format("{0:N0}", jsonRecognizer.images.Count);
                int i = 0;

                // Skip certain conditions if the subFolder is empty
                bool nonEmptySubfolder = false == String.IsNullOrWhiteSpace(subFolderPrefix);
                foreach (image image in jsonRecognizer.images)
                {
                    if (i % 10000 == 0)
                    {
                        if (cancelToken.Token.IsCancellationRequested)
                        {
                            results = RecognizerPathTestResults.Cancelled;
                            break;
                        }
                        // Progress report generated after every 10,000 images
                        int percentDone = Convert.ToInt32(i * 100.0 / totalImages);
                        progress.Report(new ProgressBarArguments(percentDone, String.Format("Checking image paths ({0:N0}/{1:N0}...)", i, sTotalImages), true, false));
                        Thread.Sleep(Constant.ThrottleValues.RenderingBackoffTime);  // Allows the UI thread to update every now and then
                    }
                    if (nonEmptySubfolder && image.file.StartsWith(subFolderPrefix))
                    {
                        // Probable that Recognizer is relative to the root folder: At least one image path begins with the subfolderPrefix name
                        // System.Diagnostics.Debug.Print("Probable that Recognizer is relative to the root folder: At least one image path begins with the subfolderPrefix name.");
                        results = RecognizerPathTestResults.PathsRelativeToRootFolder;
                        break;
                    }
                    else if (File.Exists(Path.Combine(rootFolderPath, image.file)))
                    {
                        // Probable that Recognizer is relative to the root folder: At least one file is in the unaltered path.
                        // System.Diagnostics.Debug.Print("Probable that Recognizer is relative to the root folder: At least one file is in the unaltered path.");
                        results = RecognizerPathTestResults.PathsRelativeToRootFolder;
                        break;
                    }
                    else if (nonEmptySubfolder && File.Exists(Path.Combine(rootFolderPath, subFolderPrefix, image.file)))
                    {
                        // Probable that json is relative to the provided sub-folder: At lease one file is in the path altered by addeding the subfolder prefix
                        // System.Diagnostics.Debug.Print("Probable that json is relative to the provided sub-folder: At lease one file is in the path altered by addeding the subfolder prefix.");
                        results = RecognizerPathTestResults.PathsRelativeToSubFolder;
                        break;
                    }
                    else
                    {
                        // No hard evidence one way or another, so keep checking subsequent files
                        // Still, there is weak evidence that json was started in this subfolder as sample path does not have the subfolder prefix.
                        // Perhaps ask the user?
                    }
                    i++;
                }
            }).ConfigureAwait(true);

            // If nothing was reset, then the initial value (no matches) is returned 
            return results;
        }

        // For each image in the Recognizer structure, add the provided subFolderPrefix to the beginning of its File path
        // A progress bar is displayed
        static public async Task<CancelStatusEnum> RecognitionsAddPrefixToFilePaths(Recognizer jsonRecognizer, string subFolderPrefix, IProgress<ProgressBarArguments> progress, CancellationTokenSource cancelToken)
        {
            CancelStatusEnum results = CancelStatusEnum.NotCancelled;
            await Task.Run(() =>
            {
                int totalImages = jsonRecognizer.images.Count;
                string sTotalImages = String.Format("{0:N0}", jsonRecognizer.images.Count);
                int i = 0;

                foreach (image image in jsonRecognizer.images)
                {
                    if (i % 30000 == 0)
                    {
                        if (cancelToken.Token.IsCancellationRequested)
                        {
                            results = CancelStatusEnum.Cancelled;
                            break;
                        }
                        // Progress report generated after every 10,000 images
                        int percentDone = Convert.ToInt32(i * 100.0 / totalImages);
                        progress.Report(new ProgressBarArguments(percentDone, String.Format("Correcting image recognition paths ({0:N0}/{1:N0}...", i, sTotalImages), false, false));
                        Thread.Sleep(Constant.ThrottleValues.RenderingBackoffTime);  // Allows the UI thread to update every now and then
                    }
                    // Add the prefix to the path
                    image.file = Path.Combine(subFolderPrefix, image.file);
                    i++;
                }
            }).ConfigureAwait(true);
            return results;
        }
        #endregion
    }
}
