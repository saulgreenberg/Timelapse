using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Security.Policy;
using System.Windows.Documents;
using System.Windows.Markup;
using System.Xml.Linq;
using Timelapse.Constant;
using static System.Net.Mime.MediaTypeNames;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.StartPanel;
namespace TimelapseTemplateEditor.Standards
{
    public static class CamtrapDPStandard
    {
        // The standard specification: a list of StandardsRow, common species (used by one row), and level aliases
        #region The Alias specification associated with each level
        public static Dictionary<int, string> Aliases = new Dictionary<int, string>
        {
            {1, "Project"},
            {2, "Deployment"},
        };
        #endregion

        #region PracticeImageSet Folder Metadata as a Row List
        public static List<StandardsRow> FolderMetadataRows = new List<StandardsRow>
        {
            #region Level 1: Metadata
            //
            // Resources object (optional?)
            //
            // We supply only two instances 
            // - As there are ony two csv resources that Timelapse generates (deployments and the combined observations/media)
            //   We do the first two as Resource1 and Resource2 representing the Deployment and combined Media/Observations name and path.
            //   The other resource values are common, so we only need one of them.
            //   We also make most of the fields invisible as the default values will likely be used.
            //   Defaults can, of course, be changed using the TemplateEditor
            //   Post-processing: convert / combine multiple resources into json instances

            // Resource1 - name * 
            new StandardsRow(
                Control.FixedChoice, 1, "deployments", "Deployment name*", "resource_Deployment_name",
                $"Deployment identifier.{Environment.NewLine}" +
                "• e.g., \"deployments\"",
                StandardsBase.CreateChoiceList(false, new List<string> { "deployments", "media/observations"}),
                false, false),
           
            // Resource - path *
            new StandardsRow(
                Control.MultiLine, 1, "Deployment.csv", "Deployment .csv file path*", "resource_Deployment_path",
                $"Path or URL to the Deployment .csv data file.{Environment.NewLine}" +
                "• e.g., \"Deployment.csv\"",
                null, false, true),

            new StandardsRow(
                Control.FixedChoice, 1, "media/observations", "Media/Observations name*", "resource_MediaObservations_name",
                $"Media and Observations identifier.{Environment.NewLine}" +
                "• e.g., \"media/observations\"",
                StandardsBase.CreateChoiceList(false, new List<string> { "deployments", "media/observations"}),
                false, false),
            
            new StandardsRow(
                Control.MultiLine, 1, "TimelapseData.csv", "Observations/media .csv file path*", "resource_MediaObservations_path",
                $"Path or URL to the combined Observations / Media .csv data file.{Environment.NewLine}" +
                "• e.g., \"TimelapseData.csv\"",
                null, true),

            new StandardsRow(
                Control.Note, 1, "tabular-data-resource", "Resource profile*", "resource_profile",
                $"Profile of the resource{Environment.NewLine}" +
                "• e.g., \"tabular-data-resource\"",
                null, false, false),

            new StandardsRow(
                Control.Note, 1, "https://raw.githubusercontent.com/tdwg/camtrap-dp/1.0/deployments-table-schema.json", "Resource schema*", "resource_schema",
                $"Profile of the resource{Environment.NewLine}" +
                "• e.g., \"https://raw.githubusercontent.com/tdwg/camtrap-dp/1.0/deployments-table-schema.json\"",
                null, false, false),


            // Profile
            new StandardsRow(
                Control.Note, 1, "https://raw.githubusercontent.com/tdwg/camtrap-dp/1.0/camtrap-dp-profile.json", "Profile*", "profile",
                $"The URL of the used Camtrap DP Profile version.{Environment.NewLine}" +
                "• e.g., \"https://raw.githubusercontent.com/tdwg/camtrap-dp/1.0/camtrap-dp-profile.json\"",
                null, false, false),

            // Name
            new StandardsRow(
                Control.AlphaNumeric, 1, "Timelapse-CamtrapDataset", "Name", "name",
                $"A short url-usable (and preferably human-readable) name of the package.{Environment.NewLine}" +
                "• e.g., \"Timelapse-CamtrapDataset\"",
                null,false, false),

            // Id
            new StandardsRow(
                Control.AlphaNumeric, 1, "", "Id", "guid_id",
                $"A property reserved for globally unique identifiers, including UUIDs and DOIs.{Environment.NewLine}" +
                "• e.g., \"b03ec84-77fd-4270-813b-0c698943f7ce\"",
                null, false, false),

            // Created
            new StandardsRow(
                Control.DateTime_, 1, "2024-01-01 12:00:00", "Created*", "created",
                $"The datetime on which this was created.{Environment.NewLine}" +
                "• e.g., \"2024-01-12 12:00:00\"",
                null, false, false),

            // Title
            new StandardsRow(
                Control.MultiLine, 1, "", "Title*", "title",
                $"Title of the source (e.g. document or organization name).{Environment.NewLine}" +
                "• e.g., \"EcoLive organization\"",
                null, false, false),



            // Contributors is a json array - we provide space for a single contributor for now. 
            // Contributors - Title
            new StandardsRow(
                Control.MultiLine, 1, "", "Contributors1 title*", "contributors1_title",
                $"Name/title of the contributor (name for person, name/title of organization).{Environment.NewLine}" +
                $"Note: Contributors describe the people or organizations who contributed to this Data Package.{Environment.NewLine}" +
                "• e.g., \"Joe Bloggs\"",
                null, false, true),

            // Contributors - Path
            new StandardsRow(
                Control.MultiLine, 1, "", "Contributors1 path*", "contributors1_path",
                $"a fully qualified http URL pointing to a relevant location online for the contributor.{Environment.NewLine}" +
                $"Note: Contributors describe the people or organizations who contributed to this Data Package.{Environment.NewLine}" +
                "• e.g., \"http://www.bloggs.com\"",
                null, false, true),

            // Contributors - Email
            new StandardsRow(
                Control.MultiLine, 1, "", "Contributors1 email*", "contributors1_email",
                $"An email address for the contributor.{Environment.NewLine}" +
                $"Note: Contributors describe the people or organizations who contributed to this Data Package.{Environment.NewLine}" +
                "• e.g., \"http://www.bloggs.com\"",
                null, false, true ),

            // Contributors - Role
            new StandardsRow(
                Control.FixedChoice, 1, "contributor", "Contributors1 role*", "contributors1_role",
                $"The role of the contributor.{Environment.NewLine}" +
                $"Note: Contributors describe the people or organizations who contributed to this Data Package.{Environment.NewLine}" +
                "• e.g., \"contributor\"",
                StandardsBase.CreateChoiceList(false, new List<string> { "contributor", "contact", "principalInvestigator", "rightsHolder", "publisher"}), 
                false, true),

            // Description
            new StandardsRow(
                Control.MultiLine, 1, "", "Description", "description",
                $"A description of the package. The description MUST be markdown (opens new window)formatted.{Environment.NewLine}" +
                $"This also allows for simple plain text as plain text is itself valid markdown.{Environment.NewLine}" +
                $"The first paragraph (up to the first double line break) should be usable as summary information for the package{Environment.NewLine}" +
                "• e.g., \"Wolvering camera trap observations in Alberta, Canada. Part of an effort for tracking Wolverines.\"",
                null, false, true ),

            // Description
            new StandardsRow(
                Control.Note, 1, "1.0", "Version", "version",
                $"A version string identifying the version of the package.{Environment.NewLine}" +
                $"It should conform to MAJOR.MINOR.PATCH. semantics{Environment.NewLine}" +
                "• e.g., \"1.0.0\"",
                null, false, false),

            new StandardsRow(
                Control.MultiLine, 1, "", "Keywords", "keywords",
                $"A comma-separated list of keywordsto assist users searching for the package in catalogs.{Environment.NewLine}" +
                "• e.g., \"wolverine, wildlife management, conservation, population monitoring\"",
                null, false, false),

            new StandardsRow(
                Control.Note, 1, "", "Image", "image",
                $"A url or path to a representive image to use, for example, to show the package in a listing.{Environment.NewLine}" +
                "• e.g., \"http://wolverine_project.org/images/wolverine.jpg\"",
                null),

            new StandardsRow(
                Control.Note, 1, "", "Home Page", "homepage",
                $"A URL for the home on the web that is related to this data package.{Environment.NewLine}" +
                "• e.g., \"http://wolverine_project.org\"",
                null, false, false),

            // Sources - an array of which we provide only one
            // Sources Title
            new StandardsRow(
                Control.MultiLine, 1, "", "Source1 title*", "sources1_title",
                $"Title of the source (e.g. document or organization name).{Environment.NewLine}" +
                "• e.g., \"World Bank and OECD\"",
                null, false, false),

            // Sources Path
            new StandardsRow(
                Control.MultiLine, 1, "", "Source1 path", "sources1_path",
                $"A url-or-path to the source.{Environment.NewLine}" +
                "• e.g., \"https://www.agouti.eu\"",
                null, false, false),

            // Sources Email
            new StandardsRow(
                Control.MultiLine, 1, "", "Source1 email", "sources1_email",
                $"An email to the source.{Environment.NewLine}" +
                "• e.g., \"bloggs@agouti.com\"",
                null, false, false),

            // Sources Version
            new StandardsRow(
                Control.Note, 1, "", "Source1 version", "sources1_version",
                $"Version of the source.{Environment.NewLine}" +
                "• e.g., \"v3.21\"",
                null, false, false),

            // License - an array of which we provide only one
            // License Name
            new StandardsRow(
                Control.MultiLine, 1, "", "License1 Name", "license1_name",
                $"An Open Definition license ID. See https://opendefinition.org/licenses/api/{Environment.NewLine}" +
                "• e.g., \"ODC-PDDL-1.0\"",
                null, false, false),

            // License Path
            new StandardsRow(
                Control.Note, 1, "", "License1 Path", "license1_path",
                $"An Open Definition license ID. See https://opendefinition.org/licenses/api/{Environment.NewLine}" +
                "• e.g., \"http://opendatacommons.org/licenses/pddl/\"",
                null, false, false),

            // License Title
            new StandardsRow(
                Control.MultiLine, 1, "", "License1 Title", "license1_title",
                $"A human-readable title of the license{Environment.NewLine}" +
                "• e.g., \"Open Data Commons Public Domain Dedication and License v1.0\"",
                null, false, false),


            // BibliographicCitation
            new StandardsRow(
                Control.MultiLine, 1, "", "Bibliographic citation", "bibliographicCitation",
                $"Bibliographic/recommended citation for the package.{Environment.NewLine}" +
                $"• e.g., \"Desmet P, Neukermans A, Van der beeck D, Cartuyvels E (2022).{Environment.NewLine}" +
                $"Sample from: MICA - Muskrat and coypu camera trap observations in Belgium, the Netherlands and Germany.{Environment.NewLine}" +
                $"Version 1.0. Research Institute for Nature and Forest (INBO).{Environment.NewLine}" +
                $"Dataset. https://camtrap-dp.tdwg.org/example/\"",
                null, false, false),


            // Project - an object
            // Project - id
            new StandardsRow(
                Control.Note, 1, "", "Project ID*", "project_id",
                $"Unique identifier of the project.{Environment.NewLine}" +
                $"• e.g., \"86cabc14-d475-4439-98a7-e7b590bed60e\"",
                null, false, false),

            // Project - title
            new StandardsRow(
                Control.Note, 1, "", "Project title*", "project_title",
                $"Title of the project.{Environment.NewLine}" +
                $"• e.g., \"Management of Invasive Coypu and muskrAt in Europe\"",
                null, false, false),

            
            // Project - acronym
            new StandardsRow(
                Control.Note, 1, "", "Project acronym", "project_acronym",
                $"Title of the project.{Environment.NewLine}" +
                $"• e.g., \"MICA\"",
                null, false, false),
            //Project - Description
            new StandardsRow(
                Control.MultiLine, 1, "", "Description", "project_description",
                $"Description of the project.{Environment.NewLine}" +
                "• e.g., \"Invasive alien species such as the coypu and muskrat pose a major threat to biodiversity and cost millions of euros annually, etc.\"",
                null),

            //Project - Path
            new StandardsRow(
                Control.MultiLine, 1, "", "Path", "project_path",
                $"Project website.{Environment.NewLine}" +
                "• e.g., \"https://saul.cpsc.ucalgary.ca/timelapse/.\"",
                null, false, false),

            //Project - Sampling Design
            new StandardsRow(
                Control.FixedChoice, 1, "", "Sampling Design*", "project_samplingDesign",
                $"Type of a sampling design/layout. {Environment.NewLine}" +
                $"The values are based on Wearn & Glover-Kapfer (2017), pages 80-82{Environment.NewLine}" +
                "• e.g., \"simpleRandom\"",
                StandardsBase.CreateChoiceList(true, new List<string> { "simpleRandom", "systematicRandom", "clusteredRandom", "experimental", "targeted", "opportunistic"}),
                    false, false),

            //Project - CaptureMethod
            new StandardsRow(
                Control.MultiChoice, 1, "", "Capture Method*", "project_captureMethod",
                $"Method(s) used to capture the media files. {Environment.NewLine}" +
                "• e.g., \"activityDetection\"",
                StandardsBase.CreateChoiceList(true, new List<string> { "activityDetection", "timeLapse"}),
                    false, false),

            //Project - Individual Animals
            new StandardsRow(
                Control.Flag, 1, "", "Individual Animals*", "project_individualAnimals",
                $"true if the project includes marked or recognizable individuals.{Environment.NewLine}" +
                "• e.g., \"false\"",
                null, false, false),

            //Project - ObservationLevel
            new StandardsRow(
                Control.FixedChoice, 1, "", "Observation Level*", "project_observationLevel",
                $"Level at which observations are provided. {Environment.NewLine}" +
                "• e.g., \"media\"",
                StandardsBase.CreateChoiceList(true, new List<string> { "media", "event"}),
                false, false),


            // CoordinatePrecision
            new StandardsRow(
                Control.DecimalPositive, 1, "0", "Coordinate precision", "coordinatePrecision",
                $"Least precise coordinate precision of the deployments.latitude and deployments.longitude (e.g. 0.01 for coordinates with a precision of 0.01 and 0.001 degree).{Environment.NewLine}" +
                $"Especially relevant when coordinates have been rounded to protect sensitive species.{Environment.NewLine}" +
                "• e.g., \"0.01\"",
                null,
                false, false),
            
            // Spatial
            new StandardsRow(
                Control.MultiLine, 1, "0", "Spatial coverage", "spatial",
                $"Spatial coverage of the package, expressed as GeoJSON.{Environment.NewLine}" +
                "• e.g., \"{\r\n    \"type\": \"Polygon\",\r\n    \"bbox\": [\r\n      4.013,\r\n      50.699,\r\n      5.659,\r\n      51.496\r\n    ],\r\n    \"coordinates\": [\r\n      [\r\n        [\r\n          4.013,\r\n          50.699\r\n        ],\r\n        [\r\n          5.659,\r\n          50.699\r\n        ],\r\n        [\r\n          5.659,\r\n          51.496\r\n        ],\r\n        [\r\n          4.013,\r\n          51.496\r\n        ],\r\n        [\r\n          4.013,\r\n          50.699\r\n        ]\r\n      ]\r\n    ]\r\n  }\"",
                null,
                false, false),

            // Temporal OBJECT
            // Temporal start*
            new StandardsRow(
                Control.Date_, 1, "2024-01-01", "Temporal start", "temporal_start",
                $"Start date of the first deployment.{Environment.NewLine}" +
                "• e.g., \"2024-01-12\"",
                null, false, false),

            // Temporal end**
            new StandardsRow(
                Control.Date_, 1, "2024-01-01", "Temporal end", "temporal_end",
                $"End date of the last (completed) deployment.{Environment.NewLine}" +
                "• e.g., \"2024-01-12\"",
                null, false, false),

            // Taxonomic Object
            // There are a variety of fileds in this object, but I only include 1 - the scientific anem - as a multi choice list that can be parsed into a json object
            // taxonomic - Scientific name 
            // left out: taxonID, taxonRank, kingdom, phylum, class, order, family, genus, vernacularNames (as languageCode: vernacular name pairs)
            //Project - ObservationLevel
            new StandardsRow(
                Control.MultiChoice, 1, "", "Taxonomic sientific name*", "taxonomic_scientificName",
                $"Taxonomic coverage. {Environment.NewLine}" +
                "• e.g., \"see drop-down menu\"",
                StandardsBase.CreateChoiceList(true, new List<string> { "Anas platyrhynchos", "Anas strepera", "Ardea", "Ardea cinerea", "Aves", "Homo sapiens", "Martes foina", "Mustela putorius", "Rattus norvegicus", "Vulpes vulpes"}),
                false, false),

            // Related Identifiers object - I only provide 1 but multiples are allowed in spec
            // Related Identifiers - relation type*
            new StandardsRow(
                Control.FixedChoice, 1, "", "Related identifiers - relation type*", "relatedIdentifiers1_relationType",
                $"Description of the relationship between the resource (the package) and the related resource.{Environment.NewLine}" +
            "• e.g., \"IsDerivedFrom\"",
                StandardsBase.CreateChoiceList(true, new List<string> { "IsCitedBy", "Cites", "IsSupplementTo", "IsSupplementedBy", "IsContinuedBy", "Continues",
                "IsNewVersionOf", "IsPreviousVersionOf", "IsPartOf", "HasPart", "IsPublishedIn", "IsReferencedBy", "References", 
                "IsDocumentedBy", "Documents", "IsCompiledBy", "Compiles", "IsVariantFormOf", "IsOriginalFormOf", "IsIdenticalTo", 
                "HasMetadata", "IsMetadataFor", "Reviews", "IsReviewedBy", "IsDerivedFrom", "IsSourceOf", "Describes", "IsDescribedBy", 
                "HasVersion", "IsVersionOf", "Requires", "IsRequiredBy", "Obsoletes", "IsObsoletedBy"}),
            false, false),

            // Related Identifiers - relatedIdentifier*
            new StandardsRow(
                Control.MultiLine, 1, "", "Related identifiers - Related identifier*", "relatedIdentifiers1_relatedIdentifier",
                $"End date of the last (completed) deployment.{Environment.NewLine}" +
                "• e.g., \"https://doi.org/10.15468/5tb6ze\"",
                null, false, false),

            // Related Identifiers - resourceTypeGeneral
            new StandardsRow(
                Control.FixedChoice, 1, "", "Related identifiers - Resource type general", "relatedIdentifiers1_resourceTypeGeneral",
                $"Description of the relationship between the resource (the package) and the related resource.{Environment.NewLine}" +
            "• e.g., \"IsDerivedFrom\"",
                StandardsBase.CreateChoiceList(true, new List<string> { "Audiovisual", "Book", "BookChapter", "Collection", "ComputationalNotebook", "ConferencePaper", "ConferenceProceeding", 
                    "DataPaper", "Dataset", "Dissertation", "Event", "Image", "InteractiveResource", "Journal", "JournalArticle", "Model", "OutputManagementPlan", "PeerReview", "PhysicalObject", 
                    "Preprint", "Report", "Service", "Software", "Sound", "Standard", "Text", "Workflow", "Other"}),
                false, false),

            // Related Identifiers - relatedIdentifierType *
            new StandardsRow(
                Control.FixedChoice, 1, "", "Related identifiers - Related identifier type", "relatedIdentifiers1_relatedIdentifierType",
                $"Description of the relationship between the resource (the package) and the related resource.{Environment.NewLine}" +
            "• e.g., \"IsDerivedFrom\"",
            StandardsBase.CreateChoiceList(true, new List<string> { "ARK", "arXiv", "bibcode", "DOI", "EAN13", "EISSN", 
                "Handle", "IGSN", "ISBN", "ISSN", "ISTC", "LISSN", "LSID", "PMID", "PURL", "UPC", "URL", "URN", "w3id"}),
                false, false),

            #endregion

            #region Level 2: Deployment
            new StandardsRow(
            Control.Note, 2, "", "Deployment ID*", "deploymentID",
            $"Unique identifier of the deployment.{Environment.NewLine}" +
            $"Requried.{Environment.NewLine}" +
            "• e.g., \"dep1\"",
            null),
            new StandardsRow(
            Control.Note, 2, "", "Location ID", "locationID",
            $"Identifier of the deployment location{Environment.NewLine}" +
            $"Optional.{Environment.NewLine}" +
            "• e.g., \"loc1\".",
            null),
            new StandardsRow(
            Control.Note, 2, "", "Location Name", "locationName",
            $"Name given to the deployment location.{Environment.NewLine}" +
            $"Optional.{Environment.NewLine}" +
            "• e.g., \"Bialowieza MRI 01\".",
            null),
            new StandardsRow(Control.DecimalAny, 2, "", "Latitude*", "latitude",
            $"Latitude of the deployment location in decimal degrees, using the WGS84 datum.{Environment.NewLine}" +
            $"Minimum -90 to maximum 90, Required.{Environment.NewLine}" +
            $"Requried.{Environment.NewLine}" +
            "• e.g., \"52.70442h\".",
            null),

            new StandardsRow(Control.DecimalAny, 2, "", "Longitude*", "Longitude",
                $"Longitude of the deployment location in decimal degrees, using the WGS84 datum.{Environment.NewLine}" +
                $"Requried.{Environment.NewLine}" +
                "• e.g., \"23.84995\"",
                null),

            new StandardsRow(Control.IntegerPositive, 2, "", "Coordinate Uncertainty*", "coordinateUncertainty",
                $"Horizontal distance from the given latitude and longitude describing the smallest circle containing the deployment location. {Environment.NewLine}" +
                $"Especially relevant when coordinates are rounded to protect sensitive species.{Environment.NewLine}" +
                $"Minimum 1. Expressed in meters. Optional.{Environment.NewLine}" +
                " • e.g., \"100\"",
                null),

            new StandardsRow(Control.DateTime_, 2, "", "Deployment Start*", "deploymentStart",
                $"Date_ and time at which the deployment was started.{Environment.NewLine}" +
                $"Requried.{Environment.NewLine}" +
                "• e.g., \"2020-03-01T22:00:00Z\"",
                null),

            new StandardsRow(Control.DateTime_, 2, "", "Deployment End*", "deploymentEnd",
                $"Date_ and time at which the deployment was ended.{Environment.NewLine}" +
                $"Requried.{Environment.NewLine}" +
                "• e.g., \"2020-04-01T22:00:00Z\"",
                null),

            new StandardsRow(
                Control.MultiLine, 2, "", "Setup By", "setupBy",
                $"Name or identifier of the person or organization that deployed the camera.{Environment.NewLine}" +
                $"Optional.{Environment.NewLine}" +
                "• e.g., \"Jakub Bubnicki\".",
                null),

            new StandardsRow(
                Control.Note, 2, "", "Camera ID", "cameraID",
                $"Identifier of the camera used for the deployment (e.g. the camera device serial number).{Environment.NewLine}" +
                $"Optional.{Environment.NewLine}" +
                "• e.g., \"P800HG08192031\"",
                null),

            new StandardsRow(
                Control.AlphaNumeric, 2, "", "Camera Model", "cameraModel",
                $"Manufacturer and model of the camera {Environment.NewLine}" +
                $"Formatted as manufacturer-model. Optional.{Environment.NewLine}" +
                "• e.g., \"Reconyx-PC800\"",
                null),

            new StandardsRow(
                Control.IntegerPositive, 2, "", "Camera Delay", "cameraDelay",
                $"Predefined duration after detection when further activity is ignored. {Environment.NewLine}" +
                $"Minimum 0. Expressed in seconds. Optional.{Environment.NewLine}" +
                "• e.g., \"120\"",
                null),

            new StandardsRow(
                Control.DecimalPositive, 2, "", "Camera Height", "cameraHeight",
                $"Height at which the camera was deployed.{Environment.NewLine}" +
                $"Minimum 0. Expressed in meters. Not to be combined with cameraHeight. Optional.{Environment.NewLine}" +
                "• e.g., \"1.2\"",
                null),

            new StandardsRow(
                Control.DecimalPositive, 2, "", "Camera Depth", "cameraDepth",
                $"Depth at which the camera was deployed.{Environment.NewLine}" +
                $"Minimum 0. Expressed in meters. Optional.{Environment.NewLine}" +
                "• e.g., \"4.8\"",
                null),

            new StandardsRow(
                Control.IntegerAny, 2, "", "Camera Tilt", "cameraTilt",
                $"Angle at which the camera was deployed in the vertical plane.{Environment.NewLine}" +
                $"Minimum -90, Maximum 90. Expressed in degrees, with -90 facing down, 0 horizontal and 90 facing up. Optional.{Environment.NewLine}" +
                "• e.g., \"-90\"",
                null),

            new StandardsRow(
                Control.IntegerPositive, 2, "", "Camera Heading", "cameraHeading",
                $"Angle at which the camera was deployed in the horizontal plane.{Environment.NewLine}" +
                $"Minimum 0, Maximum 360. Expressed in decimal degrees clockwise from north, with values ranging from 0 to 360: 0 = north, 90 = east, 180 = south, 270 = west. Optional.{Environment.NewLine}" +
                "• e.g., \"225\"",
                null),

            new StandardsRow(
                Control.DecimalPositive, 2, "", "Detection Distance", "detectionDistance",
                $"Maximum distance at which the camera can reliably detect activity. Typically measured by having a human move in front of the camera.{Environment.NewLine}" +
                $"Expressed in meters. Optional.{Environment.NewLine}" +
                "• e.g., \"9.5\"",
                null),

            new StandardsRow(
                Control.Flag, 2, "", "Timestamp Issues", "timestampIssues",
                $"true if timestamps in the media resource for the deployment are known to have (unsolvable) issues (e.g. unknown timezone, am/pm switch).{Environment.NewLine}" +
                $"Optional.{Environment.NewLine}" +
                "• e.g., \"true\"",
                null),

            new StandardsRow(
                Control.Flag, 2, "", "Bait Use", "baitUse",
                $"true if bait was used for the deployment. More information can be provided in tags or comments.{Environment.NewLine}" +
                $"Optional.{Environment.NewLine}" +
                "• e.g., \"true\"",
                null
                ),

            new StandardsRow(
                Control.FixedChoice, 2, "", "Feature Type", "featureType",
                $"Type of the feature (if any) associated with the deployment.{Environment.NewLine}" +
                $"Optional.{Environment.NewLine}" +
                "• e.g., \"culvert\"",
                StandardsBase.CreateChoiceList(true, new List<string> {"roadPaved", "roadDirt", "trailHiking", "trailGame", "roadUnderpass", "roadOverpass", "roadBridge", "culvert", "burrow", "nestSite", "carcass", "waterSource", "fruitingTree"})
            ),

            new StandardsRow(
                Control.MultiLine, 2, "", "Habitat", "habitat",
                $"Short characterization of the habitat at the deployment location.{Environment.NewLine}" +
                $"Optional.{Environment.NewLine}" +
                "• e.g., \"Mixed temperate low-land forest\"",
                null 
            ),

            new StandardsRow(
                Control.MultiLine, 2, "", "Deployment Groups", "deploymentGroups",
                $"Deployment group(s) associated with the deployment. Deployment groups can have a spatial (arrays, grids, clusters), temporal (sessions, seasons, months, years) or other context.{Environment.NewLine}" +
                $"Formatted as a pipe (|) separated list for multiple values, with values preferably formatted as key:value pairs.{Environment.NewLine}" +
                $"Optional.{Environment.NewLine}" +
                "• e.g., \"season:winter 2020 | grid:A1\"",
                null
                ),

            new StandardsRow(
                Control.MultiLine, 2, "", "Deployment Tags", "deploymentTags",
                $"Tag(s) associated with the deployment.{Environment.NewLine}" +
                $"Formatted as a pipe (|) separated list for multiple values, with values optionally formatted as key:value pairs.{Environment.NewLine}" +
                $"Optional.{Environment.NewLine}" +
                "• e.g., \"forest edge | bait:food\"",
                null
            ),

            new StandardsRow(
                Control.MultiLine, 2, "", "Deployment Comments", "deploymentComments",
                $"Comments or notes about the deployment.{Environment.NewLine}" +
                $"Optional.{Environment.NewLine}" +
                "",
                null
            ),
        };
        #endregion

        #region Level 0: Media and Observations

        public static List<StandardsRow> ImageTemplateRows = new List<StandardsRow>
        {
            //new StandardsRow(Control.Note, 0, "", "Media ID", "mediaID",
            //    $"Unique identifier of the media file.{Environment.NewLine}" +
            //    $"Required.{Environment.NewLine}" +
            //    "• e.g., \"m1\"",
            //    null),

            // deploymentID LEFT OUT AS IT WILL BE AUTOMATICALLY ADDED
            //new StandardsRow(Control.Note, 0, "", "Deployment ID", "deploymentID",
            //    $"Identifier of the deployment the media file belongs to. {Environment.NewLine}" +
            //    $"Required.{Environment.NewLine}" +
            //    "• e.g., \"dep1\"",
            //    null),

            new StandardsRow(Control.FixedChoice, 0, "", "Capture Method", "captureMethod",
                $"Method used to capture the media file.{Environment.NewLine}" +
                "• e.g., \"activityDetection\"",
                $"Optional.{Environment.NewLine}" +
                StandardsBase.CreateChoiceList(true, new List<string> { "activityDetection", "timelapse" })),

           
            new StandardsRow(Control.DateTime_, 0, "", "Timestamp*", "timestamp",
                $"Date_ and time at which the media file was recorded. {Environment.NewLine}" +
                $"Required. Format as YYYY-MM-DDThh:mm:ssZ or YYYY-MM-DDThh:mm:ss±hh:mm{Environment.NewLine}" +
                "• e.g., \"2020-03-24T11:21:46Z\"",
                null),

            new StandardsRow(Control.Note, 0, "", "Filepath*", "filepath",
                $"URL or relative path to the media file, respectively for externally hosted files or files that are part of the package.{Environment.NewLine}" +
                $"Required.{Environment.NewLine}" +
                "• e.g., \"https://multimedia.agouti.eu/assets/6d65f3e4-4770-407b-b2bf-878983bf9872/file\"",
                null),

            new StandardsRow(Control.Flag, 0, "", "File Public*", "filePublic",
                $"false if the media file is not publicly accessible (e.g. to protect the privacy of people).{Environment.NewLine}" +
                $"Required.{Environment.NewLine}" +
                "• e.g., \"true\"",
                null),

            new StandardsRow(Control.Note, 0, "", "File Name", "fileName",
                "Name of the media file. " +
                $"If provided, one should be able to sort media files chronologically within a deployment on timestamp (first) and fileName (second).{Environment.NewLine}" +
                $"Optional.{Environment.NewLine}" +
                "• e.g., \"IMG0001.jpg\"",
                null),

            new StandardsRow(Control.FixedChoice, 0, "", "File Mediatype*", "fileMediatype",
                $"Mediatype of the media file. Expressed as an IANA Media Type.{Environment.NewLine}" +
                $"Pattern: ^(image|video|audio)/.*$ Required.{Environment.NewLine}" +
                "• e.g., \"image\"",
                StandardsBase.CreateChoiceList(true, new List<string> { "image", "video", "audio" })),

            new StandardsRow(Control.MultiLine, 0, "", "Exif Data*", "exifData",
                "EXIF data of the media file." +
                $"Formatted as a valid JSON object. Optional.{Environment.NewLine}" +
                "• e.g., \"{\"EXIF\":{\"ISO\":200,\"Make\":\"RECONYX\"}}\"",
                null),

            new StandardsRow(Control.Flag, 0, "", "Favorite", "favorite",
                $"true if the media file is deemed of interest (e.g. an exemplar image of an individual).{Environment.NewLine}" +
                $"Optional.{Environment.NewLine}" +
                "• e.g., \"true\"",
                null),

            new StandardsRow(Control.MultiLine, 0, "", "Media Comments", "mediaComments",
                $"Comments or notes about the media file.{Environment.NewLine}" +
                $"Optional.{Environment.NewLine}" +
                "• e.g., \"corrupted file\"",
                null),

            //
            // OBSERVATIONS SECTION
            //
            new StandardsRow(Control.Note, 0, "", "Observation ID*", "observationID",
                $"Unique identifier of the observation.{Environment.NewLine}" +
                $"Required.{Environment.NewLine}" +
                "• e.g., \"obs1\"",
                null),


            // deploymentIDLEFT OUT AS ITS IN THE SAME LEVEL
            //new StandardsRow(Control.Note, 0, "", "Deployment ID", "deploymentID",
            //    $"Identifier of the deployment the media file belongs to.{Environment.NewLine}" +
            //    $"Required.{Environment.NewLine}" +
            //    "• e.g., \"dep1\"",
            //    null),

            // mediaID LEFT OUT AS ITS IN THE SAME LEVEL

            new StandardsRow(Control.Note, 0, "", "Event ID", "eventID",
                $"Identifier of the event the observation belongs to.{Environment.NewLine}" +
                $"Facilitates linking event-based and media-based observations with a permanent identifier.{Environment.NewLine}" +
                $"Optional.{Environment.NewLine}" +
                "• e.g., \"sequence1\"",
                null),

            new StandardsRow(Control.DateTime_, 0, "", "Event Start*", "eventStart",
                $"Date and time at which the event started. .{Environment.NewLine}" +
                $"Optional.{Environment.NewLine}" +
                "• e.g., \"2023-11-31 13:01:05\"",
                null),

            new StandardsRow(Control.DateTime_, 0, "", "Event End*", "eventEnd",
                $"Date and time at which the event ended. .{Environment.NewLine}" +
                $"Optional.{Environment.NewLine}" +
                "• e.g., \"2023-11-31 13:01:05\"",
                null),

            new StandardsRow(Control.FixedChoice, 0, "", "Observation Level*", "observationLevel",
                "Level at which the observation was classified. " +
                $"• media for media-based observations that are directly associated with a media file (mediaID).{Environment.NewLine}" +
                $"  These are especially useful for machine learning and don’t need to be mutually exclusive (e.g. multiple classifications are allowed).{Environment.NewLine}" +
                $"• event for event-based observations that consider an event (comprising a collection of media files).{Environment.NewLine}" +
                $"  These are especially useful for ecological research and should be mutually exclusive, so that their count can be summed.{Environment.NewLine}" +
                $"• Facilitates linking event-based and media-based observations with a permanent identifier.{Environment.NewLine}" +
                $"Required.{Environment.NewLine}" +
                "• e.g., \"event\"",
                StandardsBase.CreateChoiceList(true, new List<string> { "media", "event" })),

            new StandardsRow(Control.FixedChoice, 0, "", "Observation Type*", "observationType",
                $"Type of the observation.{Environment.NewLine}" +
                $"All categories in this vocabulary have to be understandable from an AI point of view.{Environment.NewLine}" +
                $"unknown describes classifications with a classificationProbability below some predefined threshold{Environment.NewLine}" +
                $"i.e. neither humans nor AI can say what was recorded.{Environment.NewLine}" +
                $"Required.{Environment.NewLine}" +
                "• e.g., \"animal\"",
                StandardsBase.CreateChoiceList(true, new List<string> { "animal", "human", "vehicle", "blank", "unknown", "unclassified"})),

            new StandardsRow(Control.FixedChoice, 0, "", "Camera Setup Type", "cameraSetupType",
                $"Type of the camera setup action (if any) associated with the observation.{Environment.NewLine}" +
                $"Optional.{Environment.NewLine}" +
                "• e.g., \"calibration\"",
                StandardsBase.CreateChoiceList(true, new List<string> { "setup", "calibration"})),

            new StandardsRow(Control.Note, 0, "", "Scientific Name", "scientificName",
                $"Scientific name of the observed individual(s).{Environment.NewLine}" +
                $"Optional.{Environment.NewLine}" +
                "• e.g., \"Canis lupus\"",
                null),

            new StandardsRow(Control.Counter, 0, "", "Count", "count",
                $"Number of observed individuals (optionally of given life stage, sex and behavior).{Environment.NewLine}" +
                $"Minimum 1. Optional.{Environment.NewLine}" +
                "• e.g., \"5\"",
                null),

            new StandardsRow(Control.FixedChoice, 0, "", "Life Stage", "lifeStage",
                $"Age class or life stage of the observed individual(s).{Environment.NewLine}" +
                $"Optional.{Environment.NewLine}" +
                "• e.g., \"adult\"",
                StandardsBase.CreateChoiceList(true, new List<string> { "adult", "subadult", "juvenile"})),

            new StandardsRow(Control.FixedChoice, 0, "", "Sex", "sex",
                $"Sex of the observed individual(s).{Environment.NewLine}" +
                $"Optional.{Environment.NewLine}" +
                "• e.g., \"female\"",
                StandardsBase.CreateChoiceList(true, new List<string> { "female", "male"})),

            new StandardsRow(Control.Note, 0, "", "Behavior", "behavior",
                $"Dominant behavior of the observed individual(s), preferably expressed as controlled values.{Environment.NewLine}" +
                $"(e.g. grazing, browsing, rooting, vigilance, running, walking).{Environment.NewLine}" +
                $"Formatted as a pipe (|) separated list for multiple values, with the dominant behavior listed first.{Environment.NewLine}" +
                $"Optional.{Environment.NewLine}" +
                "• e.g., \"vigilance\"",
                null),

            new StandardsRow(Control.Note, 0, "", "Individual ID", "individualID",
                $"Identifier of the observed individual.{Environment.NewLine}" +
                $"Optional.{Environment.NewLine}" +
                "• e.g., \"RD213\"",
                null),

            new StandardsRow(Control.DecimalAny, 0, "", "Individual Position Radius", "individualPositionRadius",
                $"Distance from the camera to the observed individual identified by individualID.{Environment.NewLine} " +
                $"Required for distance analyses (e.g. Howe et al. 2017) and random encounter modelling (e.g. Rowcliffe et al. 2011).{Environment.NewLine}" +
                $"Expressed in meters. Optional.{Environment.NewLine}" +
                "• e.g., \"-6.81\"",
                null),

            new StandardsRow(Control.DecimalAny, 0, "", "Individual Position Angle", "individualPositionAngle",
                $"Angular distance from the camera view centerline to the observed individual identified by individualID.{Environment.NewLine}" +
                $"Required for distance analyses (e.g. Howe et al. 2017) and random encounter modelling (e.g. Rowcliffe et al. 2011).{Environment.NewLine}" +
                $"Expressed in degrees, with negative values left, 0 straight ahead and positive values right.{Environment.NewLine}" +
                $"Minimum: -90, Maximum: 90. Optional.{Environment.NewLine}" +
                "• e.g., \"-8.56\"",
                null),

            new StandardsRow(Control.DecimalPositive, 0, "", "Individual Position Speed", "individualPositionSpeed",
                $"Average movement speed of the observed individual identified by individualID.{Environment.NewLine}" +
                $"Required for random encounter modelling (e.g. Rowcliffe et al. 2016).{Environment.NewLine}" +
                $"Expressed in degrees, with negative values left, 0 straight ahead and positive values right.{Environment.NewLine}" +
                $"Expressed in meters per second.  Optional.{Environment.NewLine}" +
                "• e.g., \"1.75\"",
                null),

            new StandardsRow(Control.DecimalAny, 0, "", "Bbox X", "bboxX",
                $"Horizontal position of the top-left corner of a bounding box that encompasses the observed individual(s) in the media file identified by mediaID.{Environment.NewLine}" +
                $"Or the horizontal position of an object in that media file. {Environment.NewLine}" +
                $"Measured from the left and relative to media file width.{Environment.NewLine}" +
                "Minimum: 0, maximum: 1. Optional." +
                "• e.g., \".2\"",
                null),

            new StandardsRow(Control.DecimalAny, 0, "", "Bbox Y", "bboxY",
                $"Vertical position of the top-left corner of a bounding box that encompasses the observed individual(s) in the media file identified by mediaID.{Environment.NewLine}" +
                $"Or the vertical position of an object in that media file.{Environment.NewLine}" +
                $"Measured from the top and relative to media file width.{Environment.NewLine}" +
                "Minimum: 0, maximum: 1. Optional." +
                "• e.g., \".25\"",
                null),

            new StandardsRow(Control.DecimalAny, 0, "", "Bbox Width", "bboxWidth",
                $"Width of a bounding box that encompasses the observed individual(s) in the media file identified by mediaID.{Environment.NewLine}" +
                $"Measured from the left of the bounding box and relative to the media file width.{Environment.NewLine}" +
                "Minimum: 1e-15, maximum: 1. Optional." +
                "• e.g., \"0.4\"",
                null),

                new StandardsRow(Control.DecimalAny, 0, "", "Bbox Height", "bboxHeight",
                    $"Height of a bounding box that encompasses the observed individual(s) in the media file identified by mediaID.{Environment.NewLine}" +
                    $"Measured from the top of the bounding box and relative to the media file height.{Environment.NewLine}" +
                    "Minimum: 1e-15, maximum: 1. Optional." +
                    "• e.g., \"0.5\"",
                null),

                new StandardsRow(Control.FixedChoice, 0, "", "Classification Method", "classificationMethod",
                    $"Method (most recently) used to classify the observation.{Environment.NewLine}" +
                    "Optional." +
                    "• e.g., \"human\"",
                    StandardsBase.CreateChoiceList(true, new List<string> { "human", "machine"})),

                new StandardsRow(Control.Note, 0, "", "Classified By", "classifiedBy",
                    $"Name or identifier of the person or AI algorithm that (most recently) classified the observation.{Environment.NewLine}" +
                    "Optional." +
                    "• e.g., \"MegaDetector V5\"",
                    null),

                new StandardsRow(Control.DateTime_, 0, "", "Classification Timestamp", "classificationTimestamp",
                    $"Date_ and time of the (most recent) classification.{Environment.NewLine}" +
                    "Optional." +
                    "• e.g., \"2020-08-22 10:25:19\"",
                    null),

                new StandardsRow(Control.IntegerPositive, 0, "", "Classification Probability", "classificationProbability",
                    $"Degree of certainty of the (most recent) classification.{Environment.NewLine}" +
                    $"Expressed as a probability, with 1 being maximum certainty.{Environment.NewLine}" +
                    $"Omit or provide an approximate probability for human classifications.{Environment.NewLine}" +
                    "Minimum: 0, maximum: 1. Optional." +
                    "• e.g., \"0.95\"",
                    null),

                new StandardsRow(Control.MultiLine, 0, "", "Observation Tags", "observationTags",
                    $"Tag(s) associated with the observation.{Environment.NewLine}" +
                    $"Formatted as a pipe (|) separated list for multiple values, with values optionally formatted as key:value pairs.{Environment.NewLine}" +
                    "Optional." +
                    "• e.g., \"travelDirection:left\"",
                    null),

                new StandardsRow(Control.MultiLine, 0, "", "Observation Comments", "observationComments",
                    $"Comments or notes about the observation..{Environment.NewLine}" +
                    "Optional.",
                    null),
        };

        #endregion
        #endregion
    }
}