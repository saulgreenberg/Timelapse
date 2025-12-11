using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Timelapse.Standards
{
    public class CamtrapDPDataPackage
    {
        // Resources: Currently hard-wired to 3 Resources representing the deployment, media, and observations
        public List<resources> resources = [];

        public string profile;
        public string name;
        public string id;
        public string created;
        public string title;
        public Dictionary<string, string> customFields;
        public List<contributors> contributors;

        public string description;
        public string version;

        public List<string> keywords;
        public string image;
        public string homepage;

        public List<sources> sources;// = new List<sources>();
        //{
        //    new sources(),
        //}
        public List<licenses> licenses;// = new List<licenses>();
        //{
        //    new licenses(),
        //};

        public string bibliographicCitation;
        public project project = new();
        public double? coordinatePrecision;

        // NEEDS TO BE A GEOJSON - NOT SURE WHAT THE BEST WAY TO DO THIS. CURRENTLY, ENTERED AS A STRING BUT COULD BLOW UP IF FORMAT IS BAD
        // FIND OUT WHAT PART OF THE GEOJSON SPEC IT USES.. COULD MAKE THIS INTO AN OBJECT ETC
        public JObject spatial;

        public temporal temporal = new();

        public List<taxonomic> taxonomic;// = new List<taxonomic>();
        //{
        //    new taxonomic(),
        //};

        public List<relatedIdentifiers> relatedIdentifiers =
        [
            new()
        ];

        // Due to references being a keyword in sqlite, we do it as references_
        // and then converted it back to references when outputing a json.
        [JsonProperty("references")]
        public List<string> references_;// = new List<string>();
    }

    public class resources
    {
        public string name { get; set; }
        public string path { get; set; }
        public string profile  { get; set; }
        public string schema { get; set; }
    }

    public class contributors
    {
        public string title { get; set; }
        public string email { get; set; }
        public string path { get; set; }
        public string role { get; set; }
        public string organization { get; set; }
    }

    public class sources
    {
        public string title { get; set; }
        public string email { get; set; }
        public string path { get; set; }
        public string version { get; set; }
    }

    public class licenses
    {
        public string name { get; set; }
        public string path { get; set; }
        public string title { get; set; }
        public string scope { get; set; }
    }

    public class project
    {
        public string id { get; set; }
        public string title { get; set; }
        public string acronym { get; set; }
        public string description { get; set; }
        public string samplingDesign { get; set; }
        public string path { get; set; }
        
        //public string captureMethodAsString { get; set; }
        public List<string> captureMethod { get; set; }
        public bool individualAnimals { get; set; }
        //public string observationLevelAsString { get; set; }
        public List<string> observationLevel { get; set; }
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
        public string scientificName { get; set; } 
        public string taxonID { get; set; } 
        public string taxonRank { get; set; } 
        public string kingdom { get; set; } 
        public string phylum { get; set; } 
        public string class_ { get; set; } 
        public string order { get; set; } 
        public string family { get; set; } 
        public string genus { get; set; } 
        public Dictionary<string, string> vernacularNames { get; set; }

        // 
        // Generates a truncated string used for feedback

        // Unused but keep for now in case it becomes useful at some point
        //[JsonIgnore]
        //public string vernacularCount => vernacularNames == null ? "0" : $"{vernacularNames.Count.ToString()} - {TruncatedVernacularNames()}";

        // Unused but keep for now in case it becomes useful at some point
        // Generate a possibly truncated string representation of the vernacular name list for display in the data grid.
        //private string TruncatedVernacularNames()
        //{
        //    const int max = 30;
        //    bool isStringTruncated = false;
        //    string truncatedString = string.Empty;
        //    foreach (KeyValuePair<string, string> vName in vernacularNames)
        //    {
        //        truncatedString += $"{vName.Key}:{vName.Value},";
        //        if (truncatedString.Length > max)
        //        {
        //            isStringTruncated = true;
        //            break;
        //        }
        //    }
        //    truncatedString = isStringTruncated
        //        ? $"{truncatedString.Substring(0, Math.Min(truncatedString.Length, max)).TrimEnd(',')}\u2026"
        //        : truncatedString.TrimEnd(',', ' ');
        //    return truncatedString;
        //}
    }

    public class relatedIdentifiers
    {
        public string relationType { get; set; }
        public string relatedIdentifier { get; set; }
        public string resourceTypeGeneral { get; set; }
        public string relatedIdentifierType { get; set; }
    }
}
