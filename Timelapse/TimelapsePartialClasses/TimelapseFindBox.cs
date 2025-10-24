﻿using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;
using Timelapse.DataTables;

// ReSharper disable once CheckNamespace
namespace Timelapse
{
    // Find Box event handlers and helpers
    public partial class TimelapseWindow
    {
        #region Callbacks
        // KeyDown: find forward on enter
        private void FindBoxTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                FindBox_FindImage(true);
            }
        }

        // Click: Depending upon which button was pressed, invoke a forwards or backwards find operation 
        private void FindBoxButton_Click(object sender, RoutedEventArgs e)
        {
            Button findButton = sender as Button;
            bool isForward = (findButton == FindForwardButton);
            FindBox_FindImage(isForward);
        }

        // Close
        private void FindBoxClose_Click(object sender, RoutedEventArgs e)
        {
            FindBoxSetVisibility(false);
        }
        #endregion

        #region Methods
        // Adjust Find Box visibility
        // Note: Invoked from above and from other files
        private void FindBoxSetVisibility(bool isVisible)
        {
            // Only make the find box visible if there are files to view
            if (FindBox != null && IsFileDatabaseAvailable() && DataHandler.FileDatabase.CountAllCurrentlySelectedFiles > 0)
            {
                FindBox.IsOpen = isVisible;
                FindBoxTextBox.Focus();
            }
        }
        #endregion

        #region Helper methods used only here
        // Search either forwards or backwards for the image file name specified in the text box
        // Only invoked by the above
        private void FindBox_FindImage(bool isForward)
        {
            string searchTerm = FindBoxTextBox.Text;
            ImageRow row = DataHandler.ImageCache.Current;

            int currentIndex = DataHandler.FileDatabase.FileTable.IndexOf(row);
            int foundIndex = DataHandler.FileDatabase.FindByFileName(currentIndex, isForward, searchTerm);
            if (foundIndex != -1)
            {
                FileShow(foundIndex);
            }
            else
            {
                // Flash the text field to indicate no result
                if (FindResource("ColorAnimationBriefFlash") is Storyboard sb)
                {
                    Storyboard.SetTarget(sb, FindBoxTextBox);
                    sb.Begin();
                }
            }
        }
        #endregion
    }
}
