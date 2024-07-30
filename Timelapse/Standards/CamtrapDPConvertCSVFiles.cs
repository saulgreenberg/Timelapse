using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using Newtonsoft.Json;
using Timelapse.Controls;
using Timelapse.ControlsMetadata;
using Timelapse.Database;
using Timelapse.DataTables;
using DataPackage = Timelapse.Standards.CamtrapDPConstants.DataPackage;
#pragma warning disable IDE1006

namespace Timelapse.Standards
{
    public class CamtrapDPConvertCSVFiles
    {
        #region Export CamtrapDPDataPackage To JsonFile
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
                    Dictionary<string, string> dataLabelsAndTypesInSpreadsheetOrder = database.MetadataGetDataLabelsInSpreadsheetOrderForExport(level);
                    JsonSerializerSettings settings = new JsonSerializerSettings
                    {
                        NullValueHandling = NullValueHandling.Ignore,
                        Formatting = Formatting.Indented
                    };

                    foreach (MetadataRow row in rows)
                    {
                        foreach (KeyValuePair<string, string> dataLabelAndType in dataLabelsAndTypesInSpreadsheetOrder)
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
                                    datapackage.created = DateTime.TryParseExact(row[dataLabelAndType.Key], CamtrapDPConstants.DateTimeFormats.TimelapseFullDateTimeFormat, CultureInfo.InvariantCulture,
                                                           DateTimeStyles.None, out DateTime dateTime)
                                                            ? dateTime.ToString(CamtrapDPConstants.DateTimeFormats.CamtrapFormat)
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
                                    datapackage.keywords = string.IsNullOrWhiteSpace(row[dataLabelAndType.Key]) ? null : row[dataLabelAndType.Key];
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
                                    datapackage.project.captureMethod = string.IsNullOrWhiteSpace(row[dataLabelAndType.Key]) ? null : row[dataLabelAndType.Key];
                                    break;
                                case DataPackage.Project.IndividualAnimals:
                                    datapackage.project.individualAnimals = Boolean.TryParse(row[dataLabelAndType.Key], out bool boolValue) && boolValue;
                                    break;
                                case DataPackage.Project.ObservationLevel:
                                    datapackage.project.observationLevel = string.IsNullOrWhiteSpace(row[dataLabelAndType.Key]) ? null : row[dataLabelAndType.Key];
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
                                    datapackage.spatial = row[dataLabelAndType.Key];
                                    break;

                                #region Temporal
                                case DataPackage.Temporal.Start: // Convert to CAMTRAPDP DATE Format
                                    datapackage.temporal.start = datapackage.temporal.start = DateTime.TryParseExact(row[dataLabelAndType.Key], CamtrapDPConstants.DateTimeFormats.TimelapseDateOnlyFormat, CultureInfo.InvariantCulture,
                                        DateTimeStyles.None, out DateTime dateTimeStart)
                                        ? dateTimeStart.ToString(CamtrapDPConstants.DateTimeFormats.TimelapseDateOnlyFormat)
                                        : row[dataLabelAndType.Key];
                                    break;
                                case DataPackage.Temporal.End: // Convert to CAMTRAPDP DATE Format
                                    datapackage.temporal.end = datapackage.temporal.start = DateTime.TryParseExact(row[dataLabelAndType.Key], CamtrapDPConstants.DateTimeFormats.TimelapseDateOnlyFormat, CultureInfo.InvariantCulture,
                                        DateTimeStyles.None, out DateTime dateTimeEnd)
                                        ? dateTimeEnd.ToString(CamtrapDPConstants.DateTimeFormats.TimelapseDateOnlyFormat)
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
                    if (datapackage.project.captureMethod == null)
                    {
                        missingProjectFields += ", capture method";
                    }
                    #pragma warning disable CS0472 // The result of the expression is always the same since a value of this type is never equal to 'null'
                    if (datapackage.project.individualAnimals == null)
                    #pragma warning restore CS0472 
                    {
                        missingProjectFields += ", individual animals";
                    }
                    if (datapackage.project.observationLevel == null)
                    {
                        missingProjectFields += ", observation level";
                    }

                    if (missingProjectFields != string.Empty)
                    {
                        missingDataPackageFields.Add("Project: required fields include " + missingProjectFields.Trim(' ', ','));
                    }

                    if (string.IsNullOrWhiteSpace(datapackage.spatial))
                    {
                        missingDataPackageFields.Add("Spatial: is required, but not yet implemented in Timelapse.");
                    }
                    //else
                    //{
                    //foreach (Standards.spatial spatial in datapackage.spatial)
                    //{
                    //}
                    //}

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


        #region Public Static Method - Import from CSV (async)
        // Try importing a CSV file, checking its headers and values against the template's DataLabels and data types.
        //public static bool ConvertCamtrapDPMetadataToCamtrapJson(string timelapseDataPackagePath, string camtrapDPExportFolder)
        //{


        //    MetadataCamtrapDP camtrapDp = new MetadataCamtrapDP();
        //    //        int processedFilesCount = 0;
        //    //        int totalFilesProcessed = 0;
        //    //        int dateTimeErrors = 0;
        //    List<string> importErrors = new List<string>();
        //    // PART 1. Read in the CSV file. Return false if there is a problem in reading the CSV file or if the CSV file is empty
        //    if (false == CsvReaderWriter.TryReadingCSVFile(timelapseDataPackagePath, out List<List<string>> parsedFile, importErrors))
        //    {
        //        return false;
        //    }

        //    if (false == Directory.Exists(camtrapDPExportFolder))
        //    {
        //        Directory.CreateDirectory(camtrapDPExportFolder);
        //    }
        //    string exportFilePath = Path.Combine(camtrapDPExportFolder, Path.GetFileNameWithoutExtension(timelapseDataPackagePath) + ".json");

        //    // Now that we have a parsed file, get its headers, which we will use as DataLabels
        //    List<string> dataLabelsFromCSV = parsedFile[0].Where(s => !string.IsNullOrWhiteSpace(s)).Distinct().ToList();

        //    // Part 2: Create a List of all data rows, where each row is a dictionary containing the header and that row's valued for the header
        //    List<Dictionary<string, string>> rowDictionaryList = CsvReaderWriter.GetAllDataRows(dataLabelsFromCSV, parsedFile);
        //    foreach (Dictionary<string, string> row in rowDictionaryList)
        //    {
        //        foreach (KeyValuePair<string, string> cell in row)
        //        {
        //            switch (cell.Key)
        //            {
        //                // Resources - Deployment
        //                case CamtrapDPConstants.DataPackage.Resources.Deployment_name:
        //                    camtrapDp.resources.ElementAt(0).name = cell.Value;
        //                    break;
        //                case CamtrapDPConstants.DataPackage.Resources.Deployment_path:
        //                    camtrapDp.resources.ElementAt(0).path = cell.Value;
        //                    break;
        //                case CamtrapDPConstants.DataPackage.Resources.Deployment_schema:
        //                    camtrapDp.resources.ElementAt(0).schema = cell.Value;
        //                    break;

        //                // Resources - Media
        //                case CamtrapDPConstants.DataPackage.Resources.Media_name:
        //                    camtrapDp.resources.ElementAt(1).name = cell.Value;
        //                    break;
        //                case CamtrapDPConstants.DataPackage.Resources.Media_path:
        //                    camtrapDp.resources.ElementAt(1).path = cell.Value;
        //                    break;
        //                case CamtrapDPConstants.DataPackage.Resources.Media_schema:
        //                    camtrapDp.resources.ElementAt(1).schema = cell.Value;
        //                    break;

        //                // Resources - Observations
        //                case CamtrapDPConstants.DataPackage.Resources.Observations_name:
        //                    camtrapDp.resources.ElementAt(2).name = cell.Value;
        //                    break;
        //                case CamtrapDPConstants.DataPackage.Resources.Observations_path:
        //                    camtrapDp.resources.ElementAt(2).path = cell.Value;
        //                    break;
        //                case CamtrapDPConstants.DataPackage.Resources.Observations_schema:
        //                    camtrapDp.resources.ElementAt(2).schema = cell.Value;
        //                    break;
        //                // Resources - Profile (Common)
        //                case CamtrapDPConstants.DataPackage.Resources.Resource_profile:
        //                    camtrapDp.resources.ElementAt(0).profile = cell.Value;
        //                    camtrapDp.resources.ElementAt(1).profile = cell.Value;
        //                    camtrapDp.resources.ElementAt(2).profile = cell.Value;
        //                    break;

        //                case CamtrapDPConstants.DataPackage.Profile:
        //                    camtrapDp.profile = cell.Value;
        //                    break;
        //                case CamtrapDPConstants.DataPackage.Name:
        //                    camtrapDp.name = cell.Value;
        //                    break;
        //                case CamtrapDPConstants.DataPackage.IdAlias:
        //                    camtrapDp.id = cell.Value;
        //                    break;
        //                case CamtrapDPConstants.DataPackage.Created:
        //                    // Try to convert string from Timelapse dateTime format to the CamtrapDP dateTime format
        //                    camtrapDp.created = DateTime.TryParseExact(cell.Value, CamtrapDPConstants.DateTimeFormats.TimelapseFullDateTimeFormat, CultureInfo.InvariantCulture,
        //                        DateTimeStyles.None, out DateTime dateTime)
        //                        ? dateTime.ToString(CamtrapDPConstants.DateTimeFormats.CamtrapFormat)
        //                        : cell.Value;
        //                    break;
        //                case CamtrapDPConstants.DataPackage.Title:
        //                    camtrapDp.title = cell.Value;
        //                    break;

        //                // Contributors array from json
        //                case CamtrapDPConstants.DataPackage.Contributors:
        //                    camtrapDp.contributors = JsonConvert.DeserializeObject<List<contributors>>(cell.Value);
        //                    break;

        //                case CamtrapDPConstants.DataPackage.Description:
        //                    camtrapDp.description = cell.Value;
        //                    break;
        //                case CamtrapDPConstants.DataPackage.Version:
        //                    camtrapDp.version = cell.Value;
        //                    break;
        //                case CamtrapDPConstants.DataPackage.Keywords:
        //                    camtrapDp.keywords = $"[{cell.Value}]"; // comma separated list as json array
        //                    break;
        //                case CamtrapDPConstants.DataPackage.Image:
        //                    camtrapDp.image = cell.Value;
        //                    break;
        //                case CamtrapDPConstants.DataPackage.Homepage:
        //                    camtrapDp.homepage = cell.Value;
        //                    break;

        //                // Sources array from json
        //                case CamtrapDPConstants.DataPackage.Sources:
        //                    camtrapDp.sources = JsonConvert.DeserializeObject<List<sources>>(cell.Value);
        //                    break;

        //                // Licenses array from json
        //                case CamtrapDPConstants.DataPackage.Licenses:
        //                    camtrapDp.licenses = JsonConvert.DeserializeObject<List<licenses>>(cell.Value);
        //                    break;

        //                case CamtrapDPConstants.DataPackage.BibliographicCitation:
        //                    camtrapDp.bibliographicCitation = cell.Value;
        //                    break;

        //                case CamtrapDPConstants.DataPackage.Project.Id:
        //                    camtrapDp.project.id = cell.Value;
        //                    break;
        //                case CamtrapDPConstants.DataPackage.Project.Title:
        //                    camtrapDp.project.title = cell.Value;
        //                    break;
        //                case CamtrapDPConstants.DataPackage.Project.Acronym:
        //                    camtrapDp.project.acronym = cell.Value;
        //                    break;
        //                case CamtrapDPConstants.DataPackage.Project.Description:
        //                    camtrapDp.project.description = cell.Value;
        //                    break;
        //                case CamtrapDPConstants.DataPackage.Project.SamplingDesign:
        //                    camtrapDp.project.samplingDesign = cell.Value;
        //                    break;
        //                case CamtrapDPConstants.DataPackage.Project.Path:
        //                    camtrapDp.project.path = cell.Value;
        //                    break;
        //                case CamtrapDPConstants.DataPackage.Project.CaptureMethod:
        //                    camtrapDp.project.captureMethod = $"[{cell.Value}]"; // comma separated list as json array
        //                    break;
        //                case CamtrapDPConstants.DataPackage.Project.IndividualAnimals:
        //                    camtrapDp.project.individualAnimals = bool.TryParse(cell.Value, out bool boolValue) && boolValue;
        //                    break;
        //                case CamtrapDPConstants.DataPackage.Project.ObservationLevel:
        //                    camtrapDp.project.observationLevel = $"[{cell.Value}]"; // comma separated list as json array
        //                    break;

        //                case CamtrapDPConstants.DataPackage.CoordinatePrecision:
        //                    camtrapDp.coordinatePrecision = Double.TryParse(cell.Value, out double doubleValue)
        //                        ? doubleValue
        //                        : 0;
        //                    break;

        //                case CamtrapDPConstants.DataPackage.Spatial:
        //                    camtrapDp.spatial = cell.Value;
        //                    break;

        //                // Temporal
        //                case CamtrapDPConstants.DataPackage.Temporal.Start:
        //                    camtrapDp.temporal.start = DateTime.TryParseExact(cell.Value, CamtrapDPConstants.DateTimeFormats.TimelapseDateOnlyFormat, CultureInfo.InvariantCulture,
        //                        DateTimeStyles.None, out _)
        //                        ? cell.Value
        //                        : string.Empty;
        //                    break;
        //                case CamtrapDPConstants.DataPackage.Temporal.End:
        //                    camtrapDp.temporal.end = DateTime.TryParseExact(cell.Value, CamtrapDPConstants.DateTimeFormats.TimelapseDateOnlyFormat, CultureInfo.InvariantCulture,
        //                        DateTimeStyles.None, out _)
        //                        ? cell.Value
        //                        : string.Empty;
        //                    break;
        //                case CamtrapDPConstants.DataPackage.Taxonomic:
        //                    var settings = new JsonSerializerSettings
        //                    {
        //                        NullValueHandling = NullValueHandling.Ignore,
        //                        MissingMemberHandling = MissingMemberHandling.Ignore
        //                    };
        //                    camtrapDp.taxonomic = JsonConvert.DeserializeObject<List<taxonomic>>(cell.Value, settings);
        //                    break;

        //                    // NOT DONE:
        //                    // RelatedIdentifiers
        //                    // REFERENCES
        //            }
        //        }
        //    }
        //    using (StreamWriter fileWriter = new StreamWriter(exportFilePath, false))
        //    {
        //        StringBuilder header = new StringBuilder();
        //        var settings = new JsonSerializerSettings
        //        {
        //            NullValueHandling = NullValueHandling.Ignore,
        //            //MissingMemberHandling = MissingMemberHandling.Ignore
        //        };
        //        header.Append(JsonConvert.SerializeObject(camtrapDp, Formatting.Indented, settings));
        //        fileWriter.WriteLine(header);
        //    }
        //    return true;
        //}
        #endregion

       // public class MetadataCamtrapDP
       // {
       //     // Currently hard-wired to 3 Resources representing the deployment, media, and observations
       //     public List<resources> resources = new List<resources>()
       //{
       //    new resources(), // Deployment
       //    new resources(), // Media
       //    new resources(), // Observations
       //};

       //     public string profile;
       //     public string name;
       //     public string id;
       //     public string created;
       //     public string title;

       //     public List<contributors> contributors;

       //     public string description;
       //     public string version;
       //     public string keywords;
       //     public string image;
       //     public string homepage;

       //     public List<sources> sources = new List<sources>()
       // {
       //     new sources(),
       // };
       //     public List<licenses> licenses = new List<licenses>()
       // {
       //     new licenses(),
       // };

       //     public string bibliographicCitation;
       //     public project project = new project();
       //     public double coordinatePrecision;
       //     // NEEDS TO BE A GEOJSON - NOT SURE WHAT THE BEST WAY TO DO THIS. CURRENTLY, ENTERED AS A STRING BUT COULD BLOW UP IF FORMAT IS BAD
       //     // FIND OUT WHAT PART OF THE GEOJSON SPEC IT USES.. COULD MAKE THIS INTO AN OBJECT ETC
       //     public string spatial;
       //     public temporal temporal = new temporal();

       //     //TAXONOMIC: NOT HANDLED FOR NOW.
       //     // Only allow one taxonomic for now.But for this to be useful, we need a full(long) list.
       //     public List<taxonomic> taxonomic = new List<taxonomic>()
       // {
       //     new taxonomic(),
       // };

       //     //RelatedIdentifiers: NOT HANDLED FOR NOW.
       //     // Only allow one taxonomic for now.But for this to be useful, we need a full(long) list.
       //     public List<relatedIdentifiers> relatedIdentifiers = new List<relatedIdentifiers>()
       // {
       //     new relatedIdentifiers(),
       // };

       //     public List<string> references_ = new List<string>();
       // }

       // public class resources
       // {
       //     public string name;
       //     public string path;
       //     public string profile;
       //     public string schema;
       // }

       // public class contributors
       // {
       //     public string title;
       //     public string email;
       //     public string path;
       //     public string role;
       //     public string organization;
       // }

       // public class sources
       // {
       //     public string title;
       //     public string email;
       //     public string path;
       //     public string version;
       // }

       // public class licenses
       // {
       //     public string name;
       //     public string path;
       //     public string title;
       //     public string scope;
       // }

       // public class project
       // {
       //     public string id;
       //     public string title;
       //     public string acronym;
       //     public string description;
       //     public string samplingDesign;
       //     public string path;
       //     public string captureMethod;
       //     public bool individualAnimals;
       //     public string observationLevel;
       // }

       // // NOT USED FOR NOW, BUT WE DO WANT TO IMPLEMENT THIS PROPERLY
       // //public class spatial
       // //{
       // //    public string type;
       // //    public Point[] bbox;
       // //    public Point[] coordinates;
       // //}

       // public class temporal
       // {
       //     public string start;
       //     public string end;
       // }

       // public class taxonomic
       // {
       //     public string scientificName { get; set; } = null;
       //     public string taxonID { get; set; } = null;
       //     public string taxonRank { get; set; } = null;
       //     public string kingdom { get; set; } = null;
       //     public string phylum { get; set; } = null;
       //     public string class_ { get; set; } = null;
       //     public string order { get; set; } = null;
       //     public string family { get; set; } = null;
       //     public string genus { get; set; } = null;
       //     public Dictionary<string, string> vernacularNames { get; set; } = null;
       // }

       // public class VernacularItem
       // {
       //     public string lang { get; set; }
       //     public string vernacularName { get; set; }
       // }

       // public class relatedIdentifiers
       // {
       //     public string relationType = string.Empty;
       //     public string relationIdentifier = string.Empty;
       //     public string resourceTypeGeneral = string.Empty;
       //     public string relatedIdentifierType = string.Empty;
       // }
    }
}
