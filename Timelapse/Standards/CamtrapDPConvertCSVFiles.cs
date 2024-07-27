using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Timelapse.Database;
#pragma warning disable IDE1006

namespace Timelapse.Standards
{
    public class CamtrapDPConvertCSVFiles
    {
        #region Public Static Method - Import from CSV (async)
        // Try importing a CSV file, checking its headers and values against the template's DataLabels and data types.
        public static bool ConvertCamtrapDPMetadataToCamtrapJson(string timelapseDataPackagePath, string camtrapDPExportFolder)
        {
            MetadataCamtrapDP camtrapDp = new MetadataCamtrapDP();
            //        int processedFilesCount = 0;
            //        int totalFilesProcessed = 0;
            //        int dateTimeErrors = 0;
            List<string> importErrors = new List<string>();
            // PART 1. Read in the CSV file. Return false if there is a problem in reading the CSV file or if the CSV file is empty
            if (false == CsvReaderWriter.TryReadingCSVFile(timelapseDataPackagePath, out List<List<string>> parsedFile, importErrors))
            {
                return false;
            }

            if (false == Directory.Exists(camtrapDPExportFolder))
            {
                Directory.CreateDirectory(camtrapDPExportFolder);
            }
            string exportFilePath = Path.Combine(camtrapDPExportFolder, Path.GetFileNameWithoutExtension(timelapseDataPackagePath) + ".json");

            // Now that we have a parsed file, get its headers, which we will use as DataLabels
            List<string> dataLabelsFromCSV = parsedFile[0].Where(s => !string.IsNullOrWhiteSpace(s)).Distinct().ToList();

            // Part 2: Create a List of all data rows, where each row is a dictionary containing the header and that row's valued for the header
            List<Dictionary<string, string>> rowDictionaryList = CsvReaderWriter.GetAllDataRows(dataLabelsFromCSV, parsedFile);
            foreach (Dictionary<string, string> row in rowDictionaryList)
            {
                foreach (KeyValuePair<string, string> cell in row)
                {
                    switch (cell.Key)
                    {
                        // Resources - Deployment
                        case CamtrapDPConstants.DataPackage.Resources.Deployment_name:
                            camtrapDp.resources.ElementAt(0).name = cell.Value;
                            break;
                        case CamtrapDPConstants.DataPackage.Resources.Deployment_path:
                            camtrapDp.resources.ElementAt(0).path = cell.Value;
                            break;
                        case CamtrapDPConstants.DataPackage.Resources.Deployment_schema:
                            camtrapDp.resources.ElementAt(0).schema = cell.Value;
                            break;

                        // Resources - Media
                        case CamtrapDPConstants.DataPackage.Resources.Media_name:
                            camtrapDp.resources.ElementAt(1).name = cell.Value;
                            break;
                        case CamtrapDPConstants.DataPackage.Resources.Media_path:
                            camtrapDp.resources.ElementAt(1).path = cell.Value;
                            break;
                        case CamtrapDPConstants.DataPackage.Resources.Media_schema:
                            camtrapDp.resources.ElementAt(1).schema = cell.Value;
                            break;

                        // Resources - Observations
                        case CamtrapDPConstants.DataPackage.Resources.Observations_name:
                            camtrapDp.resources.ElementAt(2).name = cell.Value;
                            break;
                        case CamtrapDPConstants.DataPackage.Resources.Observations_path:
                            camtrapDp.resources.ElementAt(2).path = cell.Value;
                            break;
                        case CamtrapDPConstants.DataPackage.Resources.Observations_schema:
                            camtrapDp.resources.ElementAt(2).schema = cell.Value;
                            break;
                        // Resources - Profile (Common)
                        case CamtrapDPConstants.DataPackage.Resources.Resource_profile:
                            camtrapDp.resources.ElementAt(0).profile = cell.Value;
                            camtrapDp.resources.ElementAt(1).profile = cell.Value;
                            camtrapDp.resources.ElementAt(2).profile = cell.Value;
                            break;

                        case CamtrapDPConstants.DataPackage.Profile:
                            camtrapDp.profile = cell.Value;
                            break;
                        case CamtrapDPConstants.DataPackage.Name:
                            camtrapDp.name = cell.Value;
                            break;
                        case CamtrapDPConstants.DataPackage.IdAlias:
                            camtrapDp.id = cell.Value;
                            break;
                        case CamtrapDPConstants.DataPackage.Created:
                            // Try to convert string from Timelapse dateTime format to the CamtrapDP dateTime format
                            camtrapDp.created = DateTime.TryParseExact(cell.Value, CamtrapDPConstants.DateTimeFormats.TimelapseFullDateTimeFormat, CultureInfo.InvariantCulture,
                                DateTimeStyles.None, out DateTime dateTime)
                                ? dateTime.ToString(CamtrapDPConstants.DateTimeFormats.CamtrapFormat)
                                : cell.Value;
                            break;
                        case CamtrapDPConstants.DataPackage.Title:
                            camtrapDp.title = cell.Value;
                            break;

                        // Contributors array from json
                        case CamtrapDPConstants.DataPackage.Contributors:
                            camtrapDp.contributors = JsonConvert.DeserializeObject<List<contributors>>(cell.Value);
                            break;

                        case CamtrapDPConstants.DataPackage.Description:
                            camtrapDp.description = cell.Value;
                            break;
                        case CamtrapDPConstants.DataPackage.Version:
                            camtrapDp.version = cell.Value;
                            break;
                        case CamtrapDPConstants.DataPackage.Keywords:
                            camtrapDp.keywords = $"[{cell.Value}]"; // comma separated list as json array
                            break;
                        case CamtrapDPConstants.DataPackage.Image:
                            camtrapDp.image = cell.Value;
                            break;
                        case CamtrapDPConstants.DataPackage.Homepage:
                            camtrapDp.homepage = cell.Value;
                            break;

                        // Sources array from json
                        case CamtrapDPConstants.DataPackage.Sources:
                            camtrapDp.sources = JsonConvert.DeserializeObject<List<sources>>(cell.Value);
                            break;

                        // Licenses array from json
                        case CamtrapDPConstants.DataPackage.Licenses:
                            camtrapDp.licenses = JsonConvert.DeserializeObject<List<licenses>>(cell.Value);
                            break;

                        case CamtrapDPConstants.DataPackage.BibliographicCitation:
                            camtrapDp.bibliographicCitation = cell.Value;
                            break;

                        case CamtrapDPConstants.DataPackage.Project.Id:
                            camtrapDp.project.id = cell.Value;
                            break;
                        case CamtrapDPConstants.DataPackage.Project.Title:
                            camtrapDp.project.title = cell.Value;
                            break;
                        case CamtrapDPConstants.DataPackage.Project.Acronym:
                            camtrapDp.project.acronym = cell.Value;
                            break;
                        case CamtrapDPConstants.DataPackage.Project.Description:
                            camtrapDp.project.description = cell.Value;
                            break;
                        case CamtrapDPConstants.DataPackage.Project.SamplingDesign:
                            camtrapDp.project.samplingDesign = cell.Value;
                            break;
                        case CamtrapDPConstants.DataPackage.Project.Path:
                            camtrapDp.project.path = cell.Value;
                            break;
                        case CamtrapDPConstants.DataPackage.Project.CaptureMethod:
                            camtrapDp.project.captureMethod = $"[{cell.Value}]"; // comma separated list as json array
                            break;
                        case CamtrapDPConstants.DataPackage.Project.IndividualAnimals:
                            camtrapDp.project.individualAnimals = bool.TryParse(cell.Value, out bool boolValue) && boolValue;
                            break;
                        case CamtrapDPConstants.DataPackage.Project.ObservationLevel:
                            camtrapDp.project.observationLevel = $"[{cell.Value}]"; // comma separated list as json array
                            break;

                        case CamtrapDPConstants.DataPackage.CoordinatePrecision:
                            camtrapDp.coordinatePrecision = Double.TryParse(cell.Value, out double doubleValue)
                                ? doubleValue
                                : 0;
                            break;

                        case CamtrapDPConstants.DataPackage.Spatial:
                            camtrapDp.spatial = cell.Value;
                            break;

                        // Temporal
                        case CamtrapDPConstants.DataPackage.Temporal.Start:
                            camtrapDp.temporal.start = DateTime.TryParseExact(cell.Value, CamtrapDPConstants.DateTimeFormats.TimelapseDateOnlyFormat, CultureInfo.InvariantCulture,
                                DateTimeStyles.None, out _)
                                ? cell.Value
                                : string.Empty;
                            break;
                        case CamtrapDPConstants.DataPackage.Temporal.End:
                            camtrapDp.temporal.end = DateTime.TryParseExact(cell.Value, CamtrapDPConstants.DateTimeFormats.TimelapseDateOnlyFormat, CultureInfo.InvariantCulture,
                                DateTimeStyles.None, out _)
                                ? cell.Value
                                : string.Empty;
                            break;
                        case CamtrapDPConstants.DataPackage.Taxonomic:
                            var settings = new JsonSerializerSettings
                            {
                                NullValueHandling = NullValueHandling.Ignore,
                                MissingMemberHandling = MissingMemberHandling.Ignore
                            };
                            camtrapDp.taxonomic = JsonConvert.DeserializeObject<List<taxonomic>>(cell.Value, settings);
                            break;

                            // NOT DONE:
                            // RelatedIdentifiers
                            // REFERENCES
                    }
                }
            }
            using (StreamWriter fileWriter = new StreamWriter(exportFilePath, false))
            {
                StringBuilder header = new StringBuilder();
                var settings = new JsonSerializerSettings
                {
                    NullValueHandling = NullValueHandling.Ignore,
                    //MissingMemberHandling = MissingMemberHandling.Ignore
                };
                header.Append(JsonConvert.SerializeObject(camtrapDp, Formatting.Indented, settings));
                fileWriter.WriteLine(header);
            }
            return true;
        }
        #endregion

        public class MetadataCamtrapDP
        {
            // Currently hard-wired to 3 Resources representing the deployment, media, and observations
            public List<resources> resources = new List<resources>()
       {
           new resources(), // Deployment
           new resources(), // Media
           new resources(), // Observations
       };

            public string profile;
            public string name;
            public string id;
            public string created;
            public string title;

            public List<contributors> contributors;

            public string description;
            public string version;
            public string keywords;
            public string image;
            public string homepage;

            public List<sources> sources = new List<sources>()
        {
            new sources(),
        };
            public List<licenses> licenses = new List<licenses>()
        {
            new licenses(),
        };

            public string bibliographicCitation;
            public project project = new project();
            public double coordinatePrecision;
            // NEEDS TO BE A GEOJSON - NOT SURE WHAT THE BEST WAY TO DO THIS. CURRENTLY, ENTERED AS A STRING BUT COULD BLOW UP IF FORMAT IS BAD
            // FIND OUT WHAT PART OF THE GEOJSON SPEC IT USES.. COULD MAKE THIS INTO AN OBJECT ETC
            public string spatial;
            public temporal temporal = new temporal();

            //TAXONOMIC: NOT HANDLED FOR NOW.
            // Only allow one taxonomic for now.But for this to be useful, we need a full(long) list.
            public List<taxonomic> taxonomic = new List<taxonomic>()
        {
            new taxonomic(),
        };

            //RelatedIdentifiers: NOT HANDLED FOR NOW.
            // Only allow one taxonomic for now.But for this to be useful, we need a full(long) list.
            public List<relatedIdentifiers> relatedIdentifiers = new List<relatedIdentifiers>()
        {
            new relatedIdentifiers(),
        };

            public List<string> references = new List<string>();
        }

        public class resources
        {
            public string name;
            public string path;
            public string profile;
            public string schema;
        }

        public class contributors
        {
            public string title;
            public string email;
            public string path;
            public string role;
            public string organization;
        }

        public class sources
        {
            public string title;
            public string email;
            public string path;
            public string version;
        }

        public class licenses
        {
            public string name;
            public string path;
            public string title;
            public string scope;
        }

        public class project
        {
            public string id;
            public string title;
            public string acronym;
            public string description;
            public string samplingDesign;
            public string path;
            public string captureMethod;
            public bool individualAnimals;
            public string observationLevel;
        }

        // NOT USED FOR NOW, BUT WE DO WANT TO IMPLEMENT THIS PROPERLY
        //public class spatial
        //{
        //    public string type;
        //    public Point[] bbox;
        //    public Point[] coordinates;
        //}

        public class temporal
        {
            public string start;
            public string end;
        }

        public class taxonomic
        {
            public string scientificName { get; set; } = null;
            public string taxonID { get; set; } = null;
            public string taxonRank { get; set; } = null;
            public string kingdom { get; set; } = null;
            public string phylum { get; set; } = null;
            public string class_ { get; set; } = null;
            public string order { get; set; } = null;
            public string family { get; set; } = null;
            public string genus { get; set; } = null;
            public Dictionary<string, string> vernacularNames { get; set; } = null;
        }

        public class VernacularItem
        {
            public string lang { get; set; }
            public string vernacularName { get; set; }
        }

        public class relatedIdentifiers
        {
            public string relationType = string.Empty;
            public string relationIdentifier = string.Empty;
            public string resourceTypeGeneral = string.Empty;
            public string relatedIdentifierType = string.Empty;
        }
    }
}
