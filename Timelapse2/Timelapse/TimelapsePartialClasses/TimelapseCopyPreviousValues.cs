using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using Timelapse.Controls;

namespace Timelapse
{
    public partial class TimelapseWindow : Window, IDisposable
    {
        // The methods below all relate to the CopyPreviousValues button
        #region Callbacks
        // When the the mouse enters or leaves the CopyPreviousValues button 
        // determine if the copyable control should glow, have highlighted previews of the values to be copied, or just be left in its orignal state     
        private void CopyPreviousValues_MouseEnterOrLeave(object sender, MouseEventArgs e)
        {
            this.CopyPreviousValuesSetEnableStatePreviewsAndGlowsAsNeeded();
        }

        private void CopyPreviousValues_LostFocus(object sender, RoutedEventArgs e)
        {
            int previousRow = (this.DataHandler == null || this.DataHandler.ImageCache == null) ? -1 : this.DataHandler.ImageCache.CurrentRow - 1;
            this.CopyPreviousValuesSetGlowAsNeeded(previousRow);
        }

        // When the CopyPreviousValues button is clicked, or when a space is entered while it is focused,
        // copy the data values from the previous control to the current one
        private void CopyPreviousValues_Click()
        {
            if (this.TryCopyPreviousValuesPasteValues() == false)
            {
                return;
            }

            bool isMouseOver = this.CopyPreviousValuesButton.IsMouseOver;
            foreach (KeyValuePair<string, DataEntryControl> pair in this.DataEntryControls.ControlsByDataLabel)
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
                        control.FlashContentControl();
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
            int previousRow = (this.DataHandler == null || this.DataHandler.ImageCache == null) ? -1 : this.DataHandler.ImageCache.CurrentRow - 1;
            // Simulate enabled / disabled by changing the foreground color. 
            // We do this instead of disabling, as we still want the CopyPreviousValuseButton to obtain the focus so it can respond to the arrow keys.
            this.CopyPreviousValuesButton.Foreground = previousRow >= 0 && this.IsDisplayingSingleImage() ? Brushes.Black : Brushes.Gray;
            this.CopyPreviousValueSetPreviewsAsNeeded(previousRow);
            this.CopyPreviousValuesSetGlowAsNeeded(previousRow);
        }
        #endregion

        #region These methods are only accessed from within this file
        // Set the glow highlight on the copyable fields if various conditions are met
        private void CopyPreviousValuesSetGlowAsNeeded(int previousRow)
        {
            if (this.IsDisplayingSingleImage() &&
                this.CopyPreviousValuesButton != null &&
                this.CopyPreviousValuesButton.IsFocused &&
                this.CopyPreviousValuesButton.IsEnabled == true &&
                this.CopyPreviousValuesButton.IsMouseOver == false &&
                previousRow >= 0)
            {
                // Add the glow around the copyable controls
                DropShadowEffect effect = new DropShadowEffect()
                {
                    Color = Colors.LightGreen,
                    Direction = 0,
                    ShadowDepth = 0,
                    BlurRadius = 5,
                    Opacity = 1
                };
                foreach (KeyValuePair<string, DataEntryControl> pair in this.DataEntryControls.ControlsByDataLabel)
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
                foreach (KeyValuePair<string, DataEntryControl> pair in this.DataEntryControls.ControlsByDataLabel)
                {
                    DataEntryControl control = pair.Value;
                    control.Container.ClearValue(Control.EffectProperty);
                }
            }
        }

        // Place highlighted previews of the values to be copied atop the copyable controls
        // e.g., if the mouse is over the CopyPrevious button and we are not on the first row
        private void CopyPreviousValueSetPreviewsAsNeeded(int previousRow)
        {
            if (this.IsDisplayingSingleImage() &&
                this.CopyPreviousValuesButton != null &&
                this.CopyPreviousValuesButton.IsEnabled == true &&
                this.CopyPreviousValuesButton.IsMouseOver &&
                previousRow >= 0)
            {
                // Show the previews on the copyable controls
                foreach (KeyValuePair<string, DataEntryControl> pair in this.DataEntryControls.ControlsByDataLabel)
                {
                    DataEntryControl control = pair.Value;
                    if (control.Copyable)
                    {
                        string previewValue = this.DataHandler.FileDatabase.FileTable[previousRow].GetValueDisplayString(control.DataLabel);
                        control.ShowPreviewControlValue(previewValue);
                    }
                }
            }
            else
            {
                // Remove the preview from each control
                foreach (KeyValuePair<string, DataEntryControl> pair in this.DataEntryControls.ControlsByDataLabel)
                {
                    DataEntryControl control = pair.Value;
                    if (control.Copyable)
                    {
                        control.HidePreviewControlValue();
                    }
                }
            }
        }

        // Paste the data values from the previous copyable controls to the currently displayed controls
        private bool TryCopyPreviousValuesPasteValues()
        {
            int previousRow = this.DataHandler.ImageCache.CurrentRow - 1;

            // This is an unneeded test as the CopyPreviousButton should be disabled if these conditions are met
            if (this.IsDisplayingSingleImage() == false || previousRow < 0)
            {
                return false;
            }

            this.FilePlayer_Stop(); // In case the FilePlayer is going
            foreach (KeyValuePair<string, DataEntryControl> pair in this.DataEntryControls.ControlsByDataLabel)
            {
                DataEntryControl control = pair.Value;
                if (control.Copyable)
                {
                    control.SetContentAndTooltip(this.DataHandler.FileDatabase.FileTable[previousRow].GetValueDisplayString(control.DataLabel));
                }
            }
            return true;
        }
        #endregion
    }
}
