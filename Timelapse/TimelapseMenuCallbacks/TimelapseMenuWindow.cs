using System.Windows;
using Timelapse.Controls;
using Timelapse.DebuggingSupport;
using Timelapse.Enums;
using Timelapse.Extensions;
using MenuItem = System.Windows.Controls.MenuItem;

// ReSharper disable once CheckNamespace
namespace Timelapse
{
    // Window Menu Callbacks
    public partial class TimelapseWindow
    {
        // Each menu item has a tag in it (defined in the XAML) that corresponds to a Registry Key name. 
        // That tag is then used to save or restore a particular layout.

        #region Window - Restore sub-menu opening
        private void MenuItemWindowLoadCustom_SubmenuOpening(object sender, RoutedEventArgs e)
        {
            this.FilePlayer_Stop(); // In case the FilePlayer is going
            this.MenuItemWindowCustom1Load.IsEnabled = this.State.IsRegistryKeyExists(Constant.AvalonLayoutTags.Custom1);
            this.MenuItemWindowCustom2Load.IsEnabled = this.State.IsRegistryKeyExists(Constant.AvalonLayoutTags.Custom2);
            this.MenuItemWindowCustom3Load.IsEnabled = this.State.IsRegistryKeyExists(Constant.AvalonLayoutTags.Custom3);
        }
        #endregion

        #region Restore a particular window layout as identified in the menu's tag
        private void MenuItemWindowRestore_Click(object sender, RoutedEventArgs e)
        {
            if (!(sender is MenuItem mi))
            {
                TracePrint.NullException(nameof(sender));
                return;
            }
            string layout = mi.Tag.ToString();
            this.AvalonLayout_TryLoad(layout);

            if (null != this.DataHandler?.FileDatabase?.FileSelectionEnum)
            {
                // Change the title of the data panel to indicate the selection
                string status;
                switch (this.DataHandler?.FileDatabase?.FileSelectionEnum)
                {
                    case FileSelectionEnum.All:
                        status = "All files";
                        break;
                    case FileSelectionEnum.Custom:
                        status = "Custom selection";
                        break;
                    case FileSelectionEnum.MarkedForDeletion:
                        status = "Files marked for deletion";
                        break;
                    case FileSelectionEnum.Folders:
                        status = "Files in a specific folder";
                        break;
                    case FileSelectionEnum.Missing:
                        status = "Missing files";
                        break;
                    default:
                        status = "";
                        break;
                }

                // Show feedback of the status description in both the status bar and the data entry control panel title
                this.StatusBar.SetView(status);
                this.DataEntryControlPanel.Title = "Image data ";
                if (false == string.IsNullOrWhiteSpace(status))
                {
                    this.DataEntryControlPanel.Title += $"({status} selected)";
                }
            }

            // If an image set is currently loaded, make the image set pane the active pane
            if (this.IsFileDatabaseAvailable())
            {
                this.ImageSetPane.IsActive = true;
            }
            else
            {
                this.InstructionPane.IsActive = true;
            }
        }
        #endregion

        #region Save a particular window layout, where the layout name is identified in the menu's tag

        private void MenuItemWindowSave_Click(object sender, RoutedEventArgs e)
        {
            // Save the window layout to the registry, where the registry key name is found in the menu tag
            // Note that the data entry control panel must be visible in order to save its location.
            // So if its not visible, temporarily make it visible.
            if (!(sender is MenuItem mi))
            {
                TracePrint.NullException(nameof(sender));
                return;
            }

            bool visibilityState = this.DataEntryControlPanel.IsVisible;
            this.DataEntryControlPanel.IsVisible = true;
            this.AvalonLayout_TrySave(mi.Tag.ToString());
            this.DataEntryControlPanel.IsVisible = visibilityState;
        }
    }
    #endregion
}

