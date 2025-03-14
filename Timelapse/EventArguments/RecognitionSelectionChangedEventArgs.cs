using System;
namespace Timelapse.EventArguments
{
    /// <summary>
    /// The RecognitionSelectionChangedEventArgs event argument contains 
    /// - the textual category of the current detecton and/or classification selection (string.Empty if none)
    /// </summary>
    public class RecognitionSelectionChangedEventArgs :EventArgs
    {
        public string DetectionCategory;
        public string DetectionCategoryNumber;
        public string ClassificationCategory;
        public string ClassificationCategoryNumber;
        public RecognitionSelectionChangedEventArgs(string detectionCategory, string detectionCategoryNumber, string classificationCategory, string classificationCategoryNumber)
        {
            DetectionCategory = detectionCategory;
            DetectionCategoryNumber = detectionCategoryNumber;
            ClassificationCategory = classificationCategory;
            ClassificationCategoryNumber = classificationCategoryNumber;
        }
    }
}
