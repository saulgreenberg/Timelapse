using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Shapes;
using Timelapse.Database;
using Timelapse.Util;

namespace Timelapse.Dialog
{
    /// <summary>
    /// Interaction logic for TemplateChangedAndUpdate.xaml
    /// </summary>
    public partial class TemplateChangedAndUpdate : Window
    {
        #region Private Variables
        private readonly string actionAdd = "Add";
        private readonly string actionDelete = "Delete";
        private bool dontClose;

        private readonly Dictionary<string, Dictionary<string, string>> inImageOnly = new Dictionary<string, Dictionary<string, string>>();
        private readonly Dictionary<string, Dictionary<string, string>> inTemplateOnly = new Dictionary<string, Dictionary<string, string>>();
        private readonly List<ComboBox> comboBoxes = new List<ComboBox>();
        private readonly List<int> actionRows = new List<int>();

        private TemplateSyncResults TemplateSyncResults { get; set; }
        #endregion

        #region Constructor, Loaded, Closing
        public TemplateChangedAndUpdate(TemplateSyncResults templateSyncResults, Window owner)
        {
            // Check the arguments for null 
            ThrowIf.IsNullArgument(templateSyncResults, nameof(templateSyncResults));

            this.InitializeComponent();
            this.TemplateSyncResults = templateSyncResults;
            this.Owner = owner;

            // Build the interface showing datalabels in terms of whether they can be added and renamed, added only, or deleted only.
            if (this.TemplateSyncResults.SyncRequiredAsDataLabelsDiffer)
            {
                this.inImageOnly.Add(Constant.Control.Note, DictionaryFilterByType(this.TemplateSyncResults.DataLabelsInImageButNotTemplateDatabase, Constant.Control.Note));
                this.inTemplateOnly.Add(Constant.Control.Note, DictionaryFilterByType(this.TemplateSyncResults.DataLabelsInTemplateButNotImageDatabase, Constant.Control.Note));

                this.inImageOnly.Add(Constant.Control.Counter, DictionaryFilterByType(this.TemplateSyncResults.DataLabelsInImageButNotTemplateDatabase, Constant.Control.Counter));
                this.inTemplateOnly.Add(Constant.Control.Counter, DictionaryFilterByType(this.TemplateSyncResults.DataLabelsInTemplateButNotImageDatabase, Constant.Control.Counter));

                this.inImageOnly.Add(Constant.Control.FixedChoice, DictionaryFilterByType(this.TemplateSyncResults.DataLabelsInImageButNotTemplateDatabase, Constant.Control.FixedChoice));
                this.inTemplateOnly.Add(Constant.Control.FixedChoice, DictionaryFilterByType(this.TemplateSyncResults.DataLabelsInTemplateButNotImageDatabase, Constant.Control.FixedChoice));

                this.inImageOnly.Add(Constant.Control.Flag, DictionaryFilterByType(this.TemplateSyncResults.DataLabelsInImageButNotTemplateDatabase, Constant.Control.Flag));
                this.inTemplateOnly.Add(Constant.Control.Flag, DictionaryFilterByType(this.TemplateSyncResults.DataLabelsInTemplateButNotImageDatabase, Constant.Control.Flag));

                int row = 0;
                string[] types = { Constant.Control.Note, Constant.Control.Counter, Constant.Control.FixedChoice, Constant.Control.Flag };
                foreach (string type in types)
                {
                    // Changed items that can be renamed
                    int inTemplateCount = this.inTemplateOnly.ContainsKey(type) ? this.inTemplateOnly[type].Count : 0;
                    int inImageOnlyCount = this.inImageOnly.ContainsKey(type) ? this.inImageOnly[type].Count : 0;

                    if (inTemplateCount > 0 && inImageOnlyCount > 0)
                    {
                        // Iterated throught the datalabels that can be added or renamed
                        foreach (string datalabel in this.inImageOnly[type].Keys)
                        {
                            row++;
                            this.CreateRow(datalabel, type, row, false, this.actionDelete);
                        }
                        // Iterated throught the datalabels that can be added or renamed
                        foreach (string datalabel in this.inTemplateOnly[type].Keys)
                        {
                            row++;
                            this.CreateRow(datalabel, type, row, true, this.actionAdd);
                        }
                    }
                    else if (inTemplateCount > 0)
                    {
                        // Iterated throught the datalabels that can be only added 
                        foreach (string datalabel in this.inTemplateOnly[type].Keys)
                        {
                            row++;
                            this.CreateRow(datalabel, type, row, true, this.actionAdd);
                        }
                    }
                    else if (inImageOnlyCount > 0)
                    {
                        // Iterated throught the datalabels that can be only deleted 
                        foreach (string datalabel in this.inImageOnly[type].Keys)
                        {
                            row++;
                            this.CreateRow(datalabel, type, row, true, this.actionDelete);
                        }
                    }
                    if (inTemplateCount > 0 || inImageOnlyCount > 0)
                    {
                        row++;
                        this.AddSeparator();
                    }
                }
            }
            if (templateSyncResults.ControlSynchronizationWarnings.Count > 0)
            {
                this.TextBlockDetails.Inlines.Add(Environment.NewLine);
                this.TextBlockDetails.Inlines.Add(new Run { FontWeight = FontWeights.Bold, Text = "Additional Warnings" });

                foreach (string warning in templateSyncResults.ControlSynchronizationWarnings)
                {
                    this.TextBlockDetails.Inlines.Add(Environment.NewLine);
                    this.TextBlockDetails.Inlines.Add(new Run { FontWeight = FontWeights.Normal, Text = warning });
                }
            }
        }

        // Position the window relative to its parent
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            Dialogs.TryPositionAndFitDialogIntoWindow(this);
        }

        // Because the buttons automatically close the dialog, we need to cancel it if there is a warning instead.
        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (this.dontClose == true)
            {
                e.Cancel = true;
            }
            this.dontClose = false;
        }
        #endregion

        #region Private Methods
        // Get a subset of the dictionary filtered by the type of control
        private static Dictionary<string, string> DictionaryFilterByType(Dictionary<string, string> dictionary, string controlType)
        {
            return dictionary.Where(i => (i.Value == controlType)).ToDictionary(i => i.Key, i => i.Value);
        }

        // Create a single row in the grid, which displays datalabels in terms of whether they can be added and renamed, added only, or deleted only.
        private void CreateRow(string datalabel, string type, int row, bool addOrDeleteOnly, string action)
        {
            // Create a new row
            RowDefinition rd = new RowDefinition
            {
                Height = new GridLength(30)
            };
            this.ActionGrid.RowDefinitions.Add(rd);
            this.actionRows.Add(row);

            // Type
            TextBlock textblockType = new TextBlock
            {
                Text = type,
                Margin = new Thickness(20, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(textblockType, 0);
            Grid.SetRow(textblockType, row);
            this.ActionGrid.Children.Add(textblockType);

            // Data label
            TextBlock textblockDataLabel = new TextBlock
            {
                Text = datalabel,
                Margin = new Thickness(10, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(textblockDataLabel, 1);
            Grid.SetRow(textblockDataLabel, row);
            this.ActionGrid.Children.Add(textblockDataLabel);

            // Add or Delete command without renaming
            if (addOrDeleteOnly)
            {
                Label labelActionDefaultAction = new Label
                {
                    Tag = rd,
                    Content = action,
                    Margin = new Thickness(10, 0, 0, 0),
                    VerticalAlignment = VerticalAlignment.Center
                };
                Grid.SetColumn(labelActionDefaultAction, 2);
                Grid.SetRow(labelActionDefaultAction, row);
                this.ActionGrid.Children.Add(labelActionDefaultAction);
                return;
            }

            // Add command with renaming
            RadioButton radiobuttonActionDefaultAction = new RadioButton
            {
                GroupName = datalabel,
                Content = action,
                Margin = new Thickness(10, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center,
                IsChecked = true
            };
            Grid.SetColumn(radiobuttonActionDefaultAction, 2);
            Grid.SetRow(radiobuttonActionDefaultAction, row);
            this.ActionGrid.Children.Add(radiobuttonActionDefaultAction);

            // Combobox showing renaming possibilities

            ComboBox comboboxRenameMenu = new ComboBox
            {
                Width = double.NaN,
                Height = 25,
                Margin = new Thickness(10, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center,
                MinWidth = 150,
                IsEnabled = false
            };
            foreach (string str in this.inTemplateOnly[type].Keys)
            {
                ComboBoxItem item = new ComboBoxItem
                {
                    Content = str,
                    IsEnabled = true
                };
                comboboxRenameMenu.Items.Add(item);
            }

            Grid.SetColumn(comboboxRenameMenu, 4);
            Grid.SetRow(comboboxRenameMenu, row);
            this.ActionGrid.Children.Add(comboboxRenameMenu);
            this.comboBoxes.Add(comboboxRenameMenu);

            comboboxRenameMenu.SelectionChanged += this.CbRenameMenu_SelectionChanged;

            RadioButton radiobuttonRenameAction = new RadioButton
            {
                GroupName = datalabel,
                Content = "Rename to",
                Margin = new Thickness(10, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center,
                Tag = comboboxRenameMenu
            };
            Grid.SetColumn(radiobuttonRenameAction, 3);
            Grid.SetRow(radiobuttonRenameAction, row);
            this.ActionGrid.Children.Add(radiobuttonRenameAction);

            // Enable and disable the combobox depending upon which radiobutton is selected
            radiobuttonRenameAction.Checked += this.RbRenameAction_CheckChanged;
            radiobuttonRenameAction.Unchecked += this.RbRenameAction_CheckChanged;
        }

        // For each row, if it contains an enabled rename combobox, then collect its currently selected datalabel (if any)
        // For other rows, if it is a 'Deleted' row, hide or show it depending if it matches one of the currently selected datalabels
        // Note that this is fragile, as it depends on various UI Elements being in various columns and row orders 
        // - eg., arranged by type with delete after renames.
        // Also, collect all the datalabels to add, delete and rename
        private void ShowHideItemsAsNeeded()
        {
            List<string> selectedDataLabels = new List<string>();

            foreach (int row in this.actionRows)
            {
                // Retrieve selected items, but only if the rename radio button is enabled and checked
                // retrieve selected items, but only if the rename radio button is checked
                UIElement uiComboBox = this.GetUIElement(row, 4);
                if (uiComboBox != null)
                {
                    if (uiComboBox is ComboBox cb && cb.IsEnabled == true)
                    {
                        ComboBoxItem cbi = cb.SelectedItem as ComboBoxItem;
                        if (cb.SelectedItem != null)
                        {
                            selectedDataLabels.Add(cbi.Content.ToString());
                        }
                        continue;
                    }
                }

                // If this is a Delete action row and a previously selected data label matches it, hide it. 
                if (!(this.GetUIElement(row, 2) is Label labelAction) || labelAction.Content.ToString() != this.actionAdd)
                {
                    continue;
                }

                // Retrieve the data label
                if (this.GetUIElement(row, 1) is TextBlock textblockDataLabel)
                {
                    this.ActionGrid.RowDefinitions[row].Height = selectedDataLabels.Contains(textblockDataLabel.Text) ? new GridLength(0) : new GridLength(30);
                }
            }
        }

        // For each row, if it contains an enabled rename combobox, check to see that it actually has a valid menu item selected.
        // If not, display a warning message.
        private bool AreRenamedEntriesValid()
        {
            List<string> problemDataLabels = new List<string>();

            foreach (int row in this.actionRows)
            {
                // We are only interested in Renamed items, which would only occur if the combobox is enabeld
                UIElement uiComboBox = this.GetUIElement(row, 4);
                if (uiComboBox != null)
                {
                    if (uiComboBox is ComboBox cb && cb.IsEnabled == true)
                    {
                        // The combobox is enabled, thus it's a renume
                        if (cb.SelectedItem == null || cb.SelectedItem.ToString() == String.Empty)
                        {
                            // Retrieve the data label and add it as an problem 
                            if (this.GetUIElement(row, 1) is TextBlock textblockDataLabel)
                            {
                                problemDataLabels.Add(textblockDataLabel.Text);
                            }
                        }
                    }
                }
            }
            if (problemDataLabels.Count > 0)
            {
                // notify the user concerning the problem data labels
                MessageBox messageBox = new MessageBox("Select the new name for your 'Renamed' fields ", this);
                messageBox.Message.Icon = MessageBoxImage.Error;
                messageBox.Message.Problem = "You indicated that the following fields should be renamed, but did not provide the new name" + Environment.NewLine;
                messageBox.Message.Problem += "\u2022 " + string.Join<string>(", ", problemDataLabels);
                messageBox.Message.Solution = "For each Rename action, either" + Environment.NewLine;
                messageBox.Message.Solution += "\u2022 use the drop down menu to provide the new name, or" + Environment.NewLine;
                messageBox.Message.Solution += "\u2022 set the Update Action back to Delete.";
                messageBox.ShowDialog();
                return false;
            }
            return true;
        }

        private void CollectItems()
        {
            GridLength activeGridHeight = new GridLength(30);

            foreach (int row in this.actionRows)
            {
                // Check if row is active
                if (this.ActionGrid.RowDefinitions[row].Height != activeGridHeight)
                {
                    continue;
                }

                // Retrieve the data label
                string datalabel = String.Empty;
                if (this.GetUIElement(row, 1) is TextBlock textblockDataLabel)
                {
                    datalabel = textblockDataLabel.Text;
                }

                // Retrieve the command type
                // Add action 
                if (this.GetUIElement(row, 2) is Label labelAction && labelAction.Content.ToString() == this.actionAdd)
                {
                    this.TemplateSyncResults.DataLabelsToAdd.Add(datalabel);
                    continue;
                }

                // Before checking for Delete actions, we need to first check to see if it has been renamed with a valid value
                UIElement uiComboBox = this.GetUIElement(row, 4);
                if (uiComboBox != null)
                {
                    if (uiComboBox is ComboBox cb && cb.IsEnabled == true)
                    {
                        ComboBoxItem cbi = cb.SelectedItem as ComboBoxItem;
                        if (cb.SelectedItem != null)
                        {
                            this.TemplateSyncResults.DataLabelsToRename.Add(new KeyValuePair<string, string>(datalabel, cbi.Content.ToString()));
                            continue;
                        }
                    }
                }

                // If we arrived here, it must be an ACTION_DELETED
                this.TemplateSyncResults.DataLabelsToDelete.Add(datalabel);
            }
        }
        // Get the UI Element in the indicated row and column from the Action Grid.
        // returns null if no such element exists.
        private UIElement GetUIElement(int row, int column)
        {
            return this.ActionGrid.Children
                   .Cast<UIElement>()
                   .FirstOrDefault(e => Grid.GetRow(e) == row && Grid.GetColumn(e) == column);
        }

        // Create a grey line separator in the Action Grid
        private void AddSeparator()
        {
            RowDefinition rd = new RowDefinition
            {
                Height = new GridLength(1)
            };
            this.ActionGrid.RowDefinitions.Add(rd);

            Rectangle rect = new Rectangle
            {
                Fill = Brushes.Gray
            };

            Grid.SetRow(rect, this.ActionGrid.RowDefinitions.Count - 1);
            Grid.SetColumn(rect, 0);
            Grid.SetColumnSpan(rect, 5);
            this.ActionGrid.Children.Add(rect);
        }
        #endregion

        #region UI Callbacks
        // Enable or Disable the Rename comboboxdepending on the state of the Rename radio button
        private void RbRenameAction_CheckChanged(Object o, RoutedEventArgs a)
        {
            RadioButton rb = o as RadioButton;
            ComboBox cb = rb.Tag as ComboBox;
            cb.IsEnabled = (rb.IsChecked == true);
            this.ShowHideItemsAsNeeded();
        }

        // Check other combo box selected values to see if it matches the just-selected combobox data label item, 
        // and if so set it to empty
        private void CbRenameMenu_SelectionChanged(Object o, SelectionChangedEventArgs a)
        {
            ComboBox activeComboBox = o as ComboBox;
            if ((ComboBoxItem)activeComboBox.SelectedItem == null)
            {
                return;
            }
            ComboBoxItem selecteditem = (ComboBoxItem)activeComboBox.SelectedItem;
            string datalabelSelected = selecteditem.Content.ToString();
            foreach (ComboBox combobox in this.comboBoxes)
            {
                if (activeComboBox != combobox)
                {
                    if (combobox.SelectedItem != null)
                    {
                        ComboBoxItem cbi = combobox.SelectedItem as ComboBoxItem;
                        if (cbi.Content.ToString() == datalabelSelected)
                        {
                            combobox.SelectedIndex = -1;
                        }
                    }
                }
            }
            this.ShowHideItemsAsNeeded();
        }

        private void UseOldTemplate_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
        }

        private void UseNewTemplateButton_Click(object sender, RoutedEventArgs e)
        {
            if (this.AreRenamedEntriesValid() == false)
            {
                e.Handled = true;
                this.dontClose = true;
                return;
            }
            this.CollectItems();
            this.DialogResult = true;
        }
        #endregion
    }
}