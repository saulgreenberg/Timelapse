using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Documents;
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
            Controls = [];
            ControlsByDataLabelThatAreVisible = [];
            ControlsByDataLabelForExport = [];
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

            this.Controls.Clear();
            ControlsByDataLabelThatAreVisible.Clear();
            ControlsByDataLabelForExport.Clear();

            // Instead of clearing Inlines (which causes serialization errors with ObservableCollection),
            // we replace the entire Paragraph with a fresh one. This completely avoids the serialization issue.
            ControlGrid.Dispatcher.Invoke(() =>
            {
                // Get the parent FlowDocument
                if (ControlGrid.Parent is FlowDocument flowDocument)
                {
                    // Create a new Paragraph with the same structure as defined in XAML
                    Paragraph newParagraph = new()
                    {
                        Name = "ControlGrid",
                        TextAlignment = System.Windows.TextAlignment.Left
                    };

                    // Recreate the Floater structure from XAML
                    System.Windows.Documents.Floater floater = new()
                    {
                        HorizontalAlignment = System.Windows.HorizontalAlignment.Right,
                        Width = 75,
                        Margin = new System.Windows.Thickness(0),
                        Padding = new System.Windows.Thickness(0)
                    };

                    System.Windows.Documents.BlockUIContainer buttonLocation = new()
                    {
                        Name = "ButtonLocation",
                        Margin = new System.Windows.Thickness(0),
                        Padding = new System.Windows.Thickness(0)
                    };

                    floater.Blocks.Add(buttonLocation);
                    newParagraph.Inlines.Add(floater);

                    // Replace the old paragraph with the new one
                    int index = flowDocument.Blocks.ToList().IndexOf(ControlGrid);
                    if (index >= 0)
                    {
                        flowDocument.Blocks.Remove(ControlGrid);
                        flowDocument.Blocks.Add(newParagraph);
                    }

                    // Update the reference to point to the new paragraph
                    ControlGrid = newParagraph;
                }
            });

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
                        DataEntryDateTime dateTimeControl = new(control, this)
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
                            noteAutocompletions = [];
                        }
                        DataEntryNote noteControl = new(control, noteAutocompletions, this)
                        {
                            ContentReadOnly = readOnly
                        };
                        controlToAdd = noteControl;
                        break;
                    case Control.MultiLine:
                        DataEntryMultiLine multiLineControl = new(control, this)
                        {
                            ContentReadOnly = false
                        };
                        controlToAdd = multiLineControl;
                        break;
                    case Control.AlphaNumeric:
                        Dictionary<string, string> alphaAutocompletions = [];
                        DataEntryAlphaNumeric alphaNumericControl = new(control, alphaAutocompletions, this)
                        {
                            ContentReadOnly = false
                        };
                        controlToAdd = alphaNumericControl;
                        break;
                    case Control.Flag:
                    case DatabaseColumn.DeleteFlag:
                        DataEntryFlag flagControl = new(control, this)
                        {
                            ContentReadOnly = false
                        };
                        controlToAdd = flagControl;
                        break;
                    case Control.Counter:
                        DataEntryCounter counterControl = new(control, this)
                        {
                            ContentReadOnly = false
                        };
                        controlToAdd = counterControl;
                        break;
                    case Control.IntegerAny:
                        DataEntryIntegerAny integerAnyControl = new(control, this)
                        {
                            ContentReadOnly = false
                        };
                        controlToAdd = integerAnyControl;
                        break;
                    case Control.IntegerPositive:
                        DataEntryIntegerPositive integerPositiveControl = new(control, this)
                        {
                            ContentReadOnly = false
                        };
                        controlToAdd = integerPositiveControl;
                        break;
                    case Control.DecimalAny:
                        DataEntryDecimalAny decimalAnyControl = new(control, this)
                        {
                            ContentReadOnly = false
                        };
                        controlToAdd = decimalAnyControl;
                        break;
                    case Control.DecimalPositive:
                        DataEntryDecimalPositive decimalPositiveControl = new(control, this)
                        {
                            ContentReadOnly = false
                        };
                        controlToAdd = decimalPositiveControl;
                        break;
                    case Control.FixedChoice:
                        DataEntryChoice choiceControl = new(control, this)
                        {
                            ContentReadOnly = false
                        };
                        controlToAdd = choiceControl;
                        break;
                    case Control.MultiChoice:
                        DataEntryMultiChoice multiChoiceControl = new(control, this)
                        {
                            ContentReadOnly = false
                        };
                        controlToAdd = multiChoiceControl;
                        break;
                    case Control.DateTime_:
                        DataEntryDateTimeCustom dateTimeCustomControl = new(control, this, ControlDefault.DateTimeCustomDefaultValue)
                        {
                            ContentReadOnly = false
                        };
                        controlToAdd = dateTimeCustomControl;
                        break;
                    case Control.Date_:
                        DataEntryDate dateControl = new(control, this, ControlDefault.Date_DefaultValue)
                        {
                            ContentReadOnly = false
                        };
                        controlToAdd = dateControl;
                        break;
                    case Control.Time_:
                        DataEntryTime timeControl = new(control, this, ControlDefault.Time_DefaultValue)
                        {
                            ContentReadOnly = false
                        };
                        controlToAdd = timeControl;
                        break;
                    default:
                        TracePrint.PrintMessage($"Unhandled control type {control.Type} in CreateControls.");
                        TracePrint.StackTraceToFile($"|| Unhandled control type {control.Type} in CreateControls.");
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

            // Replace the Paragraph instead of clearing Inlines to avoid serialization errors
            ControlGrid.Dispatcher.Invoke(() =>
            {
                // Get the parent FlowDocument
                if (ControlGrid.Parent is FlowDocument flowDocument)
                {
                    // Create a new Paragraph with the same structure as defined in XAML
                    Paragraph newParagraph = new()
                    {
                        Name = "ControlGrid",
                        TextAlignment = System.Windows.TextAlignment.Left
                    };

                    // Recreate the Floater structure from XAML
                    System.Windows.Documents.Floater floater = new()
                    {
                        HorizontalAlignment = System.Windows.HorizontalAlignment.Right,
                        Width = 75,
                        Margin = new System.Windows.Thickness(0),
                        Padding = new System.Windows.Thickness(0)
                    };

                    System.Windows.Documents.BlockUIContainer buttonLocation = new()
                    {
                        Name = "ButtonLocation",
                        Margin = new System.Windows.Thickness(0),
                        Padding = new System.Windows.Thickness(0)
                    };

                    floater.Blocks.Add(buttonLocation);
                    newParagraph.Inlines.Add(floater);
                    try
                    {
                        // Replace the old paragraph with the new one
                        flowDocument.Blocks.Remove(ControlGrid);
                        flowDocument.Blocks.Add(newParagraph);
                    }
                    catch (Exception e)
                    {
                        Debug.Print(e.ToString());
                        throw;
                    }
                    
                    // Update the reference to point to the new paragraph
                    ControlGrid = newParagraph;
                }
            });
        }

        #region Autocompletion methods
        // Async: for busy indicator to work: database.GetDistinctValues can be slow
        // Search controls for notes and add autocompletion values from database (error? Only for NOTES!!!)
        public async Task AutocompletionPopulateAllNotesWithFileTableValuesAsync(FileDatabase database)
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
                if (control is not DataEntryNote note)
                {
                    continue;
                }
                await Task.Run(() => note.ContentControl.AddToAutocompletions(database.GetDistinctValuesInSelectedFileTableColumn(note.DataLabel, 2)));
            }
        }

        // Try to add the values in the current note fields to the autocompletion list, assuming that it is different.
        // This is triggered whenever we show a file, where it tries to add the values of the currently displayed file
        // (the 'current file') before navigating to the new file.
        // ZZZupdate DataEntryControls: During FileShow, Search controls for notes and addd current value to autocomplete
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
                    note.ContentControl.AutoCompletionsAddValuesIfNeeded(value);
                }
            }
        }
        // Return the autocompletion list for a note identified by its datalabel
        // ZZZupdate DataEntryControls: Get autocompletion list only for a note identified by its datalabel (Error? Missing AlphaNumeric?)
        public Dictionary<string, string> AutocompletionGetForNote(string datalabel)
        {
            foreach (DataEntryControl control in Controls)
            {
                // no point in autocompleting if its read-only
                if (control.DataLabel == datalabel)
                {
                    if (control is DataEntryNote note)
                    {
                        return note.ContentControl.AutoCompletionsGetAsDictionary();
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

            // Set SuppressSelectionConfirmed on all choice controls to prevent SelectionConfirmed events during programmatic updates
            foreach (DataEntryControl control in Controls)
            {
                if (control is DataEntryChoice choice)
                {
                    choice.ContentControl.SuppressSelectionConfirmed = true;
                }
            }

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
                        // datetime.IsEnabled = false;   // We currently don't allow editing of datetime in the overview. To fix, start here: datetime.IsEnabled = (imagesSelected == 1) ? true : false;
                        // Allow DateTime editing in overview, but only when one image is selected
                        datetime.IsEnabled = imagesSelected == 1;
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
                        multiChoice.IsEnabled = imagesSelected >= 1;
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
                        string contentAndTooltip = dataEntryHandler.GetValueDisplayStringCommonToFileIds(counter.DataLabel, ControlContentStyleEnum.IntegerTextBox);
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
                        string contentAndTooltip = dataEntryHandler.GetValueDisplayStringCommonToFileIds(integerBase.DataLabel, ControlContentStyleEnum.IntegerTextBox);
                        integerBase.IsEnabled = imagesSelected >= 1;
                        // Changing a counter value in multiple images view sometimes can fail triggering
                        // a ValueChanged event, so doing a forceUpdate remedies that. See comments in SetContentAndTooltip.
                        integerBase.SetContentAndTooltip(contentAndTooltip, true);
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
                        string contentAndTooltip = dataEntryHandler.GetValueDisplayStringCommonToFileIds(decimalBase.DataLabel, ControlContentStyleEnum.DoubleTextBox);
                        decimalBase.IsEnabled = imagesSelected >= 1;
                        // Changing a counter value in multiple images view sometimes can fail triggering
                        // a ValueChanged event, so doing a forceUpdate remedies that. See comments in SetContentAndTooltip.
                        decimalBase.SetContentAndTooltip(contentAndTooltip, true);
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

            // Reset SuppressSelectionConfirmed on all choice controls to allow SelectionConfirmed events for user interactions
            foreach (DataEntryControl control in Controls)
            {
                if (control is DataEntryChoice choice)
                {
                    choice.ContentControl.SuppressSelectionConfirmed = false;
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