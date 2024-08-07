using System;
using System.Collections.Generic;
using System.Diagnostics;
using Timelapse.Constant;
using Timelapse.Database;
using Timelapse.DataTables;
using Timelapse.DebuggingSupport;
using Timelapse.Enums;
using Timelapse.Util;

namespace Timelapse.ControlsDataEntry
{
    /// <summary>
    /// This user control generates and displays controls based upon the information passed into it from the templateTable
    /// It is used by and displayed within the Data Entry pane.
    /// </summary>
    public partial class DataEntryControls
    {
        #region Public properties and Private Variables
        public List<DataEntryControl> Controls { get; }
        public Dictionary<string, DataEntryControl> ControlsByDataLabelThatAreVisible { get; }
        public Dictionary<string, DataEntryControl> ControlsByDataLabelForExport { get; }

        private DataEntryHandler dataEntryHandler;
        #endregion

        #region Constructor
        public DataEntryControls()
        {
            InitializeComponent();
            Controls = new List<DataEntryControl>();
            ControlsByDataLabelThatAreVisible = new Dictionary<string, DataEntryControl>();
            ControlsByDataLabelForExport = new Dictionary<string, DataEntryControl>();
        }
        #endregion

        #region Create Controls method
        /// <summary>
        /// Generate the controls based upon the control descriptions found in the template
        /// </summary>>
        public void CreateControls(FileDatabase database, DataEntryHandler dataEntryPropagator)
        {
            // Check the arguments for null 
            ThrowIf.IsNullArgument(dataEntryPropagator, nameof(dataEntryPropagator));
            ThrowIf.IsNullArgument(database, nameof(database));

            // Depending on how the user interacts with the file import process image set loading can be aborted after controls are generated and then
            // another image set loaded.  Any existing controls therefore need to be cleared.
            Controls.Clear();
            ControlsByDataLabelThatAreVisible.Clear();
            ControlsByDataLabelForExport.Clear();
            ControlGrid.Inlines.Clear();

            foreach (ControlRow control in database.Controls)
            {
                // no point in generating a control if it doesn't render in the UX
                //if (control.Visible == false)
                //{
                //    continue;
                //}

                DataEntryControl controlToAdd;
                switch (control.Type)
                {
                    case DatabaseColumn.DateTime:
                        DataEntryDateTime dateTimeControl = new DataEntryDateTime(control, this)
                        {
                            ContentReadOnly = false
                        };
                        controlToAdd = dateTimeControl;
                        break;

                    case DatabaseColumn.File:
                    case DatabaseColumn.RelativePath:
                    case Control.Note:
                        // standard controls rendering as notes aren't editable by the user, so we don't need autocompletions on them 
                        Dictionary<string, string> noteAutocompletions = null;
                        bool readOnly = control.Type != Control.Note && control.Type != Control.AlphaNumeric;
                        if (readOnly == false)
                        {
                            noteAutocompletions = new Dictionary<string, string>();
                        }
                        DataEntryNote noteControl = new DataEntryNote(control, noteAutocompletions, this)
                        {
                            ContentReadOnly = readOnly
                        };
                        controlToAdd = noteControl;
                        break;
                    case Control.MultiLine:
                        DataEntryMultiLine multiLineControl = new DataEntryMultiLine(control, this)
                        {
                            ContentReadOnly = false
                        };
                        controlToAdd = multiLineControl;
                        break;
                    case Control.AlphaNumeric:
                        Dictionary<string, string> alphaAutocompletions = new Dictionary<string, string>();
                        DataEntryAlphaNumeric alphaNumericControl = new DataEntryAlphaNumeric(control, alphaAutocompletions, this)
                        {
                            ContentReadOnly = false
                        };
                        controlToAdd = alphaNumericControl;
                        break;
                    case Control.Flag:
                    case DatabaseColumn.DeleteFlag:
                        DataEntryFlag flagControl = new DataEntryFlag(control, this)
                        {
                            ContentReadOnly = false
                        };
                        controlToAdd = flagControl;
                        break;
                    case Control.Counter:
                        DataEntryCounter counterControl = new DataEntryCounter(control, this)
                        {
                            ContentReadOnly = false
                        };
                        controlToAdd = counterControl;
                        break;
                    case Control.IntegerAny:
                        DataEntryIntegerAny integerAnyControl = new DataEntryIntegerAny(control, this)
                        {
                            ContentReadOnly = false
                        };
                        controlToAdd = integerAnyControl;
                        break;
                    case Control.IntegerPositive:
                        DataEntryIntegerPositive integerPositiveControl = new DataEntryIntegerPositive(control, this)
                        {
                            ContentReadOnly = false
                        };
                        controlToAdd = integerPositiveControl;
                        break;
                    case Control.DecimalAny:
                        DataEntryDecimalAny decimalAnyControl = new DataEntryDecimalAny(control, this)
                        {
                            ContentReadOnly = false
                        };
                        controlToAdd = decimalAnyControl;
                        break;
                    case Control.DecimalPositive:
                        DataEntryDecimalPositive decimalPositiveControl = new DataEntryDecimalPositive(control, this)
                        {
                            ContentReadOnly = false
                        };
                        controlToAdd = decimalPositiveControl;
                        break;
                    case Control.FixedChoice:
                        DataEntryChoice choiceControl = new DataEntryChoice(control, this)
                        {
                            ContentReadOnly = false
                        };                      
                        controlToAdd = choiceControl;
                        break;
                    case Control.MultiChoice:
                        DataEntryMultiChoice multiChoiceControl = new DataEntryMultiChoice(control, this)
                        {
                            ContentReadOnly = false
                        };
                        controlToAdd = multiChoiceControl;
                        break;
                    case Control.DateTime_:
                        DataEntryDateTimeCustom dateTimeCustomControl = new DataEntryDateTimeCustom(control, this, ControlDefault.DateTimeCustomDefaultValue)
                        {
                            ContentReadOnly = false
                        };
                        controlToAdd = dateTimeCustomControl;
                        break;
                    case Control.Date_:
                        DataEntryDate dateControl = new DataEntryDate(control, this, ControlDefault.Date_DefaultValue)
                        {
                            ContentReadOnly = false
                        };
                        controlToAdd = dateControl;
                        break;
                    case Control.Time_:
                        DataEntryTime timeControl = new DataEntryTime(control, this, ControlDefault.Time_DefaultValue)
                        {
                            ContentReadOnly = false
                        };
                        controlToAdd = timeControl;
                        break;
                    default:
                        TracePrint.PrintMessage($"Unhandled control type {control.Type} in CreateControls.");
                        continue;
                }
                if (control.Visible)
                {
                    ControlGrid.Inlines.Add(controlToAdd.Container);
                    Controls.Add(controlToAdd);
                    ControlsByDataLabelThatAreVisible.Add(control.DataLabel, controlToAdd);
                }
                if (control.ExportToCSV)
                {
                    ControlsByDataLabelForExport.Add(control.DataLabel, controlToAdd);
                }
            }

            // Redundant check as for some reason CA1062 was still showing up as a warning.
            ThrowIf.IsNullArgument(dataEntryPropagator, nameof(dataEntryPropagator));
            dataEntryPropagator.SetDataEntryCallbacks(ControlsByDataLabelThatAreVisible);
            dataEntryHandler = dataEntryPropagator;
        }
        #endregion

        public void Reset()
        {
            Controls.Clear();
            ControlsByDataLabelThatAreVisible.Clear();
            ControlsByDataLabelForExport.Clear();
            try
            {
                ControlGrid.Inlines.Clear();
            }
            catch (Exception)
            {
                // ignored
            }
        }

        #region Autocompletion methods
        public void AutocompletionPopulateAllNotesWithFileTableValues(FileDatabase database)
        {
            // Check the arguments for null 
            ThrowIf.IsNullArgument(database, nameof(database));

            foreach (DataEntryControl control in Controls)
            {
                // no point in autocompleting if its read-only
                if (control.ContentReadOnly)
                {
                    continue;
                }
                // We are only autocompleting notes
                if (!(control is DataEntryNote note))
                {
                    continue;
                }
                note.ContentControl.Autocompletions = database.GetDistinctValuesInSelectedFileTableColumn(note.DataLabel, 2);
            }
        }

        // Try to add the values in the current note fields to the autocompletion list, assuming that it is different.
        public void AutocompletionUpdateWithCurrentRowValues()
        {
            foreach (DataEntryControl control in Controls)
            {
                // no point in updating autocompletion if its read-only
                if (control.ContentReadOnly)
                {
                    continue;
                }
                // We are only autocompleting notes
                // Get the value and add it to the autocompletion, but only if there are at least two characters in it.

                if (control is DataEntryNote note && note.ContentControl.Text.Length > 1)
                {
                    string value = note.ContentControl.Text;
                    if (note.ContentControl.Autocompletions.ContainsKey(value) == false)
                    {
                        note.ContentControl.Autocompletions.Add(value, string.Empty);
                    }
                }
            }
        }
        // Return the autocompletion list for a note identified by its datalabel
        public Dictionary<string, string> AutocompletionGetForNote(string datalabel)
        {
            foreach (DataEntryControl control in Controls)
            {
                // no point in autocompleting if its read-only
                if (control.DataLabel == datalabel)
                {
                    if (control is DataEntryNote note)
                    {
                        return note.ContentControl.Autocompletions;
                    }

                    return null;
                }
            }
            return null;
        }
        #endregion

        #region Enable / Disable stock controls depending on single vs thumbnail grid view
        // Enable or disable the following stock controls: 
        //     File, RelativePath,  DateTime
        // These controls refer to the specifics of a single image. Thus they should be disabled (and are thus not  editable) 
        // when the markable canvas is zoomed out to display multiple images
        public void SetEnableState(ControlsEnableStateEnum controlsToEnable, int imagesSelected)
        {
            if (dataEntryHandler?.ImageCache?.Current == null)
            {
                return;
            }
            dataEntryHandler.IsProgrammaticControlUpdate = true;
            foreach (DataEntryControl control in Controls)
            {
                if (control is DataEntryDateTime datetime)
                {
                    // DateTime
                    if (controlsToEnable == ControlsEnableStateEnum.SingleImageView)
                    {
                        // Single images view - Enable and show its contents
                        datetime.IsEnabled = true;
                        datetime.SetContentAndTooltip(dataEntryHandler.ImageCache.Current.GetValueDisplayString(datetime.DataLabel));
                    }
                    else
                    {
                        // Multiple images view
                        // When one image is selected, display it as enabled (but not editable) and show its value, otherwise disabled
                        // Note that if the contentAndTooltip is null (due to no value or to conflicting values), SetContentAndTooltip will display an ellipsis
                        string contentAndTooltip = dataEntryHandler.GetValueDisplayStringCommonToFileIds(datetime.DataLabel);
                        datetime.IsEnabled = false;   // We currently don't allow editing of utcOffset in the overview. To fix, start here: datetime.IsEnabled = (imagesSelected == 1) ? true : false;
                        datetime.SetContentAndTooltip(contentAndTooltip);
                    }
                }
                else if (control is DataEntryNote note)
                {
                    // Notes, File and Relative Path
                    if (controlsToEnable == ControlsEnableStateEnum.SingleImageView)
                    {
                        // Single images view - Enable and show its contents
                        note.IsEnabled = true;
                        note.SetContentAndTooltip(dataEntryHandler.ImageCache.Current.GetValueDisplayString(note.DataLabel));
                    }
                    else
                    {
                        // Multiple images view
                        // File, Relative Path: When one image is selected, display it as enabled and editable.
                        // Notes: When one or more images are selected, display it as enabled and editable.
                        // Note that if the contentAndTooltip is null (due to no value or to conflicting values), SetContentAndTooltip will display an ellipsis
                        string contentAndTooltip = dataEntryHandler.GetValueDisplayStringCommonToFileIds(note.DataLabel);
                        if (control.DataLabel == DatabaseColumn.File ||
                             control.DataLabel == DatabaseColumn.RelativePath)
                        {
                            note.IsEnabled = imagesSelected == 1;
                        }
                        else
                        {
                            note.IsEnabled = imagesSelected >= 1;
                        }
                        note.SetContentAndTooltip(contentAndTooltip);
                    }
                }
                else if (control is DataEntryMultiLine multiLine)
                {
                    // Notes, File and Relative Path
                    if (controlsToEnable == ControlsEnableStateEnum.SingleImageView)
                    {
                        // Single images view - Enable and show its contents
                        multiLine.IsEnabled = true;
                        multiLine.SetContentAndTooltip(dataEntryHandler.ImageCache.Current.GetValueDisplayString(multiLine.DataLabel));
                    }
                    else
                    {
                        // Multiple images view
                        // File, Relative Path: When one image is selected, display it as enabled and editable.
                        // Notes: When one or more images are selected, display it as enabled and editable.
                        // Note that if the contentAndTooltip is null (due to no value or to conflicting values), SetContentAndTooltip will display an ellipsis
                        string contentAndTooltip = dataEntryHandler.GetValueDisplayStringCommonToFileIds(multiLine.DataLabel);
                        multiLine.IsEnabled = imagesSelected >= 1;
                        multiLine.SetContentAndTooltip(contentAndTooltip);
                    }
                }
                else if (control is DataEntryAlphaNumeric alphaNumeric)
                {
                    // Alphanumeric - Same code as note, but we needed to make it specific to its type.
                    if (controlsToEnable == ControlsEnableStateEnum.SingleImageView)
                    {
                        // Single images view - Enable and show its contents
                        alphaNumeric.IsEnabled = true;
                        alphaNumeric.SetContentAndTooltip(dataEntryHandler.ImageCache.Current.GetValueDisplayString(alphaNumeric.DataLabel));
                    }
                    else
                    {
                        // Multiple images view
                        // File, Relative Path: When one image is selected, display it as enabled and editable.
                        // Notes: When one or more images are selected, display it as enabled and editable.
                        // Note that if the contentAndTooltip is null (due to no value or to conflicting values), SetContentAndTooltip will display an ellipsis
                        string contentAndTooltip = dataEntryHandler.GetValueDisplayStringCommonToFileIds(alphaNumeric.DataLabel);
                        if (control.DataLabel == DatabaseColumn.File ||
                            control.DataLabel == DatabaseColumn.RelativePath)
                        {
                            alphaNumeric.IsEnabled = imagesSelected == 1;
                        }
                        else
                        {
                            alphaNumeric.IsEnabled = imagesSelected >= 1;
                        }
                        alphaNumeric.SetContentAndTooltip(contentAndTooltip);
                    }
                }
                else if (control is DataEntryChoice choice)
                {
                    // Choices
                    if (controlsToEnable == ControlsEnableStateEnum.SingleImageView)
                    {
                        // Single images view - Enable and show its contents
                        choice.IsEnabled = true;
                        choice.SetContentAndTooltip(dataEntryHandler.ImageCache.Current.GetValueDisplayString(choice.DataLabel));
                    }
                    else
                    {
                        // Multiple images view
                        // When one or more images are selected, display it as enabled and editable.
                        // Note that if the contentAndTooltip is null (due to no value or to conflicting values), SetContentAndTooltip will display an ellipsis
                        string contentAndTooltip = dataEntryHandler.GetValueDisplayStringCommonToFileIds(choice.DataLabel);
                        choice.IsEnabled = (imagesSelected >= 1);
                        choice.SetContentAndTooltip(contentAndTooltip);
                    }
                }
                else if (control is DataEntryMultiChoice multiChoice)
                {
                    // Choices
                    if (controlsToEnable == ControlsEnableStateEnum.SingleImageView)
                    {
                        // Single images view - Enable and show its contents
                        multiChoice.IsEnabled = true;
                        multiChoice.SetContentAndTooltip(dataEntryHandler.ImageCache.Current.GetValueDisplayString(multiChoice.DataLabel));
                    }
                    else
                    {
                        // Multiple images view
                        // When one or more images are selected, display it as enabled and editable.
                        // Note that if the contentAndTooltip is null (due to no value or to conflicting values), SetContentAndTooltip will display an ellipsis
                        string contentAndTooltip = dataEntryHandler.GetValueDisplayStringCommonToFileIds(multiChoice.DataLabel);
                        multiChoice.IsEnabled = (imagesSelected >= 1);
                        multiChoice.SetContentAndTooltip(contentAndTooltip);
                    }
                }
                else if (control is DataEntryCounter counter)
                {
                    // Counters
                    if (controlsToEnable == ControlsEnableStateEnum.SingleImageView)
                    {
                        // Single images view - Enable and show its contents
                        counter.IsEnabled = true;
                        counter.SetContentAndTooltip(dataEntryHandler.ImageCache.Current.GetValueDisplayString(counter.DataLabel));
                    }
                    else
                    {
                        // Multiple images view
                        // When one or more images are selected, display it as enabled and editable.
                        // Note that if the contentAndTooltip is null (due to no value or to conflicting values), SetContentAndTooltip will display an ellipsis
                        string contentAndTooltip = dataEntryHandler.GetValueDisplayStringCommonToFileIds(counter.DataLabel);
                        counter.IsEnabled = (imagesSelected >= 1);
                        // Changing a counter value does not trigger a ValueChanged event if the values are the same.
                        // which means multiple images may not be updated even if other images have the same value.
                        // To get around this, we set a bogus value and then the real value, which means that the
                        // ValueChanged event will be triggered. Inefficient, but seems to work.
                        counter.SetBogusCounterContentAndTooltip();
                        counter.SetContentAndTooltip(contentAndTooltip);
                    }
                }

                // Integers
                else if (control is DataEntryIntegerBase integerBase)
                {
                    // Almost the same code as counters
                    // Works for IntegerAny and IntegerPositive
                    if (controlsToEnable == ControlsEnableStateEnum.SingleImageView)
                    {
                        // Single images view - Enable and show its contents
                        integerBase.IsEnabled = true;
                        integerBase.SetContentAndTooltip(dataEntryHandler.ImageCache.Current.GetValueDisplayString(integerBase.DataLabel));
                    }
                    else
                    {
                        // Multiple images view
                        // When one or more images are selected, display it as enabled and editable.
                        // Note that if the contentAndTooltip is null (due to no value or to conflicting values), SetContentAndTooltip will display an ellipsis
                        string contentAndTooltip = dataEntryHandler.GetValueDisplayStringCommonToFileIds(integerBase.DataLabel);
                        integerBase.IsEnabled = imagesSelected >= 1;
                        // Changing a counter value does not trigger a ValueChanged event if the values are the same.
                        // which means multiple images may not be updated even if other images have the same value.
                        // To get around this, we set a bogus value and then the real value, which means that the
                        // ValueChanged event will be triggered. Inefficient, but seems to work.
                        integerBase.SetBogusCounterContentAndTooltip();
                        integerBase.SetContentAndTooltip(contentAndTooltip);
                    }
                }

                // Decimals 
                else if (control is DataEntryDecimalBase decimalBase)
                {

                    // Works for DecimalAny and DecimalPositive
                    if (controlsToEnable == ControlsEnableStateEnum.SingleImageView)
                    {
                        // Single images view - Enable and show its contents
                        decimalBase.IsEnabled = true;
                        decimalBase.SetContentAndTooltip(dataEntryHandler.ImageCache.Current.GetValueDisplayString(decimalBase.DataLabel));
                    }
                    else
                    {
                        // Multiple images view
                        // When one or more images are selected, display it as enabled and editable.
                        // Note that if the contentAndTooltip is null (due to no value or to conflicting values), SetContentAndTooltip will display an ellipsis
                        string contentAndTooltip = dataEntryHandler.GetValueDisplayStringCommonToFileIds(decimalBase.DataLabel);
                        decimalBase.IsEnabled = imagesSelected >= 1;
                        // Changing a counter value does not trigger a ValueChanged event if the values are the same.
                        // which means multiple images may not be updated even if other images have the same value.
                        // To get around this, we set a bogus value and then the real value, which means that the
                        // ValueChanged event will be triggered. Inefficient, but seems to work.
                        decimalBase.SetBogusCounterContentAndTooltip();
                        decimalBase.SetContentAndTooltip(contentAndTooltip);
                    }
                }
                else if (control is DataEntryFlag flag)
                {
                    // Flag, Delete Flag
                    if (controlsToEnable == ControlsEnableStateEnum.SingleImageView)
                    {
                        // Single images view - Enable and show its contents
                        flag.IsEnabled = true;
                        flag.SetContentAndTooltip(dataEntryHandler.ImageCache.Current.GetValueDisplayString(flag.DataLabel));
                    }
                    else
                    {
                        // Multiple images view
                        // When one or more images are selected, display it as enabled and editable.
                        // Note that if the contentAndTooltip is null (due to no value or to conflicting values), SetContentAndTooltip will display an ellipsis
                        string contentAndTooltip = dataEntryHandler.GetValueDisplayStringCommonToFileIds(flag.DataLabel);
                        flag.IsEnabled = (imagesSelected >= 1);
                        flag.SetContentAndTooltip(contentAndTooltip);
                    }
                }
                else if (control is DataEntryDateTimeCustom dateTime_)
                {
                    // DateTime_
                    if (controlsToEnable == ControlsEnableStateEnum.SingleImageView)
                    {
                        // Single images view - Enable and show its contents
                        dateTime_.IsEnabled = true;
                        dateTime_.SetContentAndTooltip(dataEntryHandler.ImageCache.Current.GetValueDisplayString(dateTime_.DataLabel));
                    }
                    else
                    {
                        // Multiple images view
                        // When one or more images are selected, display it as enabled and editable.
                        // Note that if the contentAndTooltip is null (due to no value or to conflicting values), SetContentAndTooltip will display an ellipsis
                        string contentAndTooltip = dataEntryHandler.GetValueDisplayStringCommonToFileIds(dateTime_.DataLabel);
                        dateTime_.IsEnabled = (imagesSelected >= 1);
                        dateTime_.SetContentAndTooltip(contentAndTooltip);
                    }
                }
                else if (control is DataEntryDate date_)
                {
                    // Date_
                    if (controlsToEnable == ControlsEnableStateEnum.SingleImageView)
                    {
                        // Single images view - Enable and show its contents
                        date_.IsEnabled = true;
                        date_.SetContentAndTooltip(dataEntryHandler.ImageCache.Current.GetValueDisplayString(date_.DataLabel));
                    }
                    else
                    {
                        // Multiple images view
                        // When one or more images are selected, display it as enabled and editable.
                        // Note that if the contentAndTooltip is null (due to no value or to conflicting values), SetContentAndTooltip will display an ellipsis
                        string contentAndTooltip = dataEntryHandler.GetValueDisplayStringCommonToFileIds(date_.DataLabel);
                        date_.IsEnabled = (imagesSelected >= 1);
                        date_.SetContentAndTooltip(contentAndTooltip);
                    }
                }
                else if (control is DataEntryTime time_)
                {
                    // Time_
                    if (controlsToEnable == ControlsEnableStateEnum.SingleImageView)
                    {
                        // Single images view - Enable and show its contents
                        time_.IsEnabled = true;
                        time_.SetContentAndTooltip(dataEntryHandler.ImageCache.Current.GetValueDisplayString(time_.DataLabel));
                    }
                    else
                    {
                        // Multiple images view
                        // When one or more images are selected, display it as enabled and editable.
                        // Note that if the contentAndTooltip is null (due to no value or to conflicting values), SetContentAndTooltip will display an ellipsis
                        string contentAndTooltip = dataEntryHandler.GetValueDisplayStringCommonToFileIds(time_.DataLabel);
                        time_.IsEnabled = (imagesSelected >= 1);
                        time_.SetContentAndTooltip(contentAndTooltip);
                    }
                }
            }
            dataEntryHandler.IsProgrammaticControlUpdate = false;
        }
        #endregion

        public void DisposeAsNeeded()
        {
            try
            {
                dataEntryHandler?.Dispose();
                dataEntryHandler = null;

            }
            catch
            {
                Debug.Print("Failed in DataEntryControls-DisposeAsNeeded");
            }
        }
    }
}