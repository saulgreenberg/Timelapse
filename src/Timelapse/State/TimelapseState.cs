using System;
using System.Windows.Input;
using Timelapse.Constant;
using Timelapse.DataStructures;
using Timelapse.ExifTool;

namespace Timelapse.State
{
    /// <summary>
    /// A class that tracks various states and flags. 
    /// While it inherits from TimelapseUserRegistrySettings (so that all variables can be accessed collectively), 
    /// these particular variables/methods are logically separate as they are only for run-time use and not stored in the registry
    /// </summary>
    public class TimelapseState : TimelapseUserRegistrySettings
    {
        #region Public Properties
        // The threshold used for calculating combined differences between images
        public byte DifferenceThreshold { get; set; } // The threshold used for calculating combined differences

        // Whether the FileNavigator slider is being dragged
        public bool FileNavigatorSliderDragging { get; set; }

        // Whether or not Timelapse is opened with a -viewonly arguement,
        // where users will be able to view but not edit any data
        public bool IsViewOnly { get; set; }

        // Whether the mouse is over a counter control
        public string MouseOverCounter { get; set; }

        public DateTime MostRecentDragEvent { get; set; }

        public bool AlreadyWarnedAboutOpenWithOlderVersionOfTimelapse = false;
        public bool FirstTimeFileLoading { get; set; }

        // Used for tracking whether a new selection was made.
        // This is used in the ThumbnailGrid overview, where thumbnails should not be reused for new selections
        // as otherwise wrong thumbnails (matching previous selections) may be displayed.
        public bool IsNewSelection { get; set; }

        public double BoundingBoxDisplayThreshold
        {
            get;
            set
            {
                field = value;
                if (GlobalReferences.MainWindow?.DataHandler?.FileDatabase != null)
                {
                    GlobalReferences.MainWindow.DataHandler?.FileDatabase.TrySetBoundingBoxDisplayThreshold((float)value);
                }
            }
        }

        public double BoundingBoxThresholdOveride { get; set; }

        public ImageMetadataOnLoad MetadataOnLoad { get; set; }

        public ExifToolManager ExifToolManager { get; set; }

        #endregion

        #region Private (internal) variables 
        // These three variables are used for keeping track of repeated keys.
        // There is a bug in Key.IsRepeat in KeyEventArgs events, where AvalonDock always sets it as true
        // in a floating window. As a workaround, TimelapseWindow will set IsKeyRepeat to false whenever it detects a key up event.
        // keyRepeatCount and mostRecentKey are used for throttling, where keyRepeatCount is incremented everytime repeat is true and the same key is seen 
        private int keyRepeatCount;
        private KeyEventArgs mostRecentKey;
        private bool IsKeyRepeat { get; set; }
        #endregion

        #region Constructor
        public TimelapseState()
        {
            FirstTimeFileLoading = true;
            ExifToolManager = new();
            Reset();
        }
        #endregion

        #region Public Methods - Reset
        /// <summary>
        /// Reset various state variables
        /// </summary>
        public void Reset()
        {
            DifferenceThreshold = ImageValues.DifferenceThresholdDefault;
            FileNavigatorSliderDragging = false;
            MouseOverCounter = null;
            MostRecentDragEvent = DateTime.UtcNow - Throttles.DesiredIntervalBetweenRenders;
            BoundingBoxThresholdOveride = 1;
            ResetKeyRepeat();
            BoundingBoxDisplayThresholdResetToDefault();
        }

        public void BoundingBoxDisplayThresholdResetToValueInDataBase()
        {
            //  Set the default bounding box display threshold value, if needed
            // This guarantees that there is something there, at the very least.
            if (GlobalReferences.MainWindow?.DataHandler?.FileDatabase != null
                && GlobalReferences.MainWindow.DataHandler.FileDatabase.DetectionsExists()
                && true == GlobalReferences.MainWindow?.DataHandler?.FileDatabase.TryGetBoundingBoxDisplayThreshold(out float threshold)
                && Math.Abs(threshold - RecognizerValues.Undefined) > 0.1)
            {
                BoundingBoxDisplayThreshold = threshold;
            }
            else
            {
                // We don't have a way to calculate the bounding box threshold, so just use this default for now
                BoundingBoxDisplayThresholdResetToDefault();
            }
        }
        public void BoundingBoxDisplayThresholdResetToDefault()
        {
            //  Set the default bounding box display threshold value, if needed
            // This guarantees that there is something there, at the very least.
            if (GlobalReferences.MainWindow?.DataHandler?.FileDatabase == null
                || false == GlobalReferences.MainWindow.DataHandler?.FileDatabase.DetectionsExists())
            {
                // We don't have a way to calculate the bounding box threshold, so just use this default for now
                BoundingBoxDisplayThreshold = 0.5;
                return;
            }

            // Calculate the bounding box threshold from the typical and conservative values as specified in the  recognition file
            float typicalThreshold = GlobalReferences.MainWindow.DataHandler != null
                ? GlobalReferences.MainWindow.DataHandler.FileDatabase.GetTypicalDetectionThreshold()
                : RecognizerValues.DefaultTypicalDetectionThresholdIfUnknown;

            float conservativeThreshold = GlobalReferences.MainWindow.DataHandler != null
                ? GlobalReferences.MainWindow.DataHandler.FileDatabase.GetConservativeDetectionThreshold()
                : RecognizerValues.DefaultConservativeDetectionThresholdIfUnknown;

            BoundingBoxDisplayThreshold = 0.4f * (typicalThreshold - conservativeThreshold) + conservativeThreshold;
        }
        #endregion;

        #region Key Repeat methods
        /// <summary>
        /// Key Repeat: Reset 
        /// </summary>
        public void ResetKeyRepeat()
        {
            keyRepeatCount = 0;
            IsKeyRepeat = false;
            mostRecentKey = null;
        }

        /// <summary>
        /// KeyRepeat: Count of the numer of repeats for a key. Used in threshold determination
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public int GetKeyRepeatCount(KeyEventArgs key)
        {
            // check mostRecentKey for null as key delivery is not entirely deterministic
            // it's possible WPF will send the first key as a repeat if the user holds a key down or starts typing while the main window is opening
            // Note that we check the isrepeat from both the key event, and the IsKeyRepeat key that we track due to bugs in how AvalonDock returns erroneous IsRepeat values.
            if (key is { IsRepeat: true } && IsKeyRepeat && mostRecentKey is { IsRepeat: true } && (key.Key == mostRecentKey.Key))
            {
                ++keyRepeatCount;
            }
            else
            {
                keyRepeatCount = 0;
                IsKeyRepeat = true;
            }
            mostRecentKey = key;
            return keyRepeatCount;
        }
        #endregion
    }
}
