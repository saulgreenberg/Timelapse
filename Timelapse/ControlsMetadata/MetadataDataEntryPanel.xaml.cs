using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using Timelapse.Constant;
using Timelapse.ControlsDataEntry;
using Timelapse.DataStructures;
using Timelapse.DataTables;
using Timelapse.DebuggingSupport;
using Timelapse.Enums;
using Timelapse.Util;
using Xceed.Wpf.Toolkit;
using Control = System.Windows.Controls.Control;
using FileDatabase = Timelapse.Database.FileDatabase;

namespace Timelapse.ControlsMetadata
{
    /// <summary>
    /// The metadata data entry panel is the primary means for initializing, editing, and displaying
    /// the metadata associated with a particular image and level.
    /// It also checks to see if the image is in the expected sub-folder, warns the user if it is not,
    /// and disallows initialization if the image does not contain a sub-folder corresponding to this level.
    /// </summary>
    public partial class MetadataDataEntryPanel
    {
        #region Public properties
        // The level associated with this panel
        public int Level { get; set; }

        // The full relative path to the current folder
        public string RelativePathToCurrentFolder
        {
            get => relativePathToCurrentFolder;
            set => SetRelativePathToCurrentFolder(value);
        }

        // The subpath of the relativePathToCurrentFolder that collect path parts only up to the current level
        public string SubPath { get; set; }
        #endregion

        #region Private Variables
        private FileDatabase FileDatabase => GlobalReferences.MainWindow.DataHandler.FileDatabase;
        private readonly TabItem ParentTab;
        private readonly int ExpectedImageLevel;
        private string relativePathToCurrentFolder;
        private readonly Dictionary<string, MetadataDataEntryControl> LookupControlByItsDataLabel = new Dictionary<string, MetadataDataEntryControl>();
        private readonly List<MetadataDataEntryControl> TabControlOrderList = new List<MetadataDataEntryControl>();
        #endregion

        #region Constructor
        public MetadataDataEntryPanel(int level, TabItem parentTab)
        {
            InitializeComponent();
            this.Level = level;
            this.ParentTab = parentTab;
            DataTableBackedList<MetadataInfoRow> metadataInfo = this.FileDatabase.MetadataInfo;
            this.ExpectedImageLevel = metadataInfo == null ? 1 : metadataInfo[metadataInfo.RowCount - 1].Level;
        }
        #endregion

        #region Public: Initialize the panel with the controls defined in the metadataTableControlRows
        public void InitializePanelWithControls(int level, DataTableBackedList<MetadataControlRow> metadataTableControlRows)
        {
            // Always clear the children 
            // This clears things if this is invoked after all rows have been removed and prepares things if things have been changed or added
            this.ControlsPanel.Children.Clear();
            this.LookupControlByItsDataLabel.Clear();
            this.TabControlOrderList.Clear();

            // Return if no data for that level exists. 
            // e.g., when a new level is just being created, or when a level has no controls or no data is associated with it,
            if (null == metadataTableControlRows)
            {
                return;
            }

            // Create a dictionary that holds the datalabels and its values, in case we have to 
            // add it to this level's data structure and/or data table
            Dictionary<string, string> dataLabelsAndValues = new Dictionary<string, string>
            {
                { Constant.DatabaseColumn.FolderDataPath, this.SubPath }
            };

            DataEntryControls styleProvider = new DataEntryControls();
            int row = 0;
            foreach (MetadataControlRow metadataControlRow in metadataTableControlRows)
            {
                string alternateContent = null;
                // Create a control data field corresponding to each control row's definition, and set it to its default imageFolderPath 
                MetadataDataEntryControl controlToAdd;
                switch (metadataControlRow.Type)
                {
                    // Note
                    case Constant.Control.Note:
                        MetadataDataEntryNote noteControl = new MetadataDataEntryNote(metadataControlRow, new Dictionary<string, string>(), styleProvider, metadataControlRow.Tooltip);
                        noteControl.SetContentAndTooltip(metadataControlRow.DefaultValue);
                        controlToAdd = noteControl;
                        // Maybe don't add it if its invisible?
                        noteControl.Container.Visibility = metadataControlRow.Visible ? Visibility.Visible : Visibility.Collapsed;
                        noteControl.GetContentControl.PreviewKeyDown += Note_PreviewKeyDown;
                        break;

                    // Multiline
                    case Constant.Control.MultiLine:
                        MetadataDataEntryMultiLine multiLineControl = new MetadataDataEntryMultiLine(metadataControlRow, styleProvider, metadataControlRow.Tooltip);
                        multiLineControl.SetContentAndTooltip(metadataControlRow.DefaultValue);
                        controlToAdd = multiLineControl;
                        // Maybe don't add it if its invisible?
                        multiLineControl.Container.Visibility = metadataControlRow.Visible ? Visibility.Visible : Visibility.Collapsed;
                        multiLineControl.GetContentControl.PreviewKeyDown += MultiLine_PreviewKeyDown;
                        break;

                    // AlphaNumeric
                    case Constant.Control.AlphaNumeric:
                        MetadataDataEntryAlphaNumeric alphaNumericControl = new MetadataDataEntryAlphaNumeric(metadataControlRow, styleProvider, metadataControlRow.Tooltip);
                        alphaNumericControl.SetContentAndTooltip(metadataControlRow.DefaultValue);
                        controlToAdd = alphaNumericControl;
                        // Maybe don't add it if its invisible?
                        alphaNumericControl.Container.Visibility = metadataControlRow.Visible ? Visibility.Visible : Visibility.Collapsed;
                        alphaNumericControl.GetContentControl.PreviewKeyDown += AlphaNumeric_PreviewKeyDown;
                        break;

                    // IntegerAny
                    case Constant.Control.IntegerAny:
                        MetadataDataEntryIntegerAny integerAnyControl = new MetadataDataEntryIntegerAny(metadataControlRow, styleProvider, metadataControlRow.Tooltip);
                        integerAnyControl.SetContentAndTooltip(metadataControlRow.DefaultValue);
                        controlToAdd = integerAnyControl;
                        // Maybe don't add it if its invisible?
                        integerAnyControl.Container.Visibility = metadataControlRow.Visible ? Visibility.Visible : Visibility.Collapsed;
                        integerAnyControl.GetContentControl.PreviewKeyDown += Integer_PreviewKeyDown;
                        //alphaNumericControl.GetContentControl.PreviewTextInput += Alphanumeric_PreviewTextInput;
                        break;

                    // IntegerPostive
                    case Constant.Control.IntegerPositive:
                        MetadataDataEntryIntegerPositive integerPostiveControl = new MetadataDataEntryIntegerPositive(metadataControlRow, styleProvider, metadataControlRow.Tooltip);
                        integerPostiveControl.SetContentAndTooltip(metadataControlRow.DefaultValue);
                        controlToAdd = integerPostiveControl;
                        // Maybe don't add it if its invisible?
                        integerPostiveControl.Container.Visibility = metadataControlRow.Visible ? Visibility.Visible : Visibility.Collapsed;
                        integerPostiveControl.GetContentControl.PreviewKeyDown += Integer_PreviewKeyDown;
                        break;

                    // DecimalAny
                    case Constant.Control.DecimalAny:
                        MetadataDataEntryDecimalAny decimalAnyControl = new MetadataDataEntryDecimalAny(metadataControlRow, styleProvider, metadataControlRow.Tooltip);
                        decimalAnyControl.SetContentAndTooltip(metadataControlRow.DefaultValue);
                        controlToAdd = decimalAnyControl;
                        // Maybe don't add it if its invisible?
                        decimalAnyControl.Container.Visibility = metadataControlRow.Visible ? Visibility.Visible : Visibility.Collapsed;
                        decimalAnyControl.GetContentControl.PreviewKeyDown += Decimal_PreviewKeyDown;
                        break;

                    // DecimalPostive
                    case Constant.Control.DecimalPositive:
                        MetadataDataEntryDecimalPositive decimalPostiveControl = new MetadataDataEntryDecimalPositive(metadataControlRow, styleProvider, metadataControlRow.Tooltip);
                        decimalPostiveControl.SetContentAndTooltip(metadataControlRow.DefaultValue);
                        controlToAdd = decimalPostiveControl;
                        // Maybe don't add it if its invisible?
                        decimalPostiveControl.Container.Visibility = metadataControlRow.Visible ? Visibility.Visible : Visibility.Collapsed;
                        decimalPostiveControl.GetContentControl.PreviewKeyDown += Decimal_PreviewKeyDown;
                        break;

                    case Constant.Control.FixedChoice:
                        MetadataDataEntryFixedChoice fixedChoiceControl = new MetadataDataEntryFixedChoice(metadataControlRow, styleProvider, metadataControlRow.Tooltip);
                        fixedChoiceControl.SetContentAndTooltip(metadataControlRow.DefaultValue);
                        controlToAdd = fixedChoiceControl;
                        // Maybe don't add it if its invisible?
                        fixedChoiceControl.Container.Visibility = metadataControlRow.Visible ? Visibility.Visible : Visibility.Collapsed;
                        fixedChoiceControl.GetContentControl.PreviewKeyDown += FixedChoice_PreviewKeyDown;
                        break;

                    case Constant.Control.MultiChoice:
                        MetadataDataEntryMultiChoice multiChoiceControl = new MetadataDataEntryMultiChoice(metadataControlRow, styleProvider, metadataControlRow.Tooltip);
                        multiChoiceControl.SetContentAndTooltip(metadataControlRow.DefaultValue);
                        controlToAdd = multiChoiceControl;

                        // Maybe don't add it if its invisible?
                        multiChoiceControl.Container.Visibility = metadataControlRow.Visible ? Visibility.Visible : Visibility.Collapsed;
                        multiChoiceControl.GetContentControl.PreviewKeyDown += MultiChoice_PreviewKeyDown;
                        break;

                    // DateTime_
                    case Constant.Control.DateTime_:
                        MetadataDataEntryDateTimeCustom dateTimeCustomControl = new MetadataDataEntryDateTimeCustom(metadataControlRow, styleProvider, metadataControlRow.Tooltip)
                        {
                            // If we can't get a valid default dateTime, use an alternative
                            ContentControl =
                            {
                                    Value = DateTime.TryParse(metadataControlRow.DefaultValue, out DateTime tempDateTime)
                                        ? tempDateTime
                                        : Constant.ControlDefault.DateTimeCustomDefaultValue
                             }
                        };
                        MetadataDataEntryPanel.ConfigureFormatForDateTimeCustom(dateTimeCustomControl.ContentControl);
                        dateTimeCustomControl.SetContentAndTooltip(DateTimeHandler.ToStringDatabaseDateTime((DateTime)dateTimeCustomControl.ContentControl.Value));
                        controlToAdd = dateTimeCustomControl;
                        // DateTime values are stored in the database in a format different from what is displayed to the user (i.e., what is stored as the control's content),
                        // so we need to convert it the database format, where the correctly formatted date string will be stored in the database
                        alternateContent = DateTimeHandler.ToStringDatabaseDateTime((DateTime)dateTimeCustomControl.ContentControl.Value);
                        // Maybe don't add it if its invisible?
                        dateTimeCustomControl.Container.Visibility = metadataControlRow.Visible ? Visibility.Visible : Visibility.Collapsed;
                        dateTimeCustomControl.GetContentControl.PreviewKeyDown += DateTime_PreviewKeyDown;
                        break;

                    case Constant.Control.Date_:
                        MetadataDataEntryDate dateControl = new MetadataDataEntryDate(metadataControlRow, styleProvider, metadataControlRow.Tooltip)
                        {
                            // If we can't get a valid default dateTime, use an alternative
                            ContentControl =
                            {
                                Value = DateTime.TryParse(metadataControlRow.DefaultValue, out DateTime tempDate)
                                    ? tempDate
                                    : Constant.ControlDefault.DateTimeCustomDefaultValue
                            }
                        };
                        MetadataDataEntryPanel.ConfigureFormatForDate(dateControl.ContentControl);
                        dateControl.SetContentAndTooltip(DateTimeHandler.ToStringDatabaseDateTime((DateTime)dateControl.ContentControl.Value));
                        controlToAdd = dateControl;
                        // DateTime values are stored in the database in a format different from what is displayed to the user (i.e., what is stored as the control's content),
                        // so we need to convert it the database format, where the correctly formatted date string will be stored in the database
                        alternateContent = DateTimeHandler.ToStringDatabaseDate((DateTime)dateControl.ContentControl.Value);

                        // Maybe don't add it if its invisible?
                        dateControl.Container.Visibility = metadataControlRow.Visible ? Visibility.Visible : Visibility.Collapsed;
                        dateControl.GetContentControl.PreviewKeyDown += DateTime_PreviewKeyDown;
                        break;

                    case Constant.Control.Time_:
                        MetadataDataEntryTime timeControl = new MetadataDataEntryTime(metadataControlRow, styleProvider, metadataControlRow.Tooltip)
                        {
                            // If we can't get a valid default dateTime, use an alternative
                            ContentControl =
                            {
                                Value = DateTime.TryParse(metadataControlRow.DefaultValue, out DateTime tempTime)
                                    ? tempTime
                                    : Constant.ControlDefault.DateTimeCustomDefaultValue
                            }
                        };
                        MetadataDataEntryPanel.ConfigureFormatForTime(timeControl.ContentControl);
                        timeControl.SetContentAndTooltip(DateTimeHandler.ToStringDatabaseDateTime((DateTime)timeControl.ContentControl.Value));
                        controlToAdd = timeControl;
                        // Maybe don't add it if its invisible?
                        timeControl.Container.Visibility = metadataControlRow.Visible ? Visibility.Visible : Visibility.Collapsed;
                        timeControl.GetContentControl.PreviewKeyDown += Time_PreviewKeyDown;
                        break;

                    // Flag
                    case Constant.Control.Flag:
                        MetadataDataEntryFlag flagControl = new MetadataDataEntryFlag(metadataControlRow, styleProvider, metadataControlRow.Tooltip);
                        flagControl.SetContentAndTooltip(metadataControlRow.DefaultValue);
                        controlToAdd = flagControl;
                        // Maybe don't add it if its invisible?
                        flagControl.Container.Visibility = metadataControlRow.Visible ? Visibility.Visible : Visibility.Collapsed;
                        flagControl.GetContentControl.PreviewKeyDown += Flag_PreviewKeyDown;
                        break;
                    default:
                        // TODO: Currently, this ignores unknown controls (but alternate: make it a note, with a warning? Could have unintended consequences
                        TracePrint.PrintMessage($"Unhandled control type {metadataControlRow.Type} in MetadataDataEntryPanel");
                        continue;
                }

                if (null == controlToAdd)
                {
                    // This shouldn't happen
                    continue;
                }
                controlToAdd.ParentPanel = this;

                // Add the control to the Control panel
                this.ControlsPanel.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Auto) });
                Grid.SetRow(controlToAdd.Container, row++);
                this.ControlsPanel.Children.Add(controlToAdd.Container);

                // Add the control to the lookup dictionary so we can find it quickly via its data label
                this.LookupControlByItsDataLabel.Add(controlToAdd.DataLabel, controlToAdd);
                this.TabControlOrderList.Add(controlToAdd);
                // Track the datalabel and its contents so we can add it as a row if needed
                dataLabelsAndValues.Add(controlToAdd.DataLabel, alternateContent ?? controlToAdd.Content);
            }

            // Format each control for displaying one per line: label, control, then a description derived from the tooltip. To do so:
            MetadataDataEntryPanel.FormatControlsEachOnASingleLine(this.ControlsPanel.Children);

            // Its possible that no Metadata table structure entry exists for this filePath
            // So we need to test for that, and if its missing add it to both.
            MetadataRow metadataRow = this.FileDatabase.MetadataTablesGetRow(this.Level, this.SubPath);
            if (metadataRow == null)
            {
                this.FileDatabase.MetadataTablesAndDatabaseUpsertRow(this.Level, this.SubPath, dataLabelsAndValues);
            }

            // Now activate the control with its callback
            GlobalReferences.MainWindow.MetadataDataHandler.SetDataEntryCallbacks(this.LookupControlByItsDataLabel);
        }

        #endregion

        #region SetRelativePathToCurrentFolder

        // Invoked via public Set to property RelativePathToCurrentFolder
        // If the current path differs from the last, update the UI to 
        // either allow the user to initialize the metadata controls
        // or to fill in the controls if they are already initialized.
        // It also warns the user if the image path does not conform with expectations,
        // or disallows initialization if the image path does not have a subfolder at the current level
        private void SetRelativePathToCurrentFolder(string imageFolderPath)
        {
            // set the relativePath to the level's portion of the passed in relativePath
            this.SubPath = MetadataDataEntryPanel.GetSubPathByLevel(this.Level, imageFolderPath);

            // Abort if we are in the same relativePath as the previous one, as nothing needs to be reset
            if (imageFolderPath == this.relativePathToCurrentFolder) return;

            // Determine if the image is at the expected level and set the status accordingly.


            // Determine if the image is at the expected level and set the status accordingly.
            // THis is important, as it not only warns the user when their images are not in the expected location,
            // but disallows them from entering data if there is not actual sub-folder in the image path corresponding to the current level
            int levelsInPath = imageFolderPath == null
                ? 0 : string.Empty == imageFolderPath
                    ? 1 : imageFolderPath.Split(Path.DirectorySeparatorChar).Length + 1;
            ImageLevelLocationStatusEnum imageLevelLocationStatus = ImageLevelLocationStatusEnum.Okay;
            if (levelsInPath == this.ExpectedImageLevel)
            {
                imageLevelLocationStatus = ImageLevelLocationStatusEnum.Okay;
            }
            else if (levelsInPath < this.ExpectedImageLevel)
            {
                // If the current level does not exist in the path, then set the status accordingly.
                imageLevelLocationStatus = levelsInPath >= this.Level
                ? ImageLevelLocationStatusEnum.LocatedBeforeExpectedLeafLevel
                : ImageLevelLocationStatusEnum.LevelDoesNotExistInImagePath;
            }
            else if (levelsInPath > this.ExpectedImageLevel)
            {
                imageLevelLocationStatus = ImageLevelLocationStatusEnum.LocatedAfterExpectedLeafLevel;
            }

            // User Interface stuff: Set the 'Folder' field to the current subpath
            Run run = this.SubPath == null || imageLevelLocationStatus == ImageLevelLocationStatusEnum.LevelDoesNotExistInImagePath
                ? new Run
                {
                    Foreground = Brushes.Crimson,
                    FontStyle = FontStyles.Italic,
                    Text = "No such sub-folder"
                }
                : string.IsNullOrWhiteSpace(this.SubPath)
                    ? new Run { Text = "[Root folder]" }
                    : new Run { Text = this.SubPath };
            MetadataDataEntryPanel.TextBlockSetContents(TextBlockRelativePathToCurrentImage, run);
            TextBlockRelativePathToCurrentImage.ToolTip = TextBlockRelativePathToCurrentImage.Text;

            // Update the stored relative path.
            relativePathToCurrentFolder = imageFolderPath;

            // If no controls are visible and this level should show data, then initialize it to display the controls
            if (ControlsPanel.Children.Count == 0 && this.FileDatabase.MetadataTablesIsLevelAndRelativePathPresent(this.Level, this.SubPath))
            {
                this.InitializePanelWithControls(this.Level, this.FileDatabase.MetadataControlsByLevel[this.Level]);
            }
            this.TrySyncControlRowFromMetadata();
            this.SetPanelAppearance(imageLevelLocationStatus);
        }
        #endregion

        #region Callbacks: PreviewKeyDown to catch returns, enters, escapes, tabs, etc.

        // Note - commit contents when user presses return, enter, or escape. Tab is handled elsewhere.
        private void Note_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (sender is TextBox textBox && (e.Key == Key.Return || e.Key == Key.Enter || e.Key == Key.Escape || e.Key == Key.Tab))
            {
                if (textBox.Tag is MetadataDataEntryControl control)
                {
                    TryMoveFocusToNextControl(control, e);
                }
            }
        }

        // MultiLine - commit contents when user presses escape. Tab is handled elsewhere.
        private void MultiLine_PreviewKeyDown(object sender, KeyEventArgs e)
        {

            if (sender is MultiLineTextEditor editor && (e.Key == Key.Escape || e.Key == Key.Tab))
            {
                if (editor.Tag is MetadataDataEntryControl control)
                {
                    TryMoveFocusToNextControl(control, e);
                }
            }
        }

        // Alphanumeric: commit contents when user presses return, enter, escape or Tab.
        private void AlphaNumeric_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (sender is TextBox alphaNumberic && (e.Key == Key.Return || e.Key == Key.Enter || e.Key == Key.Escape || e.Key == Key.Tab))
            {
                if (alphaNumberic.Tag is MetadataDataEntryControl control)
                {
                    TryMoveFocusToNextControl(control, e);
                }
            }
        }

        // IntegerAny and IntegerPositive: commit contents when user presses return, enter, escape or Tab.
        private void Integer_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (sender is IntegerUpDown integerUpDown && (e.Key == Key.Return || e.Key == Key.Enter || e.Key == Key.Escape || e.Key == Key.Tab))
            {
                if (integerUpDown.Tag is MetadataDataEntryControl control)
                {
                    TryMoveFocusToNextControl(control, e);
                }
            }
        }

        // DecimalAny and DecimalPositive: commit contents when user presses return, enter, escape or Tab.
        private void Decimal_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (sender is DoubleUpDown doubleUpDown && (e.Key == Key.Return || e.Key == Key.Enter || e.Key == Key.Escape || e.Key == Key.Tab))
            {
                if (doubleUpDown.Tag is MetadataDataEntryControl control)
                {
                    TryMoveFocusToNextControl(control, e);
                }
            }
        }

        // FixedChoice: commit contents when user presses return, enter, escape or Tab.
        private void FixedChoice_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (sender is ComboBox comboBox && (e.Key == Key.Return || e.Key == Key.Enter || e.Key == Key.Escape || e.Key == Key.Tab))
            {
                if (comboBox.Tag is MetadataDataEntryControl control)
                {
                    TryMoveFocusToNextControl(control, e);
                }
            }
        }

        // MultiChoice: commit contents when user presses return, enter, escape or Tab.
        private void MultiChoice_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (sender is CheckComboBox checkComboBox && (e.Key == Key.Return || e.Key == Key.Enter || e.Key == Key.Escape || e.Key == Key.Tab))
            {
                if (checkComboBox.Tag is MetadataDataEntryControl control)
                {
                    TryMoveFocusToNextControl(control, e);
                }
            }
        }

        // DateTime_ and Date_: commit contents when user presses return, enter, escape or Tab.
        private void DateTime_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (sender is DateTimePicker dateTimePicker && (e.Key == Key.Return || e.Key == Key.Enter || e.Key == Key.Escape || e.Key == Key.Tab))
            {
                if (dateTimePicker.Tag is MetadataDataEntryControl control)
                {
                    TryMoveFocusToNextControl(control, e);
                }
            }
        }

        // Date_: commit contents when user presses return, enter, escape or Tab.
        //private void Date_PreviewKeyDown(object sender, KeyEventArgs e)
        //{
        //    if (sender is DateTimePicker dateTimePicker && (e.Key == Key.Return || e.Key == Key.Enter || e.Key == Key.Escape || e.Key == Key.Tab))
        //    {
        //        if (dateTimePicker.Tag is MetadataDataEntryControl control)
        //        {
        //            TryMoveFocusToNextControl(control, e);
        //        }
        //    }
        //}

        // DateTime_: commit contents when user presses return, enter, escape or Tab.
        private void Time_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (sender is TimePicker timePicker && (e.Key == Key.Return || e.Key == Key.Enter || e.Key == Key.Escape || e.Key == Key.Tab))
            {
                if (timePicker.Tag is MetadataDataEntryControl control)
                {
                    TryMoveFocusToNextControl(control, e);
                }
            }
        }

        // Flag: commit contents when user presses return, enter, escape or Tab.
        private void Flag_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (sender is CheckBox checkbox && (e.Key == Key.Return || e.Key == Key.Enter || e.Key == Key.Escape || e.Key == Key.Tab))
            {
                if (checkbox.Tag is MetadataDataEntryControl control)
                {
                    TryMoveFocusToNextControl(control, e);
                }
            }
        }
        #endregion

        #region Set Panel Appearance
        // Sets the appearance of the panel depending upon:
        // - whether a level is present in the MetadataTable
        // - if so, whether the current RelativePathToCurrentFolder is present in the database 
        private void SetPanelAppearance(ImageLevelLocationStatusEnum imageLevelLocationStatus)
        {
            if (false == this.FileDatabase.MetadataTablesIsLevelPresent(this.Level)
                || false == (this.FileDatabase.MetadataControlsByLevel.ContainsKey(this.Level)
                             && this.FileDatabase.MetadataControlsByLevel[this.Level].RowCount > 0))
            {
                // This level table is not present in the database, nor are any controls associated with that level
                // While we show that level as a tab, the user cannot do anything in it.
                this.SetPanelAppearance(false, false, imageLevelLocationStatus);
            }
            else if (this.FileDatabase.MetadataTablesIsLevelAndRelativePathPresent(this.Level, this.SubPath))
            {
                // this level and the current RelativePathToCurrentFolder are present in the database
                this.SetPanelAppearance(true, true, imageLevelLocationStatus);
            }
            else
            {
                // this level is present but the current relative path is not
                this.SetPanelAppearance(true, false, imageLevelLocationStatus);
            }
        }
        private void SetPanelAppearance(bool levelPresent, bool currentFolderPresent, ImageLevelLocationStatusEnum imageLevelLocationStatus)
        {
            // Colors: Light blue color indicates controls are present
            //         VeryLightGrey indiates no data for that level,
            //         Ivory indiates indicates it needs to be initialized,
            SolidColorBrush greenBrush = Constant.Colours.VeryLightBlue;
            Brush brushToUse;
            bool showAsterix = false; // We set this to true when we want to add an asterix after the tab name if the panel is not initialized
            // Change appearance depending upon a level being present, and/or if there are nocontrols associated with it.
            if (levelPresent && currentFolderPresent)
            {
                // The level is  defined in the template, and the data fields are being displayed for this folder

                // AddMetadata button not needed as already initialized
                this.ButtonAddMetadata.Visibility = Visibility.Collapsed;

                this.GridIfControlsAbsent.Visibility = Visibility.Collapsed;
                this.GridIfControlsPresent.Visibility = Visibility.Visible;
                this.MetadataControlsContainer.Visibility = Visibility.Visible;
                brushToUse = greenBrush;
                this.MetadataControlsContainer.Background = brushToUse;
            }
            else if (levelPresent)
            {
                // While the level is present, the current folder is not defined in the template

                // AddMetadata button displayed as long as this is a valid location
                this.ButtonAddMetadata.IsEnabled = imageLevelLocationStatus != ImageLevelLocationStatusEnum.LevelDoesNotExistInImagePath;
                this.ButtonAddMetadata.Visibility = Visibility.Visible;
                this.GridIfControlsAbsent.Visibility = Visibility.Collapsed;
                this.GridIfControlsPresent.Visibility = Visibility.Visible;
                this.MetadataControlsContainer.Visibility = Visibility.Collapsed;
                brushToUse = Colours.PaleWhite;
                showAsterix = true;
            }
            else
            {
                // The level is not defined in the template, so we don't show its controls
                this.GridIfControlsAbsent.Visibility = Visibility.Visible;
                this.GridIfControlsPresent.Visibility = Visibility.Collapsed;
                this.MetadataControlsContainer.Visibility = Visibility.Collapsed;
                brushToUse = Constant.Colours.VeryLightGrey;
                this.ButtonAddMetadata.IsEnabled = false;
                this.ButtonAddMetadata.Visibility = Visibility.Collapsed;
            }

            // Color the rest of the panel accordingly
            this.ParentTab.Background = brushToUse;
            this.Background = brushToUse;
            this.FirstContainer.Background = brushToUse;
            if (this.ParentTab.Header is TextBlock tb)
            {
                tb.Background = brushToUse;

                // Add an asterix after the tab name if its not initialized
                tb.Text = showAsterix
                    ? tb.Text.Trim('*') + "*"
                    : tb.Text.Trim('*');

                // Checks: Does the image location confrom with the expected level?
                if (imageLevelLocationStatus == ImageLevelLocationStatusEnum.LocatedBeforeExpectedLeafLevel)
                {
                    // The image is in a subfolder earlier than the expected one. Display warning.
                    tb.FontStyle = FontStyles.Normal;
                    tb.Foreground = Brushes.Black;
                    DataTableBackedList<MetadataInfoRow> metadataInfo = this.FileDatabase.MetadataInfo;
                    if (metadataInfo == null)
                    {
                        this.TBProblem.Text = "Warning: Image is not in the expected subfolder.";
                    }
                    else
                    {
                        int lastRow = metadataInfo.RowCount;
                        string expectedLevelName = lastRow == 0 ? "the expected " : $"{MetadataUI.CreateTemporaryAliasIfNeeded(lastRow, metadataInfo[lastRow - 1].Alias)}-";
                        this.TBProblem.Text = $"Warning: Images should be located in {expectedLevelName}level subfolders.";
                    }
                }
                else if (imageLevelLocationStatus == ImageLevelLocationStatusEnum.LevelDoesNotExistInImagePath)
                {
                    // The image does not have a subfolder that matches the current level. Display a warning.
                    // Note that the initialize button should have been disabled as well
                    tb.FontStyle = FontStyles.Italic;
                    tb.Foreground = Brushes.Gray;
                    DataTableBackedList<MetadataInfoRow> metadataInfo = this.FileDatabase.MetadataInfo;
                    if (metadataInfo == null)
                    {
                        this.TBProblem.Text = "Warning: Image does not contain this level's folder in its path.";
                    }
                    else
                    {
                        int lastRow = metadataInfo.RowCount;
                        string expectedLevelName = lastRow == 0 ? "this level's " : $"a {MetadataUI.CreateTemporaryAliasIfNeeded(this.Level, metadataInfo[lastRow - 1].Alias)}-level ";
                        this.TBProblem.Text = $"Warning: Image does not contain {expectedLevelName} folder in its path.";
                    }
                }
                else if (imageLevelLocationStatus == ImageLevelLocationStatusEnum.LocatedAfterExpectedLeafLevel)
                {
                    // The image is in a subfolder after than the expected one. Display warning.
                    tb.FontStyle = FontStyles.Normal;
                    tb.Foreground = Brushes.Black;
                    DataTableBackedList<MetadataInfoRow> metadataInfo = this.FileDatabase.MetadataInfo;
                    if (metadataInfo == null)
                    {
                        this.TBProblem.Text = "Warning: Image is not in the expected subfolder.";
                    }
                    else
                    {
                        int lastRow = metadataInfo.RowCount;
                        string expectedLevelName = lastRow == 0 ? "the expected " : $"{MetadataUI.CreateTemporaryAliasIfNeeded(this.Level, metadataInfo[lastRow - 1].Alias)}-";
                        this.TBProblem.Text = $"Warning: Images should be located in {expectedLevelName}level subfolders.";
                    }
                }
                else
                {
                    // The image is in the expected subfolder.
                    tb.FontStyle = FontStyles.Normal;
                    tb.Foreground = Brushes.Black;
                    this.TBProblem.Text = string.Empty;
                }
            }
        }
        #endregion

        #region Button callbacks
        private void AddMetadata_OnClick(object sender, RoutedEventArgs e)
        {
            if (GlobalReferences.MainWindow.DataHandler.FileDatabase.MetadataControlsByLevel.ContainsKey(this.Level))
            {
                // Becomes a noop if there is no level for this control
                this.InitializePanelWithControls(this.Level, GlobalReferences.MainWindow.DataHandler.FileDatabase.MetadataControlsByLevel[this.Level]);
                this.TrySyncControlRowFromMetadata();
                this.SetPanelAppearance(ImageLevelLocationStatusEnum.Okay);
            }
        }

        public bool TrySyncControlRowFromMetadata()
        {

            if (false == this.FileDatabase.MetadataTablesIsLevelPresent(this.Level))
            {
                // The level isn't present
                return false;
            }

            MetadataRow metadataRow = this.FileDatabase.MetadataTablesGetRow(this.Level, this.SubPath);
            if (metadataRow == null)
            {
                // the row isn't present
                return false;
            }

            foreach (string dataLabel in metadataRow.DataLabels)
            {
                if (dataLabel == Constant.DatabaseColumn.FolderDataPath)
                {
                    // We don't want to update the MetadataFolder, and its not in the lookup control anyways.
                    continue;
                }
                if (LookupControlByItsDataLabel.TryGetValue(dataLabel, out var control))
                {
                    if (control is MetadataDataEntryDateTimeCustom dateTimeCustom && DateTime.TryParse(metadataRow[dataLabel], out DateTime valueAsDateTime))
                    {
                        dateTimeCustom.SetContentAndTooltip(valueAsDateTime);
                    }
                    else if (control is MetadataDataEntryDate date && DateTime.TryParse(metadataRow[dataLabel], out DateTime valueAsDate))
                    {
                        date.SetContentAndTooltip(valueAsDate);
                    }
                    else
                    {
                        control.SetContentAndTooltip(metadataRow[dataLabel]);
                    }
                }
            }
            return true;
        }
        #endregion

        #region Static FormatControlsEachOnASingleLine
        // Format each control for displaying one per line: label, control, then a description derived from the tooltip. To do so:
        // - right adjusts the labels with each having the same width, so we need to find the maximum width across labels
        // - then add the control, each with the same width, so we need to find the maximum width across controls.
        // - decrease the vertical spacing between controls
        private static void FormatControlsEachOnASingleLine(UIElementCollection childrenUIElementCollection)
        {
            // Format each control for displaying one per line: label, control, then a description derived from the tooltip. To do so:
            // - right adjusts the labels with each having the same width, so we need to find the maximum width across labels
            // - then add the control, each with the same width, so we need to find the maximum width across controls.
            // - decrease the vertical spacing between controls

            // Determine the maximum column widths
            double maxColumn1Width = 0;
            double maxColumn2Width = 150; // The minimum width of column2 is set here.
            foreach (StackPanel child in childrenUIElementCollection)
            {
                MetadataDataEntryControl control = (MetadataDataEntryControl)child.Tag;
                // If for some reason we don't get the correct label, try this form instead (in both invocations of Label)
                // Label label = VisualChildren.GetVisualChild<Label>(child, "ControlLabel");
                Label label = VisualChildren.GetVisualChild<Label>(child);
                label.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                maxColumn1Width = Math.Max(label.DesiredSize.Width, maxColumn1Width);

                Control thisControl = (Control)control.GetContentControl;
                thisControl.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                maxColumn2Width = Math.Max(thisControl.DesiredSize.Width, maxColumn2Width);
            }

            // Adjust the format of the controls
            foreach (StackPanel child in childrenUIElementCollection)
            {
                MetadataDataEntryControl control = (MetadataDataEntryControl)child.Tag;

                // Decrease the vertical spacing between controls
                control.Container.Margin = new Thickness(0, -3, 0, -3);

                // Control's label format: 
                Label label = VisualChildren.GetVisualChild<Label>(child);
                // Adjust the label to that column's maximum width
                if (label != null)
                {
                    label.Width = maxColumn1Width;
                }

                // Control's control format: 
                UIElementCollection children = child.Children;
                if (children.Count >= 2)
                {
                    Control thisControl = (Control)child.Children[1];

                    // Adjust each control to the same width
                    thisControl.Width = maxColumn2Width;

                    // Tooltip: Add an ellipsis if there is more than one line
                    string toolTip = control.Tooltip ?? "No description is available for this field";
                    string[] lines = toolTip.Split('\r', '\n');
                    string firstLine = lines.Length == 1
                        ? lines[0]
                        : lines[0] + "\u2026";

                    // Now create and the tooltip, which can be multiline.
                    TextBlock description = new TextBlock
                    {
                        Height = 16,
                        Padding = new Thickness(0, 0, 0, 0),
                        TextTrimming = TextTrimming.CharacterEllipsis,
                        TextWrapping = TextWrapping.NoWrap,
                        Text = firstLine,
                        Margin = new Thickness(10, 0, 10, 0),
                        VerticalAlignment = VerticalAlignment.Center,
                        ToolTip = new ToolTip
                        {
                            // So the tooltip width doesn't go crazy when we have long sentences
                            Content = new TextBlock
                            {
                                TextWrapping = TextWrapping.Wrap,
                                Text = control.Tooltip,
                            }
                        },
                    };
                    // The tooltip display should be instantaneous
                    description.SetValue(ToolTipService.InitialShowDelayProperty, 0);
                    Grid.SetColumn(description, 2);
                    children.Add(description);
                }
            }
        }
        #endregion

        #region Static utilities
        private static string GetSubPathByLevel(int level, string relativePathToCurrentImage)
        {
            // Note that level is 1-based while pathParts is 0-based
            if (level == 1)
            {
                // First level is always an empty string
                return string.Empty;
            }
            List<string> pathParts = FilesFolders.SplitAsCascadingRelativePath(relativePathToCurrentImage);
            return (level - 2 < pathParts.Count)
                ? pathParts[level - 2]
                : null;
        }

        // Move the focus to the next or previous control in this panel
        private void TryMoveFocusToNextControl(MetadataDataEntryControl inputElement, KeyEventArgs e)
        {
            int index = this.TabControlOrderList.IndexOf(inputElement);
            if (index >= 0)
            {
                // Find the next or previous index (depending on whether a shift is presentits a tab or shift tab
                int nextIndex;
                if (Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift))
                {
                    nextIndex = index == 0
                        ? this.TabControlOrderList.Count - 1
                        : index - 1;
                }
                else
                {
                    nextIndex = index < this.TabControlOrderList.Count - 1
                        ? index + 1
                        : 0;
                }
                Keyboard.Focus(this.TabControlOrderList[nextIndex].GetContentControl);
                e.Handled = true;
            }
        }
        private static void TextBlockSetContents(TextBlock tb, Run run)
        {
            tb.Inlines.Clear();
            tb.Inlines.Add(run);
        }
        #endregion

        #region Static Control Configuration

        public static void ConfigureFormatForDateTimeCustom(DateTimePicker dateTimePicker)
        {
            // Check the arguments for null 
            ThrowIf.IsNullArgument(dateTimePicker, nameof(dateTimePicker));

            dateTimePicker.AutoCloseCalendar = true;
            dateTimePicker.Format = DateTimeFormat.Custom;
            dateTimePicker.FormatString = Constant.Time.DateTimeDisplayFormat;
            dateTimePicker.TimeFormat = DateTimeFormat.Custom;
            dateTimePicker.TimeFormatString = Constant.Time.TimeFormat;
            dateTimePicker.CultureInfo = System.Globalization.CultureInfo.CreateSpecificCulture("en-US");
        }

        public static void ConfigureFormatForDate(DateTimePicker dateTimePicker)
        {
            ThrowIf.IsNullArgument(dateTimePicker, nameof(dateTimePicker));

            dateTimePicker.AutoCloseCalendar = true; dateTimePicker.AutoCloseCalendar = true;
            dateTimePicker.CultureInfo = System.Globalization.CultureInfo.CreateSpecificCulture("en-US");
            dateTimePicker.Format = DateTimeFormat.Custom;
            dateTimePicker.FormatString = Constant.Time.DateDisplayFormat;
            dateTimePicker.TimePickerVisibility = Visibility.Collapsed;
        }

        public static void ConfigureFormatForTime(TimePicker timePicker)
        {
            ThrowIf.IsNullArgument(timePicker, nameof(timePicker));

            timePicker.CultureInfo = System.Globalization.CultureInfo.CreateSpecificCulture("en-US");
            timePicker.Format = DateTimeFormat.Custom;
            timePicker.FormatString = Constant.Time.TimeFormat;
            timePicker.TimeInterval = TimeSpan.FromMinutes(15);
            timePicker.StartTime = TimeSpan.FromHours(9);
            timePicker.MaxDropDownHeight = 250;
        }
        #endregion
    }
}
