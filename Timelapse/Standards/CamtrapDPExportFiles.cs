using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Timelapse.Controls;
using Timelapse.ControlsDataEntry;
using Timelapse.ControlsMetadata;
using Timelapse.Database;
using Timelapse.DataStructures;
using Timelapse.DataTables;
using Timelapse.Enums;
using Timelapse.Util;
using DataPackage = Timelapse.Standards.CamtrapDPConstants.DataPackage;
#pragma warning disable IDE1006

namespace Timelapse.Standards
{
    public class CamtrapDPExportFiles
    {
        #region Public static method - ExportCamtrapDPDataPackageToJsonFile

        // Export the datapackage to a file, where data is written as a json string that matches the CamtrapDP specification
        // If any of the required fields (according to the CamtrapDP specification) are missing, generate a list of text messages
        // that lists the missing required fields in a form appropriate to put into a dialog box.
        // If something goes wrong, return null
        public static async Task<List<string>> ExportCamtrapDPDataPackageToJsonFile(FileDatabase database, string dataPackageFilePath)
        {
            Standards.CamtrapDPDataPackage datapackage = new CamtrapDPDataPackage();
            Standards.resources resourceDeployment = new Standards.resources();
            Standards.resources resourceMedia = new Standards.resources();
            Standards.resources resourceObservations = new Standards.resources();
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
                        if (tmpInfoRow != null && tmpInfoRow.Level == 1)
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
                    DataTables.DataTableBackedList<MetadataRow> rows = false == database.MetadataTablesByLevel.TryGetValue(level, out var value)
                        ? null
                        : value;
                    if (rows == null)
                    {
                        return null;
                    }

                    // Get the data labels
                    Dictionary<string, string> dataLabelsAndTypes = database.MetadataGetDataLabels(level);
                    JsonSerializerSettings settings = new JsonSerializerSettings
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
                                    datapackage.contributors = JsonConvert.DeserializeObject<List<Standards.contributors>>(row[dataLabelAndType.Key], settings);
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
                                    datapackage.sources = JsonConvert.DeserializeObject<List<Standards.sources>>(row[dataLabelAndType.Key], settings);
                                    break;

                                // Licenses object
                                case DataPackage.Licenses:
                                    datapackage.licenses = JsonConvert.DeserializeObject<List<Standards.licenses>>(row[dataLabelAndType.Key], settings);
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
                                        : JsonConvert.DeserializeObject<List<Standards.taxonomic>>(row[dataLabelAndType.Key], settings);
                                    break;

                                // RelatedIdentifiers Object
                                case DataPackage.RelatedIdentifiers:
                                    datapackage.relatedIdentifiers = string.IsNullOrEmpty(row[dataLabelAndType.Key]) || "[]" == row[dataLabelAndType.Key]
                                        ? null
                                        : JsonConvert.DeserializeObject<List<Standards.relatedIdentifiers>>(row[dataLabelAndType.Key], settings);
                                    break;

                                // References Object
                                case DataPackage.References:
                                    datapackage.references_ = datapackage.references_ = string.IsNullOrEmpty(row[dataLabelAndType.Key]) || "[]" == row[dataLabelAndType.Key]
                                        ? null
                                        : JsonConvert.DeserializeObject<List<string>>(row[dataLabelAndType.Key], settings);
                                    break;

                                default:
                                    // Shouldn't happen
                                    Debug.Print($"Unknown field: {dataLabelAndType.Key} : {dataLabelAndType.Value} => {row[dataLabelAndType.Key]}");
                                    break;
                            }
                        }
                    }

                    // Write the datapackage file
                    using (StreamWriter fileWriter = new StreamWriter(dataPackageFilePath, false))
                    {
                        StringBuilder dataPackageAsJson = new StringBuilder();
                        settings.Converters.Add(new Util.JsonConverters.WhiteSpaceToNullConverter());
                        dataPackageAsJson.Append(JsonConvert.SerializeObject(datapackage, settings));
                        fileWriter.WriteLine(dataPackageAsJson);
                    }

                    //
                    // We also do a rudimentary check for missing data, where we generate a list of warning messages indicating which required fields are missing.
                    //

                    // Contributors  required fields
                    List<string> missingDataPackageFields = new List<string>();
                    if (datapackage.contributors == null)
                    {
                        missingDataPackageFields.Add(" • Contributors: at least one is required.");
                    }
                    else
                    {
                        foreach (Standards.contributors contributor in datapackage.contributors)
                        {
                            if (contributor.title == null)
                            {
                                missingDataPackageFields.Add(" • Contributors: a title is required for each contributor");
                            }
                        }
                    }

                    // Sources  required fields
                    if (datapackage.sources != null)
                    {
                        foreach (Standards.sources source in datapackage.sources)
                        {
                            if (source.title == null)
                            {
                                missingDataPackageFields.Add(" • Source: a title is required for each source");
                                break;
                            }
                        }
                    }

                    // License  required fields
                    if (datapackage.licenses != null)
                    {
                        foreach (Standards.licenses license in datapackage.licenses)
                        {
                            if (license.title == null && license.path == null)
                            {
                                missingDataPackageFields.Add(" • License: scope and at least one of a title or path is required for each license");
                            }
                        }
                    }

                    // Project required fields
                    if (datapackage.project.title == null)
                    {
                        missingDataPackageFields.Add(" • Project: title is required but is empty.");
                    }

                    if (datapackage.project.samplingDesign == null)
                    {
                        missingDataPackageFields.Add(" • Project: sampling design is required but is empty.");
                    }

                    if (datapackage.project.captureMethod == null || datapackage.project.captureMethod.Count == 0)
                    {
                        missingDataPackageFields.Add(" • Project: capture method is required but is empty.");
                    }
#pragma warning disable CS0472 // A value of bool is never equal to 'null'
                    if (datapackage.project.individualAnimals == null)
#pragma warning restore CS0472
                    {
                        missingDataPackageFields.Add(" • Project: individual animals is required but is empty.");
                    }

                    if (datapackage.project.observationLevel == null || datapackage.project.observationLevel.Count == 0)
                    {
                        missingDataPackageFields.Add(" • Project: observation level is required but is empty.");
                    }

                    // Spatial required fields
                    // Note should not normally be null as by default we create an empty spatial item
                    // and/or populate it with a lat/long bounding box
                    if (null == datapackage.spatial)
                    {
                        missingDataPackageFields.Add(" • Spatial: is required.");
                    }

                    // Temporal required fields
                    if (datapackage.temporal.start == null || datapackage.temporal.end == null)
                    {
                        missingDataPackageFields.Add(" • Temporal: both start and end are required.");
                    }

                    // Taxonomic required fields
                    if (datapackage.taxonomic == null)
                    {
                        missingDataPackageFields.Add(" • Taxonomic: taxonomic details used by this package are required.");
                    }
                    else
                    {
                        foreach (Standards.taxonomic taxonomic in datapackage.taxonomic)
                        {
                            if (taxonomic.scientificName == null)
                            {
                                missingDataPackageFields.Add(" • Taxonomic: a scientific name is required for each taxonomic entry");
                            }
                        }
                    }

                    // Related Identifiers required fields
                    if (datapackage.relatedIdentifiers != null)
                    {
                        foreach (Standards.relatedIdentifiers relatedIdentifiers in datapackage.relatedIdentifiers)
                        {
                            if (relatedIdentifiers.relationType == null || relatedIdentifiers.relatedIdentifier == null || relatedIdentifiers.relatedIdentifierType == null)
                            {
                                missingDataPackageFields.Add(" • Related identifiers: relation type, identifier, and type are required for each related identifier entry");
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
            // Set up a progress handler that will update the progress bar
            Progress<ProgressBarArguments> progressHandler = new Progress<ProgressBarArguments>(value =>
            {
                // Update the progress bar
                CsvReaderWriter.UpdateProgressBar(GlobalReferences.BusyCancelIndicator, value.PercentDone, value.Message, value.IsCancelEnabled, value.IsIndeterminate);
            });
            IProgress<ProgressBarArguments> progress = progressHandler;
            return await Task.Run(() =>
            {
                try
                {
                    progress.Report(new ProgressBarArguments(0, "Writing the CamtrapDP Deployment CSV file. Please wait", false, true));

                    List<string> problemList = new List<string>();

                    // Get the Deployment info (second) level
                    int level = 2;

                    // Get the rows for the deployment level
                    DataTables.DataTableBackedList<MetadataRow> rows = false == database.MetadataTablesByLevel.TryGetValue(level, out var value)
                        ? null
                        : value;

                    // Get the data labels in their original creation order, as the camtrapDP validator expects columns
                    // in a certain order (yup, brain-dead).
                    Dictionary<string, string> dataLabelsAndTypes = database.MetadataGetDataLabels(level);

                    using (StreamWriter fileWriter = new StreamWriter(deploymentFilePath, false))
                    {
                        // Write the header as defined by the data labels in the template file.
                        // If the data label is an empty string, we use the label instead.
                        // The append sequence results in a trailing comma which is retained when writing the line.
                        StringBuilder header = new StringBuilder();

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
                            problemList.Add($" • No deployment data is available. Did you create folder-level deployment data?");
                            return problemList;
                        }

                        // Write out each data row
                        foreach (MetadataRow row in rows)
                        {
                            includeComma = false;
                            StringBuilder rowBuilder = new StringBuilder();
                            // Set the DeploymentID to the relative path
                            foreach (KeyValuePair<string, string> dataLabelAndType in dataLabelsAndTypes)
                            {
                                string where = row[Constant.DatabaseColumn.FolderDataPath];
                                switch (dataLabelAndType.Key)
                                {
                                    case CamtrapDPConstants.Deployment.DeploymentID:
                                        // Set the deployment id to the relative path
                                        row[dataLabelAndType.Key] = row[Constant.DatabaseColumn.FolderDataPath];
                                        break;

                                    case CamtrapDPConstants.Deployment.Latitude:
                                    case CamtrapDPConstants.Deployment.Longitude:
                                        // Required: Lat/long values. Generate a problem report if values are not filled in- 
                                        if (string.IsNullOrWhiteSpace(row[dataLabelAndType.Key]))
                                        {
                                           
                                            problemList.Add($" • {dataLabelAndType.Key} in {where}: a non-empty value is required for this field.");
                                        }
                                        break;
                                    case CamtrapDPConstants.Deployment.DeploymentStart:
                                    case CamtrapDPConstants.Deployment.DeploymentEnd:
                                        if (string.IsNullOrWhiteSpace(row[dataLabelAndType.Key]) ||
                                            false == DateTime.TryParseExact(row[dataLabelAndType.Key], CamtrapDPConstants.DateTimeFormats.TimelapseFullDateTimeFormat,
                                                CultureInfo.InvariantCulture,
                                                DateTimeStyles.None, out DateTime _))
                                        {
                                            problemList.Add($" • {dataLabelAndType.Key} in {where}: a valid date/time value is required for this field.");
                                        }
                                        break;
                                }

                                // Required: Specific CamtrapDP DateTime formats
                                switch (dataLabelAndType.Value)
                                {
                                    // Deployment fields only use DateTime_, but put in the code for Date_ or Time_ for added fields
                                    case Constant.Control.DateTime_:
                                        // Export the  DateTime_ column in CamtrapDP format
                                        string dateTimeString = DateTime.TryParseExact(row[dataLabelAndType.Key],
                                            CamtrapDPConstants.DateTimeFormats.TimelapseFullDateTimeFormat, CultureInfo.InvariantCulture,
                                            DateTimeStyles.None, out DateTime dateTime)
                                            ? dateTime.ToString(CamtrapDPConstants.DateTimeFormats.CamtrapDateTimeFormat)
                                            : row[dataLabelAndType.Key];
                                        rowBuilder.Append(CSVHelpers.CSVToCommaSeparatedValue(dateTimeString, includeComma));
                                        includeComma = true;
                                        break;
                                    case Constant.Control.Date_:
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
                    }
                    progress.Report(new ProgressBarArguments(Convert.ToInt32((double)level / rows.RowCount * 100.0),
                        $"Writing the CamtrapDP Deployment CSV file. Please wait...", false, false));

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
            Progress<ProgressBarArguments> progressHandler = new Progress<ProgressBarArguments>(value =>
            {
                // Update the progress bar
                CsvReaderWriter.UpdateProgressBar(GlobalReferences.BusyCancelIndicator, value.PercentDone, value.Message, value.IsCancelEnabled, value.IsIndeterminate);
            });
            IProgress<ProgressBarArguments> progress = progressHandler;
            return await Task.Run(() =>
            {
                try
                {
                    progress.Report(new ProgressBarArguments(0, "Writing the CamtrapDP Media and Observations CSV file. Please wait", false, true));

                    List<string> problems = new List<string>();

                    // We split the data fields into two files
                    using (StreamWriter mediaFileWriter = new StreamWriter(mediaFilePath, false))
                    {
                        using (StreamWriter observationsFileWriter = new StreamWriter(observationsFilePath, false))
                        {
                            // Get all data labels
                            // Note that we preserve the order as the miniforge frictionless validate function expects the csv file to have columns in a specific order
                            List<string> dataLabels = database.GetDataLabelsFromControlsByIDCreationOrder().ToList();

                            // Write the header as defined by the data labels in the template file.
                            // If the data label is an empty string, we use the label instead.
                            // The sequence prepends a comma except for the first column.
                            StringBuilder mediaHeader = new StringBuilder();
                            StringBuilder observationsHeader = new StringBuilder();
                            bool includeMediaComma = false;
                            bool includeObservationComma = false;
                            foreach (string dataLabel in dataLabels)
                            {
                                if (MetadataCreateControl.IsStandardControlType(dataLabel))
                                {
                                    // Ignore standard columns as they are not part of the CamtrapDP standard.
                                    continue;
                                }


                                if (CamtrapDPHelpers.IsMediaField(dataLabel))
                                {
                                    mediaHeader.Append(CSVHelpers.CSVToCommaSeparatedValue(dataLabel, includeMediaComma));
                                    includeMediaComma = true;
                                }
                                else
                                {
                                    observationsHeader.Append(CSVHelpers.CSVToCommaSeparatedValue(dataLabel, includeObservationComma));
                                    includeObservationComma = true;
                                    if (dataLabel == CamtrapDPConstants.Observations.ObservationID)
                                    {
                                        observationsHeader.Append(CSVHelpers.CSVToCommaSeparatedValue(CamtrapDPConstants.Observations.DeploymentID, includeObservationComma));
                                        observationsHeader.Append(CSVHelpers.CSVToCommaSeparatedValue(CamtrapDPConstants.Observations.MediaID, includeObservationComma));
                                    }

                                }
                            }
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
                                    progress.Report(new ProgressBarArguments(percentDone, $"Writing the CamtrapDP Media and Observations CSV files. Please wait", false, false));
                                    Thread.Sleep(Constant.ThrottleValues.RenderingBackoffTime); // Allows the UI thread to update every now and then
                                }

                                includeMediaComma = false;
                                includeObservationComma = false;
                                ImageRow image = database.FileTable[row];

                                StringBuilder mediaRowBuilder = new StringBuilder();
                                StringBuilder observationsRowBuilder = new StringBuilder();
                                foreach (string dataLabel in dataLabels)
                                {
                                    // Ignore standard columns as they are not part of the CamtrapDP standard.
                                    // We do use some of their values, though, to populate other CamtrapDP columns as seen below.
                                    if (MetadataCreateControl.IsStandardControlType(dataLabel))
                                    {
                                        continue;
                                    }

                                    switch (dataLabel)
                                    {
                                        case CamtrapDPConstants.Media.MediaID:
                                            // MediaID is constructed populated as RelativePath + Filename
                                            mediaRowBuilder.Append(CSVHelpers.CSVToCommaSeparatedValue(Path.Combine(image.RelativePath, image.File), includeMediaComma));
                                            observationsRowBuilder.Append(CSVHelpers.CSVToCommaSeparatedValue(Path.Combine(image.RelativePath, image.File), includeObservationComma));
                                            includeMediaComma = includeObservationComma = true;
                                            break;

                                        case CamtrapDPConstants.Media.DeploymentID:
                                            // Both CSV files need the deploymentID for cross referencing
                                            mediaRowBuilder.Append(CSVHelpers.CSVToCommaSeparatedValue(image.RelativePath, includeMediaComma));
                                            observationsRowBuilder.Append(CSVHelpers.CSVToCommaSeparatedValue(image.RelativePath, includeObservationComma));
                                            includeMediaComma = includeObservationComma = true;
                                            break;

                                        case CamtrapDPConstants.Media.Timestamp:
                                            // Timestamp is populated by the DateTime value
                                            mediaRowBuilder.Append(CSVHelpers.CSVToCommaSeparatedValue(image.DateTime.ToString(CamtrapDPConstants.DateTimeFormats.CamtrapDateTimeFormat), includeMediaComma));
                                            includeMediaComma = includeObservationComma = true;
                                            break;

                                        case CamtrapDPConstants.Media.FilePath:
                                            // Filepath is populated by the RelativePath value
                                            mediaRowBuilder.Append(CSVHelpers.CSVToCommaSeparatedValue(image.RelativePath, includeMediaComma));
                                            includeMediaComma = true;
                                            break;

                                        case CamtrapDPConstants.Media.FileName:
                                            // Filename is populated by the File value
                                            mediaRowBuilder.Append(CSVHelpers.CSVToCommaSeparatedValue(image.File, includeMediaComma));
                                            includeMediaComma = true;
                                            break;

                                        case CamtrapDPConstants.Media.FileMediatype:
                                            // Mediatype is calculated from the file's extension
                                            string extension = Path.GetExtension(image.File);
                                            mediaRowBuilder.Append(string.Equals(extension, Constant.File.JpgFileExtension, StringComparison.OrdinalIgnoreCase)
                                                ? CSVHelpers.CSVToCommaSeparatedValue("image/jpeg", includeMediaComma)
                                                : CSVHelpers.CSVToCommaSeparatedValue($"video/{extension}", includeMediaComma));
                                            includeMediaComma = true;
                                            break;

                                        case CamtrapDPConstants.Observations.ObservationID:
                                            // ObservationID is populated as RelativePath + Filename
                                            observationsRowBuilder.Append(CSVHelpers.CSVToCommaSeparatedValue(Path.Combine(image.RelativePath, image.File), includeObservationComma));
                                            includeObservationComma = true;
                                            break;

                                        case CamtrapDPConstants.Observations.EventStart:
                                        case CamtrapDPConstants.Observations.EventEnd:
                                            // EventStart/End is populated as DateTime
                                            // This is not correct for images that are part of a sequence
                                            observationsRowBuilder.Append(CSVHelpers.CSVToCommaSeparatedValue(image.DateTime.ToString(CamtrapDPConstants.DateTimeFormats.CamtrapDateTimeFormat), includeObservationComma));
                                            includeObservationComma = true;
                                            break;

                                        default:
                                            if (CamtrapDPHelpers.IsMediaField(dataLabel))
                                            {
                                                mediaRowBuilder.Append(CSVHelpers.CSVToCommaSeparatedValue(image.GetValueDatabaseString(dataLabel), includeMediaComma));
                                                includeMediaComma = true;
                                            }
                                            else
                                            {
                                                observationsRowBuilder.Append(CSVHelpers.CSVToCommaSeparatedValue(image.GetValueDatabaseString(dataLabel), includeObservationComma));
                                                includeObservationComma = true;
                                            }
                                            break;
                                    }
                                }

                                // Write out the csv row
                                mediaFileWriter.WriteLine(mediaRowBuilder.ToString());
                                observationsFileWriter.WriteLine(observationsRowBuilder.ToString());
                            }
                        }
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
    }
}
