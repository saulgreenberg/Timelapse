using System;

namespace Timelapse.EventArguments
{
    // RecognitionSelectionChangedEventArgs 
    public class RecognitionSelectionChangedEventArgs :EventArgs
    {
        public string DetectionCategoryLabel;           // User friendly labels for the selected categories, if any
        public string ClassificationCategoryLabel;      // User friendly labels for the selected categories, if any
        public bool RefreshRecognitionCountsRequired;   // Whether its necessary to refresh the recognition counts
        public RecognitionSelectionChangedEventArgs(string detectionCategoryLabel, string classificationCategoryLabel, bool refreshRecognitionCountsRequired)
        {
            DetectionCategoryLabel = detectionCategoryLabel;
            ClassificationCategoryLabel = classificationCategoryLabel;
            RefreshRecognitionCountsRequired = refreshRecognitionCountsRequired;
        }
    }
}
