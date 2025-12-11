using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Threading;
using Timelapse.Constant;
using Timelapse.Controls;
using Timelapse.DataStructures;
using Timelapse.DataTables;
using Timelapse.Enums;
using Timelapse.Images;

namespace Timelapse.Database
{
    public partial class FileDatabase
    {
        // This file contains methods that populate note/multiline fields or counter fields with recognition data and/or detection counts
        // It implements the methods called by:
        //  - PopulateFieldWithRecognitionData dialog
        //  - PopulateFieldWithDetectionCount dialog

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

        #region RecognitionsPopulateFieldWithData
        // Given a data label corresponding to a note or multiline field,
        // populate that field with the selected recognition data from the best bounding box for each image
        // using the desired formatting options:
        // -  useClassificationOnly as just the classification category alone
        // - as Json as json formatted text or
        // - as human-readable text
        public bool RecognitionsPopulateFieldWithData(string dataLabel, RecognitionFormatEnum recognitionFormat, bool useClassificationOnly, bool useBboxCoords, bool useDetectionConf, bool useDetectionCategory, bool useClassConf, bool useClassCat, 
            IProgress<ProgressBarArguments> progress)
        {
            if (Database == null || false == DetectionsExists())
            {
                return false;
            }
            progress.Report(new(0, "Processing recognition data...", false, true));
            if (GlobalReferences.CancelTokenSource.Token.IsCancellationRequested)
            {
                return false;
            }
            Thread.Sleep(ThrottleValues.RenderingBackoffTime);  // Allows the UI thread to update every now and then

            BboxPrintable bboxPrintable = null;
            Dictionary<long, string> dict = [];
            foreach (ImageRow image in FileTable)
            {
                // Get the highest confidence bounding box, if any, for the current file
                BoundingBox bestBoundingBox = GlobalReferences.MainWindow.GetHighestConfidenceBoundingBoxForCurrentFile(image.ID);
                
                if (bestBoundingBox == null)
                {
                    continue;
                }

                // At this point, we have the best bounding box for this image
                // See what print options we have, and create the recognition data string accordingly
                string recognitionData = string.Empty;

                // Case 1: useClassificationOnly means we just print the classification by itself
                if (useClassificationOnly)
                {
                    if (bestBoundingBox.Classifications.Count >= 1)
                    {
                        recognitionData += bestBoundingBox.Classifications[0].Key;
                    }
                    else
                    {
                        recognitionData += GetDetectionLabelFromCategory(bestBoundingBox.DetectionCategory);
                    }
                }

                // Case 2: As human-readable/formatted text  
                else if (recognitionFormat == RecognitionFormatEnum.PlainText)
                {
                    if (useBboxCoords)
                    {
                        recognitionData += $"Coordinates: " +
                                           $"({bestBoundingBox.Rectangle.Left:F3}, {bestBoundingBox.Rectangle.Top:F3}, " +
                                           $"{bestBoundingBox.Rectangle.Width:F3}, {bestBoundingBox.Rectangle.Height:F3}){Environment.NewLine}";
                    }
                    if (useDetectionCategory)
                    {
                        recognitionData += $"Detection category: {GetDetectionLabelFromCategory(bestBoundingBox.DetectionCategory)}{Environment.NewLine}";
                    }
                    if (useDetectionConf)
                    {
                        recognitionData += $"Detection confidence: {bestBoundingBox.Confidence}{Environment.NewLine}";
                    }


                    bool classificationsExist = bestBoundingBox.Classifications.Count >= 1;
                    if (useClassCat && classificationsExist)
                    {
                        recognitionData += $"Classification category: {bestBoundingBox.Classifications[0].Key}{Environment.NewLine}";
                    }
                    if (useClassConf && classificationsExist)
                    {
                        recognitionData += $"Classification confidence: {bestBoundingBox.Classifications[0].Value}{Environment.NewLine}";
                    }
                    recognitionData = recognitionData.TrimEnd(); // Remove any trailing newline
                }

                // Case 3 As json formatted or unformatted text: (asJson is always true here)
                else
                {
                    bboxPrintable ??= new BboxPrintable();
                    if (useBboxCoords)
                    {
                        bboxPrintable.coordinates = new BboxPrintable._coordinates
                        {
                            X = Math.Round(bestBoundingBox.Rectangle.Left, 3),
                            Y = Math.Round(bestBoundingBox.Rectangle.Top, 3),
                            Width = Math.Round(bestBoundingBox.Rectangle.Width, 3),
                            Height = Math.Round(bestBoundingBox.Rectangle.Height, 3)
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
                            confidence = useDetectionConf ? Math.Round(bestBoundingBox.Confidence, 3) : null,
                            category = useDetectionCategory ? GetDetectionLabelFromCategory(bestBoundingBox.DetectionCategory) : null
                        };
                    }
                    else
                    {
                        bboxPrintable.detection = null;
                    }

                    if ((useClassConf || useClassCat) && bestBoundingBox.Classifications.Count >= 1)
                    {
                        // Classification confidence is a string,
                        // so try to parse it as a double so we can round it and print it as a json number
                        double? confAsDouble = null;
                        if (double.TryParse(bestBoundingBox.Classifications[0].Value, out double tmpConfAsDouble))
                        {
                            confAsDouble = Math.Round(tmpConfAsDouble, 3);
                        }
                        bboxPrintable.classification = new BboxPrintable._classification
                        {
                            confidence = useClassConf ? confAsDouble : null,
                            category = useClassCat ? bestBoundingBox.Classifications[0].Key : null
                        };
                    }
                    else
                    {
                        bboxPrintable.classification = null;
                    }

                    recognitionData = BoundingBoxPrettyPrint(bboxPrintable, recognitionFormat == RecognitionFormatEnum.FormattedJson);
                }

                // Update the current image and Add it to the list of data to be written to the database, and 
                // TODO:Check if we need to do the SetValue ???
                image.SetValueFromDatabaseString(dataLabel, recognitionData);
                dict.Add(image.ID, recognitionData);
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
            Thread.Sleep(ThrottleValues.RenderingBackoffTime);  // Allows the UI thread to update every now and then
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

        #region Recognitions: DetectionsAddCountToCounter
        // Given a Counter DataLabel, count the number of detections associated with each image, and set that image's counter to that count
        public bool RecognitionsPopulateFieldWithDetectionCounts(string counterDataLabel, double? confidenceValue, IProgress<ProgressBarArguments> progress)
        {
            if (Database == null || false == DetectionsExists())
            {
                return false;
            }
            progress.Report(new(0, "Populating fields...", false, true));
            Thread.Sleep(ThrottleValues.RenderingBackoffTime);  // Allows the UI thread to update every now and then

            Dictionary<long, int> dict = [];
            foreach (ImageRow image in FileTable)
            {
                int count = 0;
                BoundingBoxes bboxes = GlobalReferences.MainWindow.GetBoundingBoxesForCurrentFile(image.ID);
                foreach (BoundingBox bbox in bboxes.Boxes)
                {
                    if (bbox.Confidence >= confidenceValue)
                    {
                        count++;
                    }
                }
                dict.Add(image.ID, count);
                image.SetValueFromDatabaseString(counterDataLabel, count.ToString());
            }

            List<ColumnTuplesWithWhere> columnTuplesWithWhereList = [];
            foreach (KeyValuePair<long, int> kvp in dict)
            {
                // Update the imageRow in the file table with the new value
                //this.UpdateFile(kvp.Key, counterDataLabel, kvp.Value.ToString());

                // Add a query to update the row in the database
                ColumnTuplesWithWhere columnTuplesWithWhere = new();
                columnTuplesWithWhere.Columns.Add(new(counterDataLabel, kvp.Value.ToString()));
                columnTuplesWithWhere.SetWhere(kvp.Key);
                columnTuplesWithWhereList.Add(columnTuplesWithWhere);
            }

            progress.Report(new(0, "Updating database...", false, true));
            Thread.Sleep(ThrottleValues.RenderingBackoffTime);  // Allows the UI thread to update every now and then
            if (columnTuplesWithWhereList.Count > 0)
            {
                // Update the Database
                UpdateFiles(columnTuplesWithWhereList);
                // Force an update of the current image in case the current values have changed

            }
            return true;
        }
        #endregion

    }
}
