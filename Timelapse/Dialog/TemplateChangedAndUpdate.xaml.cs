using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Web.UI;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Shapes;
using Timelapse.Database;
using Timelapse.DataTables;
using Timelapse.DebuggingSupport;
using Timelapse.Util;

namespace Timelapse.Dialog
{
    /// <summary>
    /// This dialog box is used for presenting differences in the data fields by level,
    /// that is, to let the user know that a data field has been added, deleted or renamed in a level.
    /// Timelapse cannot tell the difference between a data field that has been been deleted vs renamed, so
    /// this dialog allows the user to correctc for that. 
    /// If other differences are present that are worth reporting, Timelapse will list those as well.
    /// </summary>
    public partial class TemplateChangedAndUpdate
    {
        #region Private Variables
        private readonly string actionAdd = "Add";
        private readonly string actionDelete = "Delete";
        private bool dontClose;
        private bool areRenameCandidatesAvailable = false;
        private bool areDeleteCandidatesAvailable = false;

        private readonly Dictionary<string, Dictionary<string, string>> inDdbOnly = new Dictionary<string, Dictionary<string, string>>();
        private readonly Dictionary<string, Dictionary<string, string>> inTdbOnly = new Dictionary<string, Dictionary<string, string>>();
        private readonly List<ComboBox> comboBoxes = new List<ComboBox>();
        private readonly List<int> actionRows = new List<int>();

        private TemplateSyncResults TemplateSyncResults { get; }
        #endregion

        #region Constructor, Loaded, Closing

        private static void AddResultIfThereIsSomethingToAdd(Dictionary<string, Dictionary<string, string>> inWhichTemplate,
            string type, int level,
            Dictionary<int, Dictionary<string, string>> dataLabelsInOneButNotTheOther)
        {
            if (null != dataLabelsInOneButNotTheOther && dataLabelsInOneButNotTheOther.ContainsKey(level) && dataLabelsInOneButNotTheOther[level].Count != 0)
            {
                inWhichTemplate.Add(type, DictionaryFilterByType(dataLabelsInOneButNotTheOther[level], type));
            }
        }

        // TODO Group together controls whose types can be altered without hurting them, e.g. Note and multiline, combos, certain number types (NumPos -> NumAll) etc.
        //public TemplateChangedAndUpdate(TemplateSyncResults templateSyncResults, Window owner, DataTableBackedList<MetadataInfoRow> metadataInfo)
        //{
        //    // Check the arguments for null 
        //    ThrowIf.IsNullArgument(templateSyncResults, nameof(templateSyncResults));

        //    this.InitializeComponent();
        //    this.TemplateSyncResults = templateSyncResults;
        //    this.Owner = owner;
        //    int row = 0;
        //    // Get the maximum level in metadataInfo
        //    int maxLevel = metadataInfo.RowCount;

        //    for (int level = 0; level <= maxLevel; level++)
        //    {
        //        inDdbOnly.Clear();
        //        inTdbOnly.Clear();
        //        // Build the interface showing datalabels in terms of whether they can be added and renamed, added only, or deleted only.
        //        if (this.TemplateSyncResults.SyncRequiredAsDataLabelsDiffer())
        //        {

        //            // Collect the data labels that are only in the ddb or only in the tdb
        //            foreach (string type in Constant.Control.ControlTypes)
        //            {
        //                AddResultIfThereIsSomethingToAdd(this.inDdbOnly, type, level, this.TemplateSyncResults.DataLabelsInDdbButNotTdbByLevel);
        //                AddResultIfThereIsSomethingToAdd(this.inTdbOnly, type, level, this.TemplateSyncResults.DataLabelsInTdbButNotDdbByLevel);
        //            }

        //            // Print the level name if there are any differences
        //            if ((this.TemplateSyncResults.DataLabelsInDdbButNotTdbByLevel.ContainsKey(level) && this.TemplateSyncResults.DataLabelsInDdbButNotTdbByLevel[level].Count != 0) || 
        //                (this.TemplateSyncResults.DataLabelsInTdbButNotDdbByLevel.ContainsKey(level) && this.TemplateSyncResults.DataLabelsInTdbButNotDdbByLevel[level].Count != 0))
        //            {
        //                //row++;
        //                this.AddLevelSeparator(level, level == 0 ? "Image data" : metadataInfo[level-1].Alias, ++row);
        //            }

        //            // We want to display things ordered by type, so we iterate through each type looking 
        //            // for changes, and then display that change.
        //            foreach (string type in Constant.Control.ControlTypes)
        //            {
        //                // Changed items that can be renamed
        //                int inTdbOnlyCount = this.inTdbOnly.ContainsKey(type) ? this.inTdbOnly[type].Count : 0;
        //                int inDdbOnlyCount = this.inDdbOnly.ContainsKey(type) ? this.inDdbOnly[type].Count : 0;

        //                if (inTdbOnlyCount > 0 && inDdbOnlyCount > 0)
        //                {
        //                    // Display the ddb datalabels that can be added or renamed
        //                    foreach (string datalabel in this.inDdbOnly[type].Keys)
        //                    {
        //                        this.CreateRow(level, datalabel, type, ++row, false, this.actionDelete);
        //                        this.areDeleteCandidatesAvailable = true;
        //                    }

        //                    // Displays the tdb datalabels that can be added or renamed
        //                    foreach (string datalabel in this.inTdbOnly[type].Keys)
        //                    {
        //                        this.CreateRow(level, datalabel, type, ++row, true, this.actionAdd);
        //                    }
        //                }
        //                else if (inTdbOnlyCount > 0)
        //                {
        //                    // Displays the tdb datalabels that can be added or renamed
        //                    foreach (string datalabel in this.inTdbOnly[type].Keys)
        //                    {
        //                        this.CreateRow(level, datalabel, type, ++row, true, this.actionAdd);
        //                    }
        //                }
        //                else if (inDdbOnlyCount > 0)
        //                {
        //                    // Display the ddb datalabels that can be added or renamed
        //                    foreach (string datalabel in this.inDdbOnly[type].Keys)
        //                    {
        //                        this.CreateRow(level, datalabel, type, ++row, true, this.actionDelete);
        //                        this.areDeleteCandidatesAvailable = true;
        //                    }
        //                }

        //                //if (inTdbOnlyCount > 0 || inDdbOnlyCount > 0)
        //                //{
        //                //if (rowAdded)
        //                //{
        //                //    this.AddSeparator();
        //                //}
        //                //}
        //            }
        //        }
        //    }
        //    if (templateSyncResults.ControlSynchronizationWarningsByLevel.Count > 0)
        //    {
        //        TextBlock tb = new TextBlock();
        //        tb.Inlines.Add(new Run
        //        {
        //            FontWeight = FontWeights.Bold,
        //            FontSize = 14,
        //            Text = "Minor differences (no changes will be made to your data)"
        //        });
        //        CreateRow(tb, ++row);

        //        // Now add the warnings
        //        for (int level = 0; level <= maxLevel; level++)
        //        {
        //            if (templateSyncResults.ControlSynchronizationWarningsByLevel.ContainsKey(level))
        //            {
        //                // Print the level name if there are any differences
        //                this.AddLevelSeparator(level, level == 0 ? "Image data" : metadataInfo[level-1].Alias, ++row);
        //                foreach (string warning in templateSyncResults.ControlSynchronizationWarningsByLevel[level])
        //                {
        //                    CreateRow(warning, ++row);
        //                }
        //            }
        //        }
        //    }
        //    // Tailor the dialog message - if any renaming candidates exists, then add info to explan that.
        //    if (this.areDeleteCandidatesAvailable)
        //    {
        //        this.Message.What += $"{Environment.NewLine}This will affect what data fields you see, and could even delete some previously entered data.";
        //        this.Message.Reason += $",{Environment.NewLine}\u2022 delete data fields and any data entered into them because those fields are no longer needed.";
        //    }
        //    else
        //    {
        //        this.Message.Reason += ".";
        //    }
        //    if (this.areRenameCandidatesAvailable)
        //    {
        //        this.Message.Reason +=
        //            $",{Environment.NewLine}\u2022 rename data fields (which will preserve its previously entered data).";
        //        this.Message.Solution = $"Timelapse cannot differentiate between a renamed data field vs. adding a new data field and deleting an old one:,{Environment.NewLine}" +
        //                                $"\u2022 review and select those data fields that should be renamed instead of deleted.{Environment.NewLine}{this.Message.Solution}";
        //    }
        //    else
        //    {
        //        this.Message.Reason += ".";
        //        this.Message.Solution = $"{this.Message.Solution}";
        //    }
        //    this.Message.Reason += $"{Environment.NewLine}Alternately, you may be accidentally using a wrong (mismatched) template.";
        //}

        // Position the window relative to its parent
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            Dialogs.TryPositionAndFitDialogIntoWindow(this);
        }

        // Because the buttons automatically close the dialog, we need to cancel it if there is a warning instead.
        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (this.dontClose)
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
            if (dictionary == null)
            {
                return new Dictionary<string, string>();
            }
            return dictionary.Where(i => (i.Value == controlType)).ToDictionary(i => i.Key, i => i.Value);
        }

        // Create a single row in the grid, which displays datalabels in terms of whether they can be added and renamed, added only, or deleted only.
        private void CreateRow(TextBlock tb, int row)
        {
            // Create a new row
            RowDefinition rd = new RowDefinition
            {
                Height = GridLength.Auto //new GridLength(25)
            };
            tb.Margin = new Thickness(10, 0, 0, 0);
            this.ActionGrid.RowDefinitions.Add(rd);
            Grid.SetColumn(tb, 0);
            Grid.SetColumnSpan(tb, 5);
            Grid.SetRow(tb, row);
            this.ActionGrid.Children.Add(tb);
        }

        private void CreateRow(string text, int row)
        {
            // Create a new row
            RowDefinition rd = new RowDefinition
            {
                Height = GridLength.Auto
            };
            TextBlock tb = new TextBlock
            {
                Margin = new Thickness(10, 0, 0, 0),
            };
            tb.Inlines.Add(new Run
            {
                FontWeight = FontWeights.Normal,
                FontSize = 12,
                Text = text
            });
            this.ActionGrid.RowDefinitions.Add(rd);
            Grid.SetColumn(tb, 0);
            Grid.SetColumnSpan(tb, 5);
            Grid.SetRow(tb, row);
            this.ActionGrid.Children.Add(tb);
        }

        private void CreateRow(int level, string datalabel, string type, int row, bool addOrDeleteOnly, string action)
        {
            // Create a new row
            RowDefinition rd = new RowDefinition
            {
                Height = new GridLength(25),
                Tag=level
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
                    Padding = new Thickness(0, -3, 0, -3),
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

            foreach (string str in this.inTdbOnly[type].Keys)
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
            this.areRenameCandidatesAvailable = true;
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
                if (uiComboBox is ComboBox cb && cb.IsEnabled)
                {
                    ComboBoxItem cbi = cb.SelectedItem as ComboBoxItem;
                    if (cb.SelectedItem != null)
                    {
                        if (cbi != null)
                        {
                            selectedDataLabels.Add(cbi.Content.ToString());
                        }
                        else
                        {
                            TracePrint.NullException(nameof(cbi));
                        }
                    }
                    continue;
                }

                // If this is a Delete action row and a previously selected data label matches it, hide it. 
                if (!(this.GetUIElement(row, 2) is Label labelAction) || labelAction.Content.ToString() != this.actionAdd)
                {
                    continue;
                }

                // Retrieve the data label
                if (this.GetUIElement(row, 1) is TextBlock textblockDataLabel)
                {
                    this.ActionGrid.RowDefinitions[row].Height = selectedDataLabels.Contains(textblockDataLabel.Text) ? new GridLength(0) : new GridLength(25);
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
                    if (uiComboBox is ComboBox cb && cb.IsEnabled)
                    {
                        // The combobox is enabled, thus it's a renume
                        if (cb.SelectedItem == null || cb.SelectedItem.ToString() == string.Empty)
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

            if (problemDataLabels.Count <= 0) return true;
            // notify the user concerning the problem data labels
            MessageBox messageBox = new MessageBox("Select the new name for your 'Renamed' fields ", this)
            {
                Message =
                 {
                    Icon = MessageBoxImage.Error,
                    Problem = "You indicated that the following fields should be renamed, but did not provide the new name" + Environment.NewLine
                                + "\u2022 " + string.Join<string>(", ", problemDataLabels),
                    Solution = "For each Rename action, either" + Environment.NewLine
                                + "\u2022 use the drop down menu to provide the new name, or" + Environment.NewLine
                                + "\u2022 set the Update Action back to Delete."
                }
            };
            messageBox.ShowDialog();
            return false;
        }

        private void CollectItems()
        {
            GridLength activeGridHeight = new GridLength(25);

            foreach (int row in this.actionRows)
            {
                // Check if row is active
                if (this.ActionGrid.RowDefinitions[row].Height != activeGridHeight)
                {
                    continue;
                }

                int level = -1;
                if (this.ActionGrid.RowDefinitions[row].Tag is int i)
                {
                    level = i;
                }
                    // Retrieve the data label
                string datalabel = string.Empty;
                if (this.GetUIElement(row, 1) is TextBlock textblockDataLabel)
                {
                    datalabel = textblockDataLabel.Text;
                }

                // Retrieve the command type
                // Add action 
                if (this.GetUIElement(row, 2) is Label labelAction && labelAction.Content.ToString() == this.actionAdd)
                {
                    if (false == this.TemplateSyncResults.DataLabelsToAddByLevel.ContainsKey(level))
                    {
                        this.TemplateSyncResults.DataLabelsToAddByLevel.Add(level, new List<string>());
                    }
                    this.TemplateSyncResults.DataLabelsToAddByLevel[level].Add(datalabel);
                    continue;
                }

                // Before checking for Delete actions, we need to first check to see if it has been renamed with a valid value
                UIElement uiComboBox = this.GetUIElement(row, 4);
                if (uiComboBox != null)
                {
                    if (uiComboBox is ComboBox cb && cb.IsEnabled)
                    {
                        ComboBoxItem cbi = cb.SelectedItem as ComboBoxItem;
                        if (cb.SelectedItem != null)
                        {
                            if (cbi != null)
                            {
                                if (false == this.TemplateSyncResults.DataLabelsToRenameByLevel.ContainsKey(level))
                                {
                                    this.TemplateSyncResults.DataLabelsToRenameByLevel.Add(level, new List<KeyValuePair<string, string>>());
                                }
                                this.TemplateSyncResults.DataLabelsToRenameByLevel[level].Add(new KeyValuePair<string, string>(datalabel, cbi.Content.ToString()));
                            }
                            else
                            {
                                // Shouldn't happen. Not sure if unknown value workaround will work
                                TracePrint.NullException(nameof(cbi));
                                if (false == this.TemplateSyncResults.DataLabelsToRenameByLevel.ContainsKey(level))
                                {
                                    this.TemplateSyncResults.DataLabelsToRenameByLevel.Add(level, new List<KeyValuePair<string, string>>());
                                }
                                this.TemplateSyncResults.DataLabelsToRenameByLevel[level].Add(new KeyValuePair<string, string>(datalabel, "Unknown value"));
                            }
                            continue;
                        }
                    }
                }

                // If we arrived here, it must be an ACTION_DELETED
                if (false == this.TemplateSyncResults.DataLabelsToDeleteByLevel.ContainsKey(level))
                {
                    this.TemplateSyncResults.DataLabelsToDeleteByLevel.Add(level, new List<string>());
                }
                this.TemplateSyncResults.DataLabelsToDeleteByLevel[level].Add(datalabel);
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
                Fill = Brushes.LightGray
            };

            Grid.SetRow(rect, this.ActionGrid.RowDefinitions.Count - 1);
            Grid.SetColumn(rect, 0);
            Grid.SetColumnSpan(rect, 5);
            this.ActionGrid.Children.Add(rect);
        }
        #endregion

        // Create a level  separator in the Action Grid
        private void AddLevelSeparator(int level, string alias, int row)
        {
            RowDefinition rd = new RowDefinition
            {
                Height = new GridLength(18)
            };
            this.ActionGrid.RowDefinitions.Add(rd);
            TextBlock tb = new TextBlock
            {
                Background = Brushes.Beige,
                Margin = new Thickness(10, 0, 0, 0),
                Text = $"{alias} (Level {level})"
            };
            Grid.SetRow(tb, row);
            Grid.SetColumn(tb, 0);
            Grid.SetColumnSpan(tb, 5);
            this.ActionGrid.Children.Add(tb);
        }

        #region UI Callbacks
        // Enable or Disable the Rename comboboxdepending on the state of the Rename radio button
        private void RbRenameAction_CheckChanged(Object o, RoutedEventArgs a)
        {
            if (o is RadioButton rb)
            {
                if (rb.Tag is ComboBox cb)
                {
                    cb.IsEnabled = (rb.IsChecked == true);
                    this.ShowHideItemsAsNeeded();
                }
            }
        }

        // Check other combo box selected values to see if it matches the just-selected combobox data label item, 
        // and if so set it to empty
        private void CbRenameMenu_SelectionChanged(Object o, SelectionChangedEventArgs a)
        {
            ComboBox activeComboBox = o as ComboBox;
            if ((ComboBoxItem)activeComboBox?.SelectedItem == null)
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
                        if (cbi?.Content.ToString() == datalabelSelected)
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