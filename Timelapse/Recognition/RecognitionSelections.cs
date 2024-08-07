using System;
using Timelapse.Constant;
using Timelapse.DataStructures;
using Timelapse.Enums;

namespace Timelapse.Recognition
{
    /// <summary>
    /// A class used by Custom Selection (and the Custom Selection Dialog) to set the selection criteria on detections
    /// </summary>
    public class RecognitionSelections
    {
        #region Public Properties
        public bool Enabled => UseRecognition;

        // Whether or not image recognition should be used
        public bool UseRecognition { get; set; }

        // Detection type: Empty (Recognized images with no Detections / Classifications 
        public bool InterpretAllDetectionsAsEmpty { get; set; }

        // Detection type: All (Recognized images with at least one Detection / Classification
        public bool AllDetections { get; set; }

        public bool RankByConfidence { get; set; }

        // Whether its a detection, classification, or none
        public RecognitionType RecognitionType { get; set; }

        // Detection type: indicated by its category number
        public string DetectionCategory { get; set; }

        // Classification type: indicated by its category number
        public string ClassificationCategory { get; set; }

        // The Confidence thresholds, used by the select user interface
        public double ConfidenceThreshold1ForUI
        {
            get => CurrentDetectionThreshold;
            set => CurrentDetectionThreshold = value;
        }

        public double ConfidenceThreshold2ForUI { get; set; }

        // Transforms the confidence threshold as needed, depending on the select operation
        public Tuple<double, double> ConfidenceThresholdForSelect
        {
            get
            {
                const double justAboveZero = 0.00001;
                double lowerBound;
                double upperBound;
                if (InterpretAllDetectionsAsEmpty)
                {
                    // For Empty category, we want to invert the confidence 
                    // e.g confidence of 1 is returned as confidence of 0
                    // But note that we actually return the confidence of the different threshold in this case, as normally #1 <= #2
                    // Doing so keeps that relationship after the inversion is done.
                    // We also swap the lower/upper bound to keep one less than the other
                    // If Threshold2 is .99 in the UI for empty items, we invert that, but to just above 0
                    // so we capture all the non-zero items (i.e., all images with detections in that range) as otherwise it could
                    //  omit the rare image with a max detection between 0 and .01
                    lowerBound = (Math.Abs(ConfidenceThreshold2ForUI - 0.99) < .0001) ? justAboveZero : 1.0 - ConfidenceThreshold2ForUI;
                    upperBound = 1.0 - ConfidenceThreshold1ForUI;

                }
                else if (AllDetections)
                {
                    // We don't want All detections to include images with no detections (i.e., Confidence range includes 0), so if we see a zero, we 
                    // alter that to just above zero.
                    lowerBound = ConfidenceThreshold1ForUI == 0 ? justAboveZero : ConfidenceThreshold1ForUI;
                    upperBound = ConfidenceThreshold2ForUI == 0 ? justAboveZero : ConfidenceThreshold2ForUI;
                }
                else
                {
                    lowerBound = ConfidenceThreshold1ForUI;
                    upperBound = ConfidenceThreshold2ForUI;
                }
                return new Tuple<double, double>(lowerBound, upperBound);
            }
        }

        private static double TypicalDetectionThreshold =>
            GlobalReferences.MainWindow?.DataHandler?.FileDatabase == null
                ? RecognizerValues.DefaultTypicalDetectionThresholdIfUnknown
                : (double)GlobalReferences.MainWindow.DataHandler.FileDatabase.GetTypicalDetectionThreshold();

        private double _currentDetectionThreshold = -1;
        public double CurrentDetectionThreshold
        {
            set => _currentDetectionThreshold = value;
            get =>
                _currentDetectionThreshold < 0
                    ? TypicalDetectionThreshold
                    : _currentDetectionThreshold;
        }

        private static double TypicalClassificationThreshold =>
            GlobalReferences.MainWindow?.DataHandler?.FileDatabase == null
                ? RecognizerValues.DefaultTypicalClassificationThresholdIfUnknown
                : (double)GlobalReferences.MainWindow.DataHandler.FileDatabase.GetTypicalClassificationThreshold();

        private double _currentClassificationThreshold = -1;
        public double CurrentClassificationThreshold
        {
            set => _currentClassificationThreshold = value;
            get =>
                _currentClassificationThreshold < 0
                    ? TypicalClassificationThreshold
                    : _currentClassificationThreshold;
        }

        public static double ConservativeDetectionThreshold =>
            GlobalReferences.MainWindow?.DataHandler?.FileDatabase == null
                ? RecognizerValues.DefaultConservativeDetectionThresholdIfUnknown
                : (double)GlobalReferences.MainWindow.DataHandler.FileDatabase.GetConservativeDetectionThreshold();

        #endregion

        #region Constructor - Initializes various defaults
        public RecognitionSelections()
        {
            ClearAllDetectionsUses();

            // We don't know the recognition type yet
            RecognitionType = RecognitionType.Empty;

            DetectionCategory = "1";
            ConfidenceThreshold2ForUI = 1;

            ClassificationCategory = "1";

            InterpretAllDetectionsAsEmpty = false;
            RankByConfidence = false;
        }
        #endregion

        #region Public Clear All Detection Uses
        // Bulk disabling of detection selection criteria
        public void ClearAllDetectionsUses()
        {
            UseRecognition = false;
        }
        #endregion
    }
}
