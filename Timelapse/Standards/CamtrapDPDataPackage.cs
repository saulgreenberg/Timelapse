using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using static Timelapse.Standards.CamtrapDPReferences;

namespace Timelapse.Standards
{
    public class CamtrapDPDataPackage
    {
        // Currently hard-wired to 3 Resources representing the deployment, media, and observations
        public List<resources> resources = new List<resources>()
        {
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

        public List<references_> references = new List<references_>()
        {
            new references_(),
        };
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

    public class references_
    {
        [JsonProperty("references")]
        public string references { get; set; }
    }

}
