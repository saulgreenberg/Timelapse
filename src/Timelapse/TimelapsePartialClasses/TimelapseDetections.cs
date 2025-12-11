using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Threading.Tasks;
using Timelapse.Constant;
using Timelapse.DataStructures;
using BoundingBox = Timelapse.Images.BoundingBox;
using BoundingBoxes = Timelapse.Images.BoundingBoxes;

// ReSharper disable once CheckNamespace
namespace Timelapse
{
    public partial class TimelapseWindow
    {

        public BoundingBox GetHighestConfidenceBoundingBoxForCurrentFile(long fileID)
        {
            BoundingBoxes bboxes = GlobalReferences.MainWindow.GetBoundingBoxesForCurrentFile(fileID);
            BoundingBox bestBoundingBox = null;
            double highestConfidenceSeen = 0;
            foreach (BoundingBox bbox in bboxes.Boxes)
            {
                if (bbox.Confidence > highestConfidenceSeen)
                {
                    bestBoundingBox = bbox;
                    highestConfidenceSeen = bbox.Confidence;
                }
            }

            return bestBoundingBox;
        }

        public BoundingBoxes GetBoundingBoxesForCurrentFile(long fileID)
        {
            return GetBoundingBoxesForCurrentFile(fileID, false);
        }

        // for each image, get a list of detections and fill in the bounding box information for it. 
        // TODO: MERGE ASYNC AND SYNC VERSIONS. I SUSPECT DIFFERENCES ARE MINIMAL
        public BoundingBoxes GetBoundingBoxesForCurrentFile(long fileID, bool initialFrameOnly)
        {
            double boundingBoxDisplayThreshold = null == GlobalReferences.TimelapseState 
                ? Constant.RecognizerValues.DefaultConservativeDetectionThresholdIfUnknown
                : GlobalReferences.TimelapseState.BoundingBoxDisplayThreshold;
            BoundingBoxes bboxes = new();

            if (DataHandler.FileDatabase.DetectionsExists())
            {
                DataRow[] dataRows = DataHandler.FileDatabase.GetDetectionsFromFileID(fileID);
                foreach (DataRow detectionRow in dataRows)
                {
                    if (false == detectionRow.Table.Columns.Contains(DetectionColumns.BBox))
                    {
                        // Shouldn't happen
                        return bboxes;
                    }

                    string coords = (string)detectionRow[DetectionColumns.BBox];
                    bool setInitialVideoFrame = false;
                    if (string.IsNullOrEmpty(coords))
                    {
                        // This shouldn't happen, but...
                        continue;
                    }

                    float confidence = float.Parse(detectionRow[Constant.DetectionColumns.Conf].ToString() ?? "0");
                    // Determine the maximum confidence of these detections. However, we ignore confidences below the display threshold
                    if (bboxes.MaxConfidence < confidence && confidence >= boundingBoxDisplayThreshold)
                    {
                        bboxes.MaxConfidence = confidence;
                        setInitialVideoFrame = true;
                    }

                    string detectionCategoryLabel = DataHandler.FileDatabase.GetDetectionLabelFromCategory((string)detectionRow[DetectionColumns.Category]);

                    int frameNumber = 0; // if there is no frame number, user the 1st frame. It may be wrong, but better than nothing.
                    if (detectionRow.Table.Columns.Contains(DetectionColumns.FrameNumber))
                    {
                        if (System.DBNull.Value != detectionRow[DetectionColumns.FrameNumber] && null != detectionRow[DetectionColumns.FrameNumber])
                        {
                            frameNumber = (int)((long)detectionRow[DetectionColumns.FrameNumber]);
                        }
                    }

                    bboxes.FrameRate = null; // if there is no frame rate, we keep it at that as we may later try to get it from the file itself.
                    if (detectionRow.Table.Columns.Contains(DetectionColumns.FrameRate))
                    {
                        if (System.DBNull.Value != detectionRow[DetectionColumns.FrameRate] && null != detectionRow[DetectionColumns.FrameRate])
                        {
                            if (float.TryParse(detectionRow[DetectionColumns.FrameRate].ToString(), out float floatValue))
                            {
                                bboxes.FrameRate = floatValue;
                            }
                        }

                        // As this must be a video and the maxconfidence was changed, set the initial frame to show
                        // This will be the (first if tied) frame with the highest confidence value.
                        if (setInitialVideoFrame)
                        {
                            bboxes.InitialVideoFrame = frameNumber;
                        }
                    }

                    // Get the classifications for this detection, if any
                    List<KeyValuePair<string, string>> classifications = [];
                    if (detectionRow[DetectionColumns.Classification] != System.DBNull.Value && detectionRow[DetectionColumns.ClassificationConf] != System.DBNull.Value)
                    {
                        string classification = (string)detectionRow[DetectionColumns.Classification];
                        double classificationConf = Double.Parse(detectionRow[DetectionColumns.ClassificationConf].ToString() ?? "0");
                        if (classificationConf > 0.00)
                        {
                            string classificationCategoryLabel = DataHandler.FileDatabase.GetClassificationLabelFromCategory(classification);
                            classifications.Add(new(classificationCategoryLabel, classificationConf.ToString(CultureInfo.InvariantCulture)));
                        }
                    }

                    BoundingBox box = new((string)detectionRow[DetectionColumns.BBox], confidence, frameNumber, (string)detectionRow[DetectionColumns.Category],
                        detectionCategoryLabel, classifications);
                    bboxes.Boxes.Add(box);
                }

                if (initialFrameOnly)
                {
                    for (int i = bboxes.Boxes.Count - 1; i >= 0; i--)
                    {
                        if (bboxes.Boxes[i].FrameNumber != bboxes.InitialVideoFrame)
                        {
                            bboxes.Boxes.RemoveAt(i);
                        }
                    }
                }
            }
            return bboxes;
        }

        public async Task<BoundingBoxes> GetBoundingBoxesForCurrentFileAsync(long fileID)
        {
            BoundingBoxes bboxes = new();
            if (DataHandler.FileDatabase.DetectionsExists())
            {
                DataRow[] dataRows = await DataHandler.FileDatabase.GetDetectionsFromFileIDAsync(fileID);
                foreach (DataRow detectionRow in dataRows)
                {
                    if (false == detectionRow.Table.Columns.Contains(DetectionColumns.BBox))
                    {
                        // Shouldn't happen
                        return bboxes;
                    }

                    string coords = (string)detectionRow[DetectionColumns.BBox];
                    if (string.IsNullOrEmpty(coords))
                    {
                        // This shouldn't happen, but...
                        continue;
                    }
                    float confidence = float.Parse(detectionRow[Constant.DetectionColumns.Conf].ToString() ?? "0");
                    bool setInitialVideoFrame = false;
                    // Determine the maximum confidence of these detections
                    if (bboxes.MaxConfidence < confidence)
                    {
                        bboxes.MaxConfidence = confidence;
                        setInitialVideoFrame = true;
                    }
                    string detectionCategoryLabel = DataHandler.FileDatabase.GetDetectionLabelFromCategory((string)detectionRow[DetectionColumns.Category]);

                    int frameNumber = 0; // if there is no frame number, use the 0th frame. It may be wrong, but better than nothing.
                    if (detectionRow.Table.Columns.Contains(DetectionColumns.FrameNumber))
                    {
                        if (System.DBNull.Value != detectionRow[DetectionColumns.FrameNumber] && null != detectionRow[DetectionColumns.FrameNumber])
                        {
                            frameNumber = (int)((long)detectionRow[DetectionColumns.FrameNumber]);
                        }
                    }

                    bboxes.FrameRate = null; // if there is no frame rate, we keep it at that as we may later try to get it from the file itself.
                    if (detectionRow.Table.Columns.Contains(DetectionColumns.FrameRate))
                    {
                        if (System.DBNull.Value != detectionRow[DetectionColumns.FrameRate] && null != detectionRow[DetectionColumns.FrameRate])
                        {
                            if (float.TryParse(detectionRow[DetectionColumns.FrameRate].ToString(), out float floatValue))
                            {
                                bboxes.FrameRate = floatValue;
                            }
                        }

                        // As this must be a video and the maxconfidence was changed, set the initial frame to show
                        // This will be the (first if tied) frame with the highest confidence value.
                        if (setInitialVideoFrame)
                        {
                            bboxes.InitialVideoFrame = frameNumber;
                        }
                    }

                    // Get the classifications for this detection, if any
                    List<KeyValuePair<string, string>> classifications = [];
                    if (detectionRow[DetectionColumns.Classification] != System.DBNull.Value && detectionRow[DetectionColumns.ClassificationConf] != System.DBNull.Value)
                    {
                        string classification = (string)detectionRow[DetectionColumns.Classification];
                        double classificationConf = Double.Parse(detectionRow[DetectionColumns.ClassificationConf].ToString() ?? "0");
                        if (classificationConf > 0.00)
                        {
                            string classificationCategoryLabel = DataHandler.FileDatabase.GetClassificationLabelFromCategory(classification);
                            classifications.Add(new(classificationCategoryLabel, classificationConf.ToString(CultureInfo.InvariantCulture)));
                        }
                    }

                    BoundingBox box = new((string)detectionRow[DetectionColumns.BBox], confidence, frameNumber, (string)detectionRow[DetectionColumns.Category], detectionCategoryLabel, classifications);
                    bboxes.Boxes.Add(box);
                }
            }
            return bboxes;
        }
    }
}
