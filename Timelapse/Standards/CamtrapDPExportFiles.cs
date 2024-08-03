using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using MetadataExtractor;
using Microsoft.VisualBasic;
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
                                #region Populate the three resources - deployment, media and observations

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

                                #endregion

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
                                    datapackage.keywords = CamtrapDPExportFiles.CommaSeparatedStringToList(row[dataLabelAndType.Key]);
                                    break;
                                case DataPackage.Image:
                                    datapackage.image = string.IsNullOrWhiteSpace(row[dataLabelAndType.Key]) ? null : row[dataLabelAndType.Key];
                                    break;
                                case DataPackage.Homepage:
                                    datapackage.homepage = string.IsNullOrWhiteSpace(row[dataLabelAndType.Key]) ? null : row[dataLabelAndType.Key];
                                    break;

                                // Sources
                                case DataPackage.Sources:
                                    datapackage.sources = JsonConvert.DeserializeObject<List<Standards.sources>>(row[dataLabelAndType.Key], settings);
                                    break;

                                // Licenses
                                case DataPackage.Licenses:
                                    datapackage.licenses = JsonConvert.DeserializeObject<List<Standards.licenses>>(row[dataLabelAndType.Key], settings);
                                    break;

                                case DataPackage.BibliographicCitation:
                                    datapackage.bibliographicCitation = string.IsNullOrWhiteSpace(row[dataLabelAndType.Key]) ? null : row[dataLabelAndType.Key];
                                    break;

                                #region Project

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
                                    datapackage.project.captureMethod = CamtrapDPExportFiles.CommaSeparatedStringToList(row[dataLabelAndType.Key]);
                                    break;
                                case DataPackage.Project.IndividualAnimals:
                                    datapackage.project.individualAnimals = Boolean.TryParse(row[dataLabelAndType.Key], out bool boolValue) && boolValue;
                                    break;
                                case DataPackage.Project.ObservationLevel:
                                    datapackage.project.observationLevel = CamtrapDPExportFiles.CommaSeparatedStringToList(row[dataLabelAndType.Key]);
                                    break;

                                #endregion

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

                                    //string.IsNullOrWhiteSpace(row[dataLabelAndType.Key].ToString())
                                    //? null
                                    //: (Double.TryParse(row[dataLabelAndType.Key], out double coordPrecision)) ? coordPrecision : 0.0;
                                    //(Double.TryParse(row[dataLabelAndType.Key], out double coordPrecision) ? coordPrecision : "0.0");
                                    break;

                                case DataPackage.Spatial: // NOT REALLY DONE
                                    string jsonString = string.IsNullOrWhiteSpace(row[dataLabelAndType.Key]) || row[dataLabelAndType.Key] == "[]"
                                        ? "{\"type\": \"FeatureCollection\", \"features\": [] }"
                                        : row[dataLabelAndType.Key];
                                    JObject jobject = JObject.Parse(jsonString);
                                    datapackage.spatial = jobject;
                                    break;

                                #region Temporal

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

                                #endregion

                                // Taxonomic
                                case DataPackage.Taxonomic:
                                    datapackage.taxonomic = string.IsNullOrEmpty(row[dataLabelAndType.Key]) || "[]" == row[dataLabelAndType.Key]
                                        ? null
                                        : JsonConvert.DeserializeObject<List<Standards.taxonomic>>(row[dataLabelAndType.Key], settings);
                                    break;

                                // RelatedIdentifiers
                                case DataPackage.RelatedIdentifiers:
                                    datapackage.relatedIdentifiers = string.IsNullOrEmpty(row[dataLabelAndType.Key]) || "[]" == row[dataLabelAndType.Key]
                                        ? null
                                        : JsonConvert.DeserializeObject<List<Standards.relatedIdentifiers>>(row[dataLabelAndType.Key], settings);
                                    break;

                                // References
                                case DataPackage.References:
                                    datapackage.references_ = datapackage.references_ = string.IsNullOrEmpty(row[dataLabelAndType.Key]) || "[]" == row[dataLabelAndType.Key]
                                        ? null
                                        : JsonConvert.DeserializeObject<List<string>>(row[dataLabelAndType.Key], settings);


                                    //= JsonConvert.DeserializeObject<List<string>>(row[dataLabelAndType.Key], settings);
                                    break;

                                default:
                                    Debug.Print($"Unknown field: {dataLabelAndType.Key} : {dataLabelAndType.Value} => {row[dataLabelAndType.Key]}");
                                    break;
                            }
                        }
                    }

                    //
                    // Generate a list of messages indicating which required fields are missing.
                    //
                    List<string> missingDataPackageFields = new List<string>();
                    if (datapackage.contributors == null)
                    {
                        missingDataPackageFields.Add("Contributors: at least one is required.");
                    }
                    else
                    {
                        foreach (Standards.contributors contributor in datapackage.contributors)
                        {
                            if (contributor.title == null)
                            {
                                missingDataPackageFields.Add("Contributors: a title is required for each contributor");
                            }
                        }
                    }

                    if (datapackage.sources != null)
                    {
                        foreach (Standards.sources source in datapackage.sources)
                        {
                            if (source.title == null)
                            {
                                missingDataPackageFields.Add("Source: a title is required for each source");
                                break;
                            }
                        }
                    }

                    if (datapackage.licenses != null)
                    {
                        foreach (Standards.licenses license in datapackage.licenses)
                        {
                            if (license.title == null && license.path == null)
                            {
                                missingDataPackageFields.Add("License: scope and at least one of a title or path is required for each license");
                            }
                        }
                    }


                    string missingProjectFields = string.Empty;
                    if (datapackage.project.title == null)
                    {
                        missingProjectFields += ", title";
                    }

                    if (datapackage.project.samplingDesign == null)
                    {
                        missingProjectFields += ", sampling design";
                    }

                    if (datapackage.project.captureMethod == null || datapackage.project.captureMethod.Count == 0)
                    {
                        missingProjectFields += ", capture method";
                    }
#pragma warning disable CS0472 // A value of bool is never equal to 'null'
                    if (datapackage.project.individualAnimals == null)
#pragma warning restore CS0472
                    {
                        missingProjectFields += ", individual animals";
                    }

                    if (datapackage.project.observationLevel == null || datapackage.project.observationLevel.Count == 0)
                    {
                        missingProjectFields += ", observation level";
                    }

                    if (missingProjectFields != string.Empty)
                    {
                        missingDataPackageFields.Add("Project: required fields include " + missingProjectFields.Trim(' ', ','));
                    }

                    // Spatial should not normall be null as by default we create an empty spatia item
                    if (null == datapackage.spatial)
                    {
                        missingDataPackageFields.Add("Spatial: is required, but not yet implemented in Timelapse.");
                    }

                    if (datapackage.temporal.start == null || datapackage.temporal.end == null)
                    {
                        missingDataPackageFields.Add("Temporal: both start and end are required.");
                    }

                    if (datapackage.taxonomic == null)
                    {
                        missingDataPackageFields.Add("Taxonomic: taxonomic details used by this package are required.");
                    }
                    else
                    {
                        foreach (Standards.taxonomic taxonomic in datapackage.taxonomic)
                        {
                            if (taxonomic.scientificName == null)
                            {
                                missingDataPackageFields.Add("Taxonomic: a scientific name is required for each taxonomic entry");
                            }
                        }
                    }

                    if (datapackage.relatedIdentifiers != null)
                    {
                        foreach (Standards.relatedIdentifiers relatedIdentifiers in datapackage.relatedIdentifiers)
                        {
                            if (relatedIdentifiers.relationType == null || relatedIdentifiers.relatedIdentifier == null || relatedIdentifiers.relatedIdentifierType == null)
                            {
                                missingDataPackageFields.Add("Related identifiers: relation type, identifier, and type are required for each related identifier entry");
                            }
                        }
                    }

                    using (StreamWriter fileWriter = new StreamWriter(dataPackageFilePath, false))
                    {
                        StringBuilder dataPackageAsJson = new StringBuilder();
                        settings.Converters.Add(new Util.JsonConverters.WhiteSpaceToNullConverter());
                        dataPackageAsJson.Append(JsonConvert.SerializeObject(datapackage, settings));
                        fileWriter.WriteLine(dataPackageAsJson);
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
        public static async Task<List<string>> ExportCamtrapDPDeploymentToCsv(FileDatabase database, string folderPath, CSVDateTimeOptionsEnum csvDateTimeOptions, bool csvInsertSpaceBeforeDates)
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

                    List<string> problemDeploymentFields = new List<string>();

                    // For every level
                    foreach (MetadataInfoRow infoRow in database.MetadataInfo)
                    {
                        if (infoRow.Level != 2)
                        {
                            // no CSV file written for datapackage
                            continue;
                        }

                        int level = infoRow.Level;
                        string filePath = Path.Combine(folderPath, "CamtrapDP", "deployments.csv");

                        // Get the rows for this level
                        DataTables.DataTableBackedList<MetadataRow> rows = false == database.MetadataTablesByLevel.TryGetValue(level, out var value)
                            ? null
                            : value;

                        // Get the data labels in their original creation order, as the camtrapDP validator expects columns
                        // in a certain order (yup, brain-dead).
                        Dictionary<string, string> dataLabelsAndTypes = database.MetadataGetDataLabels(level);

                        using (StreamWriter fileWriter = new StreamWriter(filePath, false))
                        {
                            // Write the header as defined by the data labels in the template file.
                            // If the data label is an empty string, we use the label instead.
                            // The append sequence results in a trailing comma which is retained when writing the line.
                            StringBuilder header = new StringBuilder();

                            // Insert the folder data path column at the beginning
                            // Note that unlike the usual metadata csv export, we do not create columns for the levels, as these are not included in the standard 
                            // Write each line
                            int numberRows = dataLabelsAndTypes.Count;
                            int rowsRead = 0;
                            bool includeComma = true;
                            foreach (KeyValuePair<string, string> dataLabelAndType in dataLabelsAndTypes)
                            {
                                includeComma = rowsRead++ != numberRows - 1;
                                header.Append(CsvReaderWriter.AddColumnValue(dataLabelAndType.Key, includeComma));
                            }

                            fileWriter.WriteLine(header.ToString());

                            if (null == rows || rows.RowCount == 0)
                            {
                                // No data for this level, so skip it
                                continue;
                            }

                            foreach (MetadataRow row in rows)
                            {
                                List<string> cascadingPaths = FilesFolders.SplitAsCascadingRelativePath(row[Constant.DatabaseColumn.FolderDataPath]);
                                numberRows = dataLabelsAndTypes.Count;
                                rowsRead = 0;
                                includeComma = true;
                                StringBuilder rowBuilder = new StringBuilder();
                                // Set the DeploymentID to the relative path
                                foreach (KeyValuePair<string, string> dataLabelAndType in dataLabelsAndTypes)
                                {
                                    includeComma = rowsRead++ != numberRows - 1;
                                    string prefix = csvInsertSpaceBeforeDates ? " " : string.Empty;

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
                                                problemDeploymentFields.Add($"{dataLabelAndType.Key}: a non-empty value is required for this field.");
                                            }
                                            break;
                                        case CamtrapDPConstants.Deployment.DeploymentStart:
                                        case CamtrapDPConstants.Deployment.DeploymentEnd:
                                            if (string.IsNullOrWhiteSpace(row[dataLabelAndType.Key]) ||
                                                false == DateTime.TryParseExact(row[dataLabelAndType.Key], CamtrapDPConstants.DateTimeFormats.TimelapseFullDateTimeFormat,
                                                    CultureInfo.InvariantCulture,
                                                    DateTimeStyles.None, out DateTime _))
                                            {
                                                problemDeploymentFields.Add($"{dataLabelAndType.Key}: a valid date/time value is required for this field.");
                                            }
                                            // Dont append, as this will be caught by the 
                                            //rowBuilder.Append(CsvReaderWriter.AddColumnValue(row[Constant.DatabaseColumn.FolderDataPath], includeComma));
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
                                            rowBuilder.Append(CsvReaderWriter.AddColumnValue(dateTimeString, includeComma));
                                            break;
                                        case Constant.Control.Date_:
                                            // Export the  Date_ column column in CamtrapDP format
                                            string dateString = DateTime.TryParseExact(row[dataLabelAndType.Key], CamtrapDPConstants.DateTimeFormats.TimelapseDateOnlyFormat,
                                                CultureInfo.InvariantCulture,
                                                DateTimeStyles.None, out DateTime dateOnly)
                                                ? dateOnly.ToString(CamtrapDPConstants.DateTimeFormats.CamtrapDateTimeFormat)
                                                : row[dataLabelAndType.Key];
                                            rowBuilder.Append(CsvReaderWriter.AddColumnValue(dateString, includeComma));
                                            break;

                                        default:
                                            rowBuilder.Append(CsvReaderWriter.AddColumnValue(row[dataLabelAndType.Key], includeComma));
                                            break;
                                    }
                                }

                                // We have a row. Write it out.
                                fileWriter.WriteLine(rowBuilder.ToString());
                            }
                        }

                        progress.Report(new ProgressBarArguments(Convert.ToInt32((double)level / rows.RowCount * 100.0),
                            $"Writing {filePath}.csv file. Please wait...", false, false));
                    }

                    return problemDeploymentFields;
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
        ///  Essentially repeates the ExportMetadataToCSV code but
        /// - filters values for a few fields
        /// - removes uneeded columns
        /// - ignores export flag
        /// - ignores spreadsheet order (as validator expects things in a particular order)
        /// - does not append level columns for cross-referencing
        /// I should integrate everything, but this is just easier to do.
        /// Export all the database data associated with the selected view to the .csv file indicated in the file path
        /// </summary>
        public static async Task<List<string>> ExportCamtrapDPMediaObservationsToCsv(FileDatabase database, DataEntryControls controls, string folderPath)
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
                    progress.Report(new ProgressBarArguments(0, "Writing the CamtrapDP Media and Observations CSV file. Please wait", false, true));

                    List<string> problemDeploymentFields = new List<string>();

                    string mediaFilePath = Path.Combine(folderPath, "CamtrapDP", "media.csv");
                    string observationsFilePath = Path.Combine(folderPath, "CamtrapDP", "observations.csv");

                    // Get the rows for this level
                    //DataTables.DataTableBackedList<MetadataRow> rows = false == database.MetadataTablesByLevel.TryGetValue(level, out var value)
                    //    ? null
                    //    : value;

                    // Get the data labels in their original creation order, as the camtrapDP validator expects columns
                    // in a certain order (yup, brain-dead).
                    //Dictionary<string, string> dataLabelsAndTypes = database.MetadataGetDataLabels(level);

                    using (StreamWriter mediaFileWriter = new StreamWriter(mediaFilePath, false))
                    {
                        using (StreamWriter observationsFileWriter = new StreamWriter(observationsFilePath, false))
                        {

                            // Get all data labels
                            List<string> dataLabels = database.GetDataLabelsFromControls().ToList();

                            // Write the header as defined by the data labels in the template file.
                            // If the data label is an empty string, we use the label instead.
                            // The append sequence results in a trailing comma which is retained when writing the line.
                            StringBuilder mediaHeader = new StringBuilder();
                            StringBuilder observationsHeader = new StringBuilder();
                            int numberColumns = dataLabels.Count;
                            int columnsRead = 0;
                            bool includeComma = true;
                            foreach (string dataLabel in dataLabels)
                            {
                                includeComma = columnsRead++ != numberColumns - 1;
                                if (MetadataCreateControl.IsStandardControlType(dataLabel))
                                {
                                    // Ignore these columns
                                    continue;
                                }

                                if (isMediaField(dataLabel))
                                {
                                    mediaHeader.Append(CsvReaderWriter.AddColumnValue(dataLabel, includeComma));
                                }
                                else
                                {
                                    observationsHeader.Append(CsvReaderWriter.AddColumnValue(dataLabel, includeComma));
                                    if (dataLabel == CamtrapDPConstants.Observations.ObservationID)
                                    {
                                        observationsHeader.Append(CsvReaderWriter.AddColumnValue(CamtrapDPConstants.Observations.DeploymentID, includeComma));
                                        observationsHeader.Append(CsvReaderWriter.AddColumnValue(CamtrapDPConstants.Observations.MediaID, includeComma));
                                    }

                                }
                            }
                            // Remove trailing comma
                            mediaHeader.Length--;
                            //observationsHeader.Length--;
                            // Write out the csv row
                            mediaFileWriter.WriteLine(mediaHeader.ToString());
                            observationsFileWriter.WriteLine(observationsHeader.ToString());

                            // For each row in the data table, write out the columns in the same order as the 
                            // data labels in the template file (again, skipping the ones we don't use and special casing the date/time data)
                            int countAllCurrentlySelectedFiles = database.CountAllCurrentlySelectedFiles;
                            for (int row = 0; row < countAllCurrentlySelectedFiles; row++)
                            {
                                ImageRow image = database.FileTable[row];
                                //numberColumns = dataLabels.Count;
                                columnsRead = 0;
                                StringBuilder mediaRowBuilder = new StringBuilder();
                                StringBuilder observationsRowBuilder = new StringBuilder();
                                foreach (string dataLabel in dataLabels)
                                {
                                    includeComma = columnsRead++ != numberColumns - 1;
                                    if (MetadataCreateControl.IsStandardControlType(dataLabel))
                                    {
                                        // Ignore these columns
                                        continue;
                                    }

                                    switch (dataLabel)
                                    {
                                        case CamtrapDPConstants.Media.MediaID:
                                            // RelativePath + Filename
                                            mediaRowBuilder.Append(CsvReaderWriter.AddColumnValue(Path.Combine(image.RelativePath, image.File), includeComma));
                                            observationsRowBuilder.Append(CsvReaderWriter.AddColumnValue(Path.Combine(image.RelativePath, image.File), includeComma));
                                            break;

                                        case CamtrapDPConstants.Media.DeploymentID:
                                            // both need the deploymentID for cross referencing
                                            mediaRowBuilder.Append(CsvReaderWriter.AddColumnValue(image.RelativePath, includeComma));
                                            observationsRowBuilder.Append(CsvReaderWriter.AddColumnValue(image.RelativePath, includeComma));
                                            break;

                                        case CamtrapDPConstants.Media.Timestamp:
                                            // DateTime value
                                            mediaRowBuilder.Append(CsvReaderWriter.AddColumnValue(image.DateTime.ToString(CamtrapDPConstants.DateTimeFormats.CamtrapDateTimeFormat), includeComma)); 
                                            break;

                                        case CamtrapDPConstants.Media.FilePath:
                                            mediaRowBuilder.Append(CsvReaderWriter.AddColumnValue(image.RelativePath, includeComma));
                                            break;

                                        case CamtrapDPConstants.Media.FileName:
                                            mediaRowBuilder.Append(CsvReaderWriter.AddColumnValue(image.File, includeComma));
                                            break;

                                        case CamtrapDPConstants.Media.FileMediatype:
                                            string extension = Path.GetExtension(image.File);
                                            mediaRowBuilder.Append(string.Equals(extension, Constant.File.JpgFileExtension, StringComparison.OrdinalIgnoreCase)
                                                ? CsvReaderWriter.AddColumnValue("image/jpeg", includeComma)
                                                : CsvReaderWriter.AddColumnValue($"video/{extension}", includeComma));
                                            break;

                                        case CamtrapDPConstants.Observations.ObservationID:
                                            // Filename
                                            observationsRowBuilder.Append(CsvReaderWriter.AddColumnValue(Path.Combine(image.RelativePath, image.File), includeComma));
                                            break;

                                        // For now, we do events as the timestamp of a single image
                                        // This should be changed
                                        case CamtrapDPConstants.Observations.EventStart:
                                        case CamtrapDPConstants.Observations.EventEnd:
                                            // Filename
                                            observationsRowBuilder.Append(CsvReaderWriter.AddColumnValue(image.DateTime.ToString(CamtrapDPConstants.DateTimeFormats.CamtrapDateTimeFormat), includeComma));
                                            break;

                                        default:
                                            if (isMediaField(dataLabel))
                                            {
                                                mediaRowBuilder.Append(CsvReaderWriter.AddColumnValue(image.GetValueDatabaseString(dataLabel), includeComma));
                                            }
                                            else
                                            {
                                                observationsRowBuilder.Append(CsvReaderWriter.AddColumnValue(image.GetValueDatabaseString(dataLabel), includeComma));
                                            }
                                            break;
                                    }
                                }

                                // Remove trailing comma
                                mediaRowBuilder.Length--;
                                //observationsRowBuilder.Length--;
                                // Write out the csv row
                                mediaFileWriter.WriteLine(mediaRowBuilder.ToString());
                                observationsFileWriter.WriteLine(observationsRowBuilder.ToString());
                            }
                        }
                    }

                    //progress.Report(new ProgressBarArguments(Convert.ToInt32((double)rows.RowCount * 100.0),
                    //    $"Writing {mediaFilePath}.csv and {observationsFilePath}.csv files. Please wait...", false, false));


                    return problemDeploymentFields;
                }
                catch
                {
                    return null;
                }
            }).ConfigureAwait(true);
        }

        #endregion

        #region CamtrapDPDataPackage Helpers

        // Convert a comma-separated string into a list of non-empty trimmed strings
        private static List<string> CommaSeparatedStringToList(string commaSeparatedList)
        {
            if (string.IsNullOrWhiteSpace(commaSeparatedList) || commaSeparatedList == "[]")
            {
                return null;
            }

            return commaSeparatedList.Split(',').Select(s => s.Trim()).Select(s => Regex.Replace(s, @"\s+", " ")).Where(s => !string.IsNullOrEmpty(s)).ToList();
        }

        private static bool isMediaField(string fieldName)
        {
            switch (fieldName)
            {
                case CamtrapDPConstants.Media.MediaID:
                case CamtrapDPConstants.Media.DeploymentID:
                case CamtrapDPConstants.Media.CaptureMethod:
                case CamtrapDPConstants.Media.Timestamp:
                case CamtrapDPConstants.Media.FilePath:
                case CamtrapDPConstants.Media.FilePublic:
                case CamtrapDPConstants.Media.FileName:
                case CamtrapDPConstants.Media.FileMediatype:
                case CamtrapDPConstants.Media.ExifData:
                case CamtrapDPConstants.Media.Favorite:
                case CamtrapDPConstants.Media.MediaComments:
                    return true;
            }

            return false;
        }

        private static bool isObservationsField(string fieldName)
        {
            switch (fieldName)
            {
                case CamtrapDPConstants.Observations.ObservationID:
                case CamtrapDPConstants.Observations.DeploymentID:
                case CamtrapDPConstants.Observations.MediaID:
                case CamtrapDPConstants.Observations.EventID:
                case CamtrapDPConstants.Observations.EventStart:
                case CamtrapDPConstants.Observations.EventEnd:
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
                case CamtrapDPConstants.Observations.ClassificationTimestamp:
                case CamtrapDPConstants.Observations.ClassificationProbability:
                case CamtrapDPConstants.Observations.ObservationTags:
                case CamtrapDPConstants.Observations.ObservationComments:
                    return true;
            }

            return false;
        }
        #endregion
    }
}
