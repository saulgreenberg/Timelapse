using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Timelapse.Controls;
using Timelapse.ControlsMetadata;
using Timelapse.Database;
using Timelapse.DataTables;
using DataPackage = Timelapse.Standards.CamtrapDPConstants.DataPackage;
#pragma warning disable IDE1006

namespace Timelapse.Standards
{
    public class CamtrapDPExportFiles
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
                                    datapackage.temporal.start = datapackage.temporal.start = DateTime.TryParseExact(row[dataLabelAndType.Key], CamtrapDPConstants.DateTimeFormats.TimelapseDateOnlyFormat, CultureInfo.InvariantCulture,
                                        DateTimeStyles.None, out DateTime dateTimeStart)
                                        ? dateTimeStart.ToString(CamtrapDPConstants.DateTimeFormats.CamtrapDateOnlyFormat)
                                        : row[dataLabelAndType.Key];
                                    break;
                                case DataPackage.Temporal.End: // Convert to CAMTRAPDP DATE Format
                                    datapackage.temporal.end = DateTime.TryParseExact(row[dataLabelAndType.Key], CamtrapDPConstants.DateTimeFormats.TimelapseDateOnlyFormat, CultureInfo.InvariantCulture,
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

        #region Export Deployments to CSV file

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
        #endregion

    }
}
