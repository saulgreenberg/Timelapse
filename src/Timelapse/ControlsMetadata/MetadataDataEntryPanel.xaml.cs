using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
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
using Timelapse.Dialog;
using Timelapse.Enums;
using Timelapse.Standards;
using Timelapse.Util;
using TimelapseWpf.Toolkit;
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
        private static FileDatabase FileDatabase => GlobalReferences.MainWindow.DataHandler.FileDatabase;
        private readonly TabItem ParentTab;
        private readonly int ExpectedImageLevel;
        private string relativePathToCurrentFolder;
        private readonly Dictionary<string, MetadataDataEntryControl> LookupControlByItsDataLabel = [];
        private readonly List<MetadataDataEntryControl> TabControlOrderList = [];
        #endregion

        #region Constructor
        public MetadataDataEntryPanel(int level, TabItem parentTab, string tabHeader)
        {
            InitializeComponent();
            Level = level;
            ParentTab = parentTab;
            DataTableBackedList<MetadataInfoRow> metadataInfo = FileDatabase.MetadataInfo;
            ExpectedImageLevel = metadataInfo == null ? 1 : metadataInfo[metadataInfo.RowCount - 1].Level;
            this.ButtonPreviousFolder.ToolTip = $"Go to the previous {tabHeader} folder{Environment.NewLine}• the displayed image will change to an image in that folder";
            this.ButtonNextFolder.ToolTip = $"Go to the next {tabHeader} folder{Environment.NewLine}• the displayed image will change to an image in that folder";
        }
        #endregion


        #region Public: Initialize the panel with the controls defined in the metadataTableControlRows
        public void InitializePanelWithControls(int level, DataTableBackedList<MetadataControlRow> metadataTableControlRows)
        {
            // Always clear the children 
            // This clears things if this is invoked after all rows have been removed and prepares things if things have been changed or added
            ControlsPanel.Children.Clear();
            LookupControlByItsDataLabel.Clear();
            TabControlOrderList.Clear();
            // Return if no data for that level exists. 
            // e.g., when a new level is just being created, or when a level has no controls or no data is associated with it,
            if (null == metadataTableControlRows)
            {
                return;
            }

            // Create a dictionary that holds the datalabels and its values, in case we have to 
            // add it to this level's data structure and/or data table
            Dictionary<string, string> dataLabelsAndValues = new()
            {
                { DatabaseColumn.FolderDataPath, SubPath }
            };

            DataEntryControls styleProvider = new();
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
                        Dictionary<string, string> noteAutocompletions = string.IsNullOrWhiteSpace(metadataControlRow.DefaultValue)
                        ? []
                        : new Dictionary<string, string>
                            {
                                { metadataControlRow.DefaultValue, null },
                            };
                        MetadataDataEntryNote noteControl = new(metadataControlRow, noteAutocompletions, styleProvider, metadataControlRow.Tooltip)
                        {
                            ContentReadOnly = false
                        };
                        noteControl.SetContentAndTooltip(metadataControlRow.DefaultValue);
                        controlToAdd = noteControl;
                        // Maybe don't add it if its invisible?
                        noteControl.Container.Visibility = metadataControlRow.Visible ? Visibility.Visible : Visibility.Collapsed;
                        noteControl.GetContentControl.PreviewKeyDown += Note_PreviewKeyDown;
                        break;

                    // AlphaNumeric
                    case Constant.Control.AlphaNumeric:
                        Dictionary<string, string> alphaNumericAutocompletions = string.IsNullOrWhiteSpace(metadataControlRow.DefaultValue)
                            ? []
                            : new Dictionary<string, string>
                            {
                                { metadataControlRow.DefaultValue, null },
                            };
                        MetadataDataEntryAlphaNumeric alphaNumericControl = new(metadataControlRow, alphaNumericAutocompletions, styleProvider, metadataControlRow.Tooltip)
                        {
                            ContentReadOnly = false
                        };
                        alphaNumericControl.SetContentAndTooltip(metadataControlRow.DefaultValue);
                        controlToAdd = alphaNumericControl;
                        // Maybe don't add it if its invisible?
                        alphaNumericControl.Container.Visibility = metadataControlRow.Visible ? Visibility.Visible : Visibility.Collapsed;
                        alphaNumericControl.GetContentControl.PreviewKeyDown += AlphaNumeric_PreviewKeyDown;
                        break;

                    // Multiline
                    case Constant.Control.MultiLine:
                        MetadataDataEntryMultiLine multiLineControl = new(metadataControlRow, styleProvider, metadataControlRow.Tooltip)
                        {
                            ContentReadOnly = false
                        };
                        multiLineControl.SetContentAndTooltip(metadataControlRow.DefaultValue);
                        controlToAdd = multiLineControl;
                        // Maybe don't add it if its invisible?
                        multiLineControl.Container.Visibility = metadataControlRow.Visible ? Visibility.Visible : Visibility.Collapsed;
                        multiLineControl.GetContentControl.PreviewKeyDown += MultiLine_PreviewKeyDown;
                        break;


                    // IntegerAny
                    case Constant.Control.IntegerAny:
                        MetadataDataEntryIntegerAny integerAnyControl = new(metadataControlRow, styleProvider, metadataControlRow.Tooltip)
                        {
                            ContentReadOnly = false
                        };
                        integerAnyControl.SetContentAndTooltip(metadataControlRow.DefaultValue);
                        controlToAdd = integerAnyControl;
                        // Maybe don't add it if its invisible?
                        integerAnyControl.Container.Visibility = metadataControlRow.Visible ? Visibility.Visible : Visibility.Collapsed;
                        integerAnyControl.GetContentControl.PreviewKeyDown += Integer_PreviewKeyDown;
                        //alphaNumericControl.GetContentControl.PreviewTextInput += Alphanumeric_PreviewTextInput;
                        break;

                    // IntegerPostive
                    case Constant.Control.IntegerPositive:
                        MetadataDataEntryIntegerPositive integerPostiveControl = new(metadataControlRow, styleProvider, metadataControlRow.Tooltip)
                        {
                            ContentReadOnly = false
                        };
                        integerPostiveControl.SetContentAndTooltip(metadataControlRow.DefaultValue);
                        controlToAdd = integerPostiveControl;
                        // Maybe don't add it if its invisible?
                        integerPostiveControl.Container.Visibility = metadataControlRow.Visible ? Visibility.Visible : Visibility.Collapsed;
                        integerPostiveControl.GetContentControl.PreviewKeyDown += Integer_PreviewKeyDown;
                        break;

                    // DecimalAny
                    case Constant.Control.DecimalAny:
                        MetadataDataEntryDecimalAny decimalAnyControl = new(metadataControlRow, styleProvider, metadataControlRow.Tooltip)
                        {
                            ContentReadOnly = false
                        };
                        decimalAnyControl.SetContentAndTooltip(metadataControlRow.DefaultValue);
                        controlToAdd = decimalAnyControl;
                        // Maybe don't add it if its invisible?
                        decimalAnyControl.Container.Visibility = metadataControlRow.Visible ? Visibility.Visible : Visibility.Collapsed;
                        decimalAnyControl.GetContentControl.PreviewKeyDown += Decimal_PreviewKeyDown;
                        break;

                    // DecimalPostive
                    case Constant.Control.DecimalPositive:
                        MetadataDataEntryDecimalPositive decimalPostiveControl = new(metadataControlRow, styleProvider, metadataControlRow.Tooltip)
                        {
                            ContentReadOnly = false
                        };
                        decimalPostiveControl.SetContentAndTooltip(metadataControlRow.DefaultValue);
                        controlToAdd = decimalPostiveControl;
                        // Maybe don't add it if its invisible?
                        decimalPostiveControl.Container.Visibility = metadataControlRow.Visible ? Visibility.Visible : Visibility.Collapsed;
                        decimalPostiveControl.GetContentControl.PreviewKeyDown += Decimal_PreviewKeyDown;
                        break;

                    case Constant.Control.FixedChoice:
                        MetadataDataEntryFixedChoice fixedChoiceControl = new(metadataControlRow, styleProvider, metadataControlRow.Tooltip)
                        {
                            ContentReadOnly = false
                        };
                        fixedChoiceControl.SetContentAndTooltip(metadataControlRow.DefaultValue);
                        controlToAdd = fixedChoiceControl;
                        // Maybe don't add it if its invisible?
                        fixedChoiceControl.Container.Visibility = metadataControlRow.Visible ? Visibility.Visible : Visibility.Collapsed;
                        fixedChoiceControl.GetContentControl.PreviewKeyDown += FixedChoice_PreviewKeyDown;
                        break;

                    case Constant.Control.MultiChoice:
                        MetadataDataEntryMultiChoice multiChoiceControl = new(metadataControlRow, styleProvider, metadataControlRow.Tooltip)
                        {
                            ContentReadOnly = false
                        };
                        multiChoiceControl.SetContentAndTooltip(metadataControlRow.DefaultValue);
                        controlToAdd = multiChoiceControl;

                        // Maybe don't add it if its invisible?
                        multiChoiceControl.Container.Visibility = metadataControlRow.Visible ? Visibility.Visible : Visibility.Collapsed;
                        multiChoiceControl.GetContentControl.PreviewKeyDown += MultiChoice_PreviewKeyDown;
                        break;

                    // DateTime_
                    case Constant.Control.DateTime_:
                        MetadataDataEntryDateTimeCustom dateTimeCustomControl = new(metadataControlRow, styleProvider, metadataControlRow.Tooltip)
                        {
                            // If we can't get a valid default dateTime, use an alternative
                            ContentControl =
                            {
                                    Value = DateTime.TryParse(metadataControlRow.DefaultValue, out DateTime tempDateTime)
                                        ? tempDateTime
                                        : ControlDefault.DateTimeCustomDefaultValue
                            },
                            ContentReadOnly = false
                        };
                        ConfigureFormatForDateTimeCustom(dateTimeCustomControl.ContentControl);
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
                        MetadataDataEntryDate dateControl = new(metadataControlRow, styleProvider, metadataControlRow.Tooltip)
                        {
                            // If we can't get a valid default dateTime, use an alternative
                            ContentControl =
                            {
                                Value = DateTime.TryParse(metadataControlRow.DefaultValue, out DateTime tempDate)
                                    ? tempDate
                                    : ControlDefault.DateTimeCustomDefaultValue
                            },
                            ContentReadOnly = false
                        };
                        ConfigureFormatForDate(dateControl.ContentControl);
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
                        MetadataDataEntryTime timeControl = new(metadataControlRow, styleProvider, metadataControlRow.Tooltip)
                        {
                            // If we can't get a valid default dateTime, use an alternative
                            ContentControl =
                            {
                                Value = DateTime.TryParse(metadataControlRow.DefaultValue, out DateTime tempTime)
                                    ? tempTime
                                    : ControlDefault.DateTimeCustomDefaultValue
                            },
                            ContentReadOnly = false
                        };
                        ConfigureFormatForTime(timeControl.ContentControl);
                        timeControl.SetContentAndTooltip(DateTimeHandler.ToStringDatabaseDateTime((DateTime)timeControl.ContentControl.Value));
                        controlToAdd = timeControl;
                        // Maybe don't add it if its invisible?
                        timeControl.Container.Visibility = metadataControlRow.Visible ? Visibility.Visible : Visibility.Collapsed;
                        timeControl.GetContentControl.PreviewKeyDown += Time_PreviewKeyDown;
                        break;

                    // Flag
                    case Constant.Control.Flag:
                        MetadataDataEntryFlag flagControl = new(metadataControlRow, styleProvider, metadataControlRow.Tooltip)
                        {
                            ContentReadOnly = false
                        };
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
                ControlsPanel.RowDefinitions.Add(new() { Height = new(1, GridUnitType.Auto) });
                Grid.SetRow(controlToAdd.Container, row++);
                ControlsPanel.Children.Add(controlToAdd.Container);

                // Add the control to the lookup dictionary so we can find it quickly via its data label
                LookupControlByItsDataLabel.Add(controlToAdd.DataLabel, controlToAdd);
                TabControlOrderList.Add(controlToAdd);
                // Track the datalabel and its contents so we can add it as a row if needed
                dataLabelsAndValues.Add(controlToAdd.DataLabel, alternateContent ?? controlToAdd.Content);

            }

            RenderFieldsIfCamtrapDPStandards();

            // Format each control for displaying one per line: label, control, then a description derived from the tooltip. To do so:
            FormatControlsEachOnASingleLine(ControlsPanel.Children);

            // Its possible that no Metadata table structure entry exists for this filePath
            // So we need to test for that, and if its missing add it to both.
            MetadataRow metadataRow = FileDatabase.MetadataTablesGetRow(Level, SubPath);
            if (metadataRow == null)
            {
                FileDatabase.MetadataTablesAndDatabaseUpsertRow(Level, SubPath, dataLabelsAndValues);
            }

            // Now activate the control with its callback
            GlobalReferences.MainWindow.MetadataDataHandler.SetDataEntryCallbacks(LookupControlByItsDataLabel);
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
            SubPath = GetSubPathByLevel(Level, imageFolderPath);

            // Abort if we are in the same relativePath as the previous one, as nothing needs to be reset
            if (imageFolderPath == relativePathToCurrentFolder) return;

            // Determine if the image is at the expected level and set the status accordingly.


            // Determine if the image is at the expected level and set the status accordingly.
            // THis is important, as it not only warns the user when their images are not in the expected location,
            // but disallows them from entering data if there is not actual sub-folder in the image path corresponding to the current level
            int levelsInPath = imageFolderPath == null
                ? 0 : string.Empty == imageFolderPath
                ? 1 : imageFolderPath.Split(Path.DirectorySeparatorChar).Length + 1;
            ImageLevelLocationStatusEnum imageLevelLocationStatus = ImageLevelLocationStatusEnum.Okay;
            if (levelsInPath == ExpectedImageLevel)
            {
                imageLevelLocationStatus = ImageLevelLocationStatusEnum.Okay;
            }
            else if (levelsInPath < ExpectedImageLevel)
            {
                // If the current level does not exist in the path, then set the status accordingly.
                imageLevelLocationStatus = levelsInPath >= Level
                ? ImageLevelLocationStatusEnum.LocatedBeforeExpectedLeafLevel
                : ImageLevelLocationStatusEnum.LevelDoesNotExistInImagePath;
            }
            else if (levelsInPath > ExpectedImageLevel)
            {
                imageLevelLocationStatus = ImageLevelLocationStatusEnum.LocatedAfterExpectedLeafLevel;
            }

            // User Interface stuff: Set the 'Folder' field to the current subpath
            Run run = SubPath == null || imageLevelLocationStatus == ImageLevelLocationStatusEnum.LevelDoesNotExistInImagePath
                ? new()
                {
                    Foreground = Brushes.Crimson,
                    FontStyle = FontStyles.Italic,
                    Text = "No such sub-folder"
                }
                : string.IsNullOrWhiteSpace(SubPath)
                    ? new() { Text = "[Root folder]" }
                    : new Run { Text = SubPath };
            TextBlockSetContents(TextBlockRelativePathToCurrentImage, run);
            TextBlockRelativePathToCurrentImage.ToolTip = TextBlockRelativePathToCurrentImage.Text;

            // Update the stored relative path.
            relativePathToCurrentFolder = imageFolderPath;

            // If no controls are visible and this level should show data, then initialize it to display the controls
            if (ControlsPanel.Children.Count == 0 && FileDatabase.MetadataTablesIsLevelAndRelativePathPresent(Level, SubPath))
            {
                InitializePanelWithControls(Level, FileDatabase.MetadataControlsByLevel[Level]);
            }
            TrySyncControlRowFromMetadata();
            SetPanelAppearance(imageLevelLocationStatus);
        }
        #endregion

        #region Callbacks: PreviewKeyDown to catch returns, enters, escapes, tabs, etc.

        // Note - commit contents when user presses return, enter, or escape. Tab is handled elsewhere.
        private void Note_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (sender is TextBox textBox && (e.Key is Key.Return or Key.Enter or Key.Escape or Key.Tab))
            {
                if (textBox.Tag is MetadataDataEntryControl control)
                {
                    TryMoveFocusToNextControl(control, e);
                }
            }
        }

        // MultiLine - commit contents when user presses escape/tab/enter (with popup closed)
        private void MultiLine_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (sender is MultiLineText editor &&
                (e.Key is Key.Escape or Key.Tab ||
                (IsCondition.IsKeyReturnOrEnter(e.Key) &&
                 false == editor.EditorPopup?.IsOpen)))
            {
                if (editor.Tag is MetadataDataEntryControl control)
                {
                    if (editor.EditorPopup is { IsOpen: true })
                    {
                        editor.Commit(); 
                    }
                    TryMoveFocusToNextControl(control, e);
                }
            }
        }



        // Alphanumeric: commit contents when user presses return, enter, escape or Tab.
        private void AlphaNumeric_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (sender is TextBox alphaNumberic &&
                e.Key is Key.Return or Key.Enter or Key.Escape or Key.Tab)
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
            if (sender is IntegerUpDown integerUpDown && e.Key is Key.Return or Key.Enter or Key.Escape or Key.Tab)
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
            if (sender is DoubleUpDown doubleUpDown && e.Key is Key.Return or Key.Enter or Key.Escape or Key.Tab)
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
            if (sender is ComboBox comboBox && e.Key is Key.Return or Key.Enter or Key.Escape or Key.Tab)
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
            if (sender is WatermarkCheckComboBox checkComboBox && e.Key is Key.Return or Key.Enter or Key.Escape or Key.Tab)
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
            if (sender is WatermarkDateTimePicker dateTimePicker && e.Key is Key.Return or Key.Enter or Key.Escape or Key.Tab)
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
        //    if (sender is WatermarkDateTimePicker dateTimePicker && (e.Key == Key.Return || e.Key == Key.Enter || e.Key == Key.Escape || e.Key == Key.Tab))
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
            if (sender is WatermarkTimePicker timePicker && e.Key is Key.Return or Key.Enter or Key.Escape or Key.Tab)
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
            if (sender is CheckBox checkbox && e.Key is Key.Return or Key.Enter or Key.Escape or Key.Tab)
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
            if (false == FileDatabase.MetadataTablesIsLevelPresent(Level)
                || false == (FileDatabase.MetadataControlsByLevel.ContainsKey(Level)
                             && FileDatabase.MetadataControlsByLevel[Level].RowCount > 0))
            {
                // This level table is not present in the database, nor are any controls associated with that level
                // While we show that level as a tab, the user cannot do anything in it.
                SetPanelAppearance(false, false, imageLevelLocationStatus);
            }
            else if (FileDatabase.MetadataTablesIsLevelAndRelativePathPresent(Level, SubPath))
            {
                // this level and the current RelativePathToCurrentFolder are present in the database
                SetPanelAppearance(true, true, imageLevelLocationStatus);
            }
            else
            {
                // this level is present but the current relative path is not
                SetPanelAppearance(true, false, imageLevelLocationStatus);
            }
        }
        private void SetPanelAppearance(bool levelPresent, bool currentFolderPresent, ImageLevelLocationStatusEnum imageLevelLocationStatus)
        {
            // Colors: Light blue color indicates controls are present
            //         VeryLightGrey indiates no data for that level,
            //         Ivory indiates indicates it needs to be initialized,
            SolidColorBrush greenBrush = Colours.VeryLightBlue;
            Brush brushToUse;
            bool showAsterix = false; // We set this to true when we want to add an asterix after the tab name if the panel is not initialized
            // Change appearance depending upon a level being present, and/or if there are nocontrols associated with it.
            if (levelPresent && currentFolderPresent)
            {
                // The level is  defined in the template, and the data fields are being displayed for this folder

                // AddMetadata button not needed as already initialized
                ButtonAddMetadata.Visibility = Visibility.Collapsed;

                GridIfControlsAbsent.Visibility = Visibility.Collapsed;
                GridIfControlsPresent.Visibility = Visibility.Visible;
                MetadataControlsContainer.Visibility = Visibility.Visible;
                brushToUse = greenBrush;
                MetadataControlsContainer.Background = brushToUse;
            }
            else if (levelPresent)
            {
                // While the level is present, the current folder is not defined in the template

                // AddMetadata button displayed as long as this is a valid location
                ButtonAddMetadata.IsEnabled = imageLevelLocationStatus != ImageLevelLocationStatusEnum.LevelDoesNotExistInImagePath;
                ButtonAddMetadata.Visibility = Visibility.Visible;
                GridIfControlsAbsent.Visibility = Visibility.Collapsed;
                GridIfControlsPresent.Visibility = Visibility.Visible;
                MetadataControlsContainer.Visibility = Visibility.Collapsed;
                brushToUse = Colours.PaleWhite;
                showAsterix = true;
            }
            else
            {
                // The level is not defined in the template, so we don't show its controls
                GridIfControlsAbsent.Visibility = Visibility.Visible;
                GridIfControlsPresent.Visibility = Visibility.Collapsed;
                MetadataControlsContainer.Visibility = Visibility.Collapsed;
                brushToUse = Colours.VeryLightGrey;
                ButtonAddMetadata.IsEnabled = false;
                ButtonAddMetadata.Visibility = Visibility.Collapsed;
            }

            // Finally, if we are in view only, make sure that the button is disabled 
            // to disallow initialization
            if (GlobalReferences.TimelapseState.IsViewOnly)
            {
                ButtonAddMetadata.IsEnabled = false;
            }

            // Color the rest of the panel accordingly
            ParentTab.Background = brushToUse;
            Background = brushToUse;
            FirstContainer.Background = brushToUse;
            if (ParentTab.Header is TextBlock tb)
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
                    DataTableBackedList<MetadataInfoRow> metadataInfo = FileDatabase.MetadataInfo;
                    if (metadataInfo == null)
                    {
                        TBProblem.Text = "Warning: Image is not in the expected subfolder.";
                    }
                    else
                    {
                        int lastRow = metadataInfo.RowCount;
                        string expectedLevelName = lastRow == 0 ? "the expected " : $"{MetadataUI.CreateTemporaryAliasIfNeeded(lastRow, metadataInfo[lastRow - 1].Alias)}-";
                        TBProblem.Text = $"Warning: Images should be located in {expectedLevelName}level subfolders.";
                    }
                }
                else if (imageLevelLocationStatus == ImageLevelLocationStatusEnum.LevelDoesNotExistInImagePath)
                {
                    // The image does not have a subfolder that matches the current level. Display a warning.
                    // Note that the initialize button should have been disabled as well
                    tb.FontStyle = FontStyles.Italic;
                    tb.Foreground = Brushes.Gray;
                    DataTableBackedList<MetadataInfoRow> metadataInfo = FileDatabase.MetadataInfo;
                    if (metadataInfo == null)
                    {
                        TBProblem.Text = "Warning: Image does not contain this level's folder in its path.";
                    }
                    else
                    {
                        int lastRow = metadataInfo.RowCount;
                        string expectedLevelName = lastRow == 0 ? "this level's " : $"a {MetadataUI.CreateTemporaryAliasIfNeeded(Level, metadataInfo[lastRow - 1].Alias)}-level ";
                        TBProblem.Text = $"Warning: Image does not contain {expectedLevelName} folder in its path.";
                    }
                }
                else if (imageLevelLocationStatus == ImageLevelLocationStatusEnum.LocatedAfterExpectedLeafLevel)
                {
                    // The image is in a subfolder after than the expected one. Display warning.
                    tb.FontStyle = FontStyles.Normal;
                    tb.Foreground = Brushes.Black;
                    DataTableBackedList<MetadataInfoRow> metadataInfo = FileDatabase.MetadataInfo;
                    if (metadataInfo == null)
                    {
                        TBProblem.Text = "Warning: Image is not in the expected subfolder.";
                    }
                    else
                    {
                        int lastRow = metadataInfo.RowCount;
                        string expectedLevelName = lastRow == 0 ? "the expected " : $"{MetadataUI.CreateTemporaryAliasIfNeeded(Level, metadataInfo[lastRow - 1].Alias)}-";
                        TBProblem.Text = $"Warning: Images should be located in {expectedLevelName}level subfolders.";
                    }
                }
                else
                {
                    // The image is in the expected subfolder.
                    tb.FontStyle = FontStyles.Normal;
                    tb.Foreground = Brushes.Black;
                    TBProblem.Text = string.Empty;
                }
            }

            NavigationButtonsShowHide();
        }
        #endregion

        #region Button callbacks

        private void AddMetadata_OnClick(object sender, RoutedEventArgs e)
        {
            if (GlobalReferences.MainWindow.DataHandler.FileDatabase.MetadataControlsByLevel.TryGetValue(Level, out var value))
            {
                // Becomes a noop if there is no level for this control
                InitializePanelWithControls(Level, value);

                // If we are using the Camtrap standard, autofill some of the fields to match the standards requirements
                AutofillFieldsIfCamtrapDPStandards();

                TrySyncControlRowFromMetadata();
                SetPanelAppearance(ImageLevelLocationStatusEnum.Okay);
            }
        }

        public bool TrySyncControlRowFromMetadata()
        {

            if (false == FileDatabase.MetadataTablesIsLevelPresent(Level))
            {
                // The level isn't present
                return false;
            }

            MetadataRow metadataRow = FileDatabase.MetadataTablesGetRow(Level, SubPath);
            if (metadataRow == null)
            {
                // the row isn't present
                return false;
            }

            foreach (string dataLabel in metadataRow.DataLabels)
            {
                if (dataLabel == DatabaseColumn.FolderDataPath)
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

        #region Folder Navigation Buttons for Next/Previous folder
        private void NavigateFolder_OnClick(object sender, RoutedEventArgs e)
        {
            if (false == sender is Button btn || string.IsNullOrWhiteSpace(this.SubPath))
            {
                // Noop if we are in the root folder
                return;
            }

            // Geta  list of beginnning relative path portions in the currently selected files and the index in that list that matches the subpath.
            List<string> foldersAtThisLevelList = GetFolderListMatchingSubPathLevels(FileDatabase, this.SubPath, out int matchingIndex);
            int count = foldersAtThisLevelList.Count - 2;
            if ((btn.Name == ButtonPreviousFolder.Name && matchingIndex <= 0) || (btn.Name == ButtonNextFolder.Name && matchingIndex > count))
            {
                // index is out of bounds
                return;
            }
            string desiredRootPath = btn.Name == ButtonPreviousFolder.Name ? foldersAtThisLevelList.ElementAt(matchingIndex - 1) : foldersAtThisLevelList.ElementAt(matchingIndex + 1);
            int fileIndex = GlobalReferences.MainWindow.DataHandler.FileDatabase.FindFirstImageWithRootRelativePath(desiredRootPath);
            if (fileIndex != -1)
            {
                // This works regardless if its the previous or next button that is pressed.
                GlobalReferences.MainWindow.FileShow(fileIndex);
            }
        }

        public void NavigationButtonsShowHide()
        {
            if (string.IsNullOrWhiteSpace(this.SubPath))
            {
                // We are viewing the top level folder, so there are no previous/next folders at this level
                ButtonPreviousFolder.IsEnabled = false;
                ButtonNextFolder.IsEnabled = false;
                return;
            }
            List<string> foldersAtThisLevelList = GetFolderListMatchingSubPathLevels(FileDatabase, this.SubPath, out int matchingIndex);
            ButtonPreviousFolder.IsEnabled = matchingIndex != 0;
            ButtonNextFolder.IsEnabled = matchingIndex != foldersAtThisLevelList.Count - 1;
        }
        #endregion

        #region Standard-specific initializations

        // Contributors will be a json string that holds a list of contributor objects.
        // Instead of showing the string, we display a button that will raise a dialog box allowing
        // the user to construct and/or edit a list of contributors
        private void RenderFieldsIfCamtrapDPStandards()
        {
            if (FileDatabase.MetadataTablesIsCamtrapDPStandard())
            {
                // Data package level
                if (Level == 1)
                {
                    if (LookupControlByItsDataLabel.TryGetValue(CamtrapDPConstants.DataPackage.Temporal.Start, out var temporalStartControl))
                    {
                        Button button = new()
                        {
                            Content = "Update",
                            Visibility = Visibility.Visible,
                            Height = 24,
                            Margin = new(10, 0, 0, 0),
                            Padding = new(5, 0, 5, 0),
                            HorizontalContentAlignment = HorizontalAlignment.Left,
                            ToolTip = $"Date derived by searching your images for the one with the earliest date.{Environment.NewLine}If it doesn't change, then its already correct."
                        };
                        button.Click += TemporalStart_Click;
                        temporalStartControl.Container.Children.Insert(2, button);
                    }

                    if (LookupControlByItsDataLabel.TryGetValue(CamtrapDPConstants.DataPackage.Temporal.End, out var temporalEndControl))
                    {
                        Button button = new()
                        {
                            Content = "Update",
                            Visibility = Visibility.Visible,
                            Height = 24,
                            Margin = new(10, 0, 0, 0),
                            Padding = new(5, 0, 5, 0),
                            HorizontalContentAlignment = HorizontalAlignment.Left,
                            ToolTip = $"Date derived by searching your images for the one with the latest date.{Environment.NewLine}If it doesn't change, then its already correct."
                        };
                        button.Click += TemporalEnd_Click;
                        temporalEndControl.Container.Children.Insert(2, button);
                    }

                    if (LookupControlByItsDataLabel.TryGetValue(CamtrapDPConstants.DataPackage.Contributors, out var contributorsControl))
                    {
                        Button button = new()
                        {
                            Content = "Click to edit a list of Contributors",
                            Visibility = Visibility.Visible,
                            Height = 24,
                            Padding = new(15, 0, 15, 0),
                            HorizontalContentAlignment = HorizontalAlignment.Left,
                        };
                        button.Click += Contributors_Click;
                        contributorsControl.GetContentControl.Visibility = Visibility.Collapsed;
                        contributorsControl.Container.Children.Insert(1, button);
                    }

                    if (LookupControlByItsDataLabel.TryGetValue(CamtrapDPConstants.DataPackage.Sources, out var sourcesControl))
                    {
                        Button button = new()
                        {
                            Content = "Click to edit a list of Sources",
                            Visibility = Visibility.Visible,
                            Height = 24,
                            Padding = new(15, 0, 15, 0),
                            HorizontalContentAlignment = HorizontalAlignment.Left,
                        };
                        button.Click += Sources_Click;
                        sourcesControl.GetContentControl.Visibility = Visibility.Collapsed;
                        sourcesControl.Container.Children.Insert(1, button);
                    }

                    if (LookupControlByItsDataLabel.TryGetValue(CamtrapDPConstants.DataPackage.Licenses, out var licensesControl))
                    {
                        Button button = new()
                        {
                            Content = "Click to edit a list of Licenses",
                            Visibility = Visibility.Visible,
                            Height = 24,
                            Padding = new(15, 0, 15, 0),
                            HorizontalContentAlignment = HorizontalAlignment.Left,
                        };
                        button.Click += Licenses_Click;
                        licensesControl.GetContentControl.Visibility = Visibility.Collapsed;
                        licensesControl.Container.Children.Insert(1, button);
                    }

                    if (LookupControlByItsDataLabel.TryGetValue(CamtrapDPConstants.DataPackage.Taxonomic, out var taxonomicControl))
                    {
                        Button button = new()
                        {
                            Content = "Click to edit a list of Taxonomic definitions",
                            Visibility = Visibility.Visible,
                            Height = 24,
                            Padding = new(15, 0, 15, 0),
                            HorizontalContentAlignment = HorizontalAlignment.Left,
                        };
                        button.Click += Taxonomic_Click;
                        taxonomicControl.GetContentControl.Visibility = Visibility.Collapsed;
                        taxonomicControl.Container.Children.Insert(1, button);
                    }

                    if (LookupControlByItsDataLabel.TryGetValue(CamtrapDPConstants.DataPackage.RelatedIdentifiers, out var relatedIdentifiersControl))
                    {
                        Button button = new()
                        {
                            Content = "Click to edit a list of Related identifiers",
                            Visibility = Visibility.Visible,
                            Height = 24,
                            Padding = new(15, 0, 15, 0),
                            HorizontalContentAlignment = HorizontalAlignment.Left,
                        };
                        button.Click += RelatedIdentifiers_Click;
                        relatedIdentifiersControl.GetContentControl.Visibility = Visibility.Collapsed;
                        relatedIdentifiersControl.Container.Children.Insert(1, button);
                    }

                    if (LookupControlByItsDataLabel.TryGetValue(CamtrapDPConstants.DataPackage.References, out var referencesControl))
                    {
                        Button button = new()
                        {
                            Content = "Click to edit a list of References",
                            Visibility = Visibility.Visible,
                            Height = 24,
                            Padding = new(15, 0, 15, 0),
                            HorizontalContentAlignment = HorizontalAlignment.Left,
                        };
                        button.Click += References_Click;
                        referencesControl.GetContentControl.Visibility = Visibility.Collapsed;
                        referencesControl.Container.Children.Insert(1, button);
                    }

                    if (LookupControlByItsDataLabel.TryGetValue(CamtrapDPConstants.DataPackage.Spatial, out var spatialControl))
                    {
                        StackPanel spatialPanel = new() { Orientation = Orientation.Horizontal };
                        Button buttonLatLong = new()
                        {
                            Content = "From lat/long",
                            ToolTip = $"Generates a GeoJson as a bounding box containing all your deployments' latitude/longitude coordinates.{Environment.NewLine}" +
                                      "View this bounding box by opening GeoJson.IO and copying those coordinates into it.",
                            Visibility = Visibility.Visible,
                            Height = 24,
                            Width = Double.NaN,
                            Margin = new(10, 0, 0, 0),
                            Padding = new(5, 0, 5, 0),
                            HorizontalContentAlignment = HorizontalAlignment.Left,
                        };
                        Button buttonGeoJson = new()
                        {
                            Content = "Edit with GeoJson.IO",
                            ToolTip = $"Opens a web browser on http://Geojson.IO{Environment.NewLine}" +
                                      $"Use it to outline the geographic area(s) of your project.{Environment.NewLine}" +
                                      "Then copy/paste the generated geojson into the Timelapse spatial field.",
                            Visibility = Visibility.Visible,
                            Height = 24,
                            Width = Double.NaN,
                            Margin = new(5, 0, 0, 0),
                            Padding = new(5, 0, 5, 0),
                            HorizontalContentAlignment = HorizontalAlignment.Left,
                        };
                        spatialPanel.Children.Add(buttonLatLong);
                        spatialPanel.Children.Add(buttonGeoJson);

                        buttonGeoJson.Click += SpatialGeoJson_Click;
                        buttonLatLong.Click += SpatialLatLong_Click;
                        spatialControl.GetContentControl.Visibility = Visibility.Visible;
                        spatialControl.Container.Children.Insert(2, spatialPanel);
                    }
                }

                else if (Level == 2)
                {
                    if (LookupControlByItsDataLabel.TryGetValue(CamtrapDPConstants.Deployment.DeploymentStart, out var deploymentStartControl))
                    {
                        Button button = new()
                        {
                            Content = "Update",
                            Visibility = Visibility.Visible,
                            Height = 24,
                            Margin = new(10, 0, 0, 0),
                            Padding = new(5, 0, 5, 0),
                            HorizontalContentAlignment = HorizontalAlignment.Left,
                            ToolTip = $"Date derived by searching your images in this deployment for the one with the earliest date{Environment.NewLine}If it doesn't change, then its already correct."
                        };
                        button.Click += DeploymentStart_Click;
                        deploymentStartControl.Container.Children.Insert(2, button);
                    }

                    if (LookupControlByItsDataLabel.TryGetValue(CamtrapDPConstants.Deployment.DeploymentEnd, out var deploymentEndControl))
                    {
                        Button button = new()
                        {
                            Content = "Update",
                            Visibility = Visibility.Visible,
                            Height = 24,
                            Margin = new(10, 0, 0, 0),
                            Padding = new(5, 0, 5, 0),
                            HorizontalContentAlignment = HorizontalAlignment.Left,
                            ToolTip = $"Date derived by searching your images in this deployment for the one with the latest date{Environment.NewLine}If it doesn't change, then its already correct."
                        };
                        button.Click += DeploymentEnd_Click;
                        deploymentEndControl.Container.Children.Insert(2, button);
                    }
                }
            }
        }

        // Set the DeploymentStart to the earliest image date in this deployment,
        // where we search the image dates in the images whose relative path matches the sub-path images in the FileTable
        public void DeploymentStart_Click(object sender, RoutedEventArgs eventArgs)
        {

            DateTime dateTimeMinimum = DateTime.MaxValue;
            foreach (var row in FileDatabase.FileTable)
            {
                if (row.RelativePath != this.SubPath)
                {
                    continue;
                }

                if (row.DateTime < dateTimeMinimum)
                {
                    dateTimeMinimum = row.DateTime;
                }
            }
            if (dateTimeMinimum != DateTime.MaxValue)
            {
                // Update the date
                if (LookupControlByItsDataLabel.TryGetValue(CamtrapDPConstants.Deployment.DeploymentStart, out var startDateControl))
                {
                    ((MetadataDataEntryDateTimeCustom)startDateControl).SetContentAndTooltip(dateTimeMinimum);
                    GlobalReferences.MainWindow.MetadataDataHandler.UpdateMetadataTableAndMetadataDatabase(startDateControl);
                }
            }
        }

        // Set the DeploymentEnd to the latest image date in this deployment,
        // where we search the image dates in the images whose relative path matches the sub-path images in the FileTable
        public void DeploymentEnd_Click(object sender, RoutedEventArgs eventArgs)
        {
            // Set the DeploymentEnd to the latest image date in this deployment,
            // where we search the image dates in the images whose relative path matches the sub-path images in the FileTable
            DateTime dateTimeMaximum = DateTime.MinValue;
            foreach (var row in FileDatabase.FileTable)
            {
                if (row.RelativePath != this.SubPath)
                {
                    continue;
                }

                if (row.DateTime > dateTimeMaximum)
                {
                    dateTimeMaximum = row.DateTime;
                }
            }
            if (dateTimeMaximum != DateTime.MinValue)
            {
                // Update the EndDate 
                if (LookupControlByItsDataLabel.TryGetValue(CamtrapDPConstants.Deployment.DeploymentEnd, out var endDateControl))
                {
                    // Update the date
                    ((MetadataDataEntryDateTimeCustom)endDateControl).SetContentAndTooltip(dateTimeMaximum);
                    GlobalReferences.MainWindow.MetadataDataHandler.UpdateMetadataTableAndMetadataDatabase(endDateControl);
                }
            }
        }

        // Set the TemporalStart to the earliest image date in the image set,
        // where we search the image dates in the images in the FileTable
        public void TemporalStart_Click(object sender, RoutedEventArgs eventArgs)
        {
            if (Level == 1) // It should always be the DataPackage level 1
            {
                if (LookupControlByItsDataLabel.TryGetValue(CamtrapDPConstants.DataPackage.Temporal.Start, out var temporalStart))
                {
                    // Set the TemporalStart  to the earliest image date
                    DateTime dateTimeMinimum = DateTime.MaxValue;
                    foreach (var row in FileDatabase.FileTable)
                    {
                        if (row.DateTime < dateTimeMinimum)
                        {
                            dateTimeMinimum = row.DateTime;
                        }
                    }
                    if (dateTimeMinimum != DateTime.MaxValue)
                    {
                        // Update the date
                        ((MetadataDataEntryDate)temporalStart).SetContentAndTooltip(dateTimeMinimum);
                        GlobalReferences.MainWindow.MetadataDataHandler.UpdateMetadataTableAndMetadataDatabase(temporalStart);
                    }
                }
            }
        }

        // Set the TemporalEnd to the latest image date in the image set,
        // where we search the image dates in the images in the FileTable
        public void TemporalEnd_Click(object sender, RoutedEventArgs eventArgs)
        {
            if (Level == 1) // It should always be the DataPackage level 1
            {
                if (LookupControlByItsDataLabel.TryGetValue(CamtrapDPConstants.DataPackage.Temporal.End, out var temporalEnd))
                {
                    // Set the TemporalEnd to the latest image date
                    DateTime dateTimeMaximum = DateTime.MinValue;
                    foreach (var row in FileDatabase.FileTable)
                    {
                        if (row.DateTime > dateTimeMaximum)
                        {
                            dateTimeMaximum = row.DateTime;
                        }
                    }
                    if (dateTimeMaximum != DateTime.MinValue)
                    {
                        // Update the date
                        ((MetadataDataEntryDate)temporalEnd).SetContentAndTooltip(dateTimeMaximum);
                        GlobalReferences.MainWindow.MetadataDataHandler.UpdateMetadataTableAndMetadataDatabase(temporalEnd);
                    }
                }
            }
        }


        // See above - Raise a dialog box allowing the user to construct and/or edit a list of contributors
        // and set the contributors string to the json representation of that list
        public void Contributors_Click(object sender, RoutedEventArgs eventArgs)
        {
            if (Level == 1) // It should always be the DataPackage level 1
            {
                // Get and set the Contributors json
                if (LookupControlByItsDataLabel.TryGetValue(CamtrapDPConstants.DataPackage.Contributors, out var contributorsControl))
                {
                    CamptrapDPContributors contributorDialog = new(GlobalReferences.MainWindow, contributorsControl.Content);
                    if (true == contributorDialog.ShowDialog())
                    {
                        contributorsControl.SetContentAndTooltip(contributorDialog.JsonContributorsList);
                        GlobalReferences.MainWindow.MetadataDataHandler.UpdateMetadataTableAndMetadataDatabase(contributorsControl);
                    }
                }
            }
        }

        public void Sources_Click(object sender, RoutedEventArgs eventArgs)
        {
            if (Level == 1) // It should always be the DataPackage level 1
            {
                // Get and set the Sources json
                if (LookupControlByItsDataLabel.TryGetValue(CamtrapDPConstants.DataPackage.Sources, out var sourcesControl))
                {
                    CamtrapDPSources sourcesDialog = new(GlobalReferences.MainWindow, sourcesControl.Content);
                    if (true == sourcesDialog.ShowDialog())
                    {
                        sourcesControl.SetContentAndTooltip(sourcesDialog.JsonSourcesList);
                        GlobalReferences.MainWindow.MetadataDataHandler.UpdateMetadataTableAndMetadataDatabase(sourcesControl);
                    }
                }
            }
        }

        public void Licenses_Click(object sender, RoutedEventArgs eventArgs)
        {
            if (Level == 1) // It should always be the DataPackage level 1
            {
                // Get and set the Sources json
                if (LookupControlByItsDataLabel.TryGetValue(CamtrapDPConstants.DataPackage.Licenses, out var licensesControl))
                {
                    CamtrapDPLicenses licensesDialog = new(GlobalReferences.MainWindow, licensesControl.Content);
                    if (true == licensesDialog.ShowDialog())
                    {
                        licensesControl.SetContentAndTooltip(licensesDialog.JsonLicensesList);
                        GlobalReferences.MainWindow.MetadataDataHandler.UpdateMetadataTableAndMetadataDatabase(licensesControl);
                    }
                }
            }
        }

        public void Taxonomic_Click(object sender, RoutedEventArgs eventArgs)
        {
            if (Level == 1) // It should always be the DataPackage level 1
            {
                // Get and set the Sources json
                if (LookupControlByItsDataLabel.TryGetValue(CamtrapDPConstants.DataPackage.Taxonomic, out var taxonomicControl))
                {
                    CamtrapDPTaxonomic taxonomicDialog = new(GlobalReferences.MainWindow, taxonomicControl.Content);
                    if (true == taxonomicDialog.ShowDialog())
                    {
                        taxonomicControl.SetContentAndTooltip(taxonomicDialog.JsonTaxonomicList);
                        GlobalReferences.MainWindow.MetadataDataHandler.UpdateMetadataTableAndMetadataDatabase(taxonomicControl);
                    }
                }
            }
        }

        public void RelatedIdentifiers_Click(object sender, RoutedEventArgs eventArgs)
        {
            if (Level == 1) // It should always be the DataPackage level 1
            {
                // Get and set the Sources json
                if (LookupControlByItsDataLabel.TryGetValue(CamtrapDPConstants.DataPackage.RelatedIdentifiers, out var relatedIdentifiersControl))
                {
                    CamtrapDPRelatedIdentifiers licensesDialog = new(GlobalReferences.MainWindow, relatedIdentifiersControl.Content);
                    if (true == licensesDialog.ShowDialog())
                    {
                        relatedIdentifiersControl.SetContentAndTooltip(licensesDialog.JsonRelatedIdentifiersList);
                        GlobalReferences.MainWindow.MetadataDataHandler.UpdateMetadataTableAndMetadataDatabase(relatedIdentifiersControl);
                    }
                }
            }
        }


        public void References_Click(object sender, RoutedEventArgs eventArgs)
        {
            if (Level == 1) // It should always be the DataPackage level 1
            {
                // Get and set the Sources json
                if (LookupControlByItsDataLabel.TryGetValue(CamtrapDPConstants.DataPackage.References, out var referencesControl))
                {
                    CamtrapDPReferences referencesDialog = new(GlobalReferences.MainWindow, referencesControl.Content);
                    if (true == referencesDialog.ShowDialog())
                    {
                        referencesControl.SetContentAndTooltip(referencesDialog.JsonReferencesList);
                        GlobalReferences.MainWindow.MetadataDataHandler.UpdateMetadataTableAndMetadataDatabase(referencesControl);
                    }
                }
            }
        }

        public void SpatialGeoJson_Click(object sender, RoutedEventArgs eventArgs)
        {
            string command = "https://GeoJson.IO";
            string jsonParameterCode = "/#data=data:application/json,";
            Dialogs.CamtrapDPSpatialCoverageInstructions(GlobalReferences.MainWindow);
            if (LookupControlByItsDataLabel.TryGetValue(CamtrapDPConstants.DataPackage.Spatial, out var spatialControl))
            {
                if (false == string.IsNullOrWhiteSpace(spatialControl.Content))
                {
                    command += jsonParameterCode + Uri.EscapeDataString(spatialControl.Content);
                }
                ProcessExecution.TryProcessStart(new Uri(command));
            }
        }

        public void SpatialLatLong_Click(object sender, RoutedEventArgs eventArgs)
        {
            Dialogs.CamtrapDPSpatialCoverageInstructions(GlobalReferences.MainWindow);
            string jsonAsString = CamtrapDPHelpers.CalculateLatLongBoundingBoxFromDeployments(FileDatabase);
            // Set the package created date
            if (LookupControlByItsDataLabel.TryGetValue(CamtrapDPConstants.DataPackage.Spatial, out var spatialControl))
            {
                spatialControl.SetContentAndTooltip(jsonAsString);
            }
        }

        // If we are using the Camtrap standard, autofill some of the fields to match the standards requirements
        private void AutofillFieldsIfCamtrapDPStandards()
        {
            if (FileDatabase.MetadataTablesIsCamtrapDPStandard())
            {
                // Data package level
                if (Level == 1)
                {
                    // Set the package ID
                    if (LookupControlByItsDataLabel.TryGetValue(CamtrapDPConstants.DataPackage.IdAlias, out var idControl))
                    {
                        idControl.SetContentAndTooltip(Guid.NewGuid().ToString());
                        GlobalReferences.MainWindow.MetadataDataHandler.UpdateMetadataTableAndMetadataDatabase(idControl);
                    }

                    // Set the package created date
                    if (LookupControlByItsDataLabel.TryGetValue(CamtrapDPConstants.DataPackage.Created, out var createdControl))
                    {
                        createdControl.SetContentAndTooltip(DateTimeHandler.ToStringDatabaseDateTime(DateTime.Now));
                        GlobalReferences.MainWindow.MetadataDataHandler.UpdateMetadataTableAndMetadataDatabase(createdControl);
                    }
                }

                // Deployment level
                if (Level == 2)
                {
                    // Set the DeploymentID to the relative path
                    if (LookupControlByItsDataLabel.TryGetValue(Standards.CamtrapDPConstants.Deployment.DeploymentID, out var deploymentIDControl))
                    {
                        // the subpath should always be the deployment folder name, as its the 2nd level (i.e. the first subfolder)
                        deploymentIDControl.SetContentAndTooltip(this.SubPath);
                        GlobalReferences.MainWindow.MetadataDataHandler.UpdateMetadataTableAndMetadataDatabase(deploymentIDControl);
                    }

                    // Set the LocationID to a GUID
                    if (LookupControlByItsDataLabel.TryGetValue(CamtrapDPConstants.Deployment.LocationID, out var locationIDControl))
                    {
                        locationIDControl.SetContentAndTooltip(Guid.NewGuid().ToString());
                        GlobalReferences.MainWindow.MetadataDataHandler.UpdateMetadataTableAndMetadataDatabase(locationIDControl);
                    }

                    // Set the DeploymentStart and DeploymentEnd to the earliest and latest image dates in this deployment,
                    // as indicated by the dates in the matching sub-path images in the FileTable
                    DateTime dateTimeMinimum = DateTime.MaxValue;
                    DateTime dateTimeMaximum = DateTime.MinValue;
                    foreach (var row in FileDatabase.FileTable)
                    {
                        if (row.RelativePath != this.SubPath)
                        {
                            continue;
                        }
                        if (row.DateTime > dateTimeMaximum)
                        {
                            dateTimeMaximum = row.DateTime;
                        }
                        if (row.DateTime < dateTimeMinimum)
                        {
                            dateTimeMinimum = row.DateTime;
                        }
                    }
                    if (dateTimeMinimum != DateTime.MaxValue)
                    {
                        // Set the StartDate to the earliest image date
                        if (LookupControlByItsDataLabel.TryGetValue(CamtrapDPConstants.Deployment.DeploymentStart, out var startDateControl))
                        {
                            ((Timelapse.ControlsMetadata.MetadataDataEntryDateTimeCustom)startDateControl).SetContentAndTooltip(dateTimeMinimum);
                            GlobalReferences.MainWindow.MetadataDataHandler.UpdateMetadataTableAndMetadataDatabase(startDateControl);
                        }
                    }

                    if (dateTimeMaximum != DateTime.MinValue)
                    {
                        // Set the EndDate to the latest image date
                        if (LookupControlByItsDataLabel.TryGetValue(CamtrapDPConstants.Deployment.DeploymentEnd, out var endDateControl))
                        {
                            ((Timelapse.ControlsMetadata.MetadataDataEntryDateTimeCustom)endDateControl).SetContentAndTooltip(dateTimeMaximum);
                            GlobalReferences.MainWindow.MetadataDataHandler.UpdateMetadataTableAndMetadataDatabase(endDateControl);
                        }
                    }
                }
            }
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
                label.Measure(new(double.PositiveInfinity, double.PositiveInfinity));
                maxColumn1Width = Math.Max(label.DesiredSize.Width, maxColumn1Width);

                Control thisControl = (Control)control.GetContentControl;
                thisControl.Measure(new(double.PositiveInfinity, double.PositiveInfinity));
                maxColumn2Width = Math.Max(thisControl.DesiredSize.Width, maxColumn2Width);
            }

            // Adjust the format of the controls
            foreach (StackPanel child in childrenUIElementCollection)
            {
                MetadataDataEntryControl control = (MetadataDataEntryControl)child.Tag;

                // Decrease the vertical spacing between controls
                control.Container.Margin = new(0, -3, 0, -3);

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

                    // Now create the tooltip, which can be multiline.
                    TextBlock description = new()
                    {
                        Height = 16,
                        Padding = new(0, 0, 0, 0),
                        TextTrimming = TextTrimming.CharacterEllipsis,
                        TextWrapping = TextWrapping.NoWrap,
                        Text = firstLine,
                        Margin = new(10, 0, 10, 0),
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
            int index = TabControlOrderList.IndexOf(inputElement);
            if (index >= 0)
            {
                // Find the next or previous index (depending on whether a shift is presentits a tab or shift tab
                int nextIndex;
                if (Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift))
                {
                    nextIndex = index == 0
                        ? TabControlOrderList.Count - 1
                        : index - 1;
                }
                else
                {
                    nextIndex = index < TabControlOrderList.Count - 1
                        ? index + 1
                        : 0;
                }
                Keyboard.Focus(TabControlOrderList[nextIndex].GetContentControl);
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

        public static void ConfigureFormatForDateTimeCustom(WatermarkDateTimePicker dateTimePicker)
        {
            // Check the arguments for null 
            ThrowIf.IsNullArgument(dateTimePicker, nameof(dateTimePicker));

            dateTimePicker.AutoCloseCalendar = true;
            dateTimePicker.Format = DateTimeFormat.Custom;
            dateTimePicker.FormatString = Time.DateTimeDisplayFormat;
            dateTimePicker.TimeFormat = DateTimeFormat.Custom;
            dateTimePicker.TimeFormatString = Time.TimeFormat;
            dateTimePicker.CultureInfo = CultureInfo.CreateSpecificCulture("en-US");
        }

        public static void ConfigureFormatForDate(WatermarkDateTimePicker dateTimePicker)
        {
            ThrowIf.IsNullArgument(dateTimePicker, nameof(dateTimePicker));

            dateTimePicker.AutoCloseCalendar = true; dateTimePicker.AutoCloseCalendar = true;
            dateTimePicker.CultureInfo = CultureInfo.CreateSpecificCulture("en-US");
            dateTimePicker.Format = DateTimeFormat.Custom;
            dateTimePicker.FormatString = Time.DateDisplayFormat;
            dateTimePicker.TimePickerVisibility = Visibility.Collapsed;
        }

        public static void ConfigureFormatForTime(WatermarkTimePicker timePicker)
        {
            ThrowIf.IsNullArgument(timePicker, nameof(timePicker));

            timePicker.CultureInfo = CultureInfo.CreateSpecificCulture("en-US");
            timePicker.Format = DateTimeFormat.Custom;
            timePicker.FormatString = Time.TimeFormat;
            timePicker.TimeInterval = TimeSpan.FromMinutes(15);
            timePicker.StartTime = TimeSpan.FromHours(9);
            timePicker.MaxDropDownHeight = 250;
        }
        #endregion

        #region Helpers for next/previous folder navigation
        // Returns
        // - a list of beginnning relative path portions in the currently selected files at the same level as the relativePathPortion,
        // - sets the matchingIndex to the path that in that list that matches the relativePathPortion.
        // For example, if the subpath is Station2, it would return Station1, Station2, Station3 etc with the matching index of 1  
        private List<string> GetFolderListMatchingSubPathLevels(FileDatabase fileDatabase, string subPath, out int matchingIndex)
        {
            matchingIndex = -1;
            if (fileDatabase?.FileTable == null)
            {
                return [];
            }
            List<string> folderList = FileTableGetAllSubFolderNamesFromRelativePaths(FileDatabase);

            // Collect the paths that are at this level
            int currentIndex = 0;
            List<string> foldersAtThisLevelList = [];
            foreach (string folder in folderList)
            {
                if (folder.Split(Path.DirectorySeparatorChar).Length == this.Level - 1)
                {
                    foldersAtThisLevelList.Add(folder);
                    if (folder == subPath)
                    {
                        matchingIndex = currentIndex;
                    }
                    currentIndex++;
                }
            }

            return foldersAtThisLevelList;
        }

        // Get all the distinct relative paths in the current selection, each relative path split into its component parts
        // e.g., if we had Station1/Deployment1, Station1/Deployment2, Station2/Deployment1, the list would also contain Station1, Station2 
        private static List<string> FileTableGetAllSubFolderNamesFromRelativePaths(FileDatabase fileDatabase)
        {
            if (fileDatabase == null)
            {
                return [];
            }
            IEnumerable<string> relativePathList = fileDatabase.GetRelativePathsInCurrentSelection;
            List<string> allPaths = [];
            foreach (string relativePath in relativePathList)
            {
                allPaths.Add(relativePath);
                string parent = string.IsNullOrEmpty(relativePath) ? string.Empty : Path.GetDirectoryName(relativePath);
                while (!string.IsNullOrWhiteSpace(parent))
                {
                    if (!allPaths.Contains(parent))
                    {
                        allPaths.Add(parent);
                    }
                    parent = Path.GetDirectoryName(parent);
                }
            }
            allPaths.Sort();
            return allPaths;
        }
        #endregion

        #region Public Properties for compatibility with OldMetadataDataEntryPanel
        /// <summary>
        /// Expose the AddMetadata button for compatibility
        /// </summary>
        public Button AddMetadata => ButtonAddMetadata;

        /// <summary>
        /// Expose the RelativePathToCurrentImage text block for compatibility
        /// </summary>
        public TextBlock RelativePathToCurrentImage => TextBlockRelativePathToCurrentImage;

        // ControlsPanel is automatically accessible from XAML x:Name="ControlsPanel" - no additional property needed
        #endregion
    }
}
