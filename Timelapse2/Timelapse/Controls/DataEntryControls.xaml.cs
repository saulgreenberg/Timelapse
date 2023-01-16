using System;
using System.Collections.Generic;
using System.Windows.Controls;
using Timelapse.Database;
using Timelapse.Enums;
using Timelapse.Util;

namespace Timelapse.Controls
{
    /// <summary>
    /// This user control generates and displays controls based upon the information passed into it from the templateTable
    /// It is used by and displayed within the Data Entry pane.
    /// </summary>
    public partial class DataEntryControls
    {
        #region Public properties and Private Variables
        public List<DataEntryControl> Controls { get; private set; }
        public Dictionary<string, DataEntryControl> ControlsByDataLabel { get; private set; }

        private DataEntryHandler dataEntryHandler;
        #endregion

        #region Constructor
        public DataEntryControls()
        {
            this.InitializeComponent();
            this.Controls = new List<DataEntryControl>();
            this.ControlsByDataLabel = new Dictionary<string, DataEntryControl>();
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
            this.Controls.Clear();
            this.ControlsByDataLabel.Clear();
            this.ControlGrid.Inlines.Clear();

            foreach (ControlRow control in database.Controls)
            {
                // no point in generating a control if it doesn't render in the UX
                if (control.Visible == false)
                {
                    continue;
                }

                DataEntryControl controlToAdd;
                if (control.Type == Constant.DatabaseColumn.DateTime)
                {
                    DataEntryDateTime dateTimeControl = new DataEntryDateTime(control, this);
                    controlToAdd = dateTimeControl;
                }
                else if (control.Type == Constant.DatabaseColumn.File ||
                         control.Type == Constant.DatabaseColumn.RelativePath ||
                         control.Type == Constant.Control.Note)
                {
                    // standard controls rendering as notes aren't editable by the user, so we don't need autocompletions on tht 
                    Dictionary<string, string> autocompletions = null;
                    bool readOnly = control.Type != Constant.Control.Note;
                    if (readOnly == false)
                    {
                        autocompletions = new Dictionary<string, string>();
                    }
                    DataEntryNote noteControl = new DataEntryNote(control, autocompletions, this)
                    {
                        ContentReadOnly = readOnly
                    };
                    controlToAdd = noteControl;
                }
                else if (control.Type == Constant.Control.Flag || control.Type == Constant.DatabaseColumn.DeleteFlag)
                {
                    DataEntryFlag flagControl = new DataEntryFlag(control, this);
                    controlToAdd = flagControl;
                }
                else if (control.Type == Constant.Control.Counter)
                {
                    DataEntryCounter counterControl = new DataEntryCounter(control, this);
                    controlToAdd = counterControl;
                }
                else if (control.Type == Constant.Control.FixedChoice)
                {
                    DataEntryChoice choiceControl = new DataEntryChoice(control, this);
                    controlToAdd = choiceControl;
                }
                else
                {
                    TracePrint.PrintMessage(String.Format("Unhandled control type {0} in CreateControls.", control.Type));
                    continue;
                }
                this.ControlGrid.Inlines.Add(controlToAdd.Container);
                this.Controls.Add(controlToAdd);
                this.ControlsByDataLabel.Add(control.DataLabel, controlToAdd);
            }
            // Redundant check as for some reason CA1062 was still showing up as a warning.
            ThrowIf.IsNullArgument(dataEntryPropagator, nameof(dataEntryPropagator));
            dataEntryPropagator.SetDataEntryCallbacks(this.ControlsByDataLabel);
            this.dataEntryHandler = dataEntryPropagator;
        }
        #endregion

        #region Autocompletion methods
        public void AutocompletionPopulateAllNotesWithFileTableValues(FileDatabase database)
        {
            // Check the arguments for null 
            ThrowIf.IsNullArgument(database, nameof(database));

            foreach (DataEntryControl control in this.Controls)
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
            foreach (DataEntryControl control in this.Controls)
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
                        note.ContentControl.Autocompletions.Add(value, String.Empty);
                    }
                }
            }
        }
        // Return the autocompletion list for a note identified by its datalabel
        public Dictionary<string, string> AutocompletionGetForNote(string datalabel)
        {
            foreach (DataEntryControl control in this.Controls)
            {
                // no point in autocompleting if its read-only
                if (control.DataLabel == datalabel)
                {
                    if (control is DataEntryNote note)
                    {
                        return note.ContentControl.Autocompletions;
                    }
                    else
                    {
                        return null;
                    }
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
            if (this.dataEntryHandler.ImageCache.Current == null)
            {
                return;
            }
            this.dataEntryHandler.IsProgrammaticControlUpdate = true;
            foreach (DataEntryControl control in this.Controls)
            {
                if (control is DataEntryDateTime datetime)
                {
                    // DateTime
                    if (controlsToEnable == ControlsEnableStateEnum.SingleImageView)
                    {
                        // Single images view - Enable and show its contents
                        datetime.IsEnabled = true;
                        datetime.SetContentAndTooltip(this.dataEntryHandler.ImageCache.Current.GetValueDisplayString(datetime.DataLabel));
                    }
                    else
                    {
                        // Multiple images view
                        // When one image is selected, display it as enabled (but not editable) and show its value, otherwise disabled
                        // Note that if the contentAndTooltip is null (due to no value or to conflicting values), SetContentAndTooltip will display an ellipsis
                        string contentAndTooltip = this.dataEntryHandler.GetValueDisplayStringCommonToFileIds(datetime.DataLabel);
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
                        note.SetContentAndTooltip(this.dataEntryHandler.ImageCache.Current.GetValueDisplayString(note.DataLabel));
                    }
                    else
                    {
                        // Multiple images view
                        // File, Relative Path: When one image is selected, display it as enabled and editable.
                        // Notes: When one or more images are selected, display it as enabled and editable.
                        // Note that if the contentAndTooltip is null (due to no value or to conflicting values), SetContentAndTooltip will display an ellipsis
                        string contentAndTooltip = this.dataEntryHandler.GetValueDisplayStringCommonToFileIds(note.DataLabel);
                        if (control is DataEntryNote &&
                            (control.DataLabel == Constant.DatabaseColumn.File ||
                             control.DataLabel == Constant.DatabaseColumn.RelativePath))
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
                else if (control is DataEntryChoice choice)
                {
                    // Choices
                    if (controlsToEnable == ControlsEnableStateEnum.SingleImageView)
                    {
                        // Single images view - Enable and show its contents
                        choice.IsEnabled = true;
                        choice.SetContentAndTooltip(this.dataEntryHandler.ImageCache.Current.GetValueDisplayString(choice.DataLabel));
                    }
                    else
                    {
                        // Multiple images view
                        // When one or more images are selected, display it as enabled and editable.
                        // Note that if the contentAndTooltip is null (due to no value or to conflicting values), SetContentAndTooltip will display an ellipsis
                        string contentAndTooltip = this.dataEntryHandler.GetValueDisplayStringCommonToFileIds(choice.DataLabel);
                        choice.IsEnabled = (imagesSelected >= 1);
                        choice.SetContentAndTooltip(contentAndTooltip);
                    }
                }
                else if (control is DataEntryCounter counter)
                {
                    // Counters
                    if (controlsToEnable == ControlsEnableStateEnum.SingleImageView)
                    {
                        // Single images view - Enable and show its contents
                        counter.IsEnabled = true;
                        counter.SetContentAndTooltip(this.dataEntryHandler.ImageCache.Current.GetValueDisplayString(counter.DataLabel));
                    }
                    else
                    {
                        // Multiple images view
                        // When one or more images are selected, display it as enabled and editable.
                        // Note that if the contentAndTooltip is null (due to no value or to conflicting values), SetContentAndTooltip will display an ellipsis
                        string contentAndTooltip = this.dataEntryHandler.GetValueDisplayStringCommonToFileIds(counter.DataLabel);
                        counter.IsEnabled = (imagesSelected >= 1);
                        // Changing a counter value does not trigger a ValueChanged event if the values are the same.
                        // which means multiple images may not be updated even if other images have the same value.
                        // To get around this, we set a bogus value and then the real value, which means that the
                        // ValueChanged event will be triggered. Inefficient, but seems to work.
                        counter.SetBogusCounterContentAndTooltip();
                        counter.SetContentAndTooltip(contentAndTooltip);
                    }
                }
                else if (control is DataEntryFlag flag)
                {
                    // Flag, Delete Flag
                    if (controlsToEnable == ControlsEnableStateEnum.SingleImageView)
                    {
                        // Single images view - Enable and show its contents
                        flag.IsEnabled = true;
                        flag.SetContentAndTooltip(this.dataEntryHandler.ImageCache.Current.GetValueDisplayString(flag.DataLabel));
                    }
                    else
                    {
                        // Multiple images view
                        // When one or more images are selected, display it as enabled and editable.
                        // Note that if the contentAndTooltip is null (due to no value or to conflicting values), SetContentAndTooltip will display an ellipsis
                        string contentAndTooltip = this.dataEntryHandler.GetValueDisplayStringCommonToFileIds(flag.DataLabel);
                        flag.IsEnabled = (imagesSelected >= 1);
                        flag.SetContentAndTooltip(contentAndTooltip);
                    }
                }
            }
            this.dataEntryHandler.IsProgrammaticControlUpdate = false;
        }
        #endregion

        public void DisposeAsNeeded()
        {
            try
            {
                this.dataEntryHandler?.Dispose();
                this.dataEntryHandler = null;

            }
            catch
            {
                System.Diagnostics.Debug.Print("Failed in DataEntryControls-DisposeAsNeeded");
            }
        }
    }
}