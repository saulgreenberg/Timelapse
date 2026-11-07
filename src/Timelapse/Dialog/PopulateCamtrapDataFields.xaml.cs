using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;
using Newtonsoft.Json;
using Timelapse.Constant;
using Timelapse.Database;
using Timelapse.DataStructures;
using Timelapse.DataTables;
using Timelapse.DebuggingSupport;
using Timelapse.Standards;
using Timelapse.Util;
using TimelapseTemplateEditor.Dialog;

namespace Timelapse.Dialog
{
    /// <summary>
    /// Interaction logic for PopulateFieldWithEpisodeData.xaml
    /// </summary>
    public partial class PopulateCamtrapDataFields
    {
        #region Private Variables

        private readonly FileDatabase fileDatabase;
        private double TotalImages;

        // Tracks whether any changes to the data or database are made
        private bool IsAnyDataUpdated;

        // The data package's taxonomic list to save once the fields are populated, or null to leave it as it is
        private List<taxonomic> taxonomicListToSave;
        private string taxonomicFeedback = string.Empty;

        // The data package is the folder metadata at this level
        private const int DataPackageLevel = 1;

        #endregion

        public PopulateCamtrapDataFields(Window owner, FileDatabase fileDatabase) : base(owner)
        {
            ThrowIf.IsNullArgument(fileDatabase, nameof(fileDatabase));
            this.fileDatabase = fileDatabase;
            InitializeComponent();
            FormattedDialogHelper.SetupStaticReferenceResolver(Message);
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            this.Message.BuildContentFromProperties();
            // Set up a progress handler that will update the progress bar
            InitalizeProgressHandler(BusyCancelIndicator);

            // Set up the initial UI and values

            // Show the current settings
            if (Episodes.Episodes.TimeThreshold >= TimeSpan.FromMinutes(1))
            {
                RunCurrentSettings.Text = string.Format(Episodes.Episodes.TimeThreshold.ToString("m':'ss"));
                RunCurrentSettingsLabel.Text = "minutes";
            }
            else
            {
                RunCurrentSettings.Text = string.Format(Episodes.Episodes.TimeThreshold.ToString("ss"));
                RunCurrentSettingsLabel.Text = "seconds";
            }
            InitializeTaxonomicOptions();
        }

        #region Closing and Disposing

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            DialogResult = Token.IsCancellationRequested || IsAnyDataUpdated;
        }

        #endregion

        #region Button callbacks

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
            // The taxonomic information may need the user's input, so it is gathered before anything is changed.
            // If that is cancelled or fails, the dialog stays as it was.
            if (false == await PrepareTaxonomicListAsync())
            {
                return;
            }

            // Update the UI before starting the operation,
            CancelButton.IsEnabled = false;
            CancelButton.IsEnabled = false;
            StartDoneButton.Content = "_Processing";
            StartDoneButton.Click -= Start_Click;
            StartDoneButton.Click += Done_Click;
            StartDoneButton.IsEnabled = true;
            BusyCancelIndicator.IsBusy = true;
            WindowCloseButtonIsEnabled(false);

            // This call does all the actual populating...
            bool isCompleted = await PopulateAsync().ConfigureAwait(true);
            if (isCompleted && taxonomicListToSave != null)
            {
                SaveTaxonomicList();
            }

            // Update the UI to its final state
            StartDoneButton.IsEnabled = true;
            StartDoneButton.Content = "_Done";
            BusyCancelIndicator.IsBusy = false;
            WindowCloseButtonIsEnabled(true);
            TextBlockFeedbackLine1.Text = isCompleted
                ? $"Done. {TotalImages} files were populated with basic CamtrapDP data."
                : "Operation cancelled.";
            TextBlockFeedbackLine2.Text = isCompleted
                ? taxonomicFeedback
                : "No changes were made";
            PrimaryPanel1a.Visibility = Visibility.Collapsed;
            PrimaryPanel1b.Visibility = Visibility.Collapsed;
            PrimaryPanel1c.Visibility = Visibility.Collapsed;
            FeedbackPanel.Visibility = Visibility.Visible;
        }

        // Synopsis: Populate selected CamtrapDP fields 
        // - mediaID, observationID, fileMediaType are not done here, as they are populated when images are loaded or duplicated
        private async Task<bool> PopulateAsync()
        {
            bool eventBasedStrategy = this.RBTagOnyRepresentativeImage.IsChecked == true;
            return await Task.Run(() =>
            {
                TotalImages = fileDatabase.CountAllCurrentlySelectedFiles;

                int imageIndex = 0;
                string dataLabelObservationLevel = "observationLevel";
                string dataLabelEventID = "eventID";

                List<ColumnTuplesWithWhere> imagesToUpdate = [];

                // Determine whether we are using the event-based strategy (one representative image per event) or the episode-based strategy (all images in an episode) 
                // based on the RadioButton selection


                // Now populate each image in the FileTable
                while (imageIndex < TotalImages)
                {
                    // Get the images in this episode of images, which could include singletons
                    Episodes.Episodes.Reset();
                    Episodes.Episodes.EpisodeGetEpisodesInRange(fileDatabase.FileTable, imageIndex, Int32.MaxValue);

                    // Provide feedback if the operation was cancelled during the database update
                    if (Token.IsCancellationRequested)
                    {
                        return false;
                    }

                    // Provide feedback to the busy indicator every now and then
                    if (ReadyToRefresh())
                    {
                        int percentDone = Convert.ToInt32(imageIndex / TotalImages * 100.0);
                        Progress.Report(new(percentDone,
                            $"Processing {imageIndex}/{TotalImages} images.  ", true, false));
                    }

                    string observationLevel;

                    List<ColumnTuple> observationLevelColumnTuple;

                    if (Episodes.Episodes.EpisodesDictionary.Count <= 1)
                    {
                        //
                        //  Case 1. SINGLETON. This file is the only file in this episode of files
                        //
                        long ID = fileDatabase.FileTable[imageIndex].ID;
                        DateTime fileDateTime = fileDatabase.FileTable[imageIndex].DateTime;

                        // fileName and filePath
                        HelperPopulateFileNameAndRelativePath(imagesToUpdate,
                            fileDatabase.FileTable[imageIndex].RelativePath,
                            fileDatabase.FileTable[imageIndex].File, ID);

                        // eventStart/End: As its a singleton, the eventStart and eventEnd date will be the date/time of this file.
                        // TODO: if its a video, we really should adjust the end time to be the start time + duration
                        HelperPopulateStartEndEventDate(imagesToUpdate, fileDateTime, fileDateTime, ID);

                        // eventID: As its a singleton, it gets a unique eventID
                        HelperPopulateDataLabelWithGuid(imagesToUpdate, dataLabelEventID, ID);

                        // observationLevel: Depends on strategy
                        observationLevel = eventBasedStrategy
                            ? "event"
                            : "media";
                        observationLevelColumnTuple = [new(dataLabelObservationLevel, observationLevel)];
                        imagesToUpdate.Add(new(observationLevelColumnTuple, ID));

                        // deploymentID
                        HelperPopulateDeploymentID(imagesToUpdate, fileDatabase.FileTable[imageIndex].RelativePath, CamtrapDPConstants.Media.DeploymentID, ID);

                        // timestamp: we set the timestamp to be the same as the date/time of this image
                        HelperPopulateTimeStamp(imagesToUpdate, fileDatabase.FileTable[imageIndex].DateTime, ID);
                        imageIndex++;
                    }
                    else
                    {
                        //
                        //  Case 2. EPISODE OF FILES. 
                        //
                        List<ColumnTuple> eventIDColumnTuple;
                        if (eventBasedStrategy)
                        {
                            //
                            //  Case 2a. EPISODE OF FILES, EVENT BASED STRATEGY. 
                            //

                            // These fields (eventStart, eventEnd, observationLevel, eventID) are common for all files in this episode
                            // eventStart/End: set  to the date of the first image and last image of this episode 
                            var eventStartDate = fileDatabase.FileTable[Episodes.Episodes.EpisodesDictionary.Keys.First()].DateTime;
                            var eventEndDate = fileDatabase.FileTable[Episodes.Episodes.EpisodesDictionary.Keys.Last()].DateTime;
                            observationLevelColumnTuple = [new(dataLabelObservationLevel, "event")];
                            eventIDColumnTuple = [new(dataLabelEventID, Guid.NewGuid().ToString())];
                            foreach (KeyValuePair<int, Tuple<int, int>> unused in Episodes.Episodes.EpisodesDictionary)
                            {
                                // Populate all files in this episode
                                long ID = fileDatabase.FileTable[imageIndex].ID;

                                // fileName and filePath
                                HelperPopulateFileNameAndRelativePath(imagesToUpdate, 
                                    fileDatabase.FileTable[imageIndex].RelativePath,
                                    fileDatabase.FileTable[imageIndex].File, ID);

                                HelperPopulateStartEndEventDate(imagesToUpdate, eventStartDate, eventEndDate, ID);
                                imagesToUpdate.Add(new(eventIDColumnTuple, ID));
                                imagesToUpdate.Add(new(observationLevelColumnTuple, ID));

                                // observationLevel: Depends on strategy
                                imagesToUpdate.Add(new(observationLevelColumnTuple, ID));

                                // deploymentID
                                HelperPopulateDeploymentID(imagesToUpdate, fileDatabase.FileTable[imageIndex].RelativePath, CamtrapDPConstants.Media.DeploymentID, ID);

                                // timestamp: we set the timestamp to be the same as the date/time of this image
                                HelperPopulateTimeStamp(imagesToUpdate, fileDatabase.FileTable[imageIndex].DateTime, ID);
                                imageIndex++;
                            }
                        }
                        else
                        {
                            //
                            //  Case 2b. EPISODE OF FILES, SINGLE IMAGE TAGGING STRATEY 
                            //
                            // observationLevel: Depends on strategy
                            observationLevel = "media";
                            observationLevelColumnTuple = [new(dataLabelObservationLevel, observationLevel)];

                            // eventID: Every event in this episode has the same eventID
                            eventIDColumnTuple = [new(dataLabelEventID, Guid.NewGuid().ToString())];

                            foreach (KeyValuePair<int, Tuple<int, int>> unused in Episodes.Episodes.EpisodesDictionary)
                            {

                                long ID = fileDatabase.FileTable[imageIndex].ID;

                                // fileName and filePath
                                HelperPopulateFileNameAndRelativePath(imagesToUpdate,
                                    fileDatabase.FileTable[imageIndex].RelativePath,
                                    fileDatabase.FileTable[imageIndex].File, ID);

                                // Start/end date are the timestamp of the media in this episode
                                HelperPopulateStartEndEventDate(imagesToUpdate, fileDatabase.FileTable[imageIndex].DateTime, fileDatabase.FileTable[imageIndex].DateTime, ID);

                                // We use the same start/end date, eventID, and observation level for all images in this episode
                                imagesToUpdate.Add(new(eventIDColumnTuple, fileDatabase.FileTable[imageIndex].ID));
                                imagesToUpdate.Add(new(observationLevelColumnTuple, ID));

                                // deploymentID
                                HelperPopulateDeploymentID(imagesToUpdate, fileDatabase.FileTable[imageIndex].RelativePath, CamtrapDPConstants.Media.DeploymentID, ID);

                                // timestamp: we set the timestamp to be the same as the date/time of this image
                                HelperPopulateTimeStamp(imagesToUpdate, fileDatabase.FileTable[imageIndex].DateTime, ID);
                                imageIndex++;
                            }
                        }
                    }
                }

                IsAnyDataUpdated = true;
                Progress.Report(new(100,
                    $"Writing CamtrapDP data for {TotalImages} files. Please wait...", false, true));
                fileDatabase.UpdateFiles(imagesToUpdate, ThrottleValues.BackgroundWriteExtendedBusyTimeoutMs);

                return true;
            }, Token).ConfigureAwait(true);
        }

        private void Done_Click(object sender, RoutedEventArgs e)
        {

            // We return true if the database was altered but also if there was a cancellation, as a cancelled operation
            // may have changed the FileTable (but not database) date entries. Returning true will reset them, as a FileSelectAndShow will be done.
            // Kinda hacky as it expects a certain behaviour of the caller, but it works.
            DialogResult = Token.IsCancellationRequested || IsAnyDataUpdated;
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = Token.IsCancellationRequested || IsAnyDataUpdated;
        }

        #endregion

        #region Taxonomic information in the data package
        // The import options need the data package, and the GBIF option also needs names in the Scientific Name field's menu
        private void InitializeTaxonomicOptions()
        {
            List<string> reasons = [];
            if (DataPackageRow() == null)
            {
                RBTaxonomicFromScientificNames.IsEnabled = false;
                RBTaxonomicFromSpeciesList.IsEnabled = false;
                reasons.Add("The data package hasn't been created yet, so taxonomic information can't be imported into it. Open the DataPackage tab in the folder metadata to create it, then return here.");
            }
            else if (ScientificNamesInMenu().Count == 0)
            {
                RBTaxonomicFromScientificNames.IsEnabled = false;
                reasons.Add("The Scientific Name data field has no menu items to import.");
            }
            TaxonomicUnavailableText.Text = string.Join(" ", reasons);
            TaxonomicUnavailableText.Visibility = reasons.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private MetadataRow DataPackageRow()
        {
            return fileDatabase.MetadataTablesByLevel != null
                   && fileDatabase.MetadataTablesByLevel.TryGetValue(DataPackageLevel, out DataTableBackedList<MetadataRow> rows)
                   && rows.RowCount == 1
                ? rows[0]
                : null;
        }

        // The names in the Scientific Name field's menu, without the qualifier some have, e.g., Ursus sp.
        private List<string> ScientificNamesInMenu()
        {
            return [.. CamtrapDPGbifImport.ScientificNameMenuItems(fileDatabase).Select(GbifSpeciesLookup.WithoutQualifier)];
        }

        // The definitions already in the data package, or null if they can't be read
        private List<taxonomic> ExistingTaxonomicList()
        {
            string json = DataPackageRow()?[CamtrapDPConstants.DataPackage.Taxonomic];
            if (string.IsNullOrWhiteSpace(json))
            {
                return [];
            }
            try
            {
                return [.. (JsonConvert.DeserializeObject<List<taxonomic>>(json) ?? []).Where(RecognitionFileTaxonomy.HasContent)];
            }
            catch (JsonException)
            {
                return null;
            }
        }

        // Work out the taxonomic list to save, according to the option chosen. Returns false if the user cancelled, or it couldn't be done.
        // The new definitions are added to those already in the data package, leaving out any whose name is already there.
        private async Task<bool> PrepareTaxonomicListAsync()
        {
            taxonomicListToSave = null;
            taxonomicFeedback = string.Empty;
            if (RBTaxonomicDoNothing.IsChecked == true)
            {
                return true;
            }

            List<taxonomic> existing = ExistingTaxonomicList();
            if (existing == null)
            {
                Dialogs.CamtrapDPTaxonomicListUnreadableDialog(this);
                return false;
            }
            return RBTaxonomicFromSpeciesList.IsChecked == true
                ? ImportFromSpeciesList(existing)
                : await ImportFromGbifAsync(existing);
        }

        // The species list dialog asks for the file and checks it, and offers to replace the existing definitions
        private bool ImportFromSpeciesList(List<taxonomic> existing)
        {
            CamtrapDPPopulateFromSpeciesList populateDialog = new(this, existing.Count);
            if (true != populateDialog.ShowDialog())
            {
                return false;
            }

            List<taxonomic> kept = populateDialog.ReplaceExistingDefinitions ? [] : existing;
            taxonomicListToSave = CamtrapDPSpeciesList.Merged(kept, populateDialog.SpeciesList.Definitions, out int skippedCount);
            int addedCount = taxonomicListToSave.Count - kept.Count;
            List<string> notes = populateDialog.SpeciesList.Notes;
            if (skippedCount > 0)
            {
                notes.Add($"{skippedCount} definition(s) were left out, as their name was already in the list.");
            }
            if (notes.Count > 0)
            {
                Dialogs.CamtrapDPSpeciesListNotesDialog(this, addedCount, notes);
            }
            taxonomicFeedback = $"{addedCount} taxonomic definition(s) from the species list were added to the data package.";
            return true;
        }

        // Look up each name in the Scientific Name field's menu that isn't already in the data package
        private async Task<bool> ImportFromGbifAsync(List<taxonomic> existing)
        {
            List<string> menuItems = CamtrapDPGbifImport.ItemsNotInList(CamtrapDPGbifImport.ScientificNameMenuItems(fileDatabase), existing);
            List<taxonomic> found;
            List<string> notFound;
            TokenReset();
            BusyCancelIndicator.IsBusy = true;
            StartDoneButton.IsEnabled = false;
            CancelButton.IsEnabled = false;
            WindowCloseButtonIsEnabled(false);
            try
            {
                (found, notFound) = await CamtrapDPGbifImport.LookupAsync(this, menuItems, Token, Progress, isBusy => BusyCancelIndicator.IsBusy = isBusy);
            }
            catch (OperationCanceledException) when (Token.IsCancellationRequested)
            {
                return false;
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
            {
                BusyCancelIndicator.IsBusy = false;
                Dialogs.EditorSpeciesListGbifNotAvailableDialog(this, ex.Message);
                return false;
            }
            finally
            {
                BusyCancelIndicator.IsBusy = false;
                StartDoneButton.IsEnabled = true;
                CancelButton.IsEnabled = true;
                WindowCloseButtonIsEnabled(true);
            }

            // A name GBIF matched to a taxon already in the list, e.g., a synonym, is also already there
            taxonomicListToSave = CamtrapDPSpeciesList.Merged(existing, found, out int matchedExistingCount);
            int addedCount = taxonomicListToSave.Count - existing.Count;
            int alreadyThereCount = ScientificNamesInMenu().Count - menuItems.Count + matchedExistingCount;
            taxonomicFeedback = $"{addedCount} taxonomic definition(s) from GBIF were added to the data package."
                                + (alreadyThereCount > 0 ? $" {alreadyThereCount} name(s) were already there." : string.Empty)
                                + (notFound.Count > 0 ? $" Not added: {string.Join(", ", notFound)}." : string.Empty);
            return true;
        }

        private void SaveTaxonomicList()
        {
            MetadataRow row = DataPackageRow();
            if (row != null)
            {
                fileDatabase.MetadataTablesAndDatabaseUpdateValue(DataPackageLevel, row[DatabaseColumn.FolderDataPath], CamtrapDPConstants.DataPackage.Taxonomic,
                    CamtrapDPTaxonomic.SerializeTaxonomicList(taxonomicListToSave));
            }
        }
        #endregion

        #region Helpers

        // Note that we have to co-erce it to the correct format when exporting to CSV
        private static void HelperPopulateFileNameAndRelativePath(List<ColumnTuplesWithWhere> imagesToUpdate, string relativePath, string fileName,  long ID)
        {
            string fileNameDataLabel = "fileName";
            string filePathDataLabel = "filePath";

            List<ColumnTuple> fileNameColumnTuple = [new(fileNameDataLabel, fileName)];
            imagesToUpdate.Add(new(fileNameColumnTuple, ID));
            
            string relativePathWithFileName = System.IO.Path.Combine(relativePath, fileName);
            List<ColumnTuple> filePathColumnTuple = [new(filePathDataLabel, relativePathWithFileName)];
            imagesToUpdate.Add(new(filePathColumnTuple, ID));
        }

        private static void HelperPopulateStartEndEventDate(List<ColumnTuplesWithWhere> imagesToUpdate, DateTime startDate, DateTime endDate, long ID)
        {
            string dataLabelEventStart = "eventStart";
            string dataLabelEventEnd = "eventEnd";

            List<ColumnTuple> eventStartColumnTuple = [new(dataLabelEventStart, startDate)];
            List<ColumnTuple> eventEndColumnTuple = [new(dataLabelEventEnd, endDate)];
            imagesToUpdate.Add(new(eventStartColumnTuple, ID));
            imagesToUpdate.Add(new(eventEndColumnTuple, ID));
        }

        private static void HelperPopulateTimeStamp(List<ColumnTuplesWithWhere> imagesToUpdate, DateTime date, long ID)
        {
            string dataLabel = "timestamp";

            List<ColumnTuple> columnTuple = [new(dataLabel, date)];
            imagesToUpdate.Add(new(columnTuple, ID));
        }

        private static void HelperPopulateDataLabelWithGuid(List<ColumnTuplesWithWhere> imagesToUpdate, string dataLabel, long ID)
        {
            List<ColumnTuple> columnTuple = [new(dataLabel, Guid.NewGuid().ToString())];
            imagesToUpdate.Add(new(columnTuple, ID));
        }

        private static void HelperPopulateDeploymentID(List<ColumnTuplesWithWhere> imagesToUpdate, string relativePath, string dataLabel, long ID)
        {
            List<ColumnTuple> columnTuple = [new(dataLabel, relativePath)];
            imagesToUpdate.Add(new(columnTuple, ID));
        }
        #endregion
    }
}
