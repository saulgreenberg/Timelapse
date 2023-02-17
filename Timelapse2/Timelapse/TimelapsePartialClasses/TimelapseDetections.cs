using System.Collections.Generic;
using System.Data;
using System.Globalization;
using BoundingBox = Timelapse.Images.BoundingBox;
using BoundingBoxes = Timelapse.Images.BoundingBoxes;

namespace Timelapse
{
    public partial class TimelapseWindow
    {
        // for each image, get a list of detections and fill in the bounding box information for it. 
        public BoundingBoxes GetBoundingBoxesForCurrentFile(long fileID)
        {
            BoundingBoxes bboxes = new BoundingBoxes();
            if (this.DataHandler.FileDatabase.DetectionsExists())
            {
                DataRow[] dataRows = this.DataHandler.FileDatabase.GetDetectionsFromFileID(fileID);
                foreach (DataRow detectionRow in dataRows)
                {
                    string coords = (string)detectionRow[3];
                    if (string.IsNullOrEmpty(coords))
                    {
                        // This shouldn't happen, but...
                        continue;
                    }
                    float confidence = float.Parse(detectionRow[2].ToString());
                    string category = (string)detectionRow[1];
                    // Determine the maximum confidence of these detections
                    if (bboxes.MaxConfidence < confidence)
                    {
                        bboxes.MaxConfidence = confidence;
                    }
                    string detectionCategoryLabel = this.DataHandler.FileDatabase.GetDetectionLabelFromCategory((string)detectionRow[Constant.DetectionColumns.Category]);

                    DataRow[] classificationDataTableRows = this.DataHandler.FileDatabase.GetClassificationsFromDetectionID((long)detectionRow[Constant.DetectionColumns.DetectionID]);
                    List<KeyValuePair<string, string>> classifications = new List<KeyValuePair<string, string>>();

                    foreach (DataRow classificationRow in classificationDataTableRows)
                    {
                        double conf = (double)classificationRow[Constant.DetectionColumns.Conf];
                        if (conf > 0.00)
                        {
                            string classificationCategoryLabel = this.DataHandler.FileDatabase.GetClassificationLabelFromCategory((string)classificationRow[Constant.ClassificationColumns.Category]);
                            classifications.Add(new KeyValuePair<string, string>(classificationCategoryLabel, conf.ToString(CultureInfo.InvariantCulture)));
                        }
                    }
                    BoundingBox box = new BoundingBox((string)detectionRow[3], confidence, (string)detectionRow[Constant.DetectionColumns.Category], detectionCategoryLabel, classifications);
                    bboxes.Boxes.Add(box);
                }
            }
            return bboxes;
        }
    }
}
