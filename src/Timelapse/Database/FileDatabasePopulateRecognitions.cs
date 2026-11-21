using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Timelapse.Controls;
using Timelapse.DataStructures;
using Timelapse.DataTables;
using Timelapse.Enums;
using Timelapse.Images;
using Timelapse.Util;
using Control = Timelapse.Constant.Control;

namespace Timelapse.Database
{
    public partial class FileDatabase
    {
        // This file contains methods that populate note/multiline fields or counter fields with recognition data and/or detection counts
        // It implements the methods called by:
        //  - PopulateFieldWithRecognitionData dialog
        //  - PopulateFieldWithDetectionCounts dialog
        //  - PopulateFieldWithDetectionCategory, PopulateFieldWithClassificationCategory and PopulateFieldWithBoundingBoxCoordinates dialogs

        #region BboxPrintable: A class that allows us to pretty print bounding box data as json
        #pragma warning disable IDE1006
        protected class BboxPrintable
        {
            public _coordinates coordinates { get; set; } = new();
            public _detection detection { get; set; } = new();
            public _classification classification { get; set; } = new();
            public class _coordinates
            {
                public double X { get; set; }
                public double Y { get; set; }
                public double Width { get; set; }
                public double Height { get; set; }
            }

            public class _detection
            {
                public string category { get; set; }
                public double? confidence { get; set; }
            }
            public class _classification
            {
                public string category { get; set; }
                public double? confidence { get; set; }

            }
        }
        #pragma warning restore IDE1006
        #endregion

        #region RecognitionsComputeFieldData / RecognitionsWriteFieldData
        // Populating a field with recognition data is done in two steps, so the caller can check the computed values before they are written
        // (e.g., whether raw categories match a FixedChoice field's menu items) without a second pass over the detections:
        // 1. RecognitionsComputeFieldData returns the value for each file, keyed by file ID
        // 2. RecognitionsWriteFieldData writes those values into the field

        // Given a data label corresponding to a note or multiline field (or a decimal or fixed choice field, for raw data),
        // compute the selected recognition data from the best bounding box for each image
        // using the desired formatting options:
        // - as Json as json formatted text or
        // - as human-readable text or
        // - as the raw value of a single recognition item. If the field is a decimal field, only valid decimals (or blanks) are included
        // Files without a bounding box get an empty value, which clears their field
        // Returns null if there are no detections or the operation was cancelled
        public Dictionary<long, string> RecognitionsComputeFieldData(string dataLabel, RecognitionFormatEnum recognitionFormat, bool useBboxCoords, bool useDetectionConf, bool useDetectionCategory, bool useClassConf, bool useClassCat,
            IProgress<ProgressBarArguments> progress)
        {
            if (Database == null || false == DetectionsExists())
            {
                return null;
            }
            string controlType = GetControlFromControls(dataLabel)?.Type;
            progress.Report(new(0, "Processing recognition data...", false, true));
            if (GlobalReferences.CancelTokenSource.Token.IsCancellationRequested)
            {
                return null;
            }

            Dictionary<long, string> dict = [];
            foreach (ImageRow image in FileTable)
            {
                // Get the highest confidence bounding box, if any, for the current file
                BoundingBox bestBoundingBox = TimelapseWindow.GetHighestConfidenceBoundingBoxForCurrentFile(image.ID);
                
                if (bestBoundingBox == null)
                {
                    // No bounding box: the field is cleared, so any existing data in it is overwritten
                    dict.Add(image.ID, string.Empty);
                    continue;
                }

                string recognitionData = RecognitionsFormatBoundingBox(bestBoundingBox, recognitionFormat, useBboxCoords, useDetectionConf, useDetectionCategory, useClassConf, useClassCat);

                // Decimal fields can only hold a valid decimal or a blank, so skip any raw value that isn't one
                if (recognitionFormat == RecognitionFormatEnum.Raw &&
                    false == string.IsNullOrEmpty(recognitionData) &&
                    ((controlType == Control.DecimalAny && false == IsCondition.IsDecimal(recognitionData)) ||
                     (controlType == Control.DecimalPositive && false == IsCondition.IsDecimalPositive(recognitionData))))
                {
                    continue;
                }

                // Add it to the list of data to be written to the database
                dict.Add(image.ID, recognitionData);
            }
            return dict;
        }

        // Format the recognition data of a single bounding box using the desired formatting options (see RecognitionsComputeFieldData).
        // Also used by the PopulateFieldWithRecognitionData dialog to show an example of the output
        public string RecognitionsFormatBoundingBox(BoundingBox boundingBox, RecognitionFormatEnum recognitionFormat, bool useBboxCoords, bool useDetectionConf, bool useDetectionCategory, bool useClassConf, bool useClassCat)
        {
            string recognitionData = string.Empty;

            // Case 1: As the raw data of the single selected recognition item
            if (recognitionFormat == RecognitionFormatEnum.Raw)
            {
                bool classificationsExist = boundingBox.Classifications.Count >= 1;
                if (useBboxCoords)
                {
                    recognitionData = FormattableString.Invariant($"{boundingBox.Rectangle.Left:F3}, {boundingBox.Rectangle.Top:F3}, ") +
                                      FormattableString.Invariant($"{boundingBox.Rectangle.Width:F3}, {boundingBox.Rectangle.Height:F3}");
                }
                else if (useDetectionCategory)
                {
                    recognitionData = GetDetectionLabelFromCategory(boundingBox.DetectionCategory);
                }
                else if (useDetectionConf)
                {
                    recognitionData = boundingBox.Confidence.ToString(CultureInfo.InvariantCulture);
                }
                else if (useClassCat && classificationsExist)
                {
                    recognitionData = boundingBox.Classifications[0].Key;
                }
                else if (useClassConf && classificationsExist)
                {
                    recognitionData = boundingBox.Classifications[0].Value;
                }
            }

            // Case 2: As human-readable/formatted text
            else if (recognitionFormat == RecognitionFormatEnum.PlainText)
            {
                if (useBboxCoords)
                {
                    recognitionData += $"Coordinates: " +
                                       $"({boundingBox.Rectangle.Left:F3}, {boundingBox.Rectangle.Top:F3}, " +
                                       $"{boundingBox.Rectangle.Width:F3}, {boundingBox.Rectangle.Height:F3}){Environment.NewLine}";
                }
                if (useDetectionCategory)
                {
                    recognitionData += $"Detection category: {GetDetectionLabelFromCategory(boundingBox.DetectionCategory)}{Environment.NewLine}";
                }
                if (useDetectionConf)
                {
                    recognitionData += $"Detection confidence: {boundingBox.Confidence}{Environment.NewLine}";
                }


                bool classificationsExist = boundingBox.Classifications.Count >= 1;
                if (useClassCat && classificationsExist)
                {
                    recognitionData += $"Classification category: {boundingBox.Classifications[0].Key}{Environment.NewLine}";
                }
                if (useClassConf && classificationsExist)
                {
                    recognitionData += $"Classification confidence: {boundingBox.Classifications[0].Value}{Environment.NewLine}";
                }
                recognitionData = recognitionData.TrimEnd(); // Remove any trailing newline
            }

            // Case 3: As json formatted or unformatted text
            else
            {
                BboxPrintable bboxPrintable = new();
                if (useBboxCoords)
                {
                    bboxPrintable.coordinates = new BboxPrintable._coordinates
                    {
                        X = Math.Round(boundingBox.Rectangle.Left, 3),
                        Y = Math.Round(boundingBox.Rectangle.Top, 3),
                        Width = Math.Round(boundingBox.Rectangle.Width, 3),
                        Height = Math.Round(boundingBox.Rectangle.Height, 3)
                    };
                }
                else
                {
                    bboxPrintable.coordinates = null;
                }

                if (useDetectionConf || useDetectionCategory)
                {
                    bboxPrintable.detection = new BboxPrintable._detection
                    {
                        confidence = useDetectionConf ? Math.Round(boundingBox.Confidence, 3) : null,
                        category = useDetectionCategory ? GetDetectionLabelFromCategory(boundingBox.DetectionCategory) : null
                    };
                }
                else
                {
                    bboxPrintable.detection = null;
                }

                if ((useClassConf || useClassCat) && boundingBox.Classifications.Count >= 1)
                {
                    // Classification confidence is a string,
                    // so try to parse it as a double so we can round it and print it as a json number
                    double? confAsDouble = null;
                    if (double.TryParse(boundingBox.Classifications[0].Value, out double tmpConfAsDouble))
                    {
                        confAsDouble = Math.Round(tmpConfAsDouble, 3);
                    }
                    bboxPrintable.classification = new BboxPrintable._classification
                    {
                        confidence = useClassConf ? confAsDouble : null,
                        category = useClassCat ? boundingBox.Classifications[0].Key : null
                    };
                }
                else
                {
                    bboxPrintable.classification = null;
                }

                recognitionData = BoundingBoxPrettyPrint(bboxPrintable, recognitionFormat == RecognitionFormatEnum.FormattedJson);

                // An empty json object means none of the selected data exists for this box (e.g., only Classification items are selected,
                // but the box has no classification). Use an empty value instead, so the field is cleared as in the other formats,
                // and as for files without a bounding box
                if (recognitionData == "{}")
                {
                    recognitionData = string.Empty;
                }
            }
            return recognitionData;
        }

        // Write the values computed by RecognitionsComputeFieldData into the field identified by dataLabel,
        // updating both the in-memory file table and the database
        public bool RecognitionsWriteFieldData(string dataLabel, Dictionary<long, string> dict, IProgress<ProgressBarArguments> progress)
        {
            if (Database == null || dict == null)
            {
                return false;
            }

            // Update the current images
            // TODO:Check if we need to do the SetValue ???
            foreach (ImageRow image in FileTable)
            {
                if (dict.TryGetValue(image.ID, out string recognitionData))
                {
                    image.SetValueFromDatabaseString(dataLabel, recognitionData);
                }
            }

            // We now have a list of all fields and their contents
            // Update the database with these values
            List<ColumnTuplesWithWhere> columnTuplesWithWhereList = [];
            foreach (KeyValuePair<long, string> kvp in dict)
            {
                // Add a query to update the row in the database
                ColumnTuplesWithWhere columnTuplesWithWhere = new();
                columnTuplesWithWhere.Columns.Add(new(dataLabel, kvp.Value));
                columnTuplesWithWhere.SetWhere(kvp.Key);
                columnTuplesWithWhereList.Add(columnTuplesWithWhere);
            }

            progress.Report(new(0, "Updating database...", false, true));
            if (columnTuplesWithWhereList.Count > 0)
            {
                // Update the Database
                UpdateFiles(columnTuplesWithWhereList);
                // Force an update of the current image in case the current values have changed

            }
            return true;
        }

        private static string BoundingBoxPrettyPrint(BboxPrintable bbox, bool prettyPrint)
        {
            var settings = new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore,
                Formatting = prettyPrint ? Formatting.Indented : Formatting.None,
            };
            return JsonConvert.SerializeObject(bbox, settings);
        }
        #endregion

        #region Recognitions: RecognitionsComputeDetectionCounts
        // Count the number of detections associated with each image, returning that count (as a string) keyed by file ID,
        // so the caller can check the counts before writing them with RecognitionsWriteFieldData.
        // Returns null if there are no detections
        public Dictionary<long, string> RecognitionsComputeDetectionCounts(double? confidenceValue, IProgress<ProgressBarArguments> progress)
        {
            if (Database == null || false == DetectionsExists())
            {
                return null;
            }
            progress.Report(new(0, "Counting detections...", false, true));

            Dictionary<long, string> dict = [];
            foreach (ImageRow image in FileTable)
            {
                // Count the bounding boxes at or above the confidence value in each frame, and use the largest of those counts.
                // For a video, this is the frame showing the most entities, rather than the sum over all its frames.
                // An image's boxes are all in frame 0, so its count is the same as a simple count of its boxes.
                BoundingBoxes bboxes = GlobalReferences.MainWindow.GetBoundingBoxesForCurrentFile(image.ID);
                int count = bboxes.Boxes
                    .Where(bbox => bbox.Confidence >= confidenceValue)
                    .GroupBy(bbox => bbox.FrameNumber)
                    .Select(frame => frame.Count())
                    .DefaultIfEmpty(0)
                    .Max();
                dict.Add(image.ID, count.ToString());
            }
            return dict;
        }
        #endregion

        #region RecognitionsCountValuesToOverwrite
        // Count the selected files whose field already holds a non-empty value that differs from its new value,
        // i.e., the existing data that writing these new values would overwrite.
        // This only reads the in-memory file table, so it is fast even for very large selections
        public int RecognitionsCountValuesToOverwrite(string dataLabel, Dictionary<long, string> newValues)
        {
            if (newValues == null)
            {
                return 0;
            }
            int count = 0;
            foreach (ImageRow image in FileTable)
            {
                if (newValues.TryGetValue(image.ID, out string newValue))
                {
                    string existingValue = image.GetValueDatabaseString(dataLabel);
                    if (false == string.IsNullOrEmpty(existingValue) && existingValue != newValue)
                    {
                        count++;
                    }
                }
            }
            return count;
        }
        #endregion

    }
}
