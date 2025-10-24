﻿using System;
using System.Collections.Generic;
using Timelapse.Constant;

#pragma warning disable IDE1006 // Naming Style - we are using lower case names to match the json structure, we  mute the warning
namespace Timelapse.Recognition
{
    //[SuppressMessage("StyleCop.CSharp.NamingRules", "SA1300:ElementMustBeginWithUpperCaseLetter", Justification = "Reviewed.")]
    //#pragma warning disable SA1300 // ElementMustBeginWithUpperCaseLetter
    // This file contains four classes, which will hold image recognition information read in from the JSON file that follows the Microsoft Megadetector specification
    // - Recognizer
    // - info
    // - image
    // - detection

    /// <summary>
    /// The Recognizer class holds data produced by Microsoft's Megadetector
    /// Property names and structures follow the Microsoft Megadetetor JSON attribut names
    /// in order to allow the JSON data to be deserialized into the Recognizer data structure
    /// </summary>    
    public class Recognizer : IDisposable
    {
        #region Public Properties
        public info info { get; set; }
        public Dictionary<string, string> detection_categories { get; set; }
        public Dictionary<string, string> classification_categories { get; set; }
        public Dictionary<string,string> classification_category_descriptions { get; set; }
        public List<image> images { get; set; } = [];

        #endregion

        #region Public Set Defaults
        // Defaults are just used when reading in current csv files, as that file does not include the category definitions
        // ReSharper disable once UnusedMember.Global
        public void SetDetectionCategoryDefaults()
        {
            detection_categories = new Dictionary<string, string>
            {
                { RecognizerValues.NoDetectionCategory, RecognizerValues.EmptyDetectionLabel },
                { "2", "person" },
                { "4", "vehicle" }
            };
        }

        // Defaults are just used when reading in current csv files, as that file does not include the category definitions
        // ReSharper disable once UnusedMember.Global
        public void SetDetectionClassificationDefaults()
        {
            classification_categories = new Dictionary<string, string>
            {
                { RecognizerValues.NoDetectionCategory, RecognizerValues.EmptyDetectionLabel },
            };
        }
        #endregion

        #region Disposing
        // Dispose implemented to follow pattern described in CA1816
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                info = null;
                detection_categories = null;
                classification_categories = null;
                images = null;
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
        public string format_version { get; set; }
        public detector_metadata detector_metadata { get; set; }
        public classifier_metadata classifier_metadata { get; set; }
        public string summary_report { get; set; }
        #endregion

        #region Public Set Defaults
        // Defaults are just used as needed
        public void SetInfoDefaults()
        {
            detector = RecognizerValues.DetectorUnknown;
            detection_completion_time = RecognizerValues.DetectionCompletionTimeUnknown;
            classifier = RecognizerValues.ClassifierUnknown;
            classification_completion_time = RecognizerValues.ClassificationCompletionTimeUnknown;
            format_version = RecognizerValues.FormatVersionUnknown;
            detector_metadata = new detector_metadata();
            classifier_metadata = new classifier_metadata();
            summary_report = string.Empty;
        }
        #endregion
    }

    public class detector_metadata
    {
        // if its null, load it with some defaults
        // The megadetector_version is a string that provides detector version in the form md_v*
        // for example md_v5 is megadetector version 5
        public string megadetector_version { get; set; } = RecognizerValues.MDVersionUnknown;

        // typical_detection_threshold describes the typical bound of the maximum detection confidence
        // that normally produces a mostly correct result. For example, if it is .8, then the 
        // confidence range of .8 - 1 is a reasonable starting point for looking for mostly correct
        // detections
        public float? typical_detection_threshold { get; set; } = RecognizerValues.DefaultTypicalDetectionThresholdIfUnknown;

        // conservative_detection_threshold describes the lower bound of the maximum detection confidence
        // where results below that are likely mis-detections. For example, if it is .4, then anything less
        // than .4 is likely empty. Thus 'Empty' could be consider from 0 - .4
        public float? conservative_detection_threshold { get; set; } = RecognizerValues.DefaultConservativeDetectionThresholdIfUnknown;
    }

    public class classifier_metadata
    {
        // typical_classification_threshold describes the typical bound of the classification probability
        // that normally produces a mostly correct result. For example, if it is .75, then the 
        // a classification probability of .75 or higher is likely correct
        public float? typical_classification_threshold { get; set; } = RecognizerValues.DefaultTypicalClassificationThresholdIfUnknown;
    }

    /// <summary>
    /// The Image class holds information describing each image
    /// such as an ID, its file name, the max detection confidence, and a list of detections
    /// </summary>    
    public class image
    {
        #region Public Properties
        public long imageID { get; set; }
        public string file { get; set; } = string.Empty;
       
        // if frame_rate is not present for a video, just set it to a reasonable default frames/sec value.
        // it may be wrong, but its better than nothing.
        public float? frame_rate { get; set; } = 0; 
        public List<detection> detections { get; set; } = [];

        #endregion
    }

    /// <summary>
    /// The Detection class holds information describing a single detection
    /// such as an ID, its category, its confidence, and the bounding box coordinates
    /// </summary>    
    public class detection
    {
        #region Public Properties
        public long detectionID { get; set; }
        public string category { get; set; } = string.Empty;
        public float conf { get; set; } = 0;

        //#pragma warning disable CA1819 // Properties should not return arrays. Reason: A Json serializer requires direct writing into an array property of this type.
        public double[] bbox { get; set; } = new double[4];
        //#pragma warning restore CA1819 // Properties should not return arrays

        public int frame_number { get; set; }

        public List<Object[]> classifications { get; set; } = [];

        #endregion

    }
}
