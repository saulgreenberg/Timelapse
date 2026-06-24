using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Timelapse.Constant;
using Timelapse.Database;
using Timelapse.DataTables;
using Timelapse.Util;
using TimelapseWpf.Toolkit;
using Control = Timelapse.Constant.Control;
using MessageBox = System.Windows.MessageBox;

namespace Timelapse.Dialog
{
    public class DatabaseSchemaMismatchResult
    {
        public bool UserChoseRepair { get; init; }
        public bool AbortLoad { get; init; }
        public List<string> LabelsToAdd { get; init; } = [];
        public List<string> ColumnsToDelete { get; init; } = [];
        public List<(string OldName, string NewName)> ColumnsToRename { get; init; } = [];
    }

    public partial class DatabaseSchemaMismatchDialog : Window
    {
        public DatabaseSchemaMismatchResult Result { get; private set; }

        private readonly List<string> _missingLabels;
        private readonly List<string> _extraColumns;
        private readonly FileDatabase _fileDatabase;
        private readonly bool _hasMissing;
        private readonly bool _hasExtra;
        private readonly bool _renameAvailable;

        // Per-row tracking
        private readonly List<(int Row, string ExtraColumn, RadioButton RbDelete, RadioButton RbRename, ComboBox CbTarget, TextBlock WarnText)> _extraRows = [];
        private readonly List<(int Row, string DataLabel)> _missingRows = [];
        private readonly List<ComboBox> _allComboBoxes = [];
        private int _nextGridRow = 1; // row 0 is headers

        public DatabaseSchemaMismatchDialog(Window owner, List<string> missingLabels, List<string> extraColumns, FileDatabase fileDatabase)
        {
            InitializeComponent();
            Owner = owner;
            _missingLabels = missingLabels;
            _extraColumns = extraColumns;
            _fileDatabase = fileDatabase;
            _hasMissing = missingLabels.Count > 0;
            _hasExtra = extraColumns.Count > 0;
            _renameAvailable = _hasMissing && _hasExtra;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            Dialogs.TryPositionAndFitDialogIntoWindow(this);
            BuildHeaderMessage();
            FormattedDialogHelper.SetupStaticReferenceResolver(Message);
            Message.BuildContentFromProperties();

            if (_hasMissing)
                BuildMissingColumnRows();
            if (_hasExtra)
                BuildExtraColumnRows();

            // Show "Continue loading" only when there are extra columns but no missing ones —
            // loading is safe without repair in that case.
            if (_hasExtra && !_hasMissing)
                ContinueLoadingButton.Visibility = Visibility.Visible;
        }

        #region Header message
        private void BuildHeaderMessage()
        {
            Message.Icon = DialogIconType.Error;
            Message.DialogTitle = "Your Timelapse database (.ddb) file has issues";

            var problem = new System.Text.StringBuilder();
            if (_hasMissing)
            {
                int n = _missingLabels.Count;
                string colWord = n == 1 ? "column" : "columns";
                string verbExist1 = n == 1 ? "exists" : "exist";
                problem.Append($"[li] [e]Missing data {colWord}: [/e] [b]{n}[/b] template {colWord} definition {verbExist1} but there is no matching data {colWord}.");
            }
            if (_hasExtra)
            {
                if (problem.Length > 0) problem.Append("[br 2]");
                int n = _extraColumns.Count;
                string colWord = n == 1 ? "column" : "columns";
                string verbExist2 = n == 1 ? "exists" : "exist";
                problem.Append($"[li] [e]Extra data {colWord}:[/e]      [b]{n}[/b] data {colWord} {verbExist2} but no template {colWord} definitions match it.");
            }
            Message.Problem = "The data columns that hold your data do not match the description in your template.[br 2]In particular (and as detailed lower down):"
                + problem;

            Message.Reason = "The template defines a schema (a structure) for how your data is stored in the database. Timelapse always checks for template changes, and restructures your data to match your template if needed. " +
                             "[br]Mismatches between your template and data are very rare, but can happen if:"
                           + "[li] Timelapse was forcibly closed while updating its template and data (e.g., power failure, network or file server glitch, crash...)"
                           + "[li] the database became corrupted (also rare)"
                           + "[li] the database file was manually edited to introduce incompatibilities.";

            var solution = new System.Text.StringBuilder("Do one of the following.");
            solution.Append("[ni] [b] Check your backup files[/b] in the[e]Backup[/ e] folder to see if a recently saved version is available.");
            solution.Append ("[ni] [b]Repair[/b] the database (a backup of this damaged file will be made)");
            if (_hasMissing && _hasExtra)
            {
                solution.Append("[li 2] [e]Rename action:[/e] For extra data, its possible that the mismatch is just a renaming issue i.e., where that extra column and a missing column correspond. If so, use the [e]Rename[/e] option and select the matching missing column");
            }

            if (_hasExtra)
            {
                solution.Append("[li 2] [e]Delete action:[/e] If you choose to delete an extra data column, its data will be deleted as well.");
            }

            if (_hasMissing)
            {
                solution.Append("[li 2] [e]Add action:[/e] If you choose to Add a missing data column, it will be filled in with defaults as defined in your template.");
            }

            if (_hasExtra && _hasMissing == false)
            {
                solution.Append("[br][ni] [b]Continue loading [/b] to load anyways. While each extra data column will remain, you won't see a data field associated with it.");
            }
            solution.Append("[br][ni] [b]Cancel[/b] to abort loading of this image set (no changes will be made).");
            Message.Solution = solution.ToString();

            Message.Hint = $"If you are stuck, email [link: mailto:{ExternalLinks.EmailAddress}|{ExternalLinks.EmailAddress}] and explain what happened. "
                         + "He will likely ask for a copy of your [i].ddb[/i] and [i].tdb[/i] files.";
        }
        #endregion

        #region Build extra-column rows
        private void BuildExtraColumnRows()
        {
            AddSeparatorRow("Extra data columns — exist in the database but have no matching entry in the template");

            List<string> markerColumns = _fileDatabase.SchemaGetColumns(DBTables.Markers) ?? [];

            foreach (string col in _extraColumns)
            {
                AddGridRow(28);
                int row = _nextGridRow - 1;

                string type = markerColumns.Contains(col) ? Control.Counter : "—";
                AddCell(row, 0, type, margin: new(0, 0, 20, 0));
                AddCell(row, 1, col, bold: true, margin: new(0, 0, 20, 0));

                // Data-loss warning — hidden when Rename is selected, visible when Delete is selected
                var warn = new TextBlock
                {
                    Text = "⚠ Any stored data will be permanently lost if deleted",
                    Foreground = Brushes.DarkRed,
                    FontStyle = FontStyles.Italic,
                    FontSize = 12,
                    Margin = new(0, 0, 30, 0),
                    VerticalAlignment = VerticalAlignment.Center,
                    Visibility = Visibility.Visible
                };
                Grid.SetRow(warn, row); Grid.SetColumn(warn, 2);
                ActionGrid.Children.Add(warn);

                if (_renameAvailable)
                {
                    // Build ComboBox first so it can be passed as Tag to the Rename radio button
                    var cb = new ComboBox
                    {
                        MinWidth = 200, MaxWidth = 300, Height = 24,
                        IsEnabled = false,
                        Margin = new(10, 0, 0, 0),
                        VerticalAlignment = VerticalAlignment.Center,
                        HorizontalAlignment = HorizontalAlignment.Left
                    };
                    foreach (string label in _missingLabels)
                    {
                        ControlRow ctrl = _fileDatabase.GetControlFromControls(label);
                        string typeStr = ctrl?.Type ?? "?";
                        string defStr = string.IsNullOrEmpty(ctrl?.DefaultValue) ? "no default" : $"default: {ctrl.DefaultValue}";
                        cb.Items.Add(new ComboBoxItem
                        {
                            Content = label,
                            Tag = label,
                            ToolTip = $"{typeStr} — {defStr}"
                        });
                    }
                    Grid.SetRow(cb, row); Grid.SetColumn(cb, 5);
                    ActionGrid.Children.Add(cb);
                    cb.SelectionChanged += Cb_SelectionChanged;
                    _allComboBoxes.Add(cb);

                    var rbDelete = new RadioButton
                    {
                        GroupName = $"extra_{col}",
                        Content = "Delete",
                        IsChecked = true,
                        Margin = new(0, 0, 20, 0),
                        VerticalAlignment = VerticalAlignment.Center
                    };
                    Grid.SetRow(rbDelete, row); Grid.SetColumn(rbDelete, 3);
                    ActionGrid.Children.Add(rbDelete);

                    var rbRename = new RadioButton
                    {
                        GroupName = $"extra_{col}",
                        Content = "Rename to:",
                        Tag = cb,
                        Margin = new(0, 0, 10, 0),
                        VerticalAlignment = VerticalAlignment.Center
                    };
                    Grid.SetRow(rbRename, row); Grid.SetColumn(rbRename, 4);
                    ActionGrid.Children.Add(rbRename);
                    rbRename.Checked += RbRename_Checked;
                    rbRename.Unchecked += RbRename_Unchecked;

                    _extraRows.Add((row, col, rbDelete, rbRename, cb, warn));
                }
                else
                {
                    // No missing columns exist, so rename is not an option — just show the Delete label
                    AddCell(row, 3, "Delete", bold: true, foreground: Brushes.DarkRed);
                    _extraRows.Add((row, col, null, null, null, warn));
                }
            }
        }
        #endregion

        #region Build missing-column rows
        private void BuildMissingColumnRows()
        {
            AddSeparatorRow("Missing data columns — defined in the template but absent from the database");

            foreach (string label in _missingLabels)
            {
                AddGridRow(28);
                int row = _nextGridRow - 1;

                ControlRow ctrl = _fileDatabase.GetControlFromControls(label);
                string typeStr = ctrl?.Type ?? "?";
                string defStr = string.IsNullOrEmpty(ctrl?.DefaultValue) ? "(empty)" : ctrl.DefaultValue;

                AddCell(row, 0, typeStr, margin: new(0, 0, 20, 0));
                AddCell(row, 1, label, bold: true, margin: new(0, 0, 20, 0));
                AddCell(row, 2, $"Default: {defStr}", italic: true, margin: new(0, 0, 30, 0));
                AddCell(row, 3, "Add", bold: true, foreground: Brushes.DarkGreen);

                _missingRows.Add((row, label));
            }
        }
        #endregion

        #region Grid helpers
        private void AddSeparatorRow(string text)
        {
            AddGridRow(26);
            int row = _nextGridRow - 1;
            var tb = new TextBlock
            {
                Text = text,
                FontWeight = FontWeights.Medium,
                FontStyle = FontStyles.Italic,
                FontSize = 12,
                Background = Brushes.LightSteelBlue,
                Padding = new(4, 3, 0, 3),
                Margin = new(0, 6, 0, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetRow(tb, row);
            Grid.SetColumn(tb, 0);
            Grid.SetColumnSpan(tb, 6);
            ActionGrid.Children.Add(tb);
        }

        private void AddGridRow(double height)
        {
            ActionGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(height) });
            _nextGridRow++;
        }

        private void AddCell(int row, int col, string text, bool bold = false, bool italic = false,
            Brush foreground = null, Thickness? margin = null)
        {
            var tb = new TextBlock
            {
                Text = text,
                FontWeight = bold ? FontWeights.Bold : FontWeights.Normal,
                FontStyle = italic ? FontStyles.Italic : FontStyles.Normal,
                FontSize = 12,
                Foreground = foreground ?? SystemColors.ControlTextBrush,
                Margin = margin ?? new(0, 0, 20, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetRow(tb, row);
            Grid.SetColumn(tb, col);
            ActionGrid.Children.Add(tb);
        }
        #endregion

        #region ComboBox and RadioButton callbacks
        private void RbRename_Checked(object sender, RoutedEventArgs e)
        {
            if (sender is RadioButton { Tag: ComboBox cb })
                cb.IsEnabled = true;
            foreach (var (_, _, _, rbRen, _, warn) in _extraRows)
            {
                if (rbRen == sender) warn.Visibility = Visibility.Hidden;
            }
            UpdateMissingRowVisibility();
        }

        private void RbRename_Unchecked(object sender, RoutedEventArgs e)
        {
            if (sender is RadioButton { Tag: ComboBox cb })
            {
                cb.IsEnabled = false;
                cb.SelectedIndex = -1;
            }
            foreach (var (_, _, _, rbRen, _, warn) in _extraRows)
            {
                if (rbRen == sender) warn.Visibility = Visibility.Visible;
            }
            UpdateMissingRowVisibility();
        }

        private void Cb_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is not ComboBox activeCb) return;

            // Mutual exclusion: clear any other combobox that holds the same selection
            if (activeCb.SelectedItem is ComboBoxItem selectedCbi && selectedCbi.Tag is string selectedLabel)
            {
                foreach (ComboBox cb in _allComboBoxes)
                {
                    if (cb != activeCb && cb.SelectedItem is ComboBoxItem cbi && cbi.Tag as string == selectedLabel)
                        cb.SelectedIndex = -1;
                }
            }
            UpdateMissingRowVisibility();
        }

        private void UpdateMissingRowVisibility()
        {
            HashSet<string> renamedTargets = [];
            foreach (ComboBox cb in _allComboBoxes)
            {
                if (cb.IsEnabled && cb.SelectedItem is ComboBoxItem cbi && cbi.Tag is string label)
                    renamedTargets.Add(label);
            }

            foreach ((int gridRow, string dataLabel) in _missingRows)
            {
                ActionGrid.RowDefinitions[gridRow].Height =
                    renamedTargets.Contains(dataLabel) ? new GridLength(0) : new GridLength(28);
            }
        }
        #endregion

        #region Button handlers
        private void Repair_Click(object sender, RoutedEventArgs e)
        {
            // Validate: "Rename to:" selected but no target chosen
            List<string> missingRenameTargets = [];
            foreach ((int _, string col, RadioButton _, RadioButton rbRen, ComboBox cb, TextBlock _) in _extraRows)
            {
                if (rbRen is { IsChecked: true } && cb.SelectedIndex < 0)
                    missingRenameTargets.Add(col);
            }
            if (missingRenameTargets.Count > 0)
            {
                MessageBox.Show(
                    $"Please select a rename target for: {string.Join(", ", missingRenameTargets)}",
                    "Rename target required", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            List<string> toAdd = [];
            List<string> toDelete = [];
            List<(string, string)> toRename = [];

            foreach ((int _, string col, RadioButton _, RadioButton rbRen, ComboBox cb, TextBlock _) in _extraRows)
            {
                if (rbRen is { IsChecked: true } && cb?.SelectedItem is ComboBoxItem cbi && cbi.Tag is string target)
                    toRename.Add((col, target));
                else
                    toDelete.Add(col);
            }

            HashSet<string> renamedTargets = toRename.Select(r => r.Item2).ToHashSet();
            foreach ((int _, string label) in _missingRows)
            {
                if (!renamedTargets.Contains(label))
                    toAdd.Add(label);
            }

            Result = new DatabaseSchemaMismatchResult
            {
                UserChoseRepair = true,
                LabelsToAdd = toAdd,
                ColumnsToDelete = toDelete,
                ColumnsToRename = toRename
            };
            DialogResult = true;
        }

        private void ContinueLoading_Click(object sender, RoutedEventArgs e)
        {
            Result = new DatabaseSchemaMismatchResult { UserChoseRepair = false, AbortLoad = false };
            DialogResult = true;
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            Result = new DatabaseSchemaMismatchResult { UserChoseRepair = false, AbortLoad = true };
            DialogResult = true;
        }
        #endregion
    }
}
