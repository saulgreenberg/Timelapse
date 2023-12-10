using System.Windows;
using System.Windows.Controls;
using TimelapseTemplateEditor.Dialog;

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
                foreach (DataGridColumn column in this.TemplateUI.TemplateDataGridControl.DataGrid.Columns)
                {
                    if (column.Header.Equals(EditorConstant.ColumnHeader.ID) ||
                        column.Header.Equals(EditorConstant.ColumnHeader.ControlOrder) ||
                        column.Header.Equals(EditorConstant.ColumnHeader.SpreadsheetOrder))
                    {
                        column.Visibility = visibility;
                    }
                }
            }
        }

        /// <summary>
        /// Show the dialog that allows a user to inspect image metadata
        /// </summary>
        private void MenuItemInspectImageMetadata_Click(object sender, RoutedEventArgs e)
        {
            InspectMetadata inspectMetadata = new InspectMetadata(this);
            inspectMetadata.ShowDialog();
            this.State.ExifToolManager.Stop();
        }
        #endregion
    }
}
