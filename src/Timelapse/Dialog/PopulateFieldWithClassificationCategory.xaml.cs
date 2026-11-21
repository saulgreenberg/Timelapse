using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Timelapse.Database;
using Timelapse.DataStructures;
using Timelapse.DataTables;
using Timelapse.DebuggingSupport;
using Timelapse.Enums;
using Timelapse.Util;
using Control = Timelapse.Constant.Control;

namespace Timelapse.Dialog
{
    /// <summary>
    /// Interaction logic for PopulateFieldWithClassificationCategory.xaml
    /// Populates a data field with the classification category of each file's highest-confidence bounding box.
    /// This does the same as the PopulateFieldWithRecognitionData dialog when only the Classification category is output as raw data.
    /// </summary>
    public partial class PopulateFieldWithClassificationCategory
    {
        #region Private Variables
        private readonly FileDatabase fileDatabase;
        // The Note, MultiLine and FixedChoice controls that could hold the classification category, keyed by their label
        private readonly Dictionary<string, ControlRow> controlByLabel;
        private string dataFieldLabel = string.Empty;
        #endregion

        public PopulateFieldWithClassificationCategory(Window owner, FileDatabase fileDatabase) : base(owner)
        {
            InitializeComponent();
            FormattedDialogHelper.SetupStaticReferenceResolver(Message);
            ThrowIf.IsNullArgument(fileDatabase, nameof(fileDatabase));
            controlByLabel = [];
            this.fileDatabase = fileDatabase;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            this.Message.What = this.Message.What.Replace("CLASSIFICATION_CATEGORY_EXAMPLES", GetClassificationCategoryExamples());
            this.Message.BuildContentFromProperties();
            // Set up a progress handler that will update the progress bar
            InitalizeProgressHandler(BusyCancelIndicator);

            // Construct a list showing the available text and choice fields in the combobox
            foreach (ControlRow control in fileDatabase.Controls)
            {
                if (control.Type is Control.Note or Control.MultiLine or Control.FixedChoice)
                {
                    controlByLabel.Add(control.Label, control);
                    ComboBoxSelectNoteField.Items.Add(control.Label);
                }
            }
            if (ComboBoxSelectNoteField.Items.Count == 0)
            {
                PrimaryPanel.Visibility = Visibility.Collapsed;
                FeedbackPanel.Visibility = Visibility.Visible;
            }
            else if (ComboBoxSelectNoteField.Items.Count == 1)
            {
                ComboBoxSelectNoteField.SelectedIndex = 0;
            }
        }

        // Up to four of the image set's classification category labels, formatted as examples, e.g., (e.g., [i]deer[/i], [i]elk[/i]).
        // The categories are normally already in memory, so this rarely needs a database query
        private string GetClassificationCategoryExamples()
        {
            fileDatabase.CreateClassificationCategoriesDictionaryIfNeeded();
            List<string> labels = [.. (fileDatabase.classificationCategoriesDictionary ?? [])
                .Where(category => false == string.IsNullOrWhiteSpace(category.Value))
                .Select(category => $"[i]{category.Value}[/i]")
                .Distinct()
                .Take(4)];
            return labels.Count == 0
                ? string.Empty
                : $"(e.g., {string.Join(", ", labels)})";
        }

        private async void Start_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                await Start_ClickAsync();
            }
            catch (Exception ex)
            {
                TracePrint.CatchException(ex.Message);
            }
        }

        private async Task Start_ClickAsync()
        {
            bool? isCompleted = await PopulateAsync().ConfigureAwait(true);
            if (isCompleted == null)
            {
                // The user cancelled the overwrite or Choice field mismatch warning, so stay in this dialog to choose differently
                return;
            }
            DialogResult = isCompleted;
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        private void ComboBoxSelectNoteField_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is ComboBox { SelectedItem: string label })
            {
                dataFieldLabel = label.Trim();
                StartDoneButton.IsEnabled = !string.IsNullOrEmpty(dataFieldLabel);
            }
        }

        // Returns true if the field was populated, false if it wasn't, or null if the user cancelled
        // the warning that existing values would be overwritten, or that some classification categories don't match a FixedChoice field's menu items
        private async Task<bool?> PopulateAsync()
        {
            bool result = false;
            Dictionary<long, string> fieldData = null;
            int overwriteCount = 0;
            BusyCancelIndicator.IsBusy = true;

            ControlRow controlToUpdate = controlByLabel[dataFieldLabel];
            string dataLabelToUpdate = controlToUpdate.DataLabel;

            // For a Choice field, categories that match a menu item, ignoring case, get the menu's own spelling,
            // so they aren't reported as mismatches, nor counted as overwrites of the same item
            Choices choices = controlToUpdate.Type == Control.FixedChoice ? Choices.ChoicesFromJson(controlToUpdate.List) : null;
            List<string> mismatchedCategories = [];

            // The classification category alone, output as raw data.
            // Files whose highest-confidence bounding box has no classification get an empty value
            await Task.Run(() =>
            {
                fieldData = fileDatabase.RecognitionsComputeFieldData(dataLabelToUpdate,
                    RecognitionFormatEnum.Raw,
                    useBboxCoords: false,
                    useDetectionConf: false,
                    useDetectionCategory: false,
                    useClassConf: false,
                    useClassCat: true,
                    Progress);
                if (choices != null && fieldData != null)
                {
                    mismatchedCategories = choices.UseChoiceSpellingAndGetMismatches(fieldData);
                }
                overwriteCount = fileDatabase.RecognitionsCountValuesToOverwrite(dataLabelToUpdate, fieldData);
            }, Token).ConfigureAwait(true);
            if (fieldData == null)
            {
                BusyCancelIndicator.IsBusy = false;
                return false;
            }

            // Warn the user if existing, different values in the field would be overwritten.
            // This comes before the Choice field warning, so cancelling it never leaves the template updated
            if (overwriteCount > 0)
            {
                BusyCancelIndicator.IsBusy = false;
                if (false == Dialogs.PopulateFieldOverwriteWarningDialog(this, controlToUpdate.Label, overwriteCount, fileDatabase.FileTable.RowCount))
                {
                    return null;
                }
                BusyCancelIndicator.IsBusy = true;
            }

            // Warn the user if some of the classification categories don't match a FixedChoice field's menu items
            if (controlToUpdate.Type == Control.FixedChoice)
            {
                if (mismatchedCategories.Count > 0)
                {
                    BusyCancelIndicator.IsBusy = false;
                    if (false == Dialogs.PopulateChoiceFieldWithMismatchedCategoriesDialog(this, controlToUpdate.Label, "classification", mismatchedCategories, out bool updateTemplate))
                    {
                        return null;
                    }
                    if (updateTemplate && false == GlobalReferences.MainWindow.TryAddChoicesToTemplate(dataLabelToUpdate, mismatchedCategories))
                    {
                        // Shouldn't happen, but if the template's choice list can't be read, it is left as is and the field is still populated
                        TracePrint.PrintMessage($"Could not add the classification categories to the {controlToUpdate.Label} menu");
                    }
                    BusyCancelIndicator.IsBusy = true;
                }
            }

            await Task.Run(() =>
            {
                result = fileDatabase.RecognitionsWriteFieldData(dataLabelToUpdate, fieldData, Progress);
            }, Token).ConfigureAwait(true);
            BusyCancelIndicator.IsBusy = false;
            return result;
        }
    }
}
