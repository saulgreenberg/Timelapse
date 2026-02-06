using System;

namespace Timelapse.EventArguments
{
    // RecognitionSelectionChangedEventArgs 
    public class RecognitionSelectionChangedEventArgs(string detectionCategoryLabel, string classificationCategoryLabel, bool refreshRecognitionCountsRequired, bool disableEpisodeAny)
        : EventArgs
    {
        public string DetectionCategoryLabel = detectionCategoryLabel;           // User friendly labels for the selected categories, if any
        public string ClassificationCategoryLabel = classificationCategoryLabel;      // User friendly labels for the selected categories, if any
        public bool RefreshRecognitionCountsRequired = refreshRecognitionCountsRequired;   // Whether its necessary to refresh the recognition counts
        public bool DisableEpisodeAny = disableEpisodeAny;
    }
}
