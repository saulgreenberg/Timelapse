using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Timelapse.Detection
{
    public static class DetectorUtilities
    {
        public static bool IsMegadetectorVersionHigherInDestination(string source, string destination)
        {
            if (source == destination)
            {
                return false;
            }
            if (source == Constant.DetectionValues.MDVersionUnknown)
            {
                return true;
            }
            return string.Compare(source, destination) == -1;
        }

        public static bool IsMegadetectorVersionSameHigherInDestination(string source, string destination)
        {
            if (source == destination)
            {
                return true;
            }
            if (source == Constant.DetectionValues.MDVersionUnknown || String.IsNullOrWhiteSpace(source))
            {
                return true;
            }
            return string.Compare(source, destination) == -1;
        }

        public static Dictionary<string, object> GenerateBestDetectorInfoFromTwoInfoDictionaries(Dictionary<string, object> infoDict1, info info)
        {
            Dictionary<string, object> infoDictFromJsonInfo = new Dictionary<string, object>
            {
                {Constant.InfoColumns.InfoID, 1},
                {Constant.InfoColumns.Detector, info.detector ?? Constant.DetectionValues.DetectorUnknown},
                {Constant.InfoColumns.DetectionCompletionTime, info.detection_completion_time ?? Constant.DetectionValues.DetectionCompletionTimeUnknown},
                {Constant.InfoColumns.DetectorVersion, info.detector_metadata.megadetector_version ?? Constant.DetectionValues.MDVersionUnknown},
                {Constant.InfoColumns.TypicalDetectionThreshold, info.detector_metadata.typical_detection_threshold ?? Constant.DetectionValues.DefaultTypicalDetectionThresholdIfUnknown},
                {Constant.InfoColumns.ConservativeDetectionThreshold, info.detector_metadata.conservative_detection_threshold ?? Constant.DetectionValues.DefaultConservativeDetectionThresholdIfUnknown},
                {Constant.InfoColumns.Classifier, String.IsNullOrEmpty(info.classifier) 
                    ? String.Empty 
                    : info.classifier},
                {Constant.InfoColumns.ClassificationCompletionTime, 
                    String.IsNullOrEmpty(info.classification_completion_time) 
                    ? String.Empty
                    : info.classification_completion_time},
                {Constant.InfoColumns.TypicalClassificationThreshold, info.classifier_metadata.typical_classification_threshold ?? Constant.DetectionValues.DefaultTypicalClassificationThresholdIfUnknown},
            };
            return DetermineDetectorInfoToUse(infoDict1, infoDictFromJsonInfo);
        }

        // When there are two detector info structures, return a new detector structure that 
        // - fills in defaults as needed
        // - tries to populate the values with ones that make the most sense in terms of versions and confidence values to use
        public static Dictionary<string, object> DetermineDetectorInfoToUse(Dictionary<string, object> infoDict1, Dictionary<string, object> infoDict2)
        {
            Dictionary<string, object> updatedDict = new Dictionary<string, object>
            {
                // Use the same InfoID, which should always be 1
                { Constant.InfoColumns.InfoID, infoDict1[Constant.InfoColumns.InfoID] }
            };

            // Populate the dict with the latest detector version, if known.If they are the same, prefer the 2nd dictionary
            string i1DetectorVersion = infoDict1.TryGetValue(Constant.InfoColumns.DetectorVersion, out object i1dv)
                ? i1dv == null
                    ? Constant.DetectionValues.MDVersionUnknown : i1dv.ToString()
                    : Constant.DetectionValues.MDVersionUnknown;
            string i2DetectorVersion = infoDict2.TryGetValue(Constant.InfoColumns.DetectorVersion, out object i2dv)
                    ? i2dv == null
                        ? Constant.DetectionValues.MDVersionUnknown
                        : i2dv.ToString()
                    : Constant.DetectionValues.MDVersionUnknown;
            bool i2Preferred = IsMegadetectorVersionSameHigherInDestination(i1DetectorVersion, i2DetectorVersion);
            updatedDict.Add(Constant.InfoColumns.DetectorVersion, i2Preferred ? i2DetectorVersion : i1DetectorVersion);

            // Populate the dict with the preferred detector name
            string i1Detector = infoDict1.TryGetValue(Constant.InfoColumns.Detector, out object i1d)
                ? i1d == null
                    ? Constant.DetectionValues.DetectorUnknown
                    : i1d.ToString()
                : Constant.DetectionValues.DetectorUnknown;
            string i2Detector = infoDict2.TryGetValue(Constant.InfoColumns.Detector, out object i2d)
                 ? i2d == null 
                    ? Constant.DetectionValues.DetectorUnknown
                    : i2d.ToString()
                 : Constant.DetectionValues.DetectorUnknown;
            updatedDict.Add(Constant.InfoColumns.Detector, i2Preferred ? i2Detector : i1Detector);

            // Populate the dict with the preferred detection completion time
            string i1DetectionCompletionTime = infoDict1.TryGetValue(Constant.InfoColumns.DetectionCompletionTime, out object i1dct)
                ? i1dct == null
                    ? Constant.DetectionValues.DetectionCompletionTimeUnknown
                    : i1dct.ToString()
                : Constant.DetectionValues.DetectionCompletionTimeUnknown;
            string i2DetectionCompletionTime = infoDict2.TryGetValue(Constant.InfoColumns.DetectionCompletionTime, out object i2dct)
                ? i2dct == null
                    ? Constant.DetectionValues.DetectionCompletionTimeUnknown
                    : i2dct.ToString()
                : Constant.DetectionValues.DetectionCompletionTimeUnknown;
            updatedDict.Add(Constant.InfoColumns.DetectionCompletionTime, i2Preferred ? i2DetectionCompletionTime : i1DetectionCompletionTime);

            // Populate the dict with the preferred classifier 
            string i1Classifier = infoDict1.TryGetValue(Constant.InfoColumns.Classifier, out object i1c)
                ? i1c == null
                    ? Constant.DetectionValues.ClassifierUnknown
                    : i1c.ToString()
                : Constant.DetectionValues.ClassifierUnknown;
            string i2Classifier = infoDict2.TryGetValue(Constant.InfoColumns.Classifier, out object i2c)
                ? i2c == null
                    ? Constant.DetectionValues.ClassifierUnknown
                    : i2c.ToString()
                : Constant.DetectionValues.ClassifierUnknown;
            updatedDict.Add(Constant.InfoColumns.Classifier, i2Preferred ? i2Classifier : i1Classifier);

            // Populate the dict with the preferred classification completion time
            string i1ClassificationCompletionTime = infoDict1.TryGetValue(Constant.InfoColumns.ClassificationCompletionTime, out object i1cct)
                ? i1cct == null
                    ? Constant.DetectionValues.DetectionCompletionTimeUnknown
                    : i1cct.ToString()
                : Constant.DetectionValues.DetectionCompletionTimeUnknown;
            string i2ClassificationCompletionTime = infoDict2.TryGetValue(Constant.InfoColumns.ClassificationCompletionTime, out object i2cct)
                ? i2cct == null
                    ? Constant.DetectionValues.ClassificationCompletionTimeUnknown
                    : i2cct.ToString()
                : Constant.DetectionValues.ClassificationCompletionTimeUnknown;
            updatedDict.Add(Constant.InfoColumns.ClassificationCompletionTime, i2Preferred ? i2ClassificationCompletionTime : i1ClassificationCompletionTime);

            // Populate the dict with the smaller of the two typical detection thresholds
            float i1TypicalDetectionThreshold = infoDict1.TryGetValue(Constant.InfoColumns.TypicalDetectionThreshold, out object i1tdt)
                ? i1tdt == null
                    ? Constant.DetectionValues.DefaultTypicalDetectionThresholdIfUnknown
                    : Convert.ToSingle(i1tdt)
                : Constant.DetectionValues.DefaultTypicalDetectionThresholdIfUnknown;
            float i2TypicalDetectionThreshold = infoDict2.TryGetValue(Constant.InfoColumns.TypicalDetectionThreshold, out object i2tdt)
                ? i2tdt == null
                    ? Constant.DetectionValues.DefaultTypicalDetectionThresholdIfUnknown
                    : Convert.ToSingle(i2tdt)
                : Constant.DetectionValues.DefaultTypicalDetectionThresholdIfUnknown;
            updatedDict.Add(Constant.InfoColumns.TypicalDetectionThreshold, Math.Min(i1TypicalDetectionThreshold, i2TypicalDetectionThreshold));

            // Populate the dict with the smaller of the two typical classification thresholds
            float i1TypicalClassificationThreshold = infoDict1.TryGetValue(Constant.InfoColumns.TypicalClassificationThreshold, out object i1tct)
                ? i1tct == null 
                    ? Constant.DetectionValues.DefaultTypicalClassificationThresholdIfUnknown
                    : Convert.ToSingle(i1tct)
                : Constant.DetectionValues.DefaultTypicalClassificationThresholdIfUnknown;
            float i2TypicalClassificationThreshold = infoDict2.TryGetValue(Constant.InfoColumns.TypicalClassificationThreshold, out object i2tct)
                ? i2tct == null
                    ? Constant.DetectionValues.DefaultTypicalClassificationThresholdIfUnknown
                    : Convert.ToSingle(i2tct)
                : Constant.DetectionValues.DefaultTypicalClassificationThresholdIfUnknown;
            updatedDict.Add(Constant.InfoColumns.TypicalClassificationThreshold, Math.Min(i1TypicalClassificationThreshold, i2TypicalClassificationThreshold));

            // Populate the dict with the smaller of the two conservative classification thresholds
            float i1ConservativeDetectionThreshold = infoDict1.TryGetValue(Constant.InfoColumns.ConservativeDetectionThreshold, out object i1cdt)
                ? i1cdt == null 
                    ? Constant.DetectionValues.DefaultConservativeDetectionThresholdIfUnknown
                    : Convert.ToSingle(i1cdt)
                : Constant.DetectionValues.DefaultConservativeDetectionThresholdIfUnknown; 
            float i2TConservativeDetectionThreshold = infoDict2.TryGetValue(Constant.InfoColumns.ConservativeDetectionThreshold, out object i2cdt)
                ? i2cdt == null 
                    ? Constant.DetectionValues.DefaultConservativeDetectionThresholdIfUnknown
                    : Convert.ToSingle(i2cdt)
                : Constant.DetectionValues.DefaultConservativeDetectionThresholdIfUnknown;
            updatedDict.Add(Constant.InfoColumns.ConservativeDetectionThreshold, Math.Min(i1ConservativeDetectionThreshold, i2TConservativeDetectionThreshold));

            // x new ColumnTuple(Constant.InfoColumns.InfoID, 1),
            // x new ColumnTuple(Constant.InfoColumns.Detector, detector.info.detector),
            // x new ColumnTuple(Constant.InfoColumns.DetectionCompletionTime, detector.info.detection_completion_time),
            // x new ColumnTuple(Constant.InfoColumns.Classifier, detector.info.classifier),
            // x new ColumnTuple(Constant.InfoColumns.ClassificationCompletionTime, detector.info.classification_completion_time),
            // x new ColumnTuple(Constant.InfoColumns.DetectorVersion, detector.info.detector_metadata.megadetector_version),
            // x new ColumnTuple(Constant.InfoColumns.TypicalDetectionThreshold, (float)detector.info.detector_metadata.typical_detection_threshold),
            // x new ColumnTuple(Constant.InfoColumns.ConservativeDetectionThreshold, (float)detector.info.detector_metadata.conservative_detection_threshold),
            // x new ColumnTuple(Constant.InfoColumns.TypicalClassificationThreshold, (float)detector.info.classifier_metadata.typical_classification_threshold),

            return updatedDict;
        }
    }
}
