using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Timelapse.Constant;
using Timelapse.Controls;
using Timelapse.ControlsDataEntry;
using Timelapse.Database;
using Timelapse.DataStructures;
using Timelapse.DataTables;
using Timelapse.Util;
using DataPackage = Timelapse.Standards.CamtrapDPConstants.DataPackage;
using File = Timelapse.Constant.File;

namespace Timelapse.Standards
{
    public class CamtrapDPExportFiles
    {
        // Check to see if the camtrapDP Datapackage level exists in the database
        public static bool CamtrapDPDataPackageExists(FileDatabase database)
        {
            return database.MetadataTablesByLevel[1].RowCount == 1;
        }

        public static bool CamtrapDPAllDeploymentLevelsExists(FileDatabase database, List<string> missingDeployments)
        {
            DataTableBackedList<MetadataRow> deploymentLevels = database.MetadataTablesByLevel[2];
            List<string> relativePaths = [.. database.GetRelativePathsInCurrentSelection];
            if (deploymentLevels.RowCount == 0)
            {
                missingDeployments.AddRange(relativePaths);
                return false;
            }

            List<string> deploymentpaths = [];
            foreach (MetadataRow deploymentLevel in deploymentLevels)
            {
                deploymentpaths.Add(deploymentLevel[DatabaseColumn.FolderDataPath]);
            }

            foreach (string relativePath in relativePaths)
            {
                if (deploymentpaths.Contains(relativePath) == false)
                {
                    missingDeployments.Add(relativePath);
                }
            }
            return missingDeployments.Count != relativePaths.Count;
        }

        #region Public static method - ExportCamtrapDPDataPackageToJsonFile

        // Export the datapackage to a file, where data is written as a json string that matches the CamtrapDP specification
        // If any of the required fields (according to the CamtrapDP specification) are missing, generate a list of text messages
        // that lists the missing required fields in a form appropriate to put into a dialog box.
        // If something goes wrong, return null
        public static async Task<List<string>> ExportCamtrapDPDataPackageToJsonFile(FileDatabase database, string dataPackageFilePath)
        {
            CamtrapDPDataPackage datapackage = new();
            resources resourceDeployment = new();
            resources resourceMedia = new();
            resources resourceObservations = new();
            datapackage.resources.Add(resourceDeployment);
            datapackage.resources.Add(resourceMedia);
            datapackage.resources.Add(resourceObservations);

            return await Task.Run(() =>
            {
                try
                {
                    // Populate the datapackage Data structure that will be serialized as a json string
                    // Get level 1

                    MetadataInfoRow infoRow = null;
                    foreach (MetadataInfoRow tmpInfoRow in database.MetadataInfo)
                    {
                        if (tmpInfoRow is { Level: 1 })
                        {
                            infoRow = tmpInfoRow;
                            break;
                        }
                    }

                    if (null == infoRow)
                    {
                        return null;
                    }

                    int level = infoRow.Level;

                    // Get the rows for this level
                    DataTableBackedList<MetadataRow> rows = false == database.MetadataTablesByLevel.TryGetValue(level, out var value)
                        ? null
                        : value;
                    if (rows == null)
                    {
                        return null;
                    }

                    // Get the data labels
                    Dictionary<string, string> dataLabelsAndTypes = database.MetadataGetDataLabels(level);
                    JsonSerializerSettings settings = new()
                    {
                        NullValueHandling = NullValueHandling.Ignore,
                        Formatting = Formatting.Indented
                    };

                    foreach (MetadataRow row in rows)
                    {
                        foreach (KeyValuePair<string, string> dataLabelAndType in dataLabelsAndTypes)
                        {
                            switch (dataLabelAndType.Key)
                            {
                                // Populate the three resources - deployment, media and observations
                                case DataPackage.Resources.Resource_profile:
                                    resourceDeployment.profile = row[dataLabelAndType.Key];
                                    resourceMedia.profile = row[dataLabelAndType.Key];
                                    resourceObservations.profile = row[dataLabelAndType.Key];
                                    break;
                                case DataPackage.Resources.Deployment_name:
                                    resourceDeployment.name = row[dataLabelAndType.Key];
                                    break;
                                case DataPackage.Resources.Deployment_path:
                                    resourceDeployment.path = row[dataLabelAndType.Key];
                                    break;
                                case DataPackage.Resources.Deployment_schema:
                                    resourceDeployment.schema = row[dataLabelAndType.Key];
                                    break;
                                case DataPackage.Resources.Media_name:
                                    resourceMedia.name = row[dataLabelAndType.Key];
                                    break;
                                case DataPackage.Resources.Media_path:
                                    resourceMedia.path = row[dataLabelAndType.Key];
                                    break;
                                case DataPackage.Resources.Media_schema:
                                    resourceMedia.schema = row[dataLabelAndType.Key];
                                    break;
                                case DataPackage.Resources.Observations_name:
                                    resourceObservations.name = row[dataLabelAndType.Key];
                                    break;
                                case DataPackage.Resources.Observations_path:
                                    resourceObservations.path = row[dataLabelAndType.Key];
                                    break;
                                case DataPackage.Resources.Observations_schema:
                                    resourceObservations.schema = row[dataLabelAndType.Key];
                                    break;

                                // Single Fields
                                case DataPackage.Profile:
                                    datapackage.profile = row[dataLabelAndType.Key];
                                    break;
                                case DataPackage.Name:
                                    datapackage.name = row[dataLabelAndType.Key];
                                    break;
                                case DataPackage.IdAlias:
                                    datapackage.id = row[dataLabelAndType.Key];
                                    break;
                                case DataPackage.Created:
                                    datapackage.created = DateTime.TryParseExact(row[dataLabelAndType.Key], CamtrapDPConstants.DateTimeFormats.TimelapseFullDateTimeFormat,
                                        CultureInfo.InvariantCulture,
                                        DateTimeStyles.None, out DateTime dateTime)
                                        ? dateTime.ToString(CamtrapDPConstants.DateTimeFormats.CamtrapDateTimeFormat)
                                        : row[dataLabelAndType.Key];
                                    break;
                                case DataPackage.Title:
                                    datapackage.title = string.IsNullOrWhiteSpace(row[dataLabelAndType.Key]) ? null : row[dataLabelAndType.Key];
                                    break;
                                case DataPackage.Contributors:
                                    datapackage.contributors = JsonConvert.DeserializeObject<List<contributors>>(row[dataLabelAndType.Key], settings);
                                    break;
                                case DataPackage.Description:
                                    datapackage.description = string.IsNullOrWhiteSpace(row[dataLabelAndType.Key]) ? null : row[dataLabelAndType.Key];
                                    break;
                                case DataPackage.Version:
                                    datapackage.version = string.IsNullOrWhiteSpace(row[dataLabelAndType.Key]) ? null : row[dataLabelAndType.Key];
                                    break;
                                case DataPackage.Keywords:
                                    datapackage.keywords = CSVHelpers.CommaSeparatedStringToList(row[dataLabelAndType.Key]);
                                    break;
                                case DataPackage.Image:
                                    datapackage.image = string.IsNullOrWhiteSpace(row[dataLabelAndType.Key]) ? null : row[dataLabelAndType.Key];
                                    break;
                                case DataPackage.Homepage:
                                    datapackage.homepage = string.IsNullOrWhiteSpace(row[dataLabelAndType.Key]) ? null : row[dataLabelAndType.Key];
                                    break;

                                // Sources object
                                case DataPackage.Sources:
                                    datapackage.sources = JsonConvert.DeserializeObject<List<sources>>(row[dataLabelAndType.Key], settings);
                                    break;

                                // Licenses object
                                case DataPackage.Licenses:
                                    datapackage.licenses = JsonConvert.DeserializeObject<List<licenses>>(row[dataLabelAndType.Key], settings);
                                    break;

                                case DataPackage.BibliographicCitation:
                                    datapackage.bibliographicCitation = string.IsNullOrWhiteSpace(row[dataLabelAndType.Key]) ? null : row[dataLabelAndType.Key];
                                    break;

                                // Project fields
                                case DataPackage.Project.Id:
                                    datapackage.project.id = string.IsNullOrWhiteSpace(row[dataLabelAndType.Key]) ? null : row[dataLabelAndType.Key];
                                    break;
                                case DataPackage.Project.Title:
                                    datapackage.project.title = string.IsNullOrWhiteSpace(row[dataLabelAndType.Key]) ? null : row[dataLabelAndType.Key];
                                    break;
                                case DataPackage.Project.Acronym:
                                    datapackage.project.acronym = string.IsNullOrWhiteSpace(row[dataLabelAndType.Key]) ? null : row[dataLabelAndType.Key];
                                    break;
                                case DataPackage.Project.Description:
                                    datapackage.project.description = string.IsNullOrWhiteSpace(row[dataLabelAndType.Key]) ? null : row[dataLabelAndType.Key];
                                    break;
                                case DataPackage.Project.Path:
                                    datapackage.project.path = string.IsNullOrWhiteSpace(row[dataLabelAndType.Key]) ? null : row[dataLabelAndType.Key];
                                    break;
                                case DataPackage.Project.SamplingDesign:
                                    datapackage.project.samplingDesign = string.IsNullOrWhiteSpace(row[dataLabelAndType.Key]) ? null : row[dataLabelAndType.Key];
                                    break;
                                case DataPackage.Project.CaptureMethod:
                                    datapackage.project.captureMethod = CSVHelpers.CommaSeparatedStringToList(row[dataLabelAndType.Key]);
                                    break;
                                case DataPackage.Project.IndividualAnimals:
                                    datapackage.project.individualAnimals = Boolean.TryParse(row[dataLabelAndType.Key], out bool boolValue) && boolValue;
                                    break;
                                case DataPackage.Project.ObservationLevel:
                                    datapackage.project.observationLevel = CSVHelpers.CommaSeparatedStringToList(row[dataLabelAndType.Key]);
                                    break;

                                // Single Fields
                                case DataPackage.CoordinatePrecision:
                                    if (string.IsNullOrWhiteSpace(row[dataLabelAndType.Key]))
                                    {
                                        datapackage.coordinatePrecision = null;
                                    }
                                    else if (Double.TryParse(row[dataLabelAndType.Key], out double coordPrecision))
                                    {
                                        datapackage.coordinatePrecision = coordPrecision;
                                    }
                                    else
                                    {
                                        datapackage.coordinatePrecision = null;
                                    }
                                    break;

                                // Spatial
                                case DataPackage.Spatial: // TODO Spatial as bounding box 
                                    string jsonString = string.IsNullOrWhiteSpace(row[dataLabelAndType.Key]) || row[dataLabelAndType.Key] == "[]"
                                        ? "{\"type\": \"FeatureCollection\", \"features\": [] }"
                                        : row[dataLabelAndType.Key];
                                    JObject jobject = JObject.Parse(jsonString);
                                    datapackage.spatial = jobject;
                                    break;

                                //Temporal Object
                                case DataPackage.Temporal.Start: // Convert to CAMTRAPDP DATE Format
                                    datapackage.temporal.start = datapackage.temporal.start = DateTime.TryParseExact(row[dataLabelAndType.Key],
                                        CamtrapDPConstants.DateTimeFormats.TimelapseDateOnlyFormat, CultureInfo.InvariantCulture,
                                        DateTimeStyles.None, out DateTime dateTimeStart)
                                        ? dateTimeStart.ToString(CamtrapDPConstants.DateTimeFormats.CamtrapDateOnlyFormat)
                                        : row[dataLabelAndType.Key];
                                    break;
                                case DataPackage.Temporal.End: // Convert to CAMTRAPDP DATE Format
                                    datapackage.temporal.end = DateTime.TryParseExact(row[dataLabelAndType.Key], CamtrapDPConstants.DateTimeFormats.TimelapseDateOnlyFormat,
                                        CultureInfo.InvariantCulture,
                                        DateTimeStyles.None, out DateTime dateTimeEnd)
                                        ? dateTimeEnd.ToString(CamtrapDPConstants.DateTimeFormats.CamtrapDateOnlyFormat)
                                        : row[dataLabelAndType.Key];
                                    break;

                                // Taxonomic Object
                                case DataPackage.Taxonomic:
                                    datapackage.taxonomic = string.IsNullOrEmpty(row[dataLabelAndType.Key]) || "[]" == row[dataLabelAndType.Key]
                                        ? null
                                        : JsonConvert.DeserializeObject<List<taxonomic>>(row[dataLabelAndType.Key], settings);
                                    break;

                                // RelatedIdentifiers Object
                                case DataPackage.RelatedIdentifiers:
                                    datapackage.relatedIdentifiers = string.IsNullOrEmpty(row[dataLabelAndType.Key]) || "[]" == row[dataLabelAndType.Key]
                                        ? null
                                        : JsonConvert.DeserializeObject<List<relatedIdentifiers>>(row[dataLabelAndType.Key], settings);
                                    break;

                                // References Object
                                case DataPackage.References:
                                    datapackage.references_ = datapackage.references_ = string.IsNullOrEmpty(row[dataLabelAndType.Key]) || "[]" == row[dataLabelAndType.Key]
                                        ? null
                                        : JsonConvert.DeserializeObject<List<string>>(row[dataLabelAndType.Key], settings);
                                    break;

                                default:
                                    // Custom fields are  handled here, where its added as a dictionary to the json under customFields, e.g.,
                                    // "customFields": {
                                    //   "customFieldName1": "Some value "
                                    // }
                                    datapackage.customFields ??= []; // create it if its null
                                    datapackage.customFields.Add(dataLabelAndType.Key, row[dataLabelAndType.Key]);
                                    break;
                            }
                        }
                    }

                    // Write the datapackage file
                    using (StreamWriter fileWriter = new(dataPackageFilePath, false))
                    {
                        StringBuilder dataPackageAsJson = new();
                        settings.Converters.Add(new JsonConverters.WhiteSpaceToNullConverter());
                        dataPackageAsJson.Append(JsonConvert.SerializeObject(datapackage, settings));
                        fileWriter.WriteLine(dataPackageAsJson);
                    }

                    //
                    // We also do a rudimentary check for missing data, where we generate a list of warning messages indicating which required fields are missing.
                    //

                    // Contributors  required fields
                    List<string> missingDataPackageFields = [];
                    if (datapackage.contributors == null)
                    {
                        missingDataPackageFields.Add("[li] Contributors: at least one is required.");
                    }
                    else
                    {
                        foreach (contributors contributor in datapackage.contributors)
                        {
                            if (contributor.title == null)
                            {
                                missingDataPackageFields.Add("[li] Contributors: a title is required for each contributor");
                            }
                        }
                    }

                    // Sources  required fields
                    if (datapackage.sources != null)
                    {
                        foreach (sources source in datapackage.sources)
                        {
                            if (source.title == null)
                            {
                                missingDataPackageFields.Add("[li] Source: a title is required for each source");
                                break;
                            }
                        }
                    }

                    // License  required fields
                    if (datapackage.licenses != null)
                    {
                        foreach (licenses license in datapackage.licenses)
                        {
                            if (license.title == null && license.path == null)
                            {
                                missingDataPackageFields.Add("[li] License: scope and at least one of a title or path is required for each license");
                            }
                        }
                    }

                    // Project required fields
                    if (datapackage.project.title == null)
                    {
                        missingDataPackageFields.Add("[li] Project: title is required but is empty.");
                    }

                    if (datapackage.project.samplingDesign == null)
                    {
                        missingDataPackageFields.Add("[li] Project: sampling design is required but is empty.");
                    }

                    if (datapackage.project.captureMethod == null || datapackage.project.captureMethod.Count == 0)
                    {
                        missingDataPackageFields.Add("[li] Project: capture method is required but is empty.");
                    }
                    //if (datapackage.project.individualAnimals == null) // bools are never null
                    //{
                    //    missingDataPackageFields.Add("[li] Project: individual animals is required but is empty.");
                    //}

                    if (datapackage.project.observationLevel == null || datapackage.project.observationLevel.Count == 0)
                    {
                        missingDataPackageFields.Add("[li] Project: observation level is required but is empty.");
                    }

                    // Spatial required fields
                    // Note should not normally be null as by default we create an empty spatial item
                    // and/or populate it with a lat/long bounding box
                    if (null == datapackage.spatial)
                    {
                        missingDataPackageFields.Add("[li] Spatial: is required.");
                    }

                    // Temporal required fields
                    if (datapackage.temporal.start == null || datapackage.temporal.end == null)
                    {
                        missingDataPackageFields.Add("[li] Temporal: both start and end are required.");
                    }

                    // Taxonomic required fields
                    if (datapackage.taxonomic == null)
                    {
                        missingDataPackageFields.Add("[li] Taxonomic: taxonomic details used by this package are required.");
                    }
                    else
                    {
                        foreach (taxonomic taxonomic in datapackage.taxonomic)
                        {
                            if (taxonomic.scientificName == null)
                            {
                                missingDataPackageFields.Add("[li] Taxonomic: a scientific name is required for each taxonomic entry");
                            }
                        }
                    }

                    // Related Identifiers required fields
                    if (datapackage.relatedIdentifiers != null)
                    {
                        foreach (relatedIdentifiers relatedIdentifiers in datapackage.relatedIdentifiers)
                        {
                            if (relatedIdentifiers.relationType == null || relatedIdentifiers.relatedIdentifier == null || relatedIdentifiers.relatedIdentifierType == null)
                            {
                                missingDataPackageFields.Add("[li] Related identifiers: relation type, identifier, and type are required for each related identifier entry");
                            }
                        }
                    }
                    return missingDataPackageFields;
                }
                catch
                {
                    return null;
                }
            }).ConfigureAwait(true);
        }

        #endregion

        #region Public Static Method - ExportCamtrapDPDeploymentToCsv
        /// <summary>
        ///  Essentially repeates the ExportMetadataToCSV code but
        /// - filters values for a few fields
        /// - removes uneeded columns
        /// - ignores export flag
        /// - ignores spreadsheet order (as validator expects things in a particular order)
        /// - does not append level columns for cross-referencing
        /// I should integrate everything, but this is just easier to do.
        /// Export all the database data associated with the selected view to the .csv file indicated in the file path
        /// </summary>
        public static async Task<List<string>> ExportCamtrapDPDeploymentToCsv(FileDatabase database, string deploymentFilePath)
        {
            return await Task.Run(() =>
            {
                try
                {
                    List<string> problemList = [];

                    // Get the Deployment info (second) level
                    int level = 2;

                    // Get the rows for the deployment level
                    DataTableBackedList<MetadataRow> rows = false == database.MetadataTablesByLevel.TryGetValue(level, out var value)
                        ? null
                        : value;

                    // Get the data labels in their original creation order, as the camtrapDP validator expects columns
                    // in a certain order (yup, brain-dead).
                    Dictionary<string, string> dataLabelsAndTypes = database.MetadataGetDataLabels(level);

                    using StreamWriter fileWriter = new(deploymentFilePath, false);
                    // Write the header as defined by the data labels in the template file.
                    // If the data label is an empty string, we use the label instead.
                    // The append sequence results in a trailing comma which is retained when writing the line.
                    StringBuilder header = new();

                    // Insert the Headers
                    bool includeComma = false;
                    foreach (KeyValuePair<string, string> dataLabelAndType in dataLabelsAndTypes)
                    {
                        header.Append(CSVHelpers.CSVToCommaSeparatedValue(dataLabelAndType.Key, includeComma));
                        includeComma = true;
                    }
                    fileWriter.WriteLine(header.ToString());

                    // Check to see if there is any data
                    if (null == rows || rows.RowCount == 0)
                    {
                        problemList.Add("[li] No deployment data is available. Did you create folder-level deployment data?");
                        return problemList;
                    }

                    // Write out each data row
                    foreach (MetadataRow row in rows)
                    {
                        includeComma = false;
                        StringBuilder rowBuilder = new();
                        // Set the DeploymentID to the relative path
                        foreach (KeyValuePair<string, string> dataLabelAndType in dataLabelsAndTypes)
                        {
                            string where = row[DatabaseColumn.FolderDataPath];
                            switch (dataLabelAndType.Key)
                            {
                                case CamtrapDPConstants.Deployment.DeploymentID:
                                    // Set the deployment id to the relative path
                                    row[dataLabelAndType.Key] = row[DatabaseColumn.FolderDataPath];
                                    break;

                                case CamtrapDPConstants.Deployment.Latitude:
                                case CamtrapDPConstants.Deployment.Longitude:
                                    // Required: Lat/long values. Generate a problem report if values are not filled in- 
                                    if (string.IsNullOrWhiteSpace(row[dataLabelAndType.Key]))
                                    {
                                        problemList.Add($"[li] {dataLabelAndType.Key} in {where}: a non-empty value is required for this field.");
                                    }
                                    break;
                                case CamtrapDPConstants.Deployment.DeploymentStart:
                                case CamtrapDPConstants.Deployment.DeploymentEnd:
                                    if (string.IsNullOrWhiteSpace(row[dataLabelAndType.Key]) ||
                                        false == DateTime.TryParseExact(row[dataLabelAndType.Key], CamtrapDPConstants.DateTimeFormats.TimelapseFullDateTimeFormat,
                                            CultureInfo.InvariantCulture,
                                            DateTimeStyles.None, out DateTime _))
                                    {
                                        problemList.Add($"[li] {dataLabelAndType.Key} in {where}: a valid date/time value is required for this field.");
                                    }
                                    break;
                            }

                            // Required: Specific CamtrapDP DateTime formats
                            switch (dataLabelAndType.Value)
                            {
                                // Deployment fields only use DateTime_, but put in the code for Date_ or Time_ for added fields
                                case Control.DateTime_:
                                    // Export the  DateTime_ column in CamtrapDP format
                                    string dateTimeString = DateTime.TryParseExact(row[dataLabelAndType.Key],
                                        CamtrapDPConstants.DateTimeFormats.TimelapseFullDateTimeFormat, CultureInfo.InvariantCulture,
                                        DateTimeStyles.None, out DateTime dateTime)
                                        ? dateTime.ToString(CamtrapDPConstants.DateTimeFormats.CamtrapDateTimeFormat)
                                        : row[dataLabelAndType.Key];
                                    rowBuilder.Append(CSVHelpers.CSVToCommaSeparatedValue(dateTimeString, includeComma));
                                    includeComma = true;
                                    break;
                                case Control.Date_:
                                    // Export the  Date_ column column in CamtrapDP format
                                    string dateString = DateTime.TryParseExact(row[dataLabelAndType.Key], CamtrapDPConstants.DateTimeFormats.TimelapseDateOnlyFormat,
                                        CultureInfo.InvariantCulture,
                                        DateTimeStyles.None, out DateTime dateOnly)
                                        ? dateOnly.ToString(CamtrapDPConstants.DateTimeFormats.CamtrapDateTimeFormat)
                                        : row[dataLabelAndType.Key];
                                    rowBuilder.Append(CSVHelpers.CSVToCommaSeparatedValue(dateString, includeComma));
                                    includeComma = true;
                                    break;

                                default:
                                    rowBuilder.Append(CSVHelpers.CSVToCommaSeparatedValue(row[dataLabelAndType.Key], includeComma));
                                    includeComma = true;
                                    break;
                            }
                        }

                        // We have a row. Write it out.
                        fileWriter.WriteLine(rowBuilder.ToString());
                    }

                    return problemList;
                }
                catch
                {
                    return null;
                }
            }).ConfigureAwait(true);
        }

        #endregion

        #region Public Static Method - ExportCamtrapDPMediaObservationsToCsv

        /// <summary>
        ///  Somewhat similar to ExportMetadataToCSV code but
        /// - filters values for a few fields
        /// - removes uneeded columns
        /// - ignores export flag
        /// - ignores spreadsheet order (as validator expects things in a particular order)
        /// - does not append level columns for cross-referencing
        /// I should integrate everything, but this is just easier to do.
        /// Export all the database data associated with the selected view to the .csv file indicated in the file path
        /// </summary>
        public static async Task<List<string>> ExportCamtrapDPMediaObservationsToCsv(FileDatabase database, DataEntryControls controls, string mediaFilePath, string observationsFilePath)
        {
            Progress<ProgressBarArguments> progressHandler = new(value =>
            {
                // Update the progress bar
                CsvReaderWriter.UpdateProgressBar(GlobalReferences.BusyCancelIndicator, value.PercentDone, value.Message, value.IsCancelEnabled, value.IsIndeterminate);
            });
            IProgress<ProgressBarArguments> progress = progressHandler;
            return await Task.Run(() =>
            {
                try
                {
                    progress.Report(new(0, "Writing the CamtrapDP Media and Observations CSV file. Please wait", false, true));

                    List<string> problems = [];

                    // We split the data fields into two files
                    using StreamWriter mediaFileWriter = new(mediaFilePath, false);
                    using StreamWriter observationsFileWriter = new(observationsFilePath, false);
                    // Get all data labels
                    // Note that we preserve the order as the miniforge frictionless validate function expects the csv file to have columns in a specific order
                    List<string> dataLabels = [.. database.GetDataLabelsFromControlsByIDCreationOrder()];

                    // We construct the media and observation data labels in the same order as they are seen,
                    // so that we can write out the data in the same order.
                    List<string> mediaDataLabels = [];
                    List<string> observationDataLabels = [];

                    // Write the header as defined by the data labels in the template file.
                    // If the data label is an empty string, we use the label instead.
                    // The sequence prepends a comma except for the first column.
                    StringBuilder mediaHeader = new();
                    StringBuilder observationsHeader = new();
                    bool includeMediaComma = false;
                    bool includeObservationComma = false;

                    // Split the data labels into media and observations, skipping the standard columns
                    // We do these separately, so we can reorder the observations columns to match what CamtrapDP expectss
                    foreach (string dataLabel in dataLabels)
                    {
                        if (IsCondition.IsStandardControlType(dataLabel))
                        {
                            // Ignore standard columns as they are not part of the CamtrapDP standard.
                            continue;
                        }

                        if (CamtrapDPHelpers.IsMediaField(dataLabel))
                        {
                            mediaHeader.Append(CSVHelpers.CSVToCommaSeparatedValue(dataLabel, includeMediaComma));
                            mediaDataLabels.Add(dataLabel);
                        }
                        includeMediaComma = true;
                    }

                    observationsHeader.Append(CSVHelpers.CSVToCommaSeparatedValue(CamtrapDPConstants.Observations.ObservationID, includeObservationComma));
                    observationDataLabels.Add(CamtrapDPConstants.Observations.ObservationID);
                    includeObservationComma = true;
                    observationsHeader.Append(CSVHelpers.CSVToCommaSeparatedValue(CamtrapDPConstants.Observations.DeploymentID, includeObservationComma));
                    observationDataLabels.Add(CamtrapDPConstants.Observations.DeploymentID);

                    foreach (string dataLabel in dataLabels)
                    {
                        if ( dataLabel == CamtrapDPConstants.Observations.ObservationID || 
                             dataLabel == CamtrapDPConstants.Observations.DeploymentID || 
                             IsCondition.IsStandardControlType(dataLabel))
                        {
                            // omit standard controls and those that were already added
                            continue;
                        }

                        // As there are some repeated fields, we need to test it again. Also includes custom fields here
                        if (CamtrapDPHelpers.IsObservationsField(dataLabel) || false == CamtrapDPHelpers.IsMediaField(dataLabel) )
                        {
                            observationsHeader.Append(CSVHelpers.CSVToCommaSeparatedValue(dataLabel, includeObservationComma));
                            observationDataLabels.Add(dataLabel);
                        }
                        
                        // includeObservationComma = true;
                    }

                    // We need to adjust the order of the data labels as expected in Timelapse
                    // The template order is ok for Media, but not for the observationID should be the second item rather than the first item
                    //dataLabels.Remove(CamtrapDPConstants.Observations.ObservationID);
                    //dataLabels.Insert(1, CamtrapDPConstants.Observations.ObservationID);

                    // Write out the csv header row to each file
                    mediaFileWriter.WriteLine(mediaHeader.ToString());
                    observationsFileWriter.WriteLine(observationsHeader.ToString());

                    // For each row in the data table, write out the columns in the same order as the 
                    // data labels in the template file (again, skipping the ones we don't use and special casing the date/time data)
                    int countAllCurrentlySelectedFiles = database.CountAllCurrentlySelectedFiles;
                    for (int row = 0; row < countAllCurrentlySelectedFiles; row++)
                    {
                        if (database.ReadyToRefresh())
                        {
                            int percentDone = (int)100.0 * row / countAllCurrentlySelectedFiles;
                            progress.Report(new(percentDone, "Writing the CamtrapDP Media and Observations CSV files. Please wait", false, false));
                            Thread.Sleep(ThrottleValues.RenderingBackoffTime); // Allows the UI thread to update every now and then
                        }

                        includeMediaComma = false;
                        ImageRow image = database.FileTable[row];

                        StringBuilder mediaRowBuilder = new();
                        StringBuilder observationsRowBuilder = new();
                        foreach (string mediaDataLabel in mediaDataLabels)
                        {
                            // Ignore standard columns as they are not part of the CamtrapDP standard.
                            // We do use some of their values, though, to populate other CamtrapDP columns as seen below.
                            if (IsCondition.IsStandardControlType(mediaDataLabel))
                            {
                                continue;
                            }

                            switch (mediaDataLabel)
                            {
                                // MediaID is always filled in, but we need to add it to both media and observation files
                                // Adding it to the observation file is more complex, as adding it here will make it appear in the wrong spot
                                // That is, in the observation file the order is observationID, deploymentID, then mediaID
                                // So we just record it here and add it later. 
                                case CamtrapDPConstants.Media.MediaID:
                                    string mediaID = image.GetValueDatabaseString(mediaDataLabel);
                                    if (string.IsNullOrWhiteSpace(mediaID))
                                    {
                                        mediaID = Guid.NewGuid().ToString();
                                    }
                                    mediaRowBuilder.Append(CSVHelpers.CSVToCommaSeparatedValue(mediaID, includeMediaComma));
                                    includeMediaComma = true;
                                    break;

                                case CamtrapDPConstants.Media.DeploymentID:
                                    // Both CSV files need the deploymentID for cross referencing
                                    string deploymentID = image.GetValueDatabaseString(mediaDataLabel);
                                    if (string.IsNullOrWhiteSpace(deploymentID))
                                    {
                                        deploymentID = image.RelativePath;
                                    }

                                    mediaRowBuilder.Append(CSVHelpers.CSVToCommaSeparatedValue(deploymentID, includeMediaComma));
                                    includeMediaComma = true;
                                    break;

                                case CamtrapDPConstants.Media.Timestamp:
                                    // Timestamp is populated by the DateTime value.
                                    string timeStampAsString = image.GetValueDatabaseString(mediaDataLabel);
                                    if (false == Util.DateTimeHandler.TryParseDatabaseDateTime(timeStampAsString, out var timeStamp))
                                    {
                                        // Fallback in case start time is not set or is not valid
                                        timeStamp = image?.DateTime ?? Constant.ControlDefault.DateTimeDefaultValue;
                                    }
                                    // Convert the timestamp to CamtrapDP format
                                    mediaRowBuilder.Append(CSVHelpers.CSVToCommaSeparatedValue(timeStamp.ToString(CamtrapDPConstants.DateTimeFormats.CamtrapDateTimeFormat), includeMediaComma));
                                    includeMediaComma =  true;
                                    break;

                                // Filepath should be auto-populated by the user, but if not, we populate them here
                                case CamtrapDPConstants.Media.FilePath:
                                    // Filepath is populated by the RelativePath value
                                    string filePath = image.GetValueDatabaseString(mediaDataLabel);
                                    if (string.IsNullOrWhiteSpace(filePath))
                                    {
                                        // Fallback in case filepath is not set
                                        filePath = string.IsNullOrWhiteSpace(image?.RelativePath)
                                            ? image?.File ?? string.Empty
                                            : Path.Combine(image?.RelativePath ?? string.Empty, image?.File ?? string.Empty);
                                    }
                                    mediaRowBuilder.Append(CSVHelpers.CSVToCommaSeparatedValue(filePath, includeMediaComma));
                                    includeMediaComma = true;
                                    break;


                                // FileName should be auto-populated by the user, but if not, we populate them here
                                case CamtrapDPConstants.Media.FileName:
                                    // Filename is populated by the File value
                                    string fileName = image.GetValueDatabaseString(mediaDataLabel);
                                    if (string.IsNullOrWhiteSpace(fileName))
                                    {
                                        // Fallback in case fileName is not set
                                        fileName = image?.File ?? string.Empty;
                                    }
                                    mediaRowBuilder.Append(CSVHelpers.CSVToCommaSeparatedValue(fileName, includeMediaComma));
                                    includeMediaComma = true;
                                    break;

                                case CamtrapDPConstants.Media.FilePublic:
                                    string filePublic = image.GetValueDatabaseString(mediaDataLabel);
                                    if (false == Util.IsCondition.IsBoolean(filePublic))
                                    {
                                        // Fallback in case filePublic is not set
                                        filePublic = "FALSE";
                                    }
                                    mediaRowBuilder.Append(CSVHelpers.CSVToCommaSeparatedValue(filePublic, includeMediaComma));
                                    includeMediaComma = true;
                                    break;
                                case CamtrapDPConstants.Media.FileMediatype:
                                    string mediaType = image.GetValueDatabaseString(mediaDataLabel);
                                    if (string.IsNullOrWhiteSpace(mediaType))
                                    {
                                        // Fallback in case the mediatype is not set, where Mediatype is calculated from the file's extension
                                        string extension = Path.GetExtension(image.File);
                                        mediaRowBuilder.Append(string.Equals(extension, File.JpgFileExtension, StringComparison.OrdinalIgnoreCase)
                                            ? CSVHelpers.CSVToCommaSeparatedValue("image/jpeg", includeMediaComma)
                                            : CSVHelpers.CSVToCommaSeparatedValue($"video/{extension}", includeMediaComma));
                                    }
                                    else
                                    {
                                        mediaRowBuilder.Append(CSVHelpers.CSVToCommaSeparatedValue(image.GetValueDatabaseString(mediaDataLabel), includeMediaComma));
                                    }
                                    includeMediaComma = true;
                                    break;
                                case CamtrapDPConstants.Media.CaptureMethod:
                                case CamtrapDPConstants.Media.ExifData:
                                case CamtrapDPConstants.Media.Favorite:
                                case CamtrapDPConstants.Media.MediaComments:
                                    mediaRowBuilder.Append(CSVHelpers.CSVToCommaSeparatedValue(image.GetValueDatabaseString(mediaDataLabel), includeMediaComma));
                                    includeMediaComma = true;
                                    break;
                                default:
                                    Debug.Print($"Excluding {mediaDataLabel}");
                                    break;
                            }
                        }

                        includeObservationComma = false;
                        // This is a stub to fill in if we can figure out what an 'Empty' event is i.e., an event we should ignore.
                        // if (image.GetValueDatabaseString(CamtrapDPConstants.Observations.ObservationLevel) == "event")
                        // {
                        //    // We don't output empty events
                        // }
                    
                        foreach (string observationDataLabel in observationDataLabels)
                        {
                            // Ignore standard columns as they are not part of the CamtrapDP standard.
                            // We do use some of their values, though, to populate other CamtrapDP columns as seen below.
                            if (IsCondition.IsStandardControlType(observationDataLabel))
                            {
                                continue;
                            }

                            switch (observationDataLabel)
                            {

                                // MediaID is added to the observation file to allow cross referencing
                                case CamtrapDPConstants.Media.MediaID:
                                    string mediaID = image.GetValueDatabaseString(observationDataLabel);
                                    if (string.IsNullOrWhiteSpace(mediaID))
                                    {
                                        mediaID = Guid.NewGuid().ToString();
                                    }
                                    observationsRowBuilder.Append(CSVHelpers.CSVToCommaSeparatedValue(mediaID, includeObservationComma));
                                    includeObservationComma = true;
                                    break;

                                case CamtrapDPConstants.Observations.ObservationID:
                                    string observationID = image.GetValueDatabaseString(observationDataLabel);
                                    if (string.IsNullOrWhiteSpace(observationID))
                                    {
                                        // Fallback in case observationID is not set
                                        observationID = Guid.NewGuid().ToString();
                                    }
                                    observationsRowBuilder.Append(CSVHelpers.CSVToCommaSeparatedValue(observationID, includeObservationComma));
                                    includeObservationComma = true;
                                    break;

                                // deploymentID is added to the observation file to allow cross referencing
                                case CamtrapDPConstants.Media.DeploymentID:
                                    // Both CSV files need the deploymentID for cross referencing
                                    string deploymentID = image.GetValueDatabaseString(observationDataLabel);
                                    if (string.IsNullOrWhiteSpace(deploymentID))
                                    {
                                        // Fallback in case deploymentID is not set
                                        deploymentID = image.RelativePath;
                                    }

                                    observationsRowBuilder.Append(CSVHelpers.CSVToCommaSeparatedValue(deploymentID, includeObservationComma));
                                    includeObservationComma =  true;
                                    break;

                                case CamtrapDPConstants.Observations.EventID:
                                    // EventID is populated by the Event field. 
                                    // TODO Fallback in case its not populated?
                                    string eventID = image.GetValueDatabaseString(observationDataLabel);
                                    observationsRowBuilder.Append(CSVHelpers.CSVToCommaSeparatedValue(eventID, includeObservationComma));
                                    includeObservationComma = true;
                                    break;

                                case CamtrapDPConstants.Observations.EventStart:
                                    // EventStart is populated as Camtrap formatted DateTime
                                    string startTime = image.GetValueDatabaseString(observationDataLabel);
                                    if (false == Util.DateTimeHandler.TryParseDatabaseDateTime(startTime, out var startEventTime))
                                    {
                                        // Fallback in case start time is not set or is not valid
                                        startEventTime = image?.DateTime ?? Constant.ControlDefault.DateTimeDefaultValue;
                                    }
                                    observationsRowBuilder.Append(CSVHelpers.CSVToCommaSeparatedValue(startEventTime.ToString(CamtrapDPConstants.DateTimeFormats.CamtrapDateTimeFormat), includeObservationComma));
                                    includeObservationComma = true;
                                    break;

                                case CamtrapDPConstants.Observations.EventEnd:
                                    // EventEnd is populated as Camtrap formatted DateTime
                                    string endTime = image.GetValueDatabaseString(observationDataLabel);
                                    if (false == Util.DateTimeHandler.TryParseDatabaseDateTime(endTime, out var endEventTime))
                                    {
                                        // Fallback in case start time is not set or is not valid
                                        endEventTime = image?.DateTime ?? Constant.ControlDefault.DateTimeDefaultValue;
                                    }
                                    observationsRowBuilder.Append(CSVHelpers.CSVToCommaSeparatedValue(endEventTime.ToString(CamtrapDPConstants.DateTimeFormats.CamtrapDateTimeFormat), includeObservationComma));
                                    includeObservationComma = true;
                                    break;

                                case CamtrapDPConstants.Observations.ObservationLevel:
                                case CamtrapDPConstants.Observations.ObservationType:
                                case CamtrapDPConstants.Observations.CameraSetupType:
                                case CamtrapDPConstants.Observations.ScientificName:
                                case CamtrapDPConstants.Observations.Count:
                                case CamtrapDPConstants.Observations.LifeStage:
                                case CamtrapDPConstants.Observations.Sex:
                                case CamtrapDPConstants.Observations.Behavior:
                                case CamtrapDPConstants.Observations.IndividualID:
                                case CamtrapDPConstants.Observations.IndividualPositionRadius:
                                case CamtrapDPConstants.Observations.IndividualPositionAngle:
                                case CamtrapDPConstants.Observations.IndividualSpeed:
                                case CamtrapDPConstants.Observations.BboxX:
                                case CamtrapDPConstants.Observations.BboxY:
                                case CamtrapDPConstants.Observations.BboxWidth:
                                case CamtrapDPConstants.Observations.BboxHeight:
                                case CamtrapDPConstants.Observations.ClassificationMethod:
                                case CamtrapDPConstants.Observations.ClassifiedBy:
                                case CamtrapDPConstants.Observations.ClassificationTimestamp:
                                case CamtrapDPConstants.Observations.ClassificationProbability:
                                case CamtrapDPConstants.Observations.ObservationTags:
                                case CamtrapDPConstants.Observations.ObservationComments:
                                    observationsRowBuilder.Append(CSVHelpers.CSVToCommaSeparatedValue(image.GetValueDatabaseString(observationDataLabel), includeObservationComma));
                                    includeObservationComma = true;
                                    break;

                                case CamtrapDPConstants.Media.Timestamp:
                                case CamtrapDPConstants.Media.FilePath:
                                case CamtrapDPConstants.Media.FileName:
                                case CamtrapDPConstants.Media.FileMediatype:
                                    // omit these, as they are only in the media file
                                    break;

                                default:
                                    // Catch-all: likely a custom field, which is always written to observations
                                    observationsRowBuilder.Append(CSVHelpers.CSVToCommaSeparatedValue(image.GetValueDatabaseString(observationDataLabel), includeObservationComma));
                                    includeObservationComma = true;
                                    break;
                            }
                        }

                        // Write out the csv row
                        mediaFileWriter.WriteLine(mediaRowBuilder.ToString());
                        observationsFileWriter.WriteLine(observationsRowBuilder.ToString());
                    }

                    return problems;
                }
                catch
                {
                    return null;
                }
            }).ConfigureAwait(true);
        }



        #endregion

        // Not used for now, but might be useful later
        // ReSharper disable once UnusedMember.Local
        private static string HelperGetDeploymentGuid(Dictionary<string, string> relativePathToGUID, string relativePath)
        {
            if (false == relativePathToGUID.ContainsKey(relativePath))
            {
                relativePathToGUID.Add(relativePath, Guid.NewGuid().ToString());
            }
            return relativePathToGUID[relativePath];
        }
    }
}
