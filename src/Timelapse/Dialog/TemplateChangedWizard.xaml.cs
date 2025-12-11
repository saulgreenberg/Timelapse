using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using Timelapse.ControlsMetadata;
using Timelapse.Database;
using Timelapse.DataTables;
using Timelapse.DebuggingSupport;
using Timelapse.Util;
using TimelapseWpf.Toolkit;
using Control = Timelapse.Constant.Control;

namespace Timelapse.Dialog
{
    /// <summary>
    /// This wizard walks the user through the changes in a template, and in particular
    /// differences gathered between the various template tables in the .tdb file  vs the .ddb file.
    /// Differences shown focus on differences between folder levels and data fields.
    /// </summary>
    public partial class TemplateChangedWizard
    {

        // A flag that indicates whether we should use the tdb template (if true) or stick with the one in the ddb template
        // Accessed by the invoker to see which to use
        public bool? UseTdbTemplate { get; set; }

        #region Private variables
        // Contains structures and flags that summarize the differences between the various templates
        // This is filled in by prior code
        private readonly TemplateSyncResults TemplateSyncResults;

        // So we can look up the levels held by the tdb and ddb files 
        private readonly DataTableBackedList<MetadataInfoRow> TdbMetadataInfo;
        private readonly DataTableBackedList<MetadataInfoRow> DdbMetadataInfo;

        // So we can look up a control's label from its data label
        private readonly Dictionary<int, DataTableBackedList<MetadataControlRow>> TdbMetadataControlsByLevel;
        private readonly Dictionary<int, DataTableBackedList<MetadataControlRow>> DdbMetadataControlsByLevel;
        private readonly DataTableBackedList<ControlRow> TdbControls;
        private readonly DataTableBackedList<ControlRow> DdbControls;

        // These variables are used internally to signal particular differences between data fields
        private readonly string actionAdd = "Add";
        private readonly string actionDelete = "Delete";

        // Collects data field values for data fields that are either in the Ddb file, or Tdb file, but not both.
        private readonly Dictionary<string, Dictionary<string, string>> inDdbOnly = []; // Type, datalabel, type
        private readonly Dictionary<string, Dictionary<string, string>> inTdbOnly = [];

        // These two are used to differentiate between Add/Delete vs Rename
        private readonly List<ComboBox> comboBoxes = [];
        private readonly List<int> actionRows = [];

        readonly string selectAllOptionsMessage =  "Select:" +
                                                   $"[li][e]Open using New Template[/e]: updates your data [i].ddb[/i] file to match the template [i].tdb[/i] file definitions," +
                                                   $"[li][e]Open using Original Template[/e]: leaves your data [i].ddb[/i] file unchanged," +
                                                   $"[li][e]Cancel[/e]: exits this Wizard without opening your file.";

        readonly string selectLimitedOptionsMessage = "Select:" +
                                                      $"[li][e]Open using Original Template[/e]: leaves your data [i].ddb[/i] file unchanged" +
                                                      $"[li][e]Cancel[/e]: exits this Wizard without opening your file.";
        #endregion

        #region Constructor, Loading
        public TemplateChangedWizard(Window owner, TemplateSyncResults templateSyncResults,
            DataTableBackedList<MetadataInfoRow> tdbMetadataInfo, DataTableBackedList<MetadataInfoRow> ddbMetadataInfo,
            Dictionary<int, DataTableBackedList<MetadataControlRow>> tdbMetadataControlsByLevel, Dictionary<int, DataTableBackedList<MetadataControlRow>> ddbMetadataControlsByLevel,
            DataTableBackedList<ControlRow> tdbControls, DataTableBackedList<ControlRow> ddbControls)
        {
            InitializeComponent();
            // Set up static reference resolver for FormattedMessageContent
            FormattedDialogHelper.SetupStaticReferenceResolver(Message);
            Owner = owner;
            TemplateSyncResults = templateSyncResults;

            TdbMetadataInfo = tdbMetadataInfo;
            DdbMetadataInfo = ddbMetadataInfo;
            TdbMetadataControlsByLevel = tdbMetadataControlsByLevel;
            DdbMetadataControlsByLevel = ddbMetadataControlsByLevel;
            TdbControls = tdbControls;
            DdbControls = ddbControls;
        }

        // Position the window relative to its parent
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            Dialogs.TryPositionAndFitDialogIntoWindow(this);

            // Initialize FormattedMessageContent controls
            Message.BuildContentFromProperties();

            if (TemplateSyncResults.ControlSynchronizationErrorsByLevel.Count > 0)
            {
                GenerateIncompatibleDataFields();
            }
            else
            {
                GenerateHierarchyDifferencesPage();
                GenerateDataFieldDifferencesPage();
            }

            AdjustUIAppearance();
        }
        #endregion

        #region GenerateIncompatibleDDataFieldDifferences
        // Displays an error message plus list of incompatible data fields on the data fields page.
        private void GenerateIncompatibleDataFields()
        {
            MessageDataFieldsIncompatible.BuildContentFromProperties();
            MessageDataFieldsIncompatible.Visibility = Visibility.Visible;

            // Add the details
            int row = 0;
            int lastLevel = TemplateSyncResults.InfoRowsCommon.Count;

            // Yes. display differences as collected in the warnings.
            TextBlock tb = new();
            TextBlockAddHeader(tb, $"{Environment.NewLine}The incompatible data fields defined in your template differ as follows", 14);
            CreateRow(DataFieldGrid, tb, ++row);
            for (int level = 0; level <= lastLevel; level++)
            {
                // SAULXXX Check - Original was warnings but this method is only invoked if its on Errors, so I think this version is correct.
                //if (! (TemplateSyncResults.ControlSynchronizationWarningsByLevel.ContainsKey(level) || TemplateSyncResults.ControlSynchronizationErrorsByLevel.ContainsKey(level)))
                if (! TemplateSyncResults.ControlSynchronizationErrorsByLevel.ContainsKey(level))
                {
                    continue;
                }

                // Print the level name if there are any differences
                AddLevelSeparator(level, level == 0 ? "Image data" : TdbMetadataInfo[level - 1].Alias, ++row);
                tb = null;
                foreach (string errorMessage in TemplateSyncResults.ControlSynchronizationErrorsByLevel[level])
                {
                    // print each incompataible data field in this level
                    if (tb == null)
                    {
                        // So we don't add a line feed at the beginning
                        tb = new();
                        TextBlockAddPlainLine(tb, errorMessage, false);
                    }
                    else
                    {
                        TextBlockAddPlainLine(tb, errorMessage);
                    }
                }
                CreateRow(DataFieldGrid, tb, ++row);
            }
        }
        #endregion

        #region GenerateLevelPage

        // Generate the page summarizing the differences (if any) between the level hierarchies in the tdb vs ddb
        private void GenerateHierarchyDifferencesPage()
        {
            int row = -1;
            TextBlock instructions = new();
            TextBlockAddHeader(instructions, $"If you choose 'Open using New template', Timelapse will update the folder levels as follows.{Environment.NewLine}");

            // Check: No level changes? 
            if (TemplateSyncResults.InfoRowsInDdbToDelete.Count == 0
                && TemplateSyncResults.InfoRowsInDdbToRenumber.Count == 0
                && TemplateSyncResults.InfoRowsInTdbToAdd.Count == 0
                && TemplateSyncResults.InfoRowsWithNameChanges.Count == 0)
            {
                return;
            }

            // Check: only difference is an appended level?
            if (TemplateSyncResults.InfoHierarchyTdbDiffersOnlyWithAppendedLevels)
            {
                TextBlock problem = new();
                TextBlockAddHeader(problem, "Warning: A new folder level definition was added to the bottom level,", 14);
                TextBlockAddPlainLine(problem, $"Your folder hierarchy should be adjusted, if needed, to include a similar sub-folder level.{Environment.NewLine}");
                CreateRow(LevelGrid, problem, ++row);
            }

            // Show differences between new and old folder levels
            TextBlock information = new();
            TextBlockAddHeader(information, $"The folder hierarchy defined in your templates differ as follows.{Environment.NewLine}", 14);
            CreateRow(LevelGrid, information, ++row);

            CreateRow(LevelGrid,
                new() { Text = "New template", FontStyle = FontStyles.Italic, FontWeight = FontWeights.Bold, FontSize = 14 },
                new() { Text = "Original template", FontStyle = FontStyles.Italic, FontWeight = FontWeights.Bold, FontSize = 14 },
                ++row);

            CreateRow(LevelGrid,
                GenerateTdbHierarchicalList(TdbMetadataInfo, TemplateSyncResults),
                GenerateDdbHierarchicalList(DdbMetadataInfo, TemplateSyncResults),
                ++row);
        }
        #endregion

        #region GenerateDataFieldDifferences
        // Generate the page summarizing the differences (if any) between each level's data fields in the tdb vs ddb
        public void GenerateDataFieldDifferencesPage()
        {
            int row = 0;
            int lastLevel = TemplateSyncResults.InfoRowsCommon.Count;
            bool deletionsPresent = false;

            // Check: No data field changes or warnings?
            if (false == TemplateSyncResults.SyncRequiredAsDataLabelsDiffer && TemplateSyncResults.ControlSynchronizationWarningsByLevel.Count == 0)
            {
                return;
            }

            MessageDataFieldsCompatible.BuildContentFromProperties();
            MessageDataFieldsCompatible.Visibility = Visibility.Visible;
            
            // Check: Only warnings?
            if (false == TemplateSyncResults.SyncRequiredAsDataLabelsDiffer && TemplateSyncResults.ControlSynchronizationWarningsByLevel.Count > 0)
            {
                //Yes. Hide the column headers as we don't need them, then print a message saying so
                DataFieldGrid.RowDefinitions[0].Height = new(0);
            }

            // Check: Data label differences?
            if (TemplateSyncResults.SyncRequiredAsDataLabelsDiffer)
            {
                // Iterate through each level, checking and collecting changes (if any) between their datafields
                // Level 0 is the image data fields.
                for (int level = 0; level <= lastLevel; level++)
                {
                    // Check: Level 1 and above? If the level hierarchies are incompatible, there is no point showing their differences
                    if (level > 0 && TemplateSyncResults.InfoHierarchyIncompatibleDifferences)
                    {
                        break;
                    }

                    // Check: Is this a new (added) level in Tdb, or a Deleted levels in Ddb?
                    if (TemplateSyncResults.InfoRowsInTdbToAdd.Any(s => s.Level == level) ||
                        TemplateSyncResults.InfoRowsInDdbToDelete.Any(s => s.Level == level))
                    {
                        // We can ignore any data labels from such levels as the level message summarizes them
                        // Also, there is no point in listing all added data fields in the new level
                        // (and as the Deleted row is no longer present, neither are their data labels).
                        continue;
                    }

                    // Build the interface showing datalabels in terms of whether they can be added and renamed, added only, or deleted only.
                    inDdbOnly.Clear();
                    inTdbOnly.Clear();
                    if (TemplateSyncResults.SyncRequiredAsDataLabelsDiffer)
                    {
                        // Collect the data labels that are only in the ddb or only in the tdb
                        foreach (string type in Control.ControlTypes)
                        {
                            AddResultIfThereIsSomethingToAdd(inDdbOnly, type, level, TemplateSyncResults.DataLabelsInDdbButNotTdbByLevel);
                            AddResultIfThereIsSomethingToAdd(inTdbOnly, type, level, TemplateSyncResults.DataLabelsInTdbButNotDdbByLevel);
                        }

                        // Print the level name if there are any differences
                        if ((TemplateSyncResults.DataLabelsInDdbButNotTdbByLevel.ContainsKey(level) &&
                             TemplateSyncResults.DataLabelsInDdbButNotTdbByLevel[level].Count != 0) ||
                            (TemplateSyncResults.DataLabelsInTdbButNotDdbByLevel.ContainsKey(level) &&
                             TemplateSyncResults.DataLabelsInTdbButNotDdbByLevel[level].Count != 0))
                        {
                            string tempAlias =
                                level == 0
                                    ? "Image data"
                                    : MetadataUI.CreateTemporaryAliasIfNeeded(level, TdbMetadataInfo[level - 1].Alias); // levels are 1-based, so first is 0
                            AddLevelSeparator(level, tempAlias, ++row);
                        }

                        // We want to display data fields ordered by type, so we iterate through each type looking 
                        // for changes, and then display that change.
                        // TODO We should be allowing certain types to be considered together, e.g. Multiline, Text are equivalent
                        foreach (string type in Control.ControlTypes)
                        {
                            // Changed items that can be renamed
                            int inTdbOnlyCount = inTdbOnly.TryGetValue(type, out var _) ? inTdbOnly[type].Count : 0;
                            int inDdbOnlyCount = inDdbOnly.TryGetValue(type, out var value1) ? value1.Count : 0;

                            if (inTdbOnlyCount > 0 && inDdbOnlyCount > 0)
                            {
                                // Display the ddb datalabels that can be added or renamed
                                foreach (string datalabel in inDdbOnly[type].Keys)
                                {
                                    string label = GetLabelFromControls(level, datalabel, false);
                                    CreateRow(level, datalabel, label, type, ++row, false, actionDelete);
                                    deletionsPresent = true;
                                }

                                // Displays the tdb datalabels that can be added or renamed
                                foreach (string datalabel in inTdbOnly[type].Keys)
                                {
                                    string label = GetLabelFromControls(level, datalabel, true);
                                    CreateRow(level, datalabel, label, type, ++row, true, actionAdd);
                                }
                            }
                            else if (inTdbOnlyCount > 0)
                            {
                                // Displays the tdb datalabels that can be added or renamed
                                foreach (string datalabel in inTdbOnly[type].Keys)
                                {
                                    string label = GetLabelFromControls(level, datalabel, true);
                                    CreateRow(level, datalabel, label, type, ++row, true, actionAdd);
                                }
                            }
                            else if (inDdbOnlyCount > 0)
                            {
                                // Display the ddb datalabels that can be added or renamed
                                foreach (string datalabel in inDdbOnly[type].Keys)
                                {
                                    string label = GetLabelFromControls(level, datalabel, false);
                                    CreateRow(level, datalabel, label, type, ++row, true, actionDelete);
                                    deletionsPresent = true;
                                }
                            }

                            // OLD CODE THAT PUT A LINE BETWEEN TYPES, BUT IT DOESN'T QUITE WORK
                            //if (inTdbOnlyCount > 0 || inDdbOnlyCount > 0)
                            //{
                            //  if (rowAdded)
                            //  {
                            //      this.AddSeparator();
                            //  }
                            //}
                        }
                    }
                }

                // Add a warning if some deletions are present
                if (deletionsPresent)
                {
                    PageDataField.Description += $"{Environment.NewLine}WARNING: Deleting a data field also deletes data previously entered in that field, if any.";
                }
            }

            // Check: Minor differences?
            if (TemplateSyncResults.ControlSynchronizationWarningsByLevel.Count > 0)
            {
                // Yes. display minor differences as collected in the warnings.
                TextBlock tb = new();
                TextBlockAddHeader(tb, $"{Environment.NewLine}Minor differences (no changes will be made to your data)", 14);
                CreateRow(DataFieldGrid, tb, ++row);

                // Now add the warnings
                for (int level = 0; level <= lastLevel; level++)
                {
                    if (TemplateSyncResults.ControlSynchronizationWarningsByLevel.TryGetValue(level, out var _))
                    {
                        // Print the level name if there are any differences
                        AddLevelSeparator(level, level == 0 ? "Image data" : TdbMetadataInfo[level - 1].Alias, ++row);
                        tb = null;
                        foreach (string warning in TemplateSyncResults.ControlSynchronizationWarningsByLevel[level])
                        {
                            if (tb == null)
                            {
                                // So we don't add a line feed at the beginning
                                tb = new();
                                TextBlockAddPlainLine(tb, warning, false);
                            }
                            else
                            {
                                TextBlockAddPlainLine(tb, warning);
                            }
                        }
                        CreateRow(DataFieldGrid, tb, ++row);
                    }
                }

            }
        }

        // Utility used by above: given a datalabel and a level, return its label from the control in the indicated tdb or ddb template tables.
        private string GetLabelFromControls(int level, string dataLabel, bool isTdb)
        {
            if (level == 0)
            {
                if (isTdb)
                {
                    return TdbControls.FirstOrDefault(s => s.DataLabel == dataLabel)?.Label ?? "--";
                }
                return DdbControls.FirstOrDefault(s => s.DataLabel == dataLabel)?.Label ?? "--";
            }

            if (isTdb)
            {
                return TdbMetadataControlsByLevel.TryGetValue(level, out var _)
                    ? TdbMetadataControlsByLevel[level].FirstOrDefault(s => s.DataLabel == dataLabel)?.Label ?? "--"
                    : "--";
            }
            return DdbMetadataControlsByLevel.TryGetValue(level, out var value1)
                ? value1.FirstOrDefault(s => s.DataLabel == dataLabel)?.Label ?? "--"
                : "--";

        }
        #endregion

        #region TextBlock convenience methods, to insert different formatted text in a textblock

        // A bolded string (usually to indicate a header) with the default font size
        private void TextBlockAddHeader(TextBlock tb, string text)
            => TextBlockAddHeader(tb, text, 12);

        // Insert a bolded textblock (a header) with the indicated font
        private void TextBlockAddHeader(TextBlock tb, string text, int fontSize)
        {
            tb.Inlines.Add(new Run
            {
                FontWeight = FontWeights.Bold,
                FontSize = fontSize,
                Text = $"{text}"
            });
        }

        // Insert a Linebreak and Unbolded string  (usually a set of sequential lines)
        private void TextBlockAddPlainLine(TextBlock tb, string text) => TextBlockAddPlainLine(tb, text, true);
        private void TextBlockAddPlainLine(TextBlock tb, string text, bool insertNewLine)
        {
            tb.Inlines.Add(new Run
            {
                FontWeight = FontWeights.Normal,
                FontSize = 12,
                Text = insertNewLine ? $"{Environment.NewLine}{text}" : text
            });
        }
        #endregion

        #region Texblock: GenerateHierarchicalList
        // This form creates a textblock outlining the TDB  hierarchy that also indicates changes from the DDB hierarchy
        private static TextBlock GenerateTdbHierarchicalList(DataTableBackedList<MetadataInfoRow> tdbMetadataInfo, TemplateSyncResults syncResults)
        {
            int levels = tdbMetadataInfo.RowCount;

            TextBlock tb = new();
            if (levels == 0)
            {
                tb.Inlines.Add(new Run
                {
                    FontWeight = FontWeights.Normal,
                    FontStyle = FontStyles.Italic,
                    Foreground = Brushes.Crimson,
                    FontSize = 12,
                    Text = "No levels defined",
                });
                return tb;
            }

            string indent = string.Empty;
            for (int i = 0; i < levels; i++)
            {
                string tempAlias;
                string annotation;
                if (syncResults.InfoRowsInTdbToAdd.Any(s => s.Guid == tdbMetadataInfo[i].Guid))
                {
                    annotation = "new";
                }
                else if (syncResults.InfoRowsInDdbToRenumber.Any(s => s.Item1.Guid == tdbMetadataInfo[i].Guid))
                {
                    annotation = "moved";
                }
                else if (syncResults.InfoRowsWithNameChanges.Any(s => s.Item1.Guid == tdbMetadataInfo[i].Guid))
                {
                    annotation = ""; // name changes added later
                }
                else
                {
                    annotation = "";
                }


                Tuple<MetadataInfoRow, MetadataInfoRow> tuple = syncResults.InfoRowsWithNameChanges.FirstOrDefault(s => s.Item1.Guid == tdbMetadataInfo[i].Guid);
                if (tuple != null)
                {
                    annotation += annotation == string.Empty ? "was " : " and was ";
                    tempAlias = MetadataUI.CreateTemporaryAliasIfNeeded(tuple.Item2.Level, tuple.Item2.Alias);
                    annotation += tempAlias;
                }

                tempAlias = MetadataUI.CreateTemporaryAliasIfNeeded(tdbMetadataInfo[i].Level, tdbMetadataInfo[i].Alias);
                tb.Inlines.Add(new Run
                {
                    FontWeight = annotation == string.Empty ? FontWeights.Normal : FontWeights.Medium,
                    FontSize = 12,
                    Text = $"{indent}\u2022 {tdbMetadataInfo[i].Level}: {tempAlias}"
                });
                tb.Inlines.Add(new Run
                {
                    FontWeight = FontWeights.Normal,
                    FontStyle = FontStyles.Italic,
                    Foreground = Brushes.Crimson,
                    FontSize = 12,
                    Text = $" {annotation}{(i < levels - 1 ? Environment.NewLine : string.Empty)}",

                });
                indent += "    ";
            }

            return tb;
        }

        // This form returns a textblock DDBb hierarchy that also indicates changes to the TDB hierarchy
        private static TextBlock GenerateDdbHierarchicalList(DataTableBackedList<MetadataInfoRow> ddbMetadataInfo, TemplateSyncResults syncResults)
        {
            int levels = ddbMetadataInfo.RowCount;

            TextBlock tb = new();
            if (levels == 0)
            {
                tb.Inlines.Add(new Run
                {
                    FontWeight = FontWeights.Normal,
                    FontStyle = FontStyles.Italic,
                    Foreground = Brushes.Crimson,
                    FontSize = 12,
                    Text = "No levels defined",

                });
                return tb;
            }

            string indent = string.Empty;
            for (int i = 0; i < levels; i++)
            {
                string annotation;
                string tempAlias;
                if (syncResults.InfoRowsInDdbToRenumber.Any(s => s.Item1.Guid == ddbMetadataInfo[i].Guid))
                {
                    annotation = "to be moved";
                }
                else if (syncResults.InfoRowsInDdbToDelete.Any(s => s.Guid == ddbMetadataInfo[i].Guid))
                {
                    annotation = "to be deleted";
                }
                else if (syncResults.InfoRowsWithNameChanges.Any(s => s.Item1.Guid == ddbMetadataInfo[i].Guid))
                {
                    annotation = ""; // name changes added later
                }
                else
                {
                    annotation = "";
                }

                Tuple<MetadataInfoRow, MetadataInfoRow> tuple = syncResults.InfoRowsWithNameChanges.FirstOrDefault(s => s.Item1.Guid == ddbMetadataInfo[i].Guid);
                if (tuple != null)
                {
                    annotation += annotation == string.Empty ? "will be " : " and will be ";
                    tempAlias = MetadataUI.CreateTemporaryAliasIfNeeded(tuple.Item1.Level, tuple.Item1.Alias);
                    annotation += tempAlias;
                }

                tempAlias = MetadataUI.CreateTemporaryAliasIfNeeded(ddbMetadataInfo[i].Level, ddbMetadataInfo[i].Alias);
                tb.Inlines.Add(new Run
                {
                    FontWeight = annotation == string.Empty ? FontWeights.Normal : FontWeights.Medium,

                    FontSize = 12,
                    Text = $"{indent}\u2022 {ddbMetadataInfo[i].Level}: {tempAlias}"
                });
                tb.Inlines.Add(new Run
                {
                    FontWeight = FontWeights.Normal,
                    FontStyle = FontStyles.Italic,
                    Foreground = Brushes.Crimson,
                    FontSize = 12,
                    Text = $" {annotation}{(i < levels - 1 ? Environment.NewLine : string.Empty)}",

                });
                indent += "    ";
            }

            return tb;
        }

        // Create a level  separator in the Action Grid
        private void AddLevelSeparator(int level, string alias, int row)
        {
            RowDefinition rd = new()
            {
                Height = new(27)
            };
            DataFieldGrid.RowDefinitions.Add(rd);
            TextBlock tb = new()
            {
                FontWeight = FontWeights.Medium,
                FontStyle = FontStyles.Italic,
                Background = Brushes.LightGray,
                Margin = new(0, 5, 0, 0),
                Padding = new(0, 2, 0, 2),
                Text = $"{alias} (Level {level})"
            };
            Grid.SetRow(tb, row);
            Grid.SetColumn(tb, 0);
            Grid.SetColumnSpan(tb, 6);
            DataFieldGrid.Children.Add(tb);
        }
        #endregion

        #region CreateRow: various forms to create a single grid row.
        // A single textblock spanning the first 2 columns


        // two textblocks atop the first and second column respectively
        private void CreateRow(Grid grid, TextBlock tb1, TextBlock tb2, int row)
        {
            CreateRow(grid, tb1, row, 0, 1);
            CreateRow(grid, tb2, row, 1, 1);
        }


        // General form for above: a textblock in a given row, column, and columnspan
        private void CreateRow(Grid grid, TextBlock tb, int row, int column = 0, int columnspan = 6)
        {
            // Create a new row definition
            RowDefinition rd = new()
            {
                Height = GridLength.Auto
            };
            grid.RowDefinitions.Add(rd);

            // Add the textblock to that row
            int columnMargin = column == 0 ? 0 : 30;
            tb.Margin = new(columnMargin, 0, 0, 0);
            Grid.SetColumn(tb, column);
            Grid.SetColumnSpan(tb, columnspan);
            Grid.SetRow(tb, row);
            grid.Children.Add(tb);
        }

        // Specialized CreateRow:
        // Create a single row in the grid, which displays datalabels in terms of whether they can be added and renamed, added only, or deleted only
        // along with possibl radio buttons and combo boxes allowing delete/added to be changed to a rename
        private void CreateRow(int level, string datalabel, string label, string type, int row, bool addOrDeleteOnly, string action)
        {
            // Create a new row
            RowDefinition rd = new()
            {
                Height = new(25),
                Tag = level
            };
            DataFieldGrid.RowDefinitions.Add(rd);
            actionRows.Add(row);

            // Type
            TextBlock textblockType = new()
            {
                Text = type,
                Margin = new(0, 0, 20, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(textblockType, 0);
            Grid.SetRow(textblockType, row);
            DataFieldGrid.Children.Add(textblockType);

            // Data label
            TextBlock textblockDataLabel = new()
            {
                Text = datalabel,
                Margin = new(0, 0, 20, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(textblockDataLabel, 1);
            Grid.SetRow(textblockDataLabel, row);
            DataFieldGrid.Children.Add(textblockDataLabel);

            // Label
            TextBlock textblockLabel = new()
            {
                Text = label,
                Margin = new(0, 0, 20, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(textblockLabel, 2);
            Grid.SetRow(textblockLabel, row);
            DataFieldGrid.Children.Add(textblockLabel);

            // Add or Delete command without renaming
            if (addOrDeleteOnly)
            {
                Label labelActionDefaultAction = new()
                {
                    Tag = rd,
                    Padding = new(0, -3, 0, -3),
                    Content = action,
                    Margin = new(0, 0, 20, 0),
                    VerticalAlignment = VerticalAlignment.Center
                };

                Grid.SetColumn(labelActionDefaultAction, 3);
                Grid.SetRow(labelActionDefaultAction, row);
                DataFieldGrid.Children.Add(labelActionDefaultAction);
                return;
            }

            // Add command with renaming
            RadioButton radiobuttonActionDefaultAction = new()
            {
                GroupName = datalabel,
                Content = action,
                Margin = new(10, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center,
                IsChecked = true
            };
            Grid.SetColumn(radiobuttonActionDefaultAction, 3);
            Grid.SetRow(radiobuttonActionDefaultAction, row);
            DataFieldGrid.Children.Add(radiobuttonActionDefaultAction);

            // Combobox showing renaming possibilities
            ComboBox comboboxRenameMenu = new()
            {
                MaxWidth = 250,
                Height = 25,
                Margin = new(10, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Left,
                MinWidth = 150,
                IsEnabled = false
            };

            foreach (string str in inTdbOnly[type].Keys)
            {
                ComboBoxItem item = new()
                {
                    Content = str,
                    IsEnabled = true
                };
                comboboxRenameMenu.Items.Add(item);
            }

            Grid.SetColumn(comboboxRenameMenu, 5);
            Grid.SetRow(comboboxRenameMenu, row);
            DataFieldGrid.Children.Add(comboboxRenameMenu);
            comboBoxes.Add(comboboxRenameMenu);

            comboboxRenameMenu.SelectionChanged += CbRenameMenu_SelectionChanged;

            RadioButton radiobuttonRenameAction = new()
            {
                GroupName = datalabel,
                Content = "Rename to:",
                Margin = new(10, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center,
                Tag = comboboxRenameMenu
            };
            Grid.SetColumn(radiobuttonRenameAction, 4);
            Grid.SetRow(radiobuttonRenameAction, row);
            DataFieldGrid.Children.Add(radiobuttonRenameAction);

            // Enable and disable the combobox depending upon which radiobutton is selected
            radiobuttonRenameAction.Checked += RbRenameAction_CheckChanged;
            radiobuttonRenameAction.Unchecked += RbRenameAction_CheckChanged;
        }
        #endregion

        #region Row utilities
        // For each row, if it contains an enabled rename combobox, then collect its currently selected datalabel (if any)
        // For other rows, if it is a 'Deleted' row, hide or show it depending if it matches one of the currently selected datalabels
        // Note that this is fragile, as it depends on various UI Elements being in various columns and row orders 
        // - eg., arranged by type with delete after renames.
        // Also, collect all the datalabels to add, delete and rename
        private void ShowHideItemsAsNeeded()
        {
            List<string> selectedDataLabels = [];

            foreach (int row in actionRows)
            {
                // Retrieve selected items, but only if the rename radio button is enabled and checked
                // retrieve selected items, but only if the rename radio button is checked
                UIElement uiComboBox = GetUIElement(row, 5);
                if (uiComboBox is ComboBox { IsEnabled: true } cb)
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
                if (!(GetUIElement(row, 3) is Label labelAction) || labelAction.Content.ToString() != actionAdd)
                {
                    continue;
                }

                // Retrieve the data label
                if (GetUIElement(row, 1) is TextBlock textblockDataLabel)
                {
                    DataFieldGrid.RowDefinitions[row].Height = selectedDataLabels.Contains(textblockDataLabel.Text) ? new(0) : new GridLength(25);
                }
            }
        }

        // For each row, if it contains an enabled rename combobox, check to see that it actually has a valid menu item selected.
        // If not, display a warning message.
        private bool AreRenamedEntriesValid()
        {
            List<string> problemDataLabels = [];

            foreach (int row in actionRows)
            {
                // We are only interested in Renamed items, which would only occur if the combobox is enabeld
                UIElement uiComboBox = GetUIElement(row, 5);
                if (uiComboBox is ComboBox { IsEnabled: true } cb)
                {
                    // The combobox is enabled, thus it's a renume
                    if (cb.SelectedItem == null || cb.SelectedItem.ToString() == string.Empty)
                    {
                        // Retrieve the data label and add it as an problem 
                        if (GetUIElement(row, 1) is TextBlock textblockDataLabel)
                        {
                            problemDataLabels.Add(textblockDataLabel.Text);
                        }
                    }
                }
            }

            if (problemDataLabels.Count <= 0) return true;
            // notify the user concerning the problem data labels
            Dialogs.SelectNewNameForRenamedFields(this, problemDataLabels);
            return false;
        }

        // Get the UI Element in the indicated row and column from the Action Grid.
        // returns null if no such element exists.
        private UIElement GetUIElement(int row, int column)
        {
            return DataFieldGrid.Children
                .Cast<UIElement>()
                .FirstOrDefault(e => Grid.GetRow(e) == row && Grid.GetColumn(e) == column);
        }
        #endregion

        #region UpdateTemplateSyncResults
        // Update variouos fields in TemplateSyncResuls based on the use settings, eg. Add lists, Delete lists, Rename lists etc
        private void UpdateTemplateSyncResultsBasedOnSettings()
        {
            GridLength activeGridHeight = new(25);

            foreach (int row in actionRows)
            {
                // Check if row is active
                if (DataFieldGrid.RowDefinitions[row].Height != activeGridHeight)
                {
                    continue;
                }

                int level = -1;
                if (DataFieldGrid.RowDefinitions[row].Tag is int i)
                {
                    level = i;
                }
                // Retrieve the data label
                string datalabel = string.Empty;
                if (GetUIElement(row, 1) is TextBlock textblockDataLabel)
                {
                    datalabel = textblockDataLabel.Text;
                }

                // Retrieve the command type
                // Add action 
                if (GetUIElement(row, 3) is Label labelAction && labelAction.Content.ToString() == actionAdd)
                {
                    if (false == TemplateSyncResults.DataLabelsToAddByLevel.ContainsKey(level))
                    {
                        TemplateSyncResults.DataLabelsToAddByLevel.Add(level, []);
                    }
                    TemplateSyncResults.DataLabelsToAddByLevel[level].Add(datalabel);
                    continue;
                }

                // Before checking for Delete actions, we need to first check to see if it has been renamed with a valid value
                UIElement uiComboBox = GetUIElement(row, 5);
                if (uiComboBox is ComboBox { IsEnabled: true } cb)
                {
                    ComboBoxItem cbi = cb.SelectedItem as ComboBoxItem;
                    if (cb.SelectedItem != null)
                    {
                        if (cbi != null)
                        {
                            if (false == TemplateSyncResults.DataLabelsToRenameByLevel.ContainsKey(level))
                            {
                                TemplateSyncResults.DataLabelsToRenameByLevel.Add(level, []);
                            }
                            TemplateSyncResults.DataLabelsToRenameByLevel[level].Add(new(datalabel, cbi.Content.ToString()));
                        }
                        else
                        {
                            // Shouldn't happen. Not sure if unknown value workaround will work
                            TracePrint.NullException(nameof(cbi));
                            if (false == TemplateSyncResults.DataLabelsToRenameByLevel.ContainsKey(level))
                            {
                                TemplateSyncResults.DataLabelsToRenameByLevel.Add(level, []);
                            }
                            TemplateSyncResults.DataLabelsToRenameByLevel[level].Add(new(datalabel, "Unknown value"));
                        }
                        continue;
                    }
                }

                // If we arrived here, it must be an ACTION_DELETED
                if (false == TemplateSyncResults.DataLabelsToDeleteByLevel.ContainsKey(level))
                {
                    TemplateSyncResults.DataLabelsToDeleteByLevel.Add(level, []);
                }
                TemplateSyncResults.DataLabelsToDeleteByLevel[level].Add(datalabel);
            }
        }
        #endregion

        #region UI Callbacks
        // Enable or Disable the Rename comboboxdepending on the state of the Rename radio button
        private void RbRenameAction_CheckChanged(Object o, RoutedEventArgs a)
        {
            if (o is RadioButton { Tag: ComboBox cb } rb)
            {
                cb.IsEnabled = (rb.IsChecked == true);
                ShowHideItemsAsNeeded();
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
            foreach (ComboBox combobox in comboBoxes)
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
            ShowHideItemsAsNeeded();
        }

        private void UseOldTemplate_Click(object sender, RoutedEventArgs e)
        {
            UseTdbTemplate = false;
            DialogResult = true;
        }

        private void UseNewTemplateButton_Click(object sender, RoutedEventArgs e)
        {
            if (AreRenamedEntriesValid() == false)
            {
                e.Handled = true;
                return;
            }
            UpdateTemplateSyncResultsBasedOnSettings();
            UseTdbTemplate = true;
            DialogResult = true;
        }


        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }
        #endregion

        #region Private Utility Methods

        // Used to adjust which pages are visible and which buttons are visible on which pages
        private void AdjustUIAppearance()
        {
            // Check: No level changes? 
            bool hierarchyDiffers = false == (TemplateSyncResults.InfoRowsInDdbToDelete.Count == 0
                                        && TemplateSyncResults.InfoRowsInDdbToRenumber.Count == 0
                                        && TemplateSyncResults.InfoRowsInTdbToAdd.Count == 0
                                        && TemplateSyncResults.InfoRowsWithNameChanges.Count == 0);
            bool dataFieldDiffers = TemplateSyncResults.SyncRequiredAsDataLabelsDiffer || TemplateSyncResults.ControlSynchronizationWarningsByLevel.Count > 0;
            bool hierarchyIncompatible = TemplateSyncResults.InfoHierarchyIncompatibleDifferences;

            if (TemplateSyncResults.ControlSynchronizationErrorsByLevel.Count > 0)
            {
                // Show error message as some data fields are not compatable
                // Hide the column headers
                DataFieldGrid.RowDefinitions[0].Height = new(0);

                // Show only the data field differences page
                PageIntro.NextPage = PageDataField;
                PageDataField.PreviousPage = PageIntro;

                // Don't allow the new template to be used
                PageDataFieldNewTemplateButton.Visibility = Visibility.Collapsed;
                // remove the grey banner from the top of the page to make the error message clearer
                PageDataField.PageType = WizardPageType.Blank;
            }
            else if (dataFieldDiffers && false == hierarchyDiffers)
            {
                // Show only the data field differences page as there are no hierarchical differences
                PageIntro.NextPage = PageDataField;
                PageDataField.PreviousPage = PageIntro;
            }
            else if (hierarchyIncompatible)
            {
                // COMMENTED OUT FOR NOW. I WANTED TO GIVE THE USER THE OPTION OF ONLY UPDATING THE IMAGE LEVEL DATA FIELDS, IF THERE WERE CHANNGES THERE
                // BUT MY CODE IS SOMEWHAT WRONG AS IT DOESN"T CHECK FOR ONLY IMAGE LEVEL CHANGES OR RESTRICT IT TO THAT UPDATE, AND I THINK IT JUST ADDS CONFUSION.
                // Show
                // - the hierarchy page
                // - the incompatble templates error message
                // remove the grey banner from the top of the hierarchy difference page to make the error message clearer
                
                PageHierarchy.PageType = WizardPageType.Blank;
                //if (dataFieldDiffers)
                //{
                //    // Show the differences page as differences exist.
                //    // Since the hierarchy cannot be used, the user can optionally still update the ddb with the tdb's image data fields. 
                //    // Set that button's text to remind the user of that.
                //    PageDataFieldNewTemplateButton.Content = "Open using New Template (but keep original folder levels)";

                //    // Don't show the hierarchy page buttons, as we want the user to continue to the next page
                //    this.PageHierarchyOldTemplateButton.Visibility = Visibility.Collapsed;
                //    this.PageHierarchyNewTemplateButton.Visibility = Visibility.Collapsed;
                //}
                //else
                //{
                
                // As only the levels page will be displayed, change the solution to indicate that only the old template option can be selected
                MessageLevelsIncompatible.Solution += selectLimitedOptionsMessage;
                MessageLevelsIncompatible.BuildContentFromProperties();
                MessageLevelsIncompatible.Visibility = Visibility.Visible;
                
                // Dont allow the user to select the Use new template button
                PageHierarchyNewTemplateButton.Visibility = Visibility.Collapsed;

                // Since there are no data field differences, set the hierarchy page as the last page.
                PageHierarchy.NextButtonVisibility = WizardPageButtonVisibility.Collapsed;
                PageHierarchy.FinishButtonVisibility = WizardPageButtonVisibility.Collapsed;
                //}
            }
            else if (hierarchyDiffers)
            {
                // Show
                // - the hierarchy page
                if (dataFieldDiffers)
                {
                    // Show the differences page as differences exist.
                    // Don't show the hierarchy page buttons, as we want the user to continue to the differences page
                    PageHierarchyOldTemplateButton.Visibility = Visibility.Collapsed;
                    PageHierarchyNewTemplateButton.Visibility = Visibility.Collapsed;
                }
                else
                {
                    // Don't show the differences page as no differences exist.
                    PageHierarchy.NextButtonVisibility = WizardPageButtonVisibility.Collapsed;
                    PageHierarchy.FinishButtonVisibility = WizardPageButtonVisibility.Collapsed;

                    // As only the levels page will be displayed, change the solution to indicate that the old and new template options can be selected
                    MessageLevelsCompatible.Solution += selectAllOptionsMessage;
                }

                MessageLevelsCompatible.BuildContentFromProperties();
                MessageLevelsCompatible.Visibility = Visibility.Visible;
            }
            else
            {
                // No differences in the hierarchy, so show only the datafield page
                PageIntro.NextPage = PageDataField;
                PageDataField.PreviousPage = PageIntro;
            }
        }

        // Used to add to the inWhichTemplate dictionary,  where it collects types by level present in the other dictionary
        private static void AddResultIfThereIsSomethingToAdd(
            Dictionary<string, Dictionary<string, string>> inWhichTemplate,
            string type, int level,
            Dictionary<int, Dictionary<string, string>> dataLabelsInOneButNotTheOther)
        {
            if (null != dataLabelsInOneButNotTheOther && dataLabelsInOneButNotTheOther.ContainsKey(level) && dataLabelsInOneButNotTheOther[level].Count != 0)
            {
                inWhichTemplate.Add(type, DictionaryFilterByType(dataLabelsInOneButNotTheOther[level], type));
            }
        }

        // Get a subset of the dictionary filtered by the type of control
        private static Dictionary<string, string> DictionaryFilterByType(Dictionary<string, string> dictionary, string controlType)
        {
            if (dictionary == null)
            {
                return [];
            }
            return dictionary.Where(i => (i.Value == controlType)).ToDictionary(i => i.Key, i => i.Value);
        }
        #endregion

    }
}
