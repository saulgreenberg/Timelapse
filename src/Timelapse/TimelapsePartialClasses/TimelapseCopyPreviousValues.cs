using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using Timelapse.ControlsDataEntry;
using Timelapse.Enums;
using Timelapse.Util;

// ReSharper disable once CheckNamespace
namespace Timelapse
{
    public partial class TimelapseWindow
    {
        // The methods below all relate to the CopyPreviousValues button
        #region Callbacks
        // When the the mouse enters or leaves the CopyPreviousValues button 
        // determine if the copyable control should glow, have highlighted previews of the values to be copied, or just be left in its orignal state     
        private void CopyPreviousValues_MouseEnterOrLeave(object sender, MouseEventArgs e)
        {
            CopyPreviousValuesSetEnableStatePreviewsAndGlowsAsNeeded();
        }

        private void CopyPreviousValues_LostFocus(object sender, RoutedEventArgs e)
        {
            int previousRow = (DataHandler == null || DataHandler.ImageCache == null) ? -1 : DataHandler.ImageCache.CurrentRow - 1;
            CopyPreviousValuesSetGlowAsNeeded(previousRow);
        }

        // When the CopyPreviousValues button is clicked, or when a space is entered while it is focused,
        // copy the data values from the previous control to the current one
        private void CopyPreviousValues_Click()
        {
            if (TryCopyPreviousValuesPasteValues() == false)
            {
                return;
            }

            bool isMouseOver = CopyPreviousValuesButton.IsMouseOver;
            foreach (KeyValuePair<string, DataEntryControl> pair in DataEntryControls.ControlsByDataLabelThatAreVisible)
            {
                DataEntryControl control = pair.Value;
                if (control.Copyable)
                {
                    if (isMouseOver)
                    {
                        control.FlashPreviewControlValue();
                    }
                    else
                    {
                        control.FlashContentControl(FlashEnum.UsePasteFlash);
                    }
                }
            }
        }

        // When the CopyPreviousValues button gets a space entered while it is focused,
        // make it do nothing - as otherwise it will activate it.
        private void CopyPreviousValues_PreviewKeyDown(object sender, KeyEventArgs eventArgs)
        {
            if (eventArgs.Key == Key.Space)
            {
                eventArgs.Handled = true;
            }
        }
        #endregion

        #region Methods invoked  that actually do the work
        // This should be the only method (aside from the above events) invoked from outside this file
        private void CopyPreviousValuesSetEnableStatePreviewsAndGlowsAsNeeded()
        {
            int previousRow = (DataHandler == null || DataHandler.ImageCache == null) ? -1 : DataHandler.ImageCache.CurrentRow - 1;
            // Simulate enabled / disabled by changing the foreground color. 
            // We do this instead of disabling, as we still want the CopyPreviousValuseButton to obtain the focus so it can respond to the arrow keys.
            CopyPreviousValuesButton.Foreground = previousRow >= 0 && IsDisplayingSingleImage() ? Brushes.Black : Brushes.Gray;
            CopyPreviousValueSetPreviewsAsNeeded(previousRow);
            CopyPreviousValuesSetGlowAsNeeded(previousRow);
        }
        #endregion

        #region These methods are only accessed from within this file
        // Set the glow highlight on the copyable fields if various conditions are met
        private void CopyPreviousValuesSetGlowAsNeeded(int previousRow)
        {
            try // A user reported a crash here, so we catch it to prevent the application from crashing
            {


                if (IsDisplayingSingleImage() &&
                    CopyPreviousValuesButton is { IsFocused: true, IsEnabled: true, IsMouseOver: false } &&
                    previousRow >= 0)
                {
                    // Add the glow around the copyable controls
                    DropShadowEffect effect = new()
                    {
                        Color = Colors.LightGreen,
                        Direction = 0,
                        ShadowDepth = 0,
                        BlurRadius = 5,
                        Opacity = 1
                    };
                    foreach (KeyValuePair<string, DataEntryControl> pair in DataEntryControls.ControlsByDataLabelThatAreVisible)
                    {
                        DataEntryControl control = pair.Value;
                        if (control.Copyable)
                        {
                            control.Container.Effect = effect;
                        }
                    }
                }
                else
                {
                    // Remove the glow around the copyable controls
                    foreach (KeyValuePair<string, DataEntryControl> pair in DataEntryControls.ControlsByDataLabelThatAreVisible)
                    {
                        DataEntryControl control = pair.Value;
                        control.Container.ClearValue(EffectProperty);
                    }
                }
            }
            catch
            {
                // ignored
            }
        }

        // Place highlighted previews of the values to be copied atop the copyable controls
        // e.g., if the mouse is over the CopyPrevious button and we are not on the first row
        private void CopyPreviousValueSetPreviewsAsNeeded(int previousRow)
        {
            try // A user reported a crash here, so we catch it to prevent the application from crashing
            {
                if (IsDisplayingSingleImage() &&
                    CopyPreviousValuesButton is { IsEnabled: true, IsMouseOver: true } &&
                    previousRow >= 0)
                {
                    // Show the previews on the copyable controls
                    foreach (KeyValuePair<string, DataEntryControl> pair in DataEntryControls.ControlsByDataLabelThatAreVisible)
                    {
                        DataEntryControl control = pair.Value;
                        if (control.Copyable)
                        {
                            string previewValue;
                            if (control is DataEntryDateTimeCustom)
                            {
                                previewValue = DateTimeHandler.DateTimeDatabaseStringToDisplayString(DataHandler.FileDatabase.FileTable[previousRow].GetValueDisplayString(control.DataLabel));
                            }
                            else if (control is DataEntryDate)
                            {
                                previewValue = DateTimeHandler.DateDatabaseStringToDisplayString(DataHandler.FileDatabase.FileTable[previousRow].GetValueDisplayString(control.DataLabel));
                            }
                            else if (control is DataEntryTime)
                            {
                                previewValue = DateTimeHandler.TimeDatabaseStringToDisplayString(DataHandler.FileDatabase.FileTable[previousRow].GetValueDisplayString(control.DataLabel));
                            }
                            else
                            {
                                previewValue = DataHandler.FileDatabase.FileTable[previousRow].GetValueDisplayString(control.DataLabel);
                            }
                            //string previewValue = this.DataHandler.FileDatabase.FileTable[previousRow].GetValueDisplayString(control.DataLabel);
                            control.ShowPreviewControlValue(previewValue);
                        }
                    }
                }
                else
                {
                    // Remove the preview from each control
                    foreach (KeyValuePair<string, DataEntryControl> pair in DataEntryControls.ControlsByDataLabelThatAreVisible)
                    {
                        DataEntryControl control = pair.Value;
                        if (control.Copyable)
                        {
                            control.HidePreviewControlValue();
                        }
                    }
                }
            }
            catch
            {
                // ignored
            }
        }

        // Paste the data values from the previous copyable controls to the currently displayed controls
        private bool TryCopyPreviousValuesPasteValues()
        {
            int previousRow = DataHandler.ImageCache.CurrentRow - 1;

            // This is an unneeded test as the CopyPreviousButton should be disabled if these conditions are met
            if (IsDisplayingSingleImage() == false || previousRow < 0)
            {
                return false;
            }

            FilePlayer_Stop(); // In case the FilePlayer is going
            foreach (KeyValuePair<string, DataEntryControl> pair in DataEntryControls.ControlsByDataLabelThatAreVisible)
            {
                DataEntryControl control = pair.Value;
                if (control.Copyable)
                {
                    if (control is DataEntryMultiChoice multiChoice)
                    {
                        multiChoice.IntializeMenuFromCommaSeparatedList(DataHandler.FileDatabase.FileTable[previousRow].GetValueDisplayString(control.DataLabel));
                    }
                    control.SetContentAndTooltip(DataHandler.FileDatabase.FileTable[previousRow].GetValueDisplayString(control.DataLabel));
                }
            }
            return true;
        }
        #endregion
    }
}
