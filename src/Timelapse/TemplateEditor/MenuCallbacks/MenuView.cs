using System.Windows;
using System.Windows.Controls;
using Timelapse.Dialog;
using TimelapseTemplateEditor.EditorCode;

namespace TimelapseTemplateEditor
{
    public partial class TemplateEditorWindow
    {
        #region View Menu Callbacks
        /// <summary>
        /// Depending on the menu's checkbox state, show all columns or hide selected columns
        /// </summary>
        private void MenuViewShowAllColumns_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem mi)
            {
                Visibility visibility = mi.IsChecked ? Visibility.Visible : Visibility.Collapsed;
                foreach (DataGridColumn column in TemplateUI.TemplateDataGridControl.Columns)
                {
                    if (column.Header.Equals(EditorConstant.ColumnHeader.ID) ||
                        column.Header.Equals(EditorConstant.ColumnHeader.ControlOrder) ||
                        column.Header.Equals(EditorConstant.ColumnHeader.SpreadsheetOrder))
                    {
                        column.Visibility = visibility;
                    }
                }
                Globals.TemplateDataGridControl.DoLayoutUpdated(true);
                Globals.Root.MetadataUI.ExtraColumnsVisibility(visibility);

            }
        }

        /// <summary>
        /// Show the dialog that allows a user to inspect image metadata
        /// </summary>
        private void MenuItemInspectImageMetadata_Click(object sender, RoutedEventArgs e)
        {
            FileMetadataViewFromEditor imageMetadataViewFromEditor = new(this);
            imageMetadataViewFromEditor.ShowDialog();
            State.ExifToolManager.Stop();
        }
        #endregion
    }
}
