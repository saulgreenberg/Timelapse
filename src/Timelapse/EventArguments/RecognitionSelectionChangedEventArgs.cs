using System;
using System.Collections.Generic;

namespace Timelapse.EventArguments
{
    // RecognitionSelectionChangedEventArgs 
    public class RecognitionSelectionChangedEventArgs(string detectionCategoryLabel, List<string> classificationCategoryLabels, bool refreshRecognitionCountsRequired, bool disableEpisodeAny, string selectedTaxonNode)
        : EventArgs
    {
        public string DetectionCategoryLabel = detectionCategoryLabel;           // User friendly labels for the selected categories, if any
        public List<string> ClassificationCategoryLabels = classificationCategoryLabels;      // User friendly labels for the selected categories, if any
        public bool RefreshRecognitionCountsRequired = refreshRecognitionCountsRequired;   // Whether its necessary to refresh the recognition counts
        public bool DisableEpisodeAny = disableEpisodeAny;
        public string SelectedTaxonNode = selectedTaxonNode;
    }
}
