using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Timelapse.Dialog;
using Timelapse.Standards;
using TimelapseWpf.Toolkit;
using ConstantFile = Timelapse.Constant.File;

namespace TimelapseTemplateEditor.Dialog
{
    /// <summary>
    /// A wizard for importing definitions into the species list from a recognition file, a Timelapse database or a saved species list.
    /// It explains what can be imported and lets the user select the file. After that, it only shows the pages that are needed:
    /// - whether to add to or replace the list, if the list already has definitions,
    /// - how to resolve conflicts, if there are any. Conflicts within the file always apply, while
    ///   conflicts with or within the list only apply when adding to the list.
    /// If it returns true, ResultingList has the list to use. Otherwise the user cancelled.
    /// </summary>
    public partial class ImportFromFileWizard
    {
        private readonly List<RadioButton> currentButtons = [];
        private readonly List<RadioButton> newButtons = [];
        private readonly List<RadioButton> bothButtons = [];
        private const string FileFilter = "Recognition files, species lists and Timelapse databases (*.json;*.ddb)|*.json;*.ddb|Recognition files and species lists (*.json)|*.json|Timelapse databases (*.ddb)|*.ddb";
        private readonly List<taxonomic> currentDefinitions;
        private readonly bool appendQualifiers;
        private List<taxonomic> newDefinitions;
        private List<DefinitionConflictItem> listVersusFileConflicts;
        private List<DefinitionConflictItem> fileConflicts;
        private List<DefinitionConflictItem> listConflicts;

        // Whether the user chose to replace the list rather than add to it
        private bool ReplacesList => ReplaceList.IsChecked == true || currentDefinitions.Count == 0;

        public ImportFromFileWizard(Window owner, List<taxonomic> currentDefinitions, bool appendQualifiers) : base(owner)
        {
            InitializeComponent();
            this.currentDefinitions = currentDefinitions;
            this.appendQualifiers = appendQualifiers;
            SelectFileMessage.Text =
                "You can import taxonomic information into your species list from any of these:" + Environment.NewLine + Environment.NewLine +
                "  \u2022 A recognition file (.json), which is produced by a recognizer and lists the classifications it uses." + Environment.NewLine +
                "  \u2022 A Timelapse database (.ddb), if it has recognitions that include classifications." + Environment.NewLine +
                "  \u2022 A previously saved species list (.json), which was created by Save to file." + Environment.NewLine + Environment.NewLine +
                "Recognition files and databases don't include all the taxonomic fields, so the rest are looked up in GBIF (gbif.org). " +
                "This needs an internet connection, and can take a while." + Environment.NewLine + Environment.NewLine +
                "After you select the file, you will only be asked about anything that needs a decision, such as whether to add to your list or replace it. " +
                "Your list isn't changed until you finish.";
            ShowOnlyPagesNeeded();
        }

        // The list to use, if the wizard returned true
        public List<taxonomic> ResultingList()
        {
            return RecognitionFileTaxonomy.ApplyChoices(currentDefinitions, newDefinitions, ReplacesList, listVersusFileConflicts, fileConflicts, listConflicts);
        }

        private List<DefinitionConflictItem> ConflictsWhenAdding()
        {
            return [.. fileConflicts, .. listVersusFileConflicts, .. listConflicts];
        }

        private void ImportFromFileWizard_OnLoaded(object sender, RoutedEventArgs e)
        {
            InitalizeProgressHandler(BusyCancelIndicator);
        }

        #region Selecting the file and getting its definitions
        private async void SelectFile_OnClick(object sender, RoutedEventArgs e)
        {
            if (Dialogs.TryGetFileFromUserUsingOpenFileDialog(
                    "Select a recognition file or species list (json), or a Timelapse database (ddb), containing taxonomic information",
                    string.Empty,
                    FileFilter,
                    ConstantFile.JsonFileExtension,
                    out string filePath,
                    this))
            {
                await ImportFileAsync(filePath);
            }
        }

        // If there is a problem with the file, this stays on the current page so the user can select another one.
        private async Task ImportFileAsync(string filePath)
        {
            RecognitionFileTaxonomy taxonomy = await ReadRecognitionFileAsync(filePath);
            if (taxonomy == null)
            {
                return;
            }

            if (false == taxonomy.HasExportedDefinitions && false == taxonomy.HasDescriptions && false == taxonomy.HasCategories)
            {
                if (RecognitionFileTaxonomy.IsDatabaseFile(filePath))
                {
                    Dialogs.EditorSpeciesListNoClassificationCategoriesDialog(this, filePath);
                }
                else
                {
                    Dialogs.EditorSpeciesListNoTaxonomicInformationDialog(this, filePath);
                }
                return;
            }

            List<taxonomic> definitions;
            try
            {
                if (taxonomy.HasExportedDefinitions)
                {
                    // An exported list is already complete, so it doesn't need looking up
                    definitions = taxonomy.ExportedDefinitions;
                }
                else if (taxonomy.HasDescriptions)
                {
                    // Recognition files don't include these fields, but the definitions are still useful if they can't be found
                    definitions = taxonomy.DefinitionsFromDescriptions();
                    string lookupFailure = await FillIdentifyingFieldsFromGbifAsync(definitions);
                    if (lookupFailure != null)
                    {
                        Dialogs.EditorSpeciesListGbifIncompleteDialog(this, lookupFailure);
                    }
                    FillEmptyScientificNames(definitions);
                }
                else
                {
                    definitions = await DefinitionsFromCommonNamesAsync(taxonomy.Categories.Values);
                }
            }
            catch (OperationCanceledException) when (Token.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
            {
                Dialogs.EditorSpeciesListGbifNotAvailableDialog(this, ex.Message);
                return;
            }

            newDefinitions = RecognitionFileTaxonomy.WithoutExactDuplicates(definitions);
            fileConflicts = RecognitionFileTaxonomy.FindFileConflicts(newDefinitions);
            listVersusFileConflicts = RecognitionFileTaxonomy.FindConflicts(currentDefinitions, newDefinitions);
            listConflicts = RecognitionFileTaxonomy.FindListConflicts(currentDefinitions, listVersusFileConflicts);

            // If there is nothing to ask, the import is finished
            if (currentDefinitions.Count == 0 && fileConflicts.Count == 0)
            {
                DialogResult = true;
                return;
            }
            ShowOnlyPagesNeeded();
            ImportWizard.CurrentPage = (WizardPage)ImportWizard.Items[1];
        }

        // The file selection page is always there, as the user can go back and select another file
        private void ShowOnlyPagesNeeded()
        {
            ImportWizard.Items.Remove(PageAddOrReplace);
            ImportWizard.Items.Remove(PageIntro);
            ImportWizard.Items.Remove(PageConflicts);
            if (newDefinitions == null)
            {
                return;
            }

            if (currentDefinitions.Count > 0)
            {
                ImportWizard.Items.Add(PageAddOrReplace);
                string addCount = ConflictsWhenAdding().Count == 0 ? "There are no conflicts." : $"There are {ConflictsWhenAdding().Count} conflict(s), which you will be asked about next.";
                string replaceCount = fileConflicts.Count == 0 ? "There are no conflicts." : $"There are {fileConflicts.Count} conflict(s) within the file, which you will be asked about next.";
                AddOrReplaceMessage.Text = $"Your list has {currentDefinitions.Count} definition(s), and the file has {newDefinitions.Count}.";
                AddToListMessage.Text = $"Your list will keep its definitions and gain the file's definitions. {addCount}";
                ReplaceListMessage.Text = $"Your list will only contain the definitions in the file. {replaceCount}";
            }
            if (ConflictsWhenAdding().Count > 0)
            {
                ImportWizard.Items.Add(PageIntro);
                ImportWizard.Items.Add(PageConflicts);
            }
            AddToList.IsChecked = true;
            UpdateForAddOrReplace();
        }

        // Returns the file's taxonomic information, or null if the file can't be read (after telling the user why) or the user cancelled.
        // Very large files can take a while, so this shows a busy indicator with a cancel button.
        // Exceptions are caught within the background task, as otherwise they leave that task unhandled, which stops the debugger.
        private async Task<RecognitionFileTaxonomy> ReadRecognitionFileAsync(string filePath)
        {
            string failureReason = null;
            TokenReset();
            CancellationToken token = Token;
            Progress.Report(new(0, $"Reading {Path.GetFileName(filePath)}...", true, true));
            BusyCancelIndicator.IsBusy = true;
            WindowCloseButtonIsEnabled(false);
            RecognitionFileTaxonomy taxonomy = await Task.Run(() =>
            {
                try
                {
                    return RecognitionFileTaxonomy.Read(filePath, token);
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested)
                {
                    return null;
                }
                catch (Exception ex) when (ex is SQLiteException or JsonException or IOException or UnauthorizedAccessException)
                {
                    failureReason = ex.Message;
                    return null;
                }
            });
            WindowCloseButtonIsEnabled(true);
            BusyCancelIndicator.IsBusy = false;
            if (failureReason != null)
            {
                Dialogs.EditorSpeciesListCouldNotReadRecognitionFileDialog(this, filePath, failureReason);
            }
            return taxonomy;
        }

        // Fill in the rank, id, kingdom and phylum of each definition that GBIF finds, using the most specific name the definition has.
        // A definition with only a common name has all its fields filled in from that name, as its taxonomy isn't known.
        // If a request fails, the rest are skipped (as the others would likely fail as well) and the reason is returned. Otherwise returns null.
        private async Task<string> FillIdentifyingFieldsFromGbifAsync(List<taxonomic> definitions)
        {
            string failureReason = null;
            taxonomic[] definitionsWithNames = [.. definitions.Where(definition => MostSpecificName(definition).Name != null || RecognitionFileTaxonomy.CommonName(definition).Length > 0)];
            await ForEachGbifRequestAsync(definitionsWithNames, definition => MostSpecificName(definition).Name ?? RecognitionFileTaxonomy.CommonName(definition), async definition =>
            {
                if (failureReason != null)
                {
                    return definition;
                }
                try
                {
                    (string name, string rank) = MostSpecificName(definition);
                    if (name == null)
                    {
                        FillFieldsFromCommonNameMatch(definition, await GbifSpeciesLookup.FindByCommonNameAsync(RecognitionFileTaxonomy.CommonName(definition), Token));
                        return definition;
                    }
                    GbifSpeciesMatch match = await GbifSpeciesLookup.FindExactNameAsync(name, rank, definition.class_, definition.family, Token);
                    if (match != null)
                    {
                        definition.taxonID = match.TaxonID;
                        definition.taxonRank = match.TaxonRank;
                        definition.kingdom = match.Kingdom;
                        definition.phylum = match.Phylum;
                    }
                }
                catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException && false == Token.IsCancellationRequested)
                {
                    failureReason = ex.Message;
                }
                return definition;
            });
            return failureReason;
        }

        // A scientific name doesn't need a rank as it could be for a species or below
        private static (string Name, string Rank) MostSpecificName(taxonomic definition)
        {
            if (false == string.IsNullOrWhiteSpace(definition.scientificName))
            {
                return (definition.scientificName, null);
            }
            if (false == string.IsNullOrWhiteSpace(definition.genus))
            {
                return (definition.genus, GbifSpeciesLookup.GenusRank);
            }
            if (false == string.IsNullOrWhiteSpace(definition.family))
            {
                return (definition.family, GbifSpeciesLookup.FamilyRank);
            }
            if (false == string.IsNullOrWhiteSpace(definition.order))
            {
                return (definition.order, GbifSpeciesLookup.OrderRank);
            }
            if (false == string.IsNullOrWhiteSpace(definition.class_))
            {
                return (definition.class_, GbifSpeciesLookup.ClassRank);
            }
            if (false == string.IsNullOrWhiteSpace(definition.phylum))
            {
                return (definition.phylum, GbifSpeciesLookup.PhylumRank);
            }
            return string.IsNullOrWhiteSpace(definition.kingdom) ? (null, null) : (definition.kingdom, GbifSpeciesLookup.KingdomRank);
        }

        // A recognition file only has a scientific name for a species, so use the lowest rank's name for the others
        private void FillEmptyScientificNames(List<taxonomic> definitions)
        {
            foreach (taxonomic definition in definitions.Where(definition => string.IsNullOrWhiteSpace(definition.scientificName)))
            {
                (string name, string rank) = MostSpecificName(definition);
                definition.scientificName = QualifiedIfRequested(name, rank?.ToLowerInvariant());
            }
        }

        // Create a definition for each common name, filling in the other fields if GBIF finds that name.
        private async Task<List<taxonomic>> DefinitionsFromCommonNamesAsync(IEnumerable<string> commonNames)
        {
            string[] names = [.. commonNames.Where(name => false == string.IsNullOrWhiteSpace(name)).Select(name => name.Trim())];
            return await ForEachGbifRequestAsync(names, name => name, async name =>
            {
                taxonomic definition = new() { vernacularNames = RecognitionFileTaxonomy.NewVernacularNames(name) };
                FillFieldsFromCommonNameMatch(definition, await GbifSpeciesLookup.FindByCommonNameAsync(name, Token));
                return definition;
            });
        }

        // A definition is left as it is if GBIF didn't find its common name
        private void FillFieldsFromCommonNameMatch(taxonomic definition, GbifSpeciesMatch match)
        {
            if (match == null)
            {
                return;
            }
            definition.scientificName = QualifiedIfRequested(match.ScientificName, match.TaxonRank);
            definition.taxonID = match.TaxonID;
            definition.taxonRank = match.TaxonRank;
            definition.kingdom = match.Kingdom;
            definition.phylum = match.Phylum;
            definition.class_ = match.Class;
            definition.order = match.Order;
            definition.family = match.Family;
            definition.genus = match.Genus;
        }

        // Do a GBIF request for each item, a few at a time, while showing a progress bar with a cancel button.
        // The results are in the items' order. Throws an OperationCanceledException if the user cancels.
        private async Task<List<TResult>> ForEachGbifRequestAsync<TItem, TResult>(IReadOnlyCollection<TItem> items, Func<TItem, string> getItemName, Func<TItem, Task<TResult>> request)
        {
            TokenReset();
            int completed = 0;
            Progress.Report(new(0, $"Looking up {items.Count} name(s) in GBIF...", true, false));
            BusyCancelIndicator.IsBusy = true;
            WindowCloseButtonIsEnabled(false);
            using SemaphoreSlim simultaneousRequests = new(4);
            try
            {
                return [.. await Task.WhenAll(items.Select(async item =>
                {
                    await simultaneousRequests.WaitAsync(Token);
                    try
                    {
                        Token.ThrowIfCancellationRequested();
                        TResult result = await request(item);
                        completed++;
                        Progress.Report(new(100 * completed / items.Count, $"Looked up {completed} of {items.Count} name(s) in GBIF: {getItemName(item)}", true, false));
                        return result;
                    }
                    finally
                    {
                        simultaneousRequests.Release();
                    }
                }))];
            }
            finally
            {
                WindowCloseButtonIsEnabled(true);
                BusyCancelIndicator.IsBusy = false;
            }
        }

        private string QualifiedIfRequested(string name, string rank)
        {
            return appendQualifiers ? GbifSpeciesLookup.QualifiedName(name, rank) : name;
        }
        #endregion

        // Going on to the conflict pages depends on whether the selection has any conflicts. Otherwise there is nothing else to ask.
        private void AddOrReplace_OnChecked(object sender, RoutedEventArgs e)
        {
            if (newDefinitions != null)
            {
                UpdateForAddOrReplace();
            }
        }

        private void UpdateForAddOrReplace()
        {
            List<DefinitionConflictItem> conflicts = ReplacesList ? fileConflicts : ConflictsWhenAdding();
            bool hasMorePages = conflicts.Count > 0;
            PageAddOrReplace.CanSelectNextPage = hasMorePages;
            PageAddOrReplace.NextButtonVisibility = hasMorePages ? WizardPageButtonVisibility.Visible : WizardPageButtonVisibility.Collapsed;
            PageAddOrReplace.CanFinish = false == hasMorePages;
            PageAddOrReplace.FinishButtonVisibility = hasMorePages ? WizardPageButtonVisibility.Collapsed : WizardPageButtonVisibility.Visible;
            if (hasMorePages)
            {
                ShowConflicts(conflicts);
            }
        }

        private void ShowConflicts(List<DefinitionConflictItem> conflicts)
        {
            IntroMessage.Text =
                $"There are {conflicts.Count} conflict(s), so you need to decide what to keep." + Environment.NewLine + Environment.NewLine +
                "A conflict can be between:" + Environment.NewLine +
                "  \u2022 A row in your list and a definition in the file: the same taxon with different details, or the same common name with different scientific names." + Environment.NewLine +
                "  \u2022 Two definitions in the file that share a scientific or common name." + Environment.NewLine +
                "  \u2022 Two rows in your list that share a scientific or common name." + Environment.NewLine + Environment.NewLine +
                "On the next page, each conflict is a small table with its differences in red. To the left of each row, select which one to keep:" + Environment.NewLine +
                "  \u2022 current is what is in your list, and new is what is in the file. Current is initially selected." + Environment.NewLine +
                "  \u2022 file row and list row are the two rows that share a name. Both are initially kept." + Environment.NewLine +
                "  \u2022 both, where offered, keeps both of the rows." + Environment.NewLine + Environment.NewLine +
                "Definitions that don't conflict are added automatically. Selecting OK applies everything, while Cancel leaves your list as it was.";
            CreateConflictTables(conflicts);
        }

        #region Creating the conflict tables
        private void CreateConflictTables(List<DefinitionConflictItem> conflicts)
        {
            ConflictsPanel.Children.Clear();
            currentButtons.Clear();
            newButtons.Clear();
            bothButtons.Clear();

            // Only show the fields that have a value in at least one of the conflicts
            int[] fields = [.. Enumerable.Range(0, RecognitionFileTaxonomy.FieldNames.Length)
                .Where(field => conflicts.Exists(item =>
                    false == string.IsNullOrWhiteSpace(RecognitionFileTaxonomy.FieldValues(item.First)[field]) ||
                    false == string.IsNullOrWhiteSpace(RecognitionFileTaxonomy.FieldValues(item.Second)[field])))];

            for (int i = 0; i < conflicts.Count; i++)
            {
                ConflictsPanel.Children.Add(new TextBlock
                {
                    Text = $"{i + 1}. {Explanation(conflicts[i].Conflict)}",
                    FontWeight = FontWeights.Bold,
                    Margin = new(0, i == 0 ? 0 : 15, 0, 3)
                });
                ConflictsPanel.Children.Add(CreateConflictTable(conflicts[i], fields, $"Conflict{i}"));
            }

            // These buttons only apply to conflicts between the list and the file
            Visibility listVersusFileVisibility = conflicts.Exists(item => item.IsListVersusFile) ? Visibility.Visible : Visibility.Collapsed;
            SelectAllCurrent.Visibility = listVersusFileVisibility;
            SelectAllNew.Visibility = listVersusFileVisibility;
        }

        private static string Explanation(DefinitionConflict conflict)
        {
            return conflict switch
            {
                DefinitionConflict.SameCommonName => "Same common name, different scientific names. These may be different taxa, or one may be wrong.",
                DefinitionConflict.AnotherRow => "Another row in your list for this taxon. Selecting new removes this row if the new definition is already used elsewhere.",
                DefinitionConflict.FileSameScientificName => "Two definitions in the file have the same scientific name. They may be duplicates, or one may be wrong.",
                DefinitionConflict.FileSameCommonName => "Two definitions in the file have the same common name. They may be different taxa, or one may be wrong.",
                DefinitionConflict.ListSameScientificName => "Two rows in your list have the same scientific name. They may be duplicates, or one may be wrong.",
                DefinitionConflict.ListSameCommonName => "Two rows in your list have the same common name. They may be different taxa, or one may be wrong.",
                _ => "Same taxon, different details."
            };
        }

        // A header, then a row for each definition, each with a radio button to its left.
        // The columns are the same size in all the tables, so they line up.
        private Grid CreateConflictTable(DefinitionConflictItem item, int[] fields, string groupName)
        {
            Grid table = new() { HorizontalAlignment = HorizontalAlignment.Left };
            table.ColumnDefinitions.Add(new() { Width = GridLength.Auto, SharedSizeGroup = "Selection" });
            foreach (int field in fields)
            {
                table.ColumnDefinitions.Add(new() { Width = GridLength.Auto, SharedSizeGroup = $"Field{field}" });
            }
            for (int row = 0; row < 3; row++)
            {
                table.RowDefinitions.Add(new() { Height = GridLength.Auto });
            }

            string[] currentValues = RecognitionFileTaxonomy.FieldValues(item.First);
            string[] newValues = RecognitionFileTaxonomy.FieldValues(item.Second);
            AddCell(table, 0, 0, null, Brushes.WhiteSmoke);
            for (int i = 0; i < fields.Length; i++)
            {
                int field = fields[i];
                bool isDifferent = false == RecognitionFileTaxonomy.AreSame(currentValues[field], newValues[field]);
                AddCell(table, 0, i + 1, CreateText(RecognitionFileTaxonomy.FieldNames[field], true, false), Brushes.WhiteSmoke);
                AddCell(table, 1, i + 1, CreateText(currentValues[field], isDifferent, isDifferent), null);
                AddCell(table, 2, i + 1, CreateText(newValues[field], isDifferent, isDifferent), null);
            }

            (string firstLabel, string secondLabel, string firstTooltip, string secondTooltip, string bothLabel, string bothTooltip) = ChoiceText(item);
            RadioButton firstButton = CreateChoiceButton(firstLabel, groupName, item, DefinitionChoice.KeepFirst, firstTooltip);
            RadioButton secondButton = CreateChoiceButton(secondLabel, groupName, item, DefinitionChoice.KeepSecond, secondTooltip);
            AddCell(table, 1, 0, firstButton, null);
            AddCell(table, 2, 0, secondButton, null);
            if (item.IsListVersusFile)
            {
                currentButtons.Add(firstButton);
                newButtons.Add(secondButton);
            }

            if (item.Conflict != DefinitionConflict.SameTaxon)
            {
                table.RowDefinitions.Add(new() { Height = GridLength.Auto });
                RadioButton bothButton = CreateChoiceButton(bothLabel, groupName, item, DefinitionChoice.KeepBoth, bothTooltip);
                bothButton.Margin = new(3, 5, 0, 0);
                Grid.SetRow(bothButton, 3);
                Grid.SetColumnSpan(bothButton, fields.Length + 1);
                table.Children.Add(bothButton);
                bothButtons.Add(bothButton);
            }
            return table;
        }

        private static (string FirstLabel, string SecondLabel, string FirstTooltip, string SecondTooltip, string BothLabel, string BothTooltip) ChoiceText(DefinitionConflictItem item)
        {
            if (item.IsListVersusFile)
            {
                return ("current", "new", "Leave this row in your list as it is.", "Use the file's definition instead of this row in your list.",
                    "both: keep current, and add new as its own row", "Leave this row in your list as it is, and add the file's definition as a separate row.");
            }
            return item.Conflict is DefinitionConflict.FileSameScientificName or DefinitionConflict.FileSameCommonName
                ? ("file row 1", "file row 2", "Import this definition, but not the other one.", "Import this definition, but not the other one.", "both: import both rows", "Import both definitions as separate rows.")
                : ("list row 1", "list row 2", "Keep this row in your list, and remove the other one.", "Keep this row in your list, and remove the other one.", "both: keep both rows", "Keep both rows in your list.");
        }

        private static RadioButton CreateChoiceButton(string label, string groupName, DefinitionConflictItem item, DefinitionChoice choice, string tooltip)
        {
            RadioButton button = new() { Content = label, GroupName = groupName, ToolTip = tooltip, VerticalContentAlignment = VerticalAlignment.Center, IsChecked = item.Choice == choice };
            button.Checked += (_, _) => item.Choice = choice;
            return button;
        }

        private static TextBlock CreateText(string text, bool isBold, bool isRed)
        {
            return new()
            {
                Text = text ?? string.Empty,
                FontWeight = isBold ? FontWeights.Bold : FontWeights.Normal,
                Foreground = isRed ? Brushes.Red : Brushes.Black
            };
        }

        private static void AddCell(Grid table, int row, int column, UIElement content, Brush background)
        {
            Border cell = new()
            {
                BorderBrush = Brushes.LightGray,
                BorderThickness = new(column == 0 ? 1 : 0, row == 0 ? 1 : 0, 1, 1),
                Padding = new(4, 2, 4, 2),
                Background = background,
                Child = content
            };
            Grid.SetRow(cell, row);
            Grid.SetColumn(cell, column);
            table.Children.Add(cell);
        }
        #endregion

        #region Select all
        private void SelectAllCurrent_OnClick(object sender, RoutedEventArgs e)
        {
            currentButtons.ForEach(button => button.IsChecked = true);
        }

        private void SelectAllNew_OnClick(object sender, RoutedEventArgs e)
        {
            newButtons.ForEach(button => button.IsChecked = true);
        }

        private void SelectAllBoth_OnClick(object sender, RoutedEventArgs e)
        {
            bothButtons.ForEach(button => button.IsChecked = true);
        }
        #endregion
    }
}
