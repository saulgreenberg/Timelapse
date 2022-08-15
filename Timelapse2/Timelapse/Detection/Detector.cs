using System;
using System.Collections.Generic;
#pragma warning disable IDE1006 // Naming Style - we are using lower case names to match the json structure, we  mute the warning
namespace Timelapse.Detection
{
    //    [SuppressMessage("StyleCop.CSharp.NamingRules", "SA1300:ElementMustBeginWithUpperCaseLetter", Justification = "Reviewed.")]
    //#pragma warning disable SA1300 // ElementMustBeginWithUpperCaseLetter
    // This file contains four classes, which will hold image recognition information read in from the Microsoft Megadetector JSON file
    // - Detector
    // - info
    // - image
    // - detection

    /// <summary>
    /// The Detector class holds data produced by Microsoft's Megadetector
    /// Property names and structures follow the Microsoft Megadetetor JSON attribut names
    /// in order to allow the JSON data to be deserialized into the Detector data structure
    /// </summary>    
    public class Detector : IDisposable
    {
        #region Public Properties
        public info info { get; set; }
        public Dictionary<string, string> detection_categories { get; set; }
        public Dictionary<string, string> classification_categories { get; set; }

        public List<image> images { get; set; }
        #endregion

        #region Constructor 
        public Detector()
        {
            this.images = new List<image>();
        }

        #endregion

        #region Public Set Defaults
        // Defaults are just used when reading in current csv files, as that file does not includese the category definitions
        public void SetDetectionCategoryDefaults()
        {
            this.detection_categories = new Dictionary<string, string>
            {
                { Constant.DetectionValues.NoDetectionCategory, Constant.DetectionValues.NoDetectionLabel },
                { "2", "person" },
                { "4", "vehicle" }
            };
        }

        // Defaults are just used when reading in current csv files, as that file does not include the category definitions
        public void SetDetectionClassificationDefaults()
        {
            this.classification_categories = new Dictionary<string, string>
            {
                { Constant.DetectionValues.NoDetectionCategory, Constant.DetectionValues.NoDetectionLabel },
            };
        }
        #endregion

        #region Disposing
        // Dispose implemented to follow pattern described in CA1816
        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                this.info = null;
                this.detection_categories = null;
                this.classification_categories = null;
                this.images = null;
            }
        }
        #endregion
    }

    /// <summary>
    /// The Info class holds extra information produced by Microsoft's Megadetector
    /// </summary>    
    public class info
    {
        #region Public Properties
        // The detector is a string that should include the detector filename, which will likely include
        // the detector verision in the form md_v*
        // for example the file name md_v4.1.0.pb says it used the megadetector version 5
        public string detector { get; set; }
        public string detection_completion_time { get; set; }
        public string classifier { get; set; }
        public string classification_completion_time { get; set; }
        public detector_metadata detector_metadata { get; set; }
        public classifier_metadata classifier_metadata { get; set; }
        #endregion

        #region Constructor
        public info()
        {
        }
        #endregion

        #region Public Set Defaults
        // Defaults are just used when reading in current csv files, as that file does not include the detector information
        public void SetInfoDefaults()
        {
            this.detector = "megadetector_unknown_version";
            this.detection_completion_time = "unknown";
            this.classifier = "ecosystem1_unknown_version";
            this.classification_completion_time = "unknown";
        }
        #endregion
    }

    public class detector_metadata
    {
        // if its null, load it with some defaults
        public detector_metadata()
        {
            megadetector_version = Constant.DetectionValues.MDVersionUnknown;
            typical_detection_threshold = Constant.DetectionValues.DefaultTypicalDetectionThresholdIfUnknown;
            conservative_detection_threshold = Constant.DetectionValues.DefaultConservativeDetectionThresholdIfUnknown;
        }
        // The megadetector_version is a string that provides detector version in the form md_v*
        // for example md_v5 is megadetector version 5
        public string megadetector_version { get; set; }

        // typical_detection_threshold describes the typical bound of the maximum detection confidence
        // that normally produces a mostly correct result. For example, if it is .8, then the 
        // confidence range of .8 - 1 is a reasonable starting point for looking for mostly correct
        // detections
        public float? typical_detection_threshold { get; set; }

        // conservative_detection_threshold describes the lower bound of the maximum detection confidence
        // where results below that are likely mis-detections. For example, if it is .4, then anything less
        // than .4 is likely empty. Thus 'Empty' could be consider from 0 - .4
        public float? conservative_detection_threshold { get; set; }
    }

    public class classifier_metadata
    {
        public classifier_metadata()
        {
            typical_classification_threshold = Constant.DetectionValues.DefaultTypicalClassificationThresholdIfUnknown; // CHECK THIS
        }
        // typical_classification_threshold describes the typical bound of the classification probability
        // that normally produces a mostly correct result. For example, if it is .75, then the 
        // a classification probability of .75 or higher is likely correct
        public float? typical_classification_threshold { get; set; }
    }

    /// <summary>
    /// The Image class holds information describing each image
    /// such as an ID, its file name, the max detection confidence, and a list of detections
    /// </summary>    
    public class image
    {
        #region Public Properties
        public int imageID { get; set; }
        public string file { get; set; }
        public float? max_detection_conf { get; set; }
        public List<detection> detections { get; set; }
        #endregion

        #region Constructor
        public image()
        {
            this.file = String.Empty;
            this.max_detection_conf = 0;
            this.detections = new List<detection>();
        }
        #endregion
    }

    /// <summary>
    /// The Detection class holds information describing a single detections
    /// such as an ID, its category, its confidence, and the bounding box coordinates
    /// </summary>    
    public class detection
    {
        #region Public Properties
        public int detectionID { get; set; }
        public string category { get; set; }
        public float conf { get; set; }

        //#pragma warning disable CA1819 // Properties should not return arrays. Reason: A Json serializer requires direct writing into an array property of this type.
        public double[] bbox { get; set; }
        //#pragma warning restore CA1819 // Properties should not return arrays

        public List<Object[]> classifications { get; set; }
        #endregion

        #region Constructor
        public detection()
        {
            this.category = String.Empty;
            this.conf = 0;
            this.classifications = new List<Object[]>();
            this.bbox = new double[4];
        }
        #endregion
    }
}
