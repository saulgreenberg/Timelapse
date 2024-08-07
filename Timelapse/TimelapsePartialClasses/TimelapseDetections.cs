using System.Collections.Generic;
using System.Data;
using System.Globalization;
using Timelapse.Constant;
using BoundingBox = Timelapse.Images.BoundingBox;
using BoundingBoxes = Timelapse.Images.BoundingBoxes;

// ReSharper disable once CheckNamespace
namespace Timelapse
{
    public partial class TimelapseWindow
    {
        // for each image, get a list of detections and fill in the bounding box information for it. 
        public BoundingBoxes GetBoundingBoxesForCurrentFile(long fileID)
        {
            BoundingBoxes bboxes = new BoundingBoxes();
            if (DataHandler.FileDatabase.DetectionsExists())
            {
                DataRow[] dataRows = DataHandler.FileDatabase.GetDetectionsFromFileID(fileID);
                foreach (DataRow detectionRow in dataRows)
                {
                    string coords = (string)detectionRow[3];
                    if (string.IsNullOrEmpty(coords))
                    {
                        // This shouldn't happen, but...
                        continue;
                    }
                    float confidence = float.Parse(detectionRow[2].ToString());
                    // Determine the maximum confidence of these detections
                    if (bboxes.MaxConfidence < confidence)
                    {
                        bboxes.MaxConfidence = confidence;
                    }
                    string detectionCategoryLabel = DataHandler.FileDatabase.GetDetectionLabelFromCategory((string)detectionRow[DetectionColumns.Category]);

                    DataRow[] classificationDataTableRows = DataHandler.FileDatabase.GetClassificationsFromDetectionID((long)detectionRow[DetectionColumns.DetectionID]);
                    List<KeyValuePair<string, string>> classifications = new List<KeyValuePair<string, string>>();

                    foreach (DataRow classificationRow in classificationDataTableRows)
                    {
                        double conf = (double)classificationRow[DetectionColumns.Conf];
                        if (conf > 0.00)
                        {
                            string classificationCategoryLabel = DataHandler.FileDatabase.GetClassificationLabelFromCategory((string)classificationRow[ClassificationColumns.Category]);
                            classifications.Add(new KeyValuePair<string, string>(classificationCategoryLabel, conf.ToString(CultureInfo.InvariantCulture)));
                        }
                    }
                    BoundingBox box = new BoundingBox((string)detectionRow[3], confidence, (string)detectionRow[DetectionColumns.Category], detectionCategoryLabel, classifications);
                    bboxes.Boxes.Add(box);
                }
            }
            return bboxes;
        }
    }
}
