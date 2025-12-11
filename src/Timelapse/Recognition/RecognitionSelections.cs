using System;
using Timelapse.Constant;
using Timelapse.DataStructures;
using Timelapse.Enums;

namespace Timelapse.Recognition
{
    /// <summary>
    /// A class used by Custom Selection (and the Custom Selection Dialog) to set the selection criteria on detections
    /// </summary>
    [Serializable]
    public class RecognitionSelections
    {
        #region Public Properties

        // Whether or not image recognition should be used
        public bool UseRecognition { get; set; }

        // Detection type: Empty (Recognized images with no Detections / Classifications 
        public bool InterpretAllDetectionsAsEmpty { get; set; }

        // Detection type: All (Recognized images with at least one Detection / Classification
        public bool AllDetections { get; set; }

        public bool RankByDetectionConfidence { get; set; }  // For Detections. Kept this way for backwards compatability
        public bool RankByClassificationConfidence { get; set; }

        // Whether its a detection, classification, or none as determined by the contents of the various category fields
        public RecognitionType RecognitionType
        {
            get
            {
                if (string.IsNullOrEmpty(this.DetectionCategoryNumber) && string.IsNullOrEmpty(this.ClassificationCategoryNumber))
                {
                    // If there is are no recognitions, default the DetectionCategory to 'All'
                    this.DetectionCategoryNumber = Constant.RecognizerValues.AllDetectionCategoryNumber;
                    this.AllDetections = true;
                    return RecognitionType.Detection;
                }

                if (string.IsNullOrEmpty(this.ClassificationCategoryNumber))
                {
                    return RecognitionType.Detection;
                }

                return RecognitionType.Classification;
            }
        }

        // Detection type: indicated by its category number
        // Note that Empty is the same as All but with InterpretAllDetectionsAsEmpty
        public string DetectionCategoryNumber { get; set; }

        // Classification type: indicated by its category number
        public string ClassificationCategoryNumber { get; set; }


        //
        // Confidence thresholds, used by the select user interface
        //
        private double detectionConfidenceHigherForUI;
        public double DetectionConfidenceHigherForUI
        {
            get => detectionConfidenceHigherForUI;

            set => detectionConfidenceHigherForUI = Math.Round(value, Constant.RecognizerValues.ConfidenceDecimalPlaces);
        } 

        private double detectionConfidenceLowerForUI = Math.Round(TypicalDetectionThreshold, Constant.RecognizerValues.ConfidenceDecimalPlaces);
        public double DetectionConfidenceLowerForUI
        {
            get => detectionConfidenceLowerForUI < 0
                ? Math.Round(TypicalDetectionThreshold, Constant.RecognizerValues.ConfidenceDecimalPlaces)
                : Math.Round(detectionConfidenceLowerForUI, Constant.RecognizerValues.ConfidenceDecimalPlaces);
            set => detectionConfidenceLowerForUI = value;
        }

        public double ClassificationConfidenceHigherForUI { get; set; } = 1;
        public double ClassificationConfidenceLowerForUI { get; set; } = Constant.RecognizerValues.DefaultTypicalClassificationThresholdIfUnknown;

        // Transforms the confidence threshold as needed, depending on the select operation
        public Tuple<double, double> ConfidenceDetectionThresholdForSelect
        {
            get
            {
                const double justAboveZero = 0.0001;
                double lowerBound;
                double upperBound;
                if (InterpretAllDetectionsAsEmpty)
                {
                    // For Empty category, the issue is what to do if the lower bound is not 0 and the upper bound is not 1, as this is now an intermediate value
                    // So we do a sensible (abeit not very meaningful) workaround, where the empty values are always from 0 to just less than the lowest detection value 
                    lowerBound = 0;
                    upperBound = DetectionConfidenceLowerForUI - justAboveZero < 0 
                        ? 0 
                        : DetectionConfidenceLowerForUI - justAboveZero; 
                }
                else if (AllDetections)
                {
                    // We don't want All detections to include images with no detections (i.e., Confidence range includes 0), so if we see a zero, we 
                    // alter that to just above zero.
                    lowerBound = DetectionConfidenceLowerForUI == 0 ? justAboveZero : DetectionConfidenceLowerForUI;
                    upperBound = DetectionConfidenceHigherForUI == 0 ? justAboveZero : DetectionConfidenceHigherForUI;
                }
                else
                {
                    lowerBound = DetectionConfidenceLowerForUI;
                    upperBound = DetectionConfidenceHigherForUI;
                }
                return new(lowerBound, upperBound);
            }
        }

        private static double TypicalDetectionThreshold =>
            GlobalReferences.MainWindow?.DataHandler?.FileDatabase == null
                ? RecognizerValues.DefaultTypicalDetectionThresholdIfUnknown
                : Math.Round(GlobalReferences.MainWindow.DataHandler.FileDatabase.GetTypicalDetectionThreshold(), 5);

        // Unused but keep around in case it becomes useful at some point
        //private static double TypicalClassificationThreshold =>
        //    GlobalReferences.MainWindow?.DataHandler?.FileDatabase == null
        //        ? RecognizerValues.DefaultTypicalClassificationThresholdIfUnknown
        //        : (double)GlobalReferences.MainWindow.DataHandler.FileDatabase.GetTypicalClassificationThreshold();

        // Unused but keep for now in case it becomes useful at some point
        //private double _currentClassificationThreshold = -1;
        //public double CurrentClassificationThreshold
        //{
        //    set => _currentClassificationThreshold = value;
        //    get =>
        //        _currentClassificationThreshold < 0
        //            ? TypicalClassificationThreshold
        //            : _currentClassificationThreshold;
        //}

        // Unused but keep for now in case it becomes useful at some point
        //public static double ConservativeDetectionThreshold =>
        //    GlobalReferences.MainWindow?.DataHandler?.FileDatabase == null
        //        ? RecognizerValues.DefaultConservativeDetectionThresholdIfUnknown
        //        : (double)GlobalReferences.MainWindow.DataHandler.FileDatabase.GetConservativeDetectionThreshold();

        #endregion

        #region Constructor - Initializes various defaults, initiallly to All
        public RecognitionSelections()
        {
            ClearAllDetectionsUses();

            this.DetectionCategoryNumber = Constant.RecognizerValues.AllDetectionCategoryNumber;
            this.InterpretAllDetectionsAsEmpty = false;
            this.DetectionConfidenceHigherForUI = 1;
            this.AllDetections = true;
            this.RankByDetectionConfidence = false;

            this.ClassificationCategoryNumber = string.Empty;
            this.RankByClassificationConfidence = false;
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
