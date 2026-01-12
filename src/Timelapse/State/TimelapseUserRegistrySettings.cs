using System;
using System.Windows;
using Microsoft.Win32;
using Timelapse.Constant;
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
        public bool ImageMetadataAskOnLoad { get; set; }
        public double OffsetLensZoomFactor { get; set; }
        public DateTime MostRecentCheckForUpdates { get; set; }
        public RecencyOrderedList<string> RecentlyOpenedTemplateFiles { get; private set; }
        public Rect QuickPasteWindowPosition { get; set; }
        public bool SuppressAmbiguousDatesDialog { get; set; }
        public bool SuppressCsvExportDialog { get; set; }
        public bool SuppressCsvImportPrompt { get; set; }
        public bool SuppressHowDuplicatesWork { get; set; }
        public bool SuppressOpeningMessageDialog { get; set; }
        public bool SuppressOpeningWithOlderTimelapseVersionDialog { get; set; }
        public bool SuppressPropagateFromLastNonEmptyValuePrompt { get; set; }
        public bool SuppressSelectedAmbiguousDatesPrompt { get; set; }
        public bool SuppressSelectedCsvExportPrompt { get; set; }
        public bool SuppressSelectedDarkThresholdPrompt { get; set; }
        public bool SuppressSelectedDateTimeFixedCorrectionPrompt { get; set; }
        public bool SuppressSelectedDateTimeLinearCorrectionPrompt { get; set; }
        public bool SuppressSelectedDaylightSavingsCorrectionPrompt { get; set; }
        public bool SuppressSelectedPopulateFieldFromMetadataPrompt { get; set; }
        public bool SuppressSelectedRereadDatesFromFilesPrompt { get; set; }
        public bool SuppressShortcutDetectedPrompt { get; set; }
        public bool SuppressWarningToUpdateDBFilesToSQLPrompt { get; set; }
        public Throttles Throttles { get; }
        public bool TabOrderIncludeDateTime { get; set; }
        public bool TabOrderIncludeDeleteFlag { get; set; }
        public Size TimelapseWindowSize { get; set; }
        public Rect TimelapseWindowPosition { get; set; }
        public Size TemplateEditorWindowSize { get; set; }

        public bool VideoAutoPlay { get; set; }
        public bool VideoRepeat { get; set; }
        public bool VideoMute { get; set; }
        public int VideoSpeed { get; set; }
        #endregion

        #region Constructors
        public TimelapseUserRegistrySettings() : this(WindowRegistryKeys.RootKey)
        {
        }

        internal TimelapseUserRegistrySettings(string registryKey)
            : base(registryKey)
        {
            Throttles = new();
            ReadSettingsFromRegistry();
        }
        #endregion

        #region Read from registry
        /// <summary>
        /// Read all standard settings from registry
        /// </summary>
        public void ReadSettingsFromRegistry()
        {
            using RegistryKey registryKey = OpenRegistryKey();
            BookmarkScale = new(registryKey.GetDouble(WindowRegistryKeys.BookmarkScaleX, 1.0), registryKey.GetDouble(WindowRegistryKeys.BookmarkScaleY, 1.0));
            BookmarkTranslation = new(registryKey.GetDouble(WindowRegistryKeys.BookmarkTranslationX, 1.0), registryKey.GetDouble(WindowRegistryKeys.BookmarkTranslationY, 1.0));
            BoundingBoxAnnotate = registryKey.GetBoolean(WindowRegistryKeys.BoundingBoxAnnotate, true);
            BoundingBoxColorBlindFriendlyColors = registryKey.GetBoolean(WindowRegistryKeys.BoundingBoxColorBlindFriendlyColors, false);
            CSVDateTimeOptions = registryKey.GetEnum(WindowRegistryKeys.CSVDateTimeOptions, CSVDateTimeOptionsEnum.DateTimeWithoutTSeparatorColumn);
            if (CSVDateTimeOptions == CSVDateTimeOptionsEnum.DateTimeUTCWithOffset)
            {
                // We no longer use the above option, so revert it to the default CSV setting
                CSVDateTimeOptions = CSVDateTimeOptionsEnum.DateAndTimeColumns;
                CSVInsertSpaceBeforeDates = true;
            }
            CSVIncludeFolderColumn = registryKey.GetBoolean(WindowRegistryKeys.CSVIncludeFolderColumn, true);
            CSVInsertSpaceBeforeDates = registryKey.GetBoolean(WindowRegistryKeys.CSVInsertSpaceBeforeDates, true);
            CustomSelectionTermCombiningOperator = registryKey.GetEnum(WindowRegistryKeys.CustomSelectionTermCombiningOperator, CustomSelectionOperatorEnum.And);
            DarkPixelRatioThreshold = registryKey.GetDouble(WindowRegistryKeys.DarkPixelRatio, ImageValues.DarkPixelRatioThresholdDefault);
            DarkPixelThreshold = registryKey.GetInteger(WindowRegistryKeys.DarkPixelThreshold, ImageValues.DarkPixelThresholdDefault);
            DeleteFolderManagement = (DeleteFolderManagementEnum)registryKey.GetInteger(WindowRegistryKeys.DeleteFolderManagementValue, (int)DeleteFolderManagementEnum.ManualDelete);
            EpisodeTimeThreshold = registryKey.GetTimeSpanAsSeconds(WindowRegistryKeys.EpisodeTimeThreshold, TimeSpan.FromSeconds(EpisodeDefaults.TimeThresholdDefault));
            EpisodeMaxRangeToSearch = registryKey.GetInteger(WindowRegistryKeys.EpisodeMaxRangeToSearch, EpisodeDefaults.DefaultRangeToSearch);
            FilePlayerSlowValue = registryKey.GetDouble(WindowRegistryKeys.FilePlayerSlowValue, FilePlayerValues.PlaySlowDefault.TotalSeconds);
            FilePlayerFastValue = registryKey.GetDouble(WindowRegistryKeys.FilePlayerFastValue, FilePlayerValues.PlayFastDefault.TotalSeconds);
            MagnifyingGlassOffsetLensEnabled = registryKey.GetBoolean(WindowRegistryKeys.MagnifyingGlassOffsetLensEnabled, true);
            MagnifyingGlassZoomFactor = registryKey.GetDouble(WindowRegistryKeys.MagnifyingGlassZoomFactor, MarkableCanvas.MagnifyingGlassDefaultZoom);
            ImageMetadataAskOnLoad = registryKey.GetBoolean(WindowRegistryKeys.ImageMetadataAskOnLoad, false);
            MostRecentCheckForUpdates = registryKey.GetDateTime(WindowRegistryKeys.MostRecentCheckForUpdates, DateTime.Now);
            RecentlyOpenedTemplateFiles = registryKey.GetRecencyOrderedList(WindowRegistryKeys.RecentlyOpenedTemplateFiles);
            OffsetLensZoomFactor = registryKey.GetDouble(WindowRegistryKeys.OffsetLensZoomFactor, MarkableCanvas.OffsetLensDefaultZoom);
            QuickPasteWindowPosition = registryKey.GetRect(WindowRegistryKeys.QuickPasteWindowPosition, new(0.0, 0.0, 0.0, 0.0));
            SuppressAmbiguousDatesDialog = registryKey.GetBoolean(WindowRegistryKeys.SuppressAmbiguousDatesDialog, false);
            SuppressCsvExportDialog = registryKey.GetBoolean(WindowRegistryKeys.SuppressCsvExportDialog, false);
            SuppressCsvImportPrompt = registryKey.GetBoolean(WindowRegistryKeys.SuppressCsvImportPrompt, false);
            SuppressHowDuplicatesWork = registryKey.GetBoolean(WindowRegistryKeys.SuppressHowDuplicatesWorkDialog, false);
            SuppressOpeningMessageDialog = registryKey.GetBoolean(WindowRegistryKeys.SuppressOpeningMessageDialog, false);
            SuppressOpeningWithOlderTimelapseVersionDialog = registryKey.GetBoolean(WindowRegistryKeys.SuppressOpeningWithOlderTimelapseVersionDialog, false);
            SuppressPropagateFromLastNonEmptyValuePrompt = registryKey.GetBoolean(WindowRegistryKeys.SuppressPropagateFromLastNonEmptyValuePrompt, false);
            SuppressSelectedAmbiguousDatesPrompt = registryKey.GetBoolean(WindowRegistryKeys.SuppressSelectedAmbiguousDatesPrompt, false);
            SuppressSelectedCsvExportPrompt = registryKey.GetBoolean(WindowRegistryKeys.SuppressSelectedCsvExportPrompt, false);
            SuppressSelectedDarkThresholdPrompt = registryKey.GetBoolean(WindowRegistryKeys.SuppressSelectedDarkThresholdPrompt, false);
            SuppressSelectedDateTimeFixedCorrectionPrompt = registryKey.GetBoolean(WindowRegistryKeys.SuppressSelectedDateTimeFixedCorrectionPrompt, false);
            SuppressSelectedDateTimeLinearCorrectionPrompt = registryKey.GetBoolean(WindowRegistryKeys.SuppressSelectedDateTimeLinearCorrectionPrompt, false);
            SuppressSelectedDaylightSavingsCorrectionPrompt = registryKey.GetBoolean(WindowRegistryKeys.SuppressSelectedDaylightSavingsCorrectionPrompt, false);
            SuppressSelectedPopulateFieldFromMetadataPrompt = registryKey.GetBoolean(WindowRegistryKeys.SuppressSelectedPopulateFieldFromMetadataPrompt, false);
            SuppressSelectedRereadDatesFromFilesPrompt = registryKey.GetBoolean(WindowRegistryKeys.SuppressSelectedRereadDatesFromFilesPrompt, false);
            SuppressShortcutDetectedPrompt = registryKey.GetBoolean(WindowRegistryKeys.SuppressShortcutDetectedPrompt, false);
            SuppressWarningToUpdateDBFilesToSQLPrompt = registryKey.GetBoolean(WindowRegistryKeys.SuppressWarningToUpdateDBFilesToSQLPrompt, false);
            TabOrderIncludeDateTime = registryKey.GetBoolean(WindowRegistryKeys.TabOrderIncludeDateTime, false);
            TabOrderIncludeDeleteFlag = registryKey.GetBoolean(WindowRegistryKeys.TabOrderIncludeDeleteFlag, false);
            Throttles.SetDesiredImageRendersPerSecond(registryKey.GetDouble(WindowRegistryKeys.DesiredImageRendersPerSecond, ThrottleValues.DesiredMaximumImageRendersPerSecondDefault));
            TimelapseWindowPosition = registryKey.GetRect(WindowRegistryKeys.TimelapseWindowPosition, new(0.0, 0.0, 1350.0, 900.0));
            TemplateEditorWindowSize = registryKey.GetSize(WindowRegistryKeys.TemplateEditorWindowSize, new(1350.0, 900.0));
            VideoAutoPlay = registryKey.GetBoolean(WindowRegistryKeys.VideoAutoPlay, false);
            VideoMute = registryKey.GetBoolean(WindowRegistryKeys.VideoMute, false);
            VideoRepeat = registryKey.GetBoolean(WindowRegistryKeys.VideoRepeat, false);
            VideoSpeed = registryKey.GetInteger(WindowRegistryKeys.VideoSpeed, 2);
        }

        /// <summary>
        /// Check if a particular registry key exists
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public bool IsRegistryKeyExists(string key)
        {
            using RegistryKey registryKey = OpenRegistryKey();
            return !string.IsNullOrEmpty(registryKey.GetString(key, string.Empty));
        }

        /// <summary>
        /// Get a single registry entry
        /// </summary>
        public string GetFromRegistry(string key)
        {
            using RegistryKey registryKey = OpenRegistryKey();
            return registryKey.GetString(key, string.Empty);
        }

        /// <summary>
        /// Get the Timelapse window position and size from the registry 
        /// </summary>
        /// <param name="key"></param>
        /// <returns>Rect</returns>
        public Rect GetTimelapseWindowPositionAndSizeFromRegistryRect(string key)
        {
            using RegistryKey registryKey = OpenRegistryKey();
            return registryKey.GetRect(key, new(0.0, 0.0, AvalonDockValues.DefaultTimelapseWindowWidth, AvalonDockValues.DefaultTimelapseWindowHeight));
        }

        /// <summary>
        /// Get the Timelapse maximize state from the registry 
        /// </summary>
        /// <param name="key"></param>
        /// <returns>True if maximized, else false</returns>
        public bool GetTimelapseWindowMaximizeStateFromRegistryBool(string key)
        {
            using RegistryKey registryKey = OpenRegistryKey();
            return registryKey.GetBoolean(key, false);
        }
        #endregion

        #region Write to registry
        /// <summary>
        /// Write all Timelapse settings to registry
        /// </summary>
        public void WriteSettingsToRegistry()
        {
            using RegistryKey registryKey = OpenRegistryKey();
            registryKey.Write(WindowRegistryKeys.BookmarkScaleX, BookmarkScale.X);
            registryKey.Write(WindowRegistryKeys.BookmarkScaleY, BookmarkScale.Y);
            registryKey.Write(WindowRegistryKeys.BookmarkTranslationX, BookmarkTranslation.X);
            registryKey.Write(WindowRegistryKeys.BookmarkTranslationY, BookmarkTranslation.Y);
            registryKey.Write(WindowRegistryKeys.BoundingBoxAnnotate, BoundingBoxAnnotate);
            registryKey.Write(WindowRegistryKeys.BoundingBoxColorBlindFriendlyColors, BoundingBoxColorBlindFriendlyColors);
            registryKey.Write(WindowRegistryKeys.CSVDateTimeOptions, CSVDateTimeOptions.ToString());
            registryKey.Write(WindowRegistryKeys.CSVIncludeFolderColumn, CSVIncludeFolderColumn.ToString());
            registryKey.Write(WindowRegistryKeys.CSVInsertSpaceBeforeDates, CSVInsertSpaceBeforeDates);
            registryKey.Write(WindowRegistryKeys.CustomSelectionTermCombiningOperator, CustomSelectionTermCombiningOperator.ToString());
            registryKey.Write(WindowRegistryKeys.DarkPixelRatio, DarkPixelRatioThreshold);
            registryKey.Write(WindowRegistryKeys.DarkPixelThreshold, DarkPixelThreshold);
            registryKey.Write(WindowRegistryKeys.DeleteFolderManagementValue, (int)DeleteFolderManagement);
            registryKey.Write(WindowRegistryKeys.EpisodeTimeThreshold, EpisodeTimeThreshold);
            registryKey.Write(WindowRegistryKeys.EpisodeMaxRangeToSearch, EpisodeMaxRangeToSearch);
            registryKey.Write(WindowRegistryKeys.FilePlayerSlowValue, FilePlayerSlowValue);
            registryKey.Write(WindowRegistryKeys.FilePlayerFastValue, FilePlayerFastValue);
            registryKey.Write(WindowRegistryKeys.DesiredImageRendersPerSecond, Throttles.DesiredImageRendersPerSecond);
            registryKey.Write(WindowRegistryKeys.MagnifyingGlassOffsetLensEnabled, MagnifyingGlassOffsetLensEnabled);
            registryKey.Write(WindowRegistryKeys.MagnifyingGlassZoomFactor, MagnifyingGlassZoomFactor);
            registryKey.Write(WindowRegistryKeys.ImageMetadataAskOnLoad, ImageMetadataAskOnLoad);
            registryKey.Write(WindowRegistryKeys.OffsetLensZoomFactor, OffsetLensZoomFactor);
            registryKey.Write(WindowRegistryKeys.MostRecentCheckForUpdates, MostRecentCheckForUpdates);
            registryKey.Write(WindowRegistryKeys.RecentlyOpenedTemplateFiles, RecentlyOpenedTemplateFiles);
            registryKey.Write(WindowRegistryKeys.QuickPasteWindowPosition, QuickPasteWindowPosition);
            registryKey.Write(WindowRegistryKeys.SuppressAmbiguousDatesDialog, SuppressAmbiguousDatesDialog);
            registryKey.Write(WindowRegistryKeys.SuppressCsvExportDialog, SuppressCsvExportDialog);
            registryKey.Write(WindowRegistryKeys.SuppressCsvImportPrompt, SuppressCsvImportPrompt);
            registryKey.Write(WindowRegistryKeys.SuppressHowDuplicatesWorkDialog, SuppressHowDuplicatesWork);
            registryKey.Write(WindowRegistryKeys.SuppressOpeningMessageDialog, SuppressOpeningMessageDialog);
            registryKey.Write(WindowRegistryKeys.SuppressOpeningWithOlderTimelapseVersionDialog, SuppressOpeningWithOlderTimelapseVersionDialog);
            registryKey.Write(WindowRegistryKeys.SuppressPropagateFromLastNonEmptyValuePrompt, SuppressPropagateFromLastNonEmptyValuePrompt);
            registryKey.Write(WindowRegistryKeys.SuppressSelectedAmbiguousDatesPrompt, SuppressSelectedAmbiguousDatesPrompt);
            registryKey.Write(WindowRegistryKeys.SuppressSelectedCsvExportPrompt, SuppressSelectedCsvExportPrompt);
            registryKey.Write(WindowRegistryKeys.SuppressSelectedDarkThresholdPrompt, SuppressSelectedDarkThresholdPrompt);
            registryKey.Write(WindowRegistryKeys.SuppressSelectedDateTimeFixedCorrectionPrompt, SuppressSelectedDateTimeFixedCorrectionPrompt);
            registryKey.Write(WindowRegistryKeys.SuppressSelectedDateTimeLinearCorrectionPrompt, SuppressSelectedDateTimeLinearCorrectionPrompt);
            registryKey.Write(WindowRegistryKeys.SuppressSelectedDaylightSavingsCorrectionPrompt, SuppressSelectedDaylightSavingsCorrectionPrompt);
            registryKey.Write(WindowRegistryKeys.SuppressSelectedPopulateFieldFromMetadataPrompt, SuppressSelectedPopulateFieldFromMetadataPrompt);
            registryKey.Write(WindowRegistryKeys.SuppressSelectedRereadDatesFromFilesPrompt, SuppressSelectedRereadDatesFromFilesPrompt);
            registryKey.Write(WindowRegistryKeys.SuppressShortcutDetectedPrompt, SuppressShortcutDetectedPrompt);
            registryKey.Write(WindowRegistryKeys.SuppressWarningToUpdateDBFilesToSQLPrompt, SuppressWarningToUpdateDBFilesToSQLPrompt);
            registryKey.Write(WindowRegistryKeys.TabOrderIncludeDateTime, TabOrderIncludeDateTime);
            registryKey.Write(WindowRegistryKeys.TabOrderIncludeDeleteFlag, TabOrderIncludeDeleteFlag);
            registryKey.Write(WindowRegistryKeys.TimelapseWindowPosition, TimelapseWindowPosition);
            registryKey.Write(WindowRegistryKeys.TemplateEditorWindowSize, TemplateEditorWindowSize);
            registryKey.Write(WindowRegistryKeys.VideoAutoPlay, VideoAutoPlay);
            registryKey.Write(WindowRegistryKeys.VideoMute, VideoMute);
            registryKey.Write(WindowRegistryKeys.VideoRepeat, VideoRepeat);
            registryKey.Write(WindowRegistryKeys.VideoSpeed, VideoSpeed);
        }

        /// <summary>
        /// Write a single registry entry, which will eventually convert its type to a string as needed
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        public void WriteToRegistry(string key, string value)
        {
            using RegistryKey registryKey = OpenRegistryKey();
            registryKey.Write(key, value);
        }

        // ReSharper disable once UnusedMember.Global
        public void WriteToRegistry(string key, double value)
        {
            using RegistryKey registryKey = OpenRegistryKey();
            registryKey.Write(key, value);
        }

        public void WriteToRegistry(string key, Rect value)
        {
            using RegistryKey registryKey = OpenRegistryKey();
            registryKey.Write(key, value);
        }

        public void WriteToRegistry(string key, bool value)
        {
            using RegistryKey registryKey = OpenRegistryKey();
            registryKey.Write(key, value);
        }
        #endregion
    }
}
