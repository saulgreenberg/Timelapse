using System;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using Timelapse.Constant;

namespace Timelapse.Controls
{
    /// <summary>
    /// The Status Bar convenience class that collects methods to update different parts of the status bar
    /// </summary>
    internal static class StatusBarExtensions
    {
        #region Public methods
        // Display a message in the message portion of the status bar
        public static void SetMessage(this StatusBar statusBar, string message)
        {
            StatusBarItem item = (StatusBarItem)statusBar.Items[11];
            if (item != null)
            {
                item.Content = message;
            }
        }

        // Clear the message portion of the status bar
        public static void ClearMessage(this StatusBar statusBar)
        {
            statusBar.SetMessage(string.Empty);
        }

        // Set the sequence number of the current file in the number portion of the status bar
        public static void SetCurrentFile(this StatusBar statusBar, int currentImage)
        {
            StatusBarItem item = (StatusBarItem)statusBar.Items[1];
            if (item != null) item.Content = currentImage.ToString();
        }

        // Set the total counts in the total counts portion of the status bar
        public static void SetCount(this StatusBar statusBar, int selectedImageCount)
        {
            StatusBarItem item = (StatusBarItem)statusBar.Items[3];
            if (item != null) item.Content = selectedImageCount.ToString();
        }

        // Display a view in the View portion of the status bar
        public static void SetView(this StatusBar statusBar, string view)
        {
            StatusBarItem item = (StatusBarItem)statusBar.Items[6];
            if (item != null) item.Content = view;
        }

        // Display a message in the sort portion of the status bar
        // Note that we massage the message in a few cases (e.g., for File and for Id types
        public static string SetSort(this StatusBar statusBar, string primarySortTerm, bool primarySortTermIsAscending, string secondarySortTerm, bool secondarySortTermIsAscending)
        {
            StatusBarItem item = (StatusBarItem)statusBar.Items[9];
            TextBlock message = new()
            {
                Text = string.Empty
            };

            // If there is no primary sort string, then we don't know what the sorting criteria is.
            // Note that this should not happen
            if (string.IsNullOrEmpty(primarySortTerm))
            {
                if (item != null)
                {
                    item.Content = "Unknown";
                }
                return "Unknown";
            }

            // Add the primary first key
            message.Text += SetSortAlterTextAsNeeded(primarySortTerm, primarySortTermIsAscending);

            // Add the secomdary first key if it exists
            if (!string.IsNullOrEmpty(secondarySortTerm))
            {
                message.Text += " then by ";
                message.Text += SetSortAlterTextAsNeeded(secondarySortTerm, secondarySortTermIsAscending);
            }
            //if (primarySortTerm == Constant.DatabaseColumn.RelativePath && primarySortTermIsAscending && secondarySortTerm == Constant.DatabaseColumn.DateTime && secondarySortTermIsAscending)
            //{
            //    message.Text += " (default)";
            //}
            if (item != null)
            {
                item.Content = message;
            }
            return message.Text;
        }
        #endregion

        #region Private methods
        private static string SetSortAlterTextAsNeeded(string sortTerm, bool isAscending)
        {
            // Add an up or down arrow to indicate sorting direction
            string specialCharacter = isAscending ? Unicode.UpArrow : Unicode.DownArrow;

            switch (sortTerm)
            {
                // Note that the string format Constants include the position to insert the special character.
                case DatabaseColumn.ID:
                    return String.Format(SortTermValues.IDStatusBarLabel, specialCharacter);
                case DatabaseColumn.DateTime:
                    return String.Format(SortTermValues.DateStatusBarLabel, specialCharacter);
                case DatabaseColumn.File:
                    return String.Format(SortTermValues.FileStatusBarLabel, specialCharacter);
                default:
                    return $"{sortTerm}{specialCharacter}";
            }
        }
        #endregion
    }
}
