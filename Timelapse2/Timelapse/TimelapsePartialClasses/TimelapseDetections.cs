using System;
using System.Collections.Generic;
using System.Data;
using System.Windows;
using BoundingBox = Timelapse.Images.BoundingBox;
using BoundingBoxes = Timelapse.Images.BoundingBoxes;

namespace Timelapse
{
    public partial class TimelapseWindow : Window, IDisposable
    {
        // for each image, get a list of detections and fill in the bounding box information for it. 
        public BoundingBoxes GetBoundingBoxesForCurrentFile(long fileID)
        {
            BoundingBoxes bboxes = new BoundingBoxes();
            string detectionCategoryLabel;
            string classificationCategoryLabel;

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
                    detectionCategoryLabel = this.DataHandler.FileDatabase.GetDetectionLabelFromCategory((string)detectionRow[Constant.DetectionColumns.Category]);

                    DataRow[] classificationDataTableRows = this.DataHandler.FileDatabase.GetClassificationsFromDetectionID((long)detectionRow[Constant.DetectionColumns.DetectionID]);
                    List<KeyValuePair<string, string>> classifications = new List<KeyValuePair<string, string>>();

                    double conf = 0;

                    foreach (DataRow classificationRow in classificationDataTableRows)
                    {
                        conf = (double)classificationRow[Constant.DetectionColumns.Conf];
                        if (conf > 0.00)
                        {
                            classificationCategoryLabel = this.DataHandler.FileDatabase.GetClassificationLabelFromCategory((string)classificationRow[Constant.ClassificationColumns.Category]);
                            classifications.Add(new KeyValuePair<string, string>(classificationCategoryLabel, conf.ToString()));
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
