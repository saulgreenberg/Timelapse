using System;
using System.Windows;
using Microsoft.Win32;
using Timelapse.DataStructures;
using Timelapse.Enums;
using Timelapse.Extensions;
using Timelapse.Util;

namespace Timelapse.State
{
    /// 
    /// Save the state of various things in the Registry.
    /// 
    public class TimelapseUserRegistrySettings : UserRegistrySettings
    {
        #region Public Properties - Settings that will be saved into the registry
        public bool AudioFeedback { get; set; }
        public Point BookmarkScale { get; set; }
        public Point BookmarkTranslation { get; set; }
        public bool BoundingBoxAnnotate { get; set; }
        public bool BoundingBoxColorBlindFriendlyColors { get; set; }
        public CustomSelectionOperatorEnum CustomSelectionTermCombiningOperator { get; set; }
        public CSVDateTimeOptionsEnum CSVDateTimeOptions { get; set; }
        public bool CSVIncludeFolderColumn { get; set; }
        public bool CSVInsertSpaceBeforeDates { get; set; }
        public int DarkPixelThreshold { get; set; }
        public double DarkPixelRatioThreshold { get; set; }
        public TimeSpan EpisodeTimeThreshold { get; set; }
        public int EpisodeMaxRangeToSearch { get; set; }
        public DeleteFolderManagementEnum DeleteFolderManagement { get; set; }
        public double FilePlayerSlowValue { get; set; }
        public double FilePlayerFastValue { get; set; }
        public bool MagnifyingGlassOffsetLensEnabled { get; set; }
        public double MagnifyingGlassZoomFactor { get; set; }
        public bool MetadataAskOnLoad { get; set; }
        public double OffsetLensZoomFactor { get; set; }
        public DateTime MostRecentCheckForUpdates { get; set; }
        public RecencyOrderedList<string> MostRecentImageSets { get; private set; }
        public Rect QuickPasteWindowPosition { get; set; }
        public bool SuppressAmbiguousDatesDialog { get; set; }
        public bool SuppressCsvExportDialog { get; set; }
        public bool SuppressCsvImportPrompt { get; set; }
        public bool SuppressHowDuplicatesWork { get; set; }
        public bool SuppressMergeDatabasesPrompt { get; set; }
        public bool SuppressOpeningMessageDialog { get; set; }
        public bool SuppressOpeningWithOlderTimelapseVersionDialog { get; set; }
        public bool SuppressSelectedAmbiguousDatesPrompt { get; set; }
        public bool SuppressSelectedCsvExportPrompt { get; set; }
        public bool SuppressSelectedDarkThresholdPrompt { get; set; }
        public bool SuppressSelectedDateTimeFixedCorrectionPrompt { get; set; }
        public bool SuppressSelectedDateTimeLinearCorrectionPrompt { get; set; }
        public bool SuppressSelectedDaylightSavingsCorrectionPrompt { get; set; }
        public bool SuppressSelectedPopulateFieldFromMetadataPrompt { get; set; }
        public bool SuppressSelectedRereadDatesFromFilesPrompt { get; set; }
        public bool SuppressWarningToUpdateDBFilesToSQLPrompt { get; set; }
        public Throttles Throttles { get; }
        public bool TabOrderIncludeDateTime { get; set; }
        public bool TabOrderIncludeDeleteFlag { get; set; }
        public Size TimelapseWindowSize { get; set; }
        public Rect TimelapseWindowPosition { get; set; }
        #endregion

        #region Constructors
        public TimelapseUserRegistrySettings() : this(Constant.WindowRegistryKeys.RootKey)
        {
        }

        internal TimelapseUserRegistrySettings(string registryKey)
            : base(registryKey)
        {
            this.Throttles = new Throttles();
            this.ReadSettingsFromRegistry();
        }
        #endregion

        #region Read from registry
        /// <summary>
        /// Read all standard settings from registry
        /// </summary>
        public void ReadSettingsFromRegistry()
        {
            using (RegistryKey registryKey = this.OpenRegistryKey())
            {
                this.AudioFeedback = registryKey.GetBoolean(Constant.WindowRegistryKeys.AudioFeedback, false);
                this.BookmarkScale = new Point(registryKey.GetDouble(Constant.WindowRegistryKeys.BookmarkScaleX, 1.0), registryKey.GetDouble(Constant.WindowRegistryKeys.BookmarkScaleY, 1.0));
                this.BookmarkTranslation = new Point(registryKey.GetDouble(Constant.WindowRegistryKeys.BookmarkTranslationX, 1.0), registryKey.GetDouble(Constant.WindowRegistryKeys.BookmarkTranslationY, 1.0));
                this.BoundingBoxAnnotate = registryKey.GetBoolean(Constant.WindowRegistryKeys.BoundingBoxAnnotate, false);
                this.BoundingBoxColorBlindFriendlyColors = registryKey.GetBoolean(Constant.WindowRegistryKeys.BoundingBoxColorBlindFriendlyColors, false);
                this.CSVDateTimeOptions = registryKey.GetEnum(Constant.WindowRegistryKeys.CSVDateTimeOptions, CSVDateTimeOptionsEnum.DateTimeWithoutTSeparatorColumn);
                if (this.CSVDateTimeOptions == CSVDateTimeOptionsEnum.DateTimeUTCWithOffset)
                {
                    // We no longer use the above option, so revert it to the default CSV setting
                    this.CSVDateTimeOptions = CSVDateTimeOptionsEnum.DateAndTimeColumns;
                    this.CSVInsertSpaceBeforeDates = true;
                }
                this.CSVIncludeFolderColumn = registryKey.GetBoolean(Constant.WindowRegistryKeys.CSVIncludeFolderColumn, true);
                this.CSVInsertSpaceBeforeDates = registryKey.GetBoolean(Constant.WindowRegistryKeys.CSVInsertSpaceBeforeDates, true);
                this.CustomSelectionTermCombiningOperator = registryKey.GetEnum(Constant.WindowRegistryKeys.CustomSelectionTermCombiningOperator, CustomSelectionOperatorEnum.And);
                this.DarkPixelRatioThreshold = registryKey.GetDouble(Constant.WindowRegistryKeys.DarkPixelRatio, Constant.ImageValues.DarkPixelRatioThresholdDefault);
                this.DarkPixelThreshold = registryKey.GetInteger(Constant.WindowRegistryKeys.DarkPixelThreshold, Constant.ImageValues.DarkPixelThresholdDefault);
                this.DeleteFolderManagement = (DeleteFolderManagementEnum)registryKey.GetInteger(Constant.WindowRegistryKeys.DeleteFolderManagementValue, (int)DeleteFolderManagementEnum.ManualDelete);
                this.EpisodeTimeThreshold = registryKey.GetTimeSpanAsSeconds(Constant.WindowRegistryKeys.EpisodeTimeThreshold, TimeSpan.FromSeconds(Constant.EpisodeDefaults.TimeThresholdDefault));
                this.EpisodeMaxRangeToSearch = registryKey.GetInteger(Constant.WindowRegistryKeys.EpisodeMaxRangeToSearch, Constant.EpisodeDefaults.DefaultRangeToSearch);
                this.FilePlayerSlowValue = registryKey.GetDouble(Constant.WindowRegistryKeys.FilePlayerSlowValue, Constant.FilePlayerValues.PlaySlowDefault.TotalSeconds);
                this.FilePlayerFastValue = registryKey.GetDouble(Constant.WindowRegistryKeys.FilePlayerFastValue, Constant.FilePlayerValues.PlayFastDefault.TotalSeconds);
                this.MagnifyingGlassOffsetLensEnabled = registryKey.GetBoolean(Constant.WindowRegistryKeys.MagnifyingGlassOffsetLensEnabled, true);
                this.MagnifyingGlassZoomFactor = registryKey.GetDouble(Constant.WindowRegistryKeys.MagnifyingGlassZoomFactor, Constant.MarkableCanvas.MagnifyingGlassDefaultZoom);
                this.MetadataAskOnLoad = registryKey.GetBoolean(Constant.WindowRegistryKeys.MetadataAskOnLoad, false);
                this.MostRecentCheckForUpdates = registryKey.GetDateTime(Constant.WindowRegistryKeys.MostRecentCheckForUpdates, DateTime.Now);
                this.MostRecentImageSets = registryKey.GetRecencyOrderedList(Constant.WindowRegistryKeys.MostRecentlyUsedImageSets);
                this.OffsetLensZoomFactor = registryKey.GetDouble(Constant.WindowRegistryKeys.OffsetLensZoomFactor, Constant.MarkableCanvas.OffsetLensDefaultZoom);
                this.QuickPasteWindowPosition = registryKey.GetRect(Constant.WindowRegistryKeys.QuickPasteWindowPosition, new Rect(0.0, 0.0, 0.0, 0.0));
                this.SuppressAmbiguousDatesDialog = registryKey.GetBoolean(Constant.WindowRegistryKeys.SuppressAmbiguousDatesDialog, false);
                this.SuppressCsvExportDialog = registryKey.GetBoolean(Constant.WindowRegistryKeys.SuppressCsvExportDialog, false);
                this.SuppressCsvImportPrompt = registryKey.GetBoolean(Constant.WindowRegistryKeys.SuppressCsvImportPrompt, false);
                this.SuppressHowDuplicatesWork = registryKey.GetBoolean(Constant.WindowRegistryKeys.SuppressHowDuplicatesWorkDialog, false);
                this.SuppressMergeDatabasesPrompt = registryKey.GetBoolean(Constant.WindowRegistryKeys.SuppressMergeDatabasesDialog, false);
                this.SuppressOpeningMessageDialog = registryKey.GetBoolean(Constant.WindowRegistryKeys.SuppressOpeningMessageDialog, false);
                this.SuppressOpeningWithOlderTimelapseVersionDialog = registryKey.GetBoolean(Constant.WindowRegistryKeys.SuppressOpeningWithOlderTimelapseVersionDialog, false);
                this.SuppressSelectedAmbiguousDatesPrompt = registryKey.GetBoolean(Constant.WindowRegistryKeys.SuppressSelectedAmbiguousDatesPrompt, false);
                this.SuppressSelectedCsvExportPrompt = registryKey.GetBoolean(Constant.WindowRegistryKeys.SuppressSelectedCsvExportPrompt, false);
                this.SuppressSelectedDarkThresholdPrompt = registryKey.GetBoolean(Constant.WindowRegistryKeys.SuppressSelectedDarkThresholdPrompt, false);
                this.SuppressSelectedDateTimeFixedCorrectionPrompt = registryKey.GetBoolean(Constant.WindowRegistryKeys.SuppressSelectedDateTimeFixedCorrectionPrompt, false);
                this.SuppressSelectedDateTimeLinearCorrectionPrompt = registryKey.GetBoolean(Constant.WindowRegistryKeys.SuppressSelectedDateTimeLinearCorrectionPrompt, false);
                this.SuppressSelectedDaylightSavingsCorrectionPrompt = registryKey.GetBoolean(Constant.WindowRegistryKeys.SuppressSelectedDaylightSavingsCorrectionPrompt, false);
                this.SuppressSelectedPopulateFieldFromMetadataPrompt = registryKey.GetBoolean(Constant.WindowRegistryKeys.SuppressSelectedPopulateFieldFromMetadataPrompt, false);
                this.SuppressSelectedRereadDatesFromFilesPrompt = registryKey.GetBoolean(Constant.WindowRegistryKeys.SuppressSelectedRereadDatesFromFilesPrompt, false);
                this.SuppressWarningToUpdateDBFilesToSQLPrompt = registryKey.GetBoolean(Constant.WindowRegistryKeys.SuppressWarningToUpdateDBFilesToSQLPrompt, false);
                this.TabOrderIncludeDateTime = registryKey.GetBoolean(Constant.WindowRegistryKeys.TabOrderIncludeDateTime, false);
                this.TabOrderIncludeDeleteFlag = registryKey.GetBoolean(Constant.WindowRegistryKeys.TabOrderIncludeDeleteFlag, false);
                this.Throttles.SetDesiredImageRendersPerSecond(registryKey.GetDouble(Constant.WindowRegistryKeys.DesiredImageRendersPerSecond, Constant.ThrottleValues.DesiredMaximumImageRendersPerSecondDefault));
                this.TimelapseWindowPosition = registryKey.GetRect(Constant.WindowRegistryKeys.TimelapseWindowPosition, new Rect(0.0, 0.0, 1350.0, 900.0));
            }
        }

        /// <summary>
        /// Check if a particular registry key exists
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public bool IsRegistryKeyExists(string key)
        {
            using (RegistryKey registryKey = this.OpenRegistryKey())
            {
                return !string.IsNullOrEmpty(registryKey.GetString(key, string.Empty));
            }
        }

        /// <summary>
        /// Get a single registry entry
        /// </summary>
        public string GetFromRegistry(string key)
        {
            using (RegistryKey registryKey = this.OpenRegistryKey())
            {
                return registryKey.GetString(key, string.Empty);
            }
        }

        /// <summary>
        /// Get the Timelapse window position and size from the registry 
        /// </summary>
        /// <param name="key"></param>
        /// <returns>Rect</returns>
        public Rect GetTimelapseWindowPositionAndSizeFromRegistryRect(string key)
        {
            using (RegistryKey registryKey = this.OpenRegistryKey())
            {
                return registryKey.GetRect(key, new Rect(0.0, 0.0, Constant.AvalonDockValues.DefaultTimelapseWindowWidth, Constant.AvalonDockValues.DefaultTimelapseWindowHeight));
            }
        }

        /// <summary>
        /// Get the Timelapse maximize state from the registry 
        /// </summary>
        /// <param name="key"></param>
        /// <returns>True if maximized, else false</returns>
        public bool GetTimelapseWindowMaximizeStateFromRegistryBool(string key)
        {
            using (RegistryKey registryKey = this.OpenRegistryKey())
            {
                return registryKey.GetBoolean(key, false);
            }
        }
        #endregion

        #region Write to registry
        /// <summary>
        /// Write all Timelapse settings to registry
        /// </summary>
        public void WriteSettingsToRegistry()
        {
            using (RegistryKey registryKey = this.OpenRegistryKey())
            {
                registryKey.Write(Constant.WindowRegistryKeys.AudioFeedback, this.AudioFeedback);
                registryKey.Write(Constant.WindowRegistryKeys.BookmarkScaleX, this.BookmarkScale.X);
                registryKey.Write(Constant.WindowRegistryKeys.BookmarkScaleY, this.BookmarkScale.Y);
                registryKey.Write(Constant.WindowRegistryKeys.BookmarkTranslationX, this.BookmarkTranslation.X);
                registryKey.Write(Constant.WindowRegistryKeys.BookmarkTranslationY, this.BookmarkTranslation.Y);
                registryKey.Write(Constant.WindowRegistryKeys.BoundingBoxAnnotate, this.BoundingBoxAnnotate);
                registryKey.Write(Constant.WindowRegistryKeys.BoundingBoxColorBlindFriendlyColors, this.BoundingBoxColorBlindFriendlyColors);
                registryKey.Write(Constant.WindowRegistryKeys.CSVDateTimeOptions, this.CSVDateTimeOptions.ToString());
                registryKey.Write(Constant.WindowRegistryKeys.CSVIncludeFolderColumn, this.CSVIncludeFolderColumn.ToString());
                registryKey.Write(Constant.WindowRegistryKeys.CSVInsertSpaceBeforeDates, this.CSVInsertSpaceBeforeDates);
                registryKey.Write(Constant.WindowRegistryKeys.CustomSelectionTermCombiningOperator, this.CustomSelectionTermCombiningOperator.ToString());
                registryKey.Write(Constant.WindowRegistryKeys.DarkPixelRatio, this.DarkPixelRatioThreshold);
                registryKey.Write(Constant.WindowRegistryKeys.DarkPixelThreshold, this.DarkPixelThreshold);
                registryKey.Write(Constant.WindowRegistryKeys.DeleteFolderManagementValue, (int)this.DeleteFolderManagement);
                registryKey.Write(Constant.WindowRegistryKeys.EpisodeTimeThreshold, this.EpisodeTimeThreshold);
                registryKey.Write(Constant.WindowRegistryKeys.EpisodeMaxRangeToSearch, this.EpisodeMaxRangeToSearch);
                registryKey.Write(Constant.WindowRegistryKeys.FilePlayerSlowValue, this.FilePlayerSlowValue);
                registryKey.Write(Constant.WindowRegistryKeys.FilePlayerFastValue, this.FilePlayerFastValue);
                registryKey.Write(Constant.WindowRegistryKeys.DesiredImageRendersPerSecond, this.Throttles.DesiredImageRendersPerSecond);
                registryKey.Write(Constant.WindowRegistryKeys.MagnifyingGlassOffsetLensEnabled, this.MagnifyingGlassOffsetLensEnabled);
                registryKey.Write(Constant.WindowRegistryKeys.MagnifyingGlassZoomFactor, this.MagnifyingGlassZoomFactor);
                registryKey.Write(Constant.WindowRegistryKeys.MetadataAskOnLoad, this.MetadataAskOnLoad);
                registryKey.Write(Constant.WindowRegistryKeys.OffsetLensZoomFactor, this.OffsetLensZoomFactor);
                registryKey.Write(Constant.WindowRegistryKeys.MostRecentCheckForUpdates, this.MostRecentCheckForUpdates);
                registryKey.Write(Constant.WindowRegistryKeys.MostRecentlyUsedImageSets, this.MostRecentImageSets);
                registryKey.Write(Constant.WindowRegistryKeys.QuickPasteWindowPosition, this.QuickPasteWindowPosition);
                registryKey.Write(Constant.WindowRegistryKeys.SuppressAmbiguousDatesDialog, this.SuppressAmbiguousDatesDialog);
                registryKey.Write(Constant.WindowRegistryKeys.SuppressCsvExportDialog, this.SuppressCsvExportDialog);
                registryKey.Write(Constant.WindowRegistryKeys.SuppressCsvImportPrompt, this.SuppressCsvImportPrompt);
                registryKey.Write(Constant.WindowRegistryKeys.SuppressHowDuplicatesWorkDialog, this.SuppressHowDuplicatesWork);
                registryKey.Write(Constant.WindowRegistryKeys.SuppressMergeDatabasesDialog, this.SuppressMergeDatabasesPrompt);
                registryKey.Write(Constant.WindowRegistryKeys.SuppressOpeningMessageDialog, this.SuppressOpeningMessageDialog);
                registryKey.Write(Constant.WindowRegistryKeys.SuppressOpeningWithOlderTimelapseVersionDialog, this.SuppressOpeningWithOlderTimelapseVersionDialog);
                registryKey.Write(Constant.WindowRegistryKeys.SuppressSelectedAmbiguousDatesPrompt, this.SuppressSelectedAmbiguousDatesPrompt);
                registryKey.Write(Constant.WindowRegistryKeys.SuppressSelectedCsvExportPrompt, this.SuppressSelectedCsvExportPrompt);
                registryKey.Write(Constant.WindowRegistryKeys.SuppressSelectedDarkThresholdPrompt, this.SuppressSelectedDarkThresholdPrompt);
                registryKey.Write(Constant.WindowRegistryKeys.SuppressSelectedDateTimeFixedCorrectionPrompt, this.SuppressSelectedDateTimeFixedCorrectionPrompt);
                registryKey.Write(Constant.WindowRegistryKeys.SuppressSelectedDateTimeLinearCorrectionPrompt, this.SuppressSelectedDateTimeLinearCorrectionPrompt);
                registryKey.Write(Constant.WindowRegistryKeys.SuppressSelectedDaylightSavingsCorrectionPrompt, this.SuppressSelectedDaylightSavingsCorrectionPrompt);
                registryKey.Write(Constant.WindowRegistryKeys.SuppressSelectedPopulateFieldFromMetadataPrompt, this.SuppressSelectedPopulateFieldFromMetadataPrompt);
                registryKey.Write(Constant.WindowRegistryKeys.SuppressSelectedRereadDatesFromFilesPrompt, this.SuppressSelectedRereadDatesFromFilesPrompt);
                registryKey.Write(Constant.WindowRegistryKeys.SuppressWarningToUpdateDBFilesToSQLPrompt, this.SuppressWarningToUpdateDBFilesToSQLPrompt);
                registryKey.Write(Constant.WindowRegistryKeys.TabOrderIncludeDateTime, this.TabOrderIncludeDateTime);
                registryKey.Write(Constant.WindowRegistryKeys.TabOrderIncludeDeleteFlag, this.TabOrderIncludeDeleteFlag);
                registryKey.Write(Constant.WindowRegistryKeys.TimelapseWindowPosition, this.TimelapseWindowPosition);
            }
        }

        /// <summary>
        /// Write a single registry entry, which will eventually convert its type to a string as needed
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        public void WriteToRegistry(string key, string value)
        {
            using (RegistryKey registryKey = this.OpenRegistryKey())
            {
                registryKey.Write(key, value);
            }
        }

        // ReSharper disable once UnusedMember.Global
        public void WriteToRegistry(string key, double value)
        {
            using (RegistryKey registryKey = this.OpenRegistryKey())
            {
                registryKey.Write(key, value);
            }
        }

        public void WriteToRegistry(string key, Rect value)
        {
            using (RegistryKey registryKey = this.OpenRegistryKey())
            {
                registryKey.Write(key, value);
            }
        }

        public void WriteToRegistry(string key, bool value)
        {
            using (RegistryKey registryKey = this.OpenRegistryKey())
            {
                registryKey.Write(key, value);
            }
        }
        #endregion
    }
}
