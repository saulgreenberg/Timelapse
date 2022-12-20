using System;
using Timelapse.Enums;
using Timelapse.Util;

namespace Timelapse.Detection
{
    /// <summary>
    /// A class used by Custom Selection (and the Custom Selection Dialog) to set the selection criteria on detections
    /// </summary>
    public class DetectionSelections
    {
        #region Public Properties
        public bool Enabled
        {
            get
            {
                return this.UseRecognition;
            }
        }

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
            get
            {
                return this.CurrentDetectionThreshold;
            }
            set
            {
                this.CurrentDetectionThreshold = value;
            }
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
                if (this.InterpretAllDetectionsAsEmpty)
                {
                    // For Empty category, we want to invert the confidence 
                    // e.g confidence of 1 is returned as confidence of 0
                    // But note that we actually return the confidence of the different threshold in this case, as normally #1 <= #2
                    // Doing so keeps that relationship after the inversion is done.
                    // We also swap the lower/upper bound to keep one less than the other
                    // If Threshold2 is .99 in the UI for empty items, we invert that, but to just above 0
                    // so we capture all the non-zero items (i.e., all images with detections in that range) as otherwise it could
                    //  omit the rare image with a max detection between 0 and .01
                    lowerBound = (this.ConfidenceThreshold2ForUI == 0.99) ? justAboveZero : 1.0 - this.ConfidenceThreshold2ForUI;
                    upperBound = 1.0 - this.ConfidenceThreshold1ForUI;

                }
                else if (this.AllDetections)
                {
                    // We don't want All detections to include images with no detections (i.e., Confidence range includes 0), so if we see a zero, we 
                    // alter that to just above zero.
                    lowerBound = this.ConfidenceThreshold1ForUI == 0 ? justAboveZero : this.ConfidenceThreshold1ForUI;
                    upperBound = this.ConfidenceThreshold2ForUI == 0 ? justAboveZero : this.ConfidenceThreshold2ForUI;
                }
                else
                {
                    lowerBound = this.ConfidenceThreshold1ForUI;
                    upperBound = this.ConfidenceThreshold2ForUI;
                }
                return new Tuple<double, double>(lowerBound, upperBound);
            }
        }

        static private double TypicalDetectionThreshold
        {
            get
            {
                return GlobalReferences.MainWindow?.DataHandler?.FileDatabase == null
                    ? Constant.DetectionValues.DefaultTypicalDetectionThresholdIfUnknown
                    : (double)GlobalReferences.MainWindow?.DataHandler?.FileDatabase.GetTypicalDetectionThreshold();
            }
        }

        private double _currentDetectionThreshold = -1;
        public double CurrentDetectionThreshold
        {
            set
            {
                this._currentDetectionThreshold = value;
            }
            get
            {
                return this._currentDetectionThreshold < 0
                    ? TypicalDetectionThreshold
                    : this._currentDetectionThreshold;
            }
        }

        static private double TypicalClassificationThreshold
        {
            get
            {
                return GlobalReferences.MainWindow?.DataHandler?.FileDatabase == null
                    ? Constant.DetectionValues.DefaultTypicalClassificationThresholdIfUnknown
                    : (double)GlobalReferences.MainWindow?.DataHandler?.FileDatabase.GetTypicalClassificationThreshold();
            }
        }

        private double _currentClassificationThreshold = -1;
        public double CurrentClassificationThreshold
        {
            set
            {
                this._currentClassificationThreshold = value;
            }
            get
            {
                return this._currentClassificationThreshold < 0
                    ? TypicalClassificationThreshold
                    : this._currentClassificationThreshold;
            }
        }

        static public double ConservativeDetectionThreshold
        {
            get
            {
                return GlobalReferences.MainWindow?.DataHandler?.FileDatabase == null
                    ? Constant.DetectionValues.DefaultConservativeDetectionThresholdIfUnknown
                    : (double)GlobalReferences.MainWindow?.DataHandler?.FileDatabase.GetConservativeDetectionThreshold();
            }
        }

        #endregion

        #region Constructor - Initializes various defaults
        public DetectionSelections()
        {
            this.ClearAllDetectionsUses();

            // We don't know the recognition type yet
            this.RecognitionType = RecognitionType.Empty;

            this.DetectionCategory = "1";
            this.ConfidenceThreshold2ForUI = 1;

            this.ClassificationCategory = "1";

            this.InterpretAllDetectionsAsEmpty = false;
            this.RankByConfidence = false;
        }
        #endregion

        #region Public Clear All Detection Uses
        // Bulk disabling of detection selection criteria
        public void ClearAllDetectionsUses()
        {
            this.UseRecognition = false;
        }
        #endregion
    }
}
