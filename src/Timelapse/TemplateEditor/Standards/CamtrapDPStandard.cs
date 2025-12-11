using System;
using System.Collections.Generic;
using Timelapse.Constant;
using Timelapse.Standards;

namespace TimelapseTemplateEditor.Standards
{
    public static class CamtrapDPStandard
    {
        // Name of this standard
        public static string Standard = Timelapse.Constant.Standards.CamtrapDPStandard;

        // The standard specification: a list of StandardsRow, common species (used by one row), and level aliases
        #region The Alias specification associated with each level
        public static Dictionary<int, string> Aliases = new()
        {
            {1, "DataPackage"},
            {2, "Deployments"},
        };
        #endregion

        #region CamtrapDP Folder Metadata as a Row List
        public static List<StandardsRow> FolderMetadataRows =
        [
            #region Level 1: DataPackage
            //
            #region Resources object (optional?)
            //
            // We supply only two instances 
            // - As there are ony two csv Resources that Timelapse generates (deployments and the combined observations/media)
            //   We do the first two as Resource1 and Resource2 representing the Deployment and combined Media/Observations name and path.
            //   The other resource values are common, so we only need one of them.
            //   We also make most of the fields invisible as the default values will likely be used.
            //   Defaults can, of course, be changed using the TemplateEditor
            //   Post-processing: convert / combine multiple Resources into json instances

            // Resource Deployment name * 
            new(
                Control.MultiLine, 1, "deployments", "Deployment name*", CamtrapDPConstants.DataPackage.Resources.Deployment_name,
                $"Deployment identifier.{Environment.NewLine}" +
                "• e.g., \"deployments\"",
                StandardsBase.CreateChoiceList(false, ["deployments", "media", "observations"]),
                false, false),
           
            // Resource - Deployment path *
            new(
                Control.MultiLine, 1, "deployments.csv", "Deployment .csv file path*", CamtrapDPConstants.DataPackage.Resources.Deployment_path,
                $"Path or URL to the deployments .csv data file.{Environment.NewLine}" +
                "• e.g., \"deployments.csv\"",
                StandardsBase.CreateChoiceList(false, ["deployments", "media", "observations"]),
                false, false),

            // Resource - Deployment schema *
            new(
                Control.Note, 1, "https://raw.githubusercontent.com/tdwg/camtrap-dp/1.0/deployments-table-schema.json", "Deployment schema*", CamtrapDPConstants.DataPackage.Resources.Deployment_schema,
                $"Schema of the Deployment resource{Environment.NewLine}" +
                "• e.g., \"https://raw.githubusercontent.com/tdwg/camtrap-dp/1.0/deployments-table-schema.json\"",
                null, false, false),


            // Resource Media name * 
            new(
                Control.MultiLine, 1, "media", "Media name*", CamtrapDPConstants.DataPackage.Resources.Media_name,
                $"Media identifier.{Environment.NewLine}" +
                "• e.g., \"media\"",
               null, false, false),
           
            // Resource - Media path *
            new(
                Control.MultiLine, 1, "media.csv", "Media .csv file path*", CamtrapDPConstants.DataPackage.Resources.Media_path,
                $"Path or URL to the Media .csv data file.{Environment.NewLine}" +
                "• e.g., \"media.csv\"",
                null, false, false),

            // Resource - Media schema *
            new(
                Control.Note, 1, "https://raw.githubusercontent.com/tdwg/camtrap-dp/1.0/media-table-schema.json", "Media schema*", CamtrapDPConstants.DataPackage.Resources.Media_schema,
                $"Schema of the Media resource{Environment.NewLine}" +
                "• e.g., \"https://raw.githubusercontent.com/tdwg/camtrap-dp/1.0/media-table-schema.json\"",
                null, false, false),

            // Resource Observations name * 
            new(
                Control.MultiLine, 1, "observations", "Observations name*", CamtrapDPConstants.DataPackage.Resources.Observations_name,
                $"Observations identifier.{Environment.NewLine}" +
                "• e.g., \"media\"",
                null, false, false),
           
            // Resource - Observations path *
            new(
                Control.MultiLine, 1, "observations.csv", "Observations .csv file path*", CamtrapDPConstants.DataPackage.Resources.Observations_path,
                $"Path or URL to the observations .csv data file.{Environment.NewLine}" +
                "• e.g., \"observations.csv\"",
                null, false, false),

            
            // Resource - Observations schema *
            new(
                Control.Note, 1, "https://raw.githubusercontent.com/tdwg/camtrap-dp/1.0/observations-table-schema.json", "Observations schema*", CamtrapDPConstants.DataPackage.Resources.Observations_schema,
                $"Schema of the Observations resource{Environment.NewLine}" +
                "• e.g., \"https://raw.githubusercontent.com/tdwg/camtrap-dp/1.0/observations-table-schema.json\"",
                null, false, false),

            //  Resources - Common profile
            new(
                Control.Note, 1, "tabular-data-resource", "Resource profile*", CamtrapDPConstants.DataPackage.Resources.Resource_profile,
                $"Profile of the resource{Environment.NewLine}" +
                "• e.g., \"tabular-data-resource\"",
                null, false, false),
            #endregion


            // Profile
            new(
                Control.Note, 1, "https://raw.githubusercontent.com/tdwg/camtrap-dp/1.0/camtrap-dp-profile.json", "CamtrapDP profile*", CamtrapDPConstants.DataPackage.Profile,
                $"The URL of the used Camtrap DP Profile version.{Environment.NewLine}" +
                "• e.g., \"https://raw.githubusercontent.com/tdwg/camtrap-dp/1.0/camtrap-dp-profile.json\"",
                null, false, false),

            #region Package
            // Name
            new(
                Control.AlphaNumeric, 1, "timelapse_to_camtrapdp_dataset", "Package: name", CamtrapDPConstants.DataPackage.Name,
                $"A short url-usable (and preferably human-readable) name of the package.{Environment.NewLine}" +
                "• e.g., \"timelapse_to_camtrapdp_dataset\"",
                null,false),

            // Id - SAULXX - this will create a common GUID for all templates, which is not what we want.
            new(
                Control.AlphaNumeric, 1, $"{Guid.NewGuid()}", "Package: Id", CamtrapDPConstants.DataPackage.IdAlias,
                $"A property reserved for globally unique identifiers, including UUIDs and DOIs.{Environment.NewLine}" +
                "• e.g., \"b03ec84-77fd-4270-813b-0c698943f7ce\"",
                null, false, false),

            // Created - We use the date/time the template .tdb file was initially created, but this can be edited
            new(
                Control.DateTime_, 1,
                $"{Timelapse.Util.DateTimeHandler.ToStringDatabaseDateTime(DateTime.Now)}", "Package: created*", CamtrapDPConstants.DataPackage.Created,
                $"The datetime on which this was created.{Environment.NewLine}" +
                "• e.g., \"2024-01-12 12:00:00\"",
                null, false),

            // Title
            new(
                Control.MultiLine, 1, "", "Package: title", CamtrapDPConstants.DataPackage.Title,
                $"A human readable title or one sentence description for this package{Environment.NewLine}" +
                "• e.g., \"Timelapse to camtrapdp: a Timelapse dataset following the camtrapDP specifications.\"",
                null, false),


            // Description
            new(
                Control.MultiLine, 1, "", "Package: description", CamtrapDPConstants.DataPackage.Description,
                $"A description of the package. The description MUST be markdown (opens new window) formatted.{Environment.NewLine}" +
                $"This also allows for simple plain text as plain text is itself valid markdown.{Environment.NewLine}" +
                $"The first paragraph (up to the first double line break) should be usable as summary information for the package{Environment.NewLine}" +
                "• e.g., \"Wolvering camera trap observations in Alberta, Canada. Part of an effort for tracking Wolverines.\"",
                null, false),

            // Version
            new(
                Control.Note, 1, "1.0.2", "Package: version", CamtrapDPConstants.DataPackage.Version,
                $"A version string identifying the version of the package.{Environment.NewLine}" +
                $"It should conform to MAJOR.MINOR.PATCH. semantics{Environment.NewLine}" +
                "• e.g., \"1.0.2\"",
                null, false, false),


            // Image
            new(
                Control.Note, 1, "", "Package: image", CamtrapDPConstants.DataPackage.Image,
                $"A URL or path to a representive image to use, for example, to show the package in a listing.{Environment.NewLine}" +
                "• e.g., \"http://wolverine_project.org/images/wolverine.jpg\"",
                null, false),

            // Homepage
            new(
                Control.Note, 1, "", "Package: home page", CamtrapDPConstants.DataPackage.Homepage,
                $"A URL for the home page on the web that is related to this data package.{Environment.NewLine}" +
                "• e.g., \"http://wolverine_project.org\"",
                null, false),

            // Keywords
            new(
                Control.MultiLine, 1, "", "Package: keywords", CamtrapDPConstants.DataPackage.Keywords,
                $"A comma-separated list of keywords to assist users searching for the package in catalogs.{Environment.NewLine}" +
                "• e.g., \"wolverine, wildlife management, conservation, population monitoring\"",
                null, false),
            #endregion

            #region Project - a singleton object
            // Project - id
            new(
                Control.Note, 1, $"{Guid.NewGuid()}", "Project: Id", CamtrapDPConstants.DataPackage.Project.Id,
                $"Unique identifier of the project.{Environment.NewLine}" +
                "• e.g., \"86cabc14-d475-4439-98a7-e7b590bed60e\"",
                null, false, false),

            // Project - title
            new(
                Control.Note, 1, "", "Project: title*", CamtrapDPConstants.DataPackage.Project.Title,
                $"Title of the project.{Environment.NewLine}" +
                "• e.g., \"Management of Invasive Coypu and muskrAt in Europe\"",
                null, false),

            
            // Project - acronym
            new(
                Control.Note, 1, "", "Project: acronym", CamtrapDPConstants.DataPackage.Project.Acronym,
                $"Acronym for the project, if any.{Environment.NewLine}" +
                "• e.g., \"MICA\"",
                null, false),

            //Project - Description
            new(
                Control.MultiLine, 1, "", "Project: description", CamtrapDPConstants.DataPackage.Project.Description,
                $"Description of the project.{Environment.NewLine}" +
                "• e.g., \"Invasive alien species such as the coypu and muskrat pose a major threat to biodiversity and cost millions of euros annually, etc.\"",
                null, false),

            //Project - Path
            new(
                Control.MultiLine, 1, "", "Project: Path", CamtrapDPConstants.DataPackage.Project.Path,
                $"Project website.{Environment.NewLine}" +
                "• e.g., \"https://timelapse.ucalgary.ca\"",
                null, false),

            //Project - Sampling Design
            new(
                Control.FixedChoice, 1, "", "Project: sampling design*", CamtrapDPConstants.DataPackage.Project.SamplingDesign,
                $"Type of a sampling design/layout. {Environment.NewLine}" +
                $"The values are based on Wearn & Glover-Kapfer (2017), pages 80-82{Environment.NewLine}" +
                "• e.g., \"simpleRandom\"",
                StandardsBase.CreateChoiceList(true, ["simpleRandom", "systematicRandom", "clusteredRandom", "experimental", "targeted", "opportunistic"]),
                    false),

            //Project - CaptureMethod
            new(
                Control.MultiChoice, 1, "", "Project: capture method*", CamtrapDPConstants.DataPackage.Project.CaptureMethod,
                $"Method(s) used to capture the media files. {Environment.NewLine}" +
                "• e.g., \"activityDetection\"",
                StandardsBase.CreateChoiceList(true, ["activityDetection", "timeLapse"]),
                    false),

            //Project - Individual Animals
            new(
                Control.Flag, 1, "", "Project: individual animals*", CamtrapDPConstants.DataPackage.Project.IndividualAnimals,
                $"true if the project includes marked or recognizable individuals.{Environment.NewLine}" +
                "• e.g., \"false\"",
                null, false),

            //Project - ObservationLevel
            new(
                Control.MultiChoice, 1, "", "Project: observation level*", CamtrapDPConstants.DataPackage.Project.ObservationLevel,
                $"Level at which observations are provided. {Environment.NewLine}" +
                "• e.g., \"media\"",
                StandardsBase.CreateChoiceList(true, ["media", "event"]),
                false),
            #endregion

            // BibliographicCitation
            new(
                Control.MultiLine, 1, "", "Bibliographic citation", CamtrapDPConstants.DataPackage.BibliographicCitation,
                $"Bibliographic/recommended citation for the package.{Environment.NewLine}" +
                $"• e.g., \"Desmet P, Neukermans A, Van der beeck D, Cartuyvels E (2022).{Environment.NewLine}" +
                $"Sample from: MICA - Muskrat and coypu camera trap observations in Belgium, the Netherlands and Germany.{Environment.NewLine}" +
                $"Version 1.0. Research Institute for Nature and Forest (INBO).{Environment.NewLine}" +
                "Dataset. https://camtrap-dp.tdwg.org/example/\"",
                null, false),

            // CoordinatePrecision
            new(
                Control.DecimalPositive, 1, "", "Coordinate precision", CamtrapDPConstants.DataPackage.CoordinatePrecision,
                $"Least precise coordinate precision of the deployments.latitude and deployments.longitude (e.g. 0.01 for coordinates with a precision of 0.01 and 0.001 degree).{Environment.NewLine}" +
                $"Especially relevant when coordinates have been rounded to protect sensitive species.{Environment.NewLine}" +
                "• e.g., \"0.01\"",
                null,
                false),
            
            // Spatial
            new(
                Control.MultiLine, 1, "", "Spatial coverage*", CamtrapDPConstants.DataPackage.Spatial,
                $"Spatial coverage of the package, expressed as GeoJSON.{Environment.NewLine}" +
                $"Timelapse will calculate this as a bounding box outlining your deployments' latitude/longitude coordinates,{Environment.NewLine}" +
                $"or will let you copy GeoJSON as outputed by a mapping package.{Environment.NewLine}" +
                "For example, Timelapse provides the option to go to https://Geojson.io , outline the spatial coverage, and then copy the GeoJson output.",
                null,
                false),

            #region Temporal OBJECT - a singleton
            // Temporal start*
            new(
                Control.Date_, 1, "", "Temporal start*", CamtrapDPConstants.DataPackage.Temporal.Start,
                $"Start date of the first deployment.{Environment.NewLine}" +
                "• e.g., \"2024-01-12\"",
                null, false),

            // Temporal end**
            new(
                Control.Date_, 1, "", "Temporal end*", CamtrapDPConstants.DataPackage.Temporal.End,
                $"End date of the last (completed) deployment.{Environment.NewLine}" +
                "• e.g., \"2024-01-12\"",
                null, false),
            #endregion

            // Contributors - a Json array that holds a list of contributor objects
            new(
                Control.MultiLine, 1, "", "Contributors*", CamtrapDPConstants.DataPackage.Contributors,
                $"a Json array that holds a list of contributors.{Environment.NewLine}" +
                $"Note: Contributors describe the people or organizations who contributed to this Data Package.{Environment.NewLine}" +
                "• e.g., \"Joe Bloggs\"",
                null, false),

            // Sources - a Json array that holds a list of contributor objects
            new(
                Control.MultiLine, 1, "", "Sources", CamtrapDPConstants.DataPackage.Sources,
                $"a Json array that holds a list of data sources to this Data Package.{Environment.NewLine}" +
                //$"Note: Contributors describe the people or organizations who contributed to this Data Package.{Environment.NewLine}" +
                "• e.g., \"World Bank and OECD\"",
                null, false),

            // Licenses - a json array that holds a list of license objects
            new(
                Control.MultiLine, 1, "", "Licenses", CamtrapDPConstants.DataPackage.Licenses,
                $"Ideally two licenses, one for the data and another for the media provided in this package. {Environment.NewLine}",
                null, false),


            // Taxonomic - a json array that holds a list of Taxonomic objects
            new(
                Control.MultiLine, 1, "[]", "Taxonomic definitions*", CamtrapDPConstants.DataPackage.Taxonomic,
                $"A list of taxonomic definitions that apply to the data and / or media provided in this package. {Environment.NewLine}",
                null, false),

            // RelatedIdentifiers - a json array that holds a list of RelatedIdentifiers objects
            new(
                Control.MultiLine, 1, "[]", "Related identifiers definitions", CamtrapDPConstants.DataPackage.RelatedIdentifiers,
                $"A list of related identifier definitions  related to the data and / or media provided in this package. {Environment.NewLine}",
                null, false),

            // References - a json array that holds a list of references (each a string)
            new(
                Control.MultiLine, 1, "[]", "References", CamtrapDPConstants.DataPackage.References,
                $"A list of references (preferably including a DOI) related to the data and / or media provided in this package. {Environment.NewLine}",
                null, false),
            #endregion

            #region Level 2: Deployment
            new(
                Control.Note, 2, "", "Deployment ID*", CamtrapDPConstants.Deployment.DeploymentID,
                $"Unique identifier of the deployment.{Environment.NewLine}" +
                $"Required.{Environment.NewLine}" +
                "• e.g., \"dep1\" or \"0f8fad5b-d9cb-469f-a165-70867728950e\"",
            null),
            new(
                Control.Note, 2, $"{Guid.NewGuid()}", "Location ID", CamtrapDPConstants.Deployment.LocationID,
                $"Identifier of the deployment location{Environment.NewLine}" +
                $"Optional.{Environment.NewLine}" +
                "• e.g., \"loc1\" or \"7c9e6679-7425-40de-944b-e07fc1f90ae7\"",
                null),
 
            new(
                Control.Note, 2, "", "Location Name", CamtrapDPConstants.Deployment.LocationName,
                $"Name given to the deployment location.{Environment.NewLine}" +
                $"Optional.{Environment.NewLine}" +
                "• e.g., \"Bialowieza MRI 01\".",
                null),
 
            new(Control.DecimalAny, 2, "", "Latitude*", CamtrapDPConstants.Deployment.Latitude,
                $"Latitude of the deployment location in decimal degrees, using the WGS84 datum.{Environment.NewLine}" +
                $"Minimum -90 to maximum 90, Required.{Environment.NewLine}" +
                $"Required.{Environment.NewLine}" +
                "• e.g., \"52.70442h\".",
                null),

            new(Control.DecimalAny, 2, "", "Longitude*", CamtrapDPConstants.Deployment.Longitude,
                $"Longitude of the deployment location in decimal degrees, using the WGS84 datum.{Environment.NewLine}" +
                $"Requried.{Environment.NewLine}" +
                "• e.g., \"23.84995\"",
                null),

            new(Control.IntegerPositive, 2, "", "Coordinate Uncertainty*", CamtrapDPConstants.Deployment.CoordinateUncertainty,
                $"Horizontal distance from the given latitude and longitude describing the smallest circle containing the deployment location. {Environment.NewLine}" +
                $"Especially relevant when coordinates are rounded to protect sensitive species.{Environment.NewLine}" +
                $"Minimum 1. Expressed in meters.{Environment.NewLine}" +
                $"Optional.{Environment.NewLine}" +
                " • e.g., \"100\"",
                null),

            new(Control.DateTime_, 2, "", "Deployment Start*", CamtrapDPConstants.Deployment.DeploymentStart,
                $"Date_ and time at which the deployment was started.{Environment.NewLine}" +
                $"Requried.{Environment.NewLine}" +
                "• e.g., \"2020-03-01T22:00:00Z\"",
                null),

            new(Control.DateTime_, 2, "", "Deployment End*", CamtrapDPConstants.Deployment.DeploymentEnd,
                $"Date_ and time at which the deployment was ended.{Environment.NewLine}" +
                $"Requried.{Environment.NewLine}" +
                "• e.g., \"2020-04-01T22:00:00Z\"",
                null),

            new(
                Control.MultiLine, 2, "", "Setup By", CamtrapDPConstants.Deployment.SetupBy,
                $"Name or identifier of the person or organization that deployed the camera.{Environment.NewLine}" +
                $"Optional.{Environment.NewLine}" +
                "• e.g., \"Jakub Bubnicki\".",
                null),

            new(
                Control.Note, 2, "", "Camera ID", CamtrapDPConstants.Deployment.CameraID,
                $"Identifier of the camera used for the deployment (e.g. the camera device serial number).{Environment.NewLine}" +
                $"Optional.{Environment.NewLine}" +
                "• e.g., \"P800HG08192031\"",
                null),

            new(
                Control.AlphaNumeric, 2, "", "Camera Model", CamtrapDPConstants.Deployment.CameraModel,
                $"Manufacturer and model of the camera {Environment.NewLine}" +
                $"Formatted as manufacturer-model. Optional.{Environment.NewLine}" +
                "• e.g., \"Reconyx-PC800\"",
                null),

            new(
                Control.IntegerPositive, 2, "", "Camera Delay", CamtrapDPConstants.Deployment.CameraDelay,
                $"Predefined duration after detection when further activity is ignored. {Environment.NewLine}" +
                $"Minimum 0. Expressed in seconds. Optional.{Environment.NewLine}" +
                "• e.g., \"120\"",
                null),

            new(
                Control.DecimalPositive, 2, "", "Camera Height", CamtrapDPConstants.Deployment.CameraHeight,
                $"Height at which the camera was deployed.{Environment.NewLine}" +
                $"Minimum 0. Expressed in meters. Not to be combined with cameraHeight. Optional.{Environment.NewLine}" +
                "• e.g., \"1.2\"",
                null),

            new(
                Control.DecimalPositive, 2, "", "Camera Depth", CamtrapDPConstants.Deployment.CameraDepth,
                $"Depth at which the camera was deployed.{Environment.NewLine}" +
                $"Minimum 0. Expressed in meters. Optional.{Environment.NewLine}" +
                "• e.g., \"4.8\"",
                null),

            new(
                Control.IntegerAny, 2, "", "Camera Tilt", CamtrapDPConstants.Deployment.CameraTilt,
                $"Angle at which the camera was deployed in the vertical plane.{Environment.NewLine}" +
                $"Minimum -90, Maximum 90. Expressed in degrees, with -90 facing down, 0 horizontal and 90 facing up. Optional.{Environment.NewLine}" +
                "• e.g., \"-90\"",
                null),

            new(
                Control.IntegerPositive, 2, "", "Camera Heading", CamtrapDPConstants.Deployment.CameraHeading,
                $"Angle at which the camera was deployed in the horizontal plane.{Environment.NewLine}" +
                $"Minimum 0, Maximum 360. Expressed in decimal degrees clockwise from north, with values ranging from 0 to 360: 0 = north, 90 = east, 180 = south, 270 = west. Optional.{Environment.NewLine}" +
                "• e.g., \"225\"",
                null),

            new(
                Control.DecimalPositive, 2, "", "Detection Distance", CamtrapDPConstants.Deployment.DetectionDistance,
                $"Maximum distance at which the camera can reliably detect activity. Typically measured by having a human move in front of the camera.{Environment.NewLine}" +
                $"Expressed in meters. Optional.{Environment.NewLine}" +
                "• e.g., \"9.5\"",
                null),

            new(
                Control.Flag, 2, "", "Timestamp Issues", CamtrapDPConstants.Deployment.TimestampIssues,
                $"true if timestamps in the media resource for the deployment are known to have (unsolvable) issues (e.g. unknown timezone, am/pm switch).{Environment.NewLine}" +
                $"Optional.{Environment.NewLine}" +
                "• e.g., \"true\"",
                null),

            new(
                Control.Flag, 2, "", "Bait Use", CamtrapDPConstants.Deployment.BaitUse,
                $"true if bait was used for the deployment. More information can be provided in tags or comments.{Environment.NewLine}" +
                $"Optional.{Environment.NewLine}" +
                "• e.g., \"true\"",
                null
                ),

            new(
                Control.FixedChoice, 2, "", "Feature Type", CamtrapDPConstants.Deployment.FeatureType,
                $"Type of the feature (if any) associated with the deployment.{Environment.NewLine}" +
                $"Optional.{Environment.NewLine}" +
                "• e.g., \"culvert\"",
                StandardsBase.CreateChoiceList(true,
                [
                    "roadPaved", "roadDirt", "trailHiking", "trailGame", "roadUnderpass", "roadOverpass", "roadBridge", "culvert", "burrow", "nestSite", "carcass", "waterSource",
                    "fruitingTree"
                ])
            ),

            new(
                Control.MultiLine, 2, "", "Habitat", CamtrapDPConstants.Deployment.Habitat,
                $"Short characterization of the habitat at the deployment location.{Environment.NewLine}" +
                $"Optional.{Environment.NewLine}" +
                "• e.g., \"Mixed temperate low-land forest\"",
                null
            ),

            new(
                Control.MultiLine, 2, "", "Deployment Groups", CamtrapDPConstants.Deployment.DeploymentGroups,
                $"Deployment group(s) associated with the deployment. Deployment groups can have a spatial (arrays, grids, clusters), temporal (sessions, seasons, months, years) or other context.{Environment.NewLine}" +
                $"Formatted as a pipe (|) separated list for multiple values, with values preferably formatted as key:value pairs.{Environment.NewLine}" +
                $"Optional.{Environment.NewLine}" +
                "• e.g., \"season:winter 2020 | grid:A1\"",
                null
                ),

            new(
                Control.MultiLine, 2, "", "Deployment Tags", CamtrapDPConstants.Deployment.DeploymentTags,
                $"Tag(s) associated with the deployment.{Environment.NewLine}" +
                $"Formatted as a pipe (|) separated list for multiple values, with values optionally formatted as key:value pairs.{Environment.NewLine}" +
                $"Optional.{Environment.NewLine}" +
                "• e.g., \"forest edge | bait:food\"",
                null
            ),

            new(
                Control.MultiLine, 2, "", "Deployment Comments", CamtrapDPConstants.Deployment.DeploymentComments,
                $"Comments or notes about the deployment.{Environment.NewLine}" +
                $"Optional.{Environment.NewLine}" +
                "",
                null
            ),
        ];
        #endregion

        #region Level 0: Media and Observations

        public static List<StandardsRow> ImageTemplateRows =
        [
            new(Control.Note, 0, "", "Media Id", CamtrapDPConstants.Media.MediaID,
                $"Unique identifier of the media file.{Environment.NewLine}" +
                $"Required.{Environment.NewLine}" +
                "• e.g., \"m1\"",
                null, false, false),

            // deploymentID 

            new(Control.Note, 0, "", "Deployment Id", CamtrapDPConstants.Media.DeploymentID,
                $"Identifier of the deployment the media file belongs to. {Environment.NewLine}" +
                $"Required.{Environment.NewLine}" +
                "• e.g., \"dep1\"",
                null, false, false),


            new(Control.FixedChoice, 0, "", "Capture method", CamtrapDPConstants.Media.CaptureMethod,
                $"Method used to capture the media file.{Environment.NewLine}" +
                "• e.g., \"activityDetection\"" +
                $"Optional.{Environment.NewLine}",
                StandardsBase.CreateChoiceList(true, ["activityDetection", "timelapse"])),



            new(Control.DateTime_, 0, "", "Timestamp*", CamtrapDPConstants.Media.Timestamp,
                $"Date_ and time at which the media file was recorded. {Environment.NewLine}" +
                $"Required. Format as YYYY-MM-DDThh:mm:ssZ or YYYY-MM-DDThh:mm:ss±hh:mm{Environment.NewLine}" +
                "• e.g., \"2020-03-24T11:21:46Z\"",
                null, false, false),


            new(Control.Note, 0, "", "File path*", CamtrapDPConstants.Media.FilePath,
                $"URL or relative path to the media file, respectively for externally hosted files or files that are part of the package.{Environment.NewLine}" +
                $"Required.{Environment.NewLine}" +
                "• e.g., \"https://multimedia.agouti.eu/assets/6d65f3e4-4770-407b-b2bf-878983bf9872/file\"",
                null, false, false),


            new(Control.Flag, 0, "true", "File public*", CamtrapDPConstants.Media.FilePublic,
                $"false if the media file should not be publicly accessible (e.g. to protect the privacy of people captured in the media).{Environment.NewLine}" +
                $"Required.{Environment.NewLine}" +
                "• e.g., \"true\"",
                null, false),


            new(Control.Note, 0, "", "File name", CamtrapDPConstants.Media.FileName,
                "Name of the media file. " +
                $"If provided, one should be able to sort media files chronologically within a deployment on timestamp (first) and fileName (second).{Environment.NewLine}" +
                $"Optional.{Environment.NewLine}" +
                "• e.g., \"IMG0001.jpg\"",
                null, false),


            new(Control.FixedChoice, 0, "", "File mediatype*", CamtrapDPConstants.Media.FileMediatype,
                $"Mediatype of the media file. Expressed as an IANA Media Type.{Environment.NewLine}" +
                $"Pattern: ^(image|video|audio)/.*$ Required.{Environment.NewLine}" +
                "• e.g., \"image\"",
                StandardsBase.CreateChoiceList(true, ["image", "video"]), false, false), // while audio should be incuded, Timelapse doesn't have audio files


            new(Control.MultiLine, 0, "", "Exif data", CamtrapDPConstants.Media.ExifData,
                "EXIF data of the media file." +
                $"Formatted as a valid JSON object. Optional.{Environment.NewLine}" +
                "• e.g., \"{\"EXIF\":{\"ISO\":200,\"Make\":\"RECONYX\"}}\"",
                null, false),


            new(Control.Flag, 0, "false", "Favorite", CamtrapDPConstants.Media.Favorite,
                $"true if the media file is deemed of interest (e.g. an exemplar image of an individual).{Environment.NewLine}" +
                $"Optional.{Environment.NewLine}" +
                "• e.g., \"true\"",
                null),


            new(Control.MultiLine, 0, "", "Media comments", CamtrapDPConstants.Media.MediaComments,
                $"Comments or notes about the media file.{Environment.NewLine}" +
                $"Optional.{Environment.NewLine}" +
                "• e.g., \"corrupted file\"",
                null),

            //
            // OBSERVATIONS SECTION
            //

            new(Control.Note, 0, "", "Observation Id*", CamtrapDPConstants.Observations.ObservationID,
                $"Unique identifier of the observation.{Environment.NewLine}" +
                $"Required.{Environment.NewLine}" +
                "• e.g., \"obs1\"",
                null),


            new(Control.Note, 0, "", "Event Id", CamtrapDPConstants.Observations.EventID,
                $"Identifier of the event the observation belongs to.{Environment.NewLine}" +
                $"Facilitates linking event-based and media-based observations with a permanent identifier.{Environment.NewLine}" +
                $"Optional.{Environment.NewLine}" +
                "• e.g., \"sequence1\"",
                null),


            new(Control.DateTime_, 0, "", "Event start*", CamtrapDPConstants.Observations.EventStart,
                $"Date and time at which the event started. .{Environment.NewLine}" +
                $"Optional.{Environment.NewLine}" +
                "• e.g., \"2023-11-31 13:01:05\"",
                null),


            new(Control.DateTime_, 0, "", "Event end*", CamtrapDPConstants.Observations.EventEnd,
                $"Date and time at which the event ended. .{Environment.NewLine}" +
                $"Optional.{Environment.NewLine}" +
                $"Note: Timelapse currently sets it the same as EventStart.{Environment.NewLine}" +
                "• e.g., \"2023-11-31 13:01:05\"",
                null),


            new(Control.FixedChoice, 0, "", "Observation level*", CamtrapDPConstants.Observations.ObservationLevel,
                $"Level at which the observation was classified.{Environment.NewLine}" +
                $"• media for media-based observations that are directly associated with a media file (mediaID).{Environment.NewLine}" +
                $"  These are especially useful for machine learning and don’t need to be mutually exclusive (e.g. multiple classifications are allowed).{Environment.NewLine}" +
                $"• event for event-based observations that consider an event (comprising a collection of media files).{Environment.NewLine}" +
                $"  These are especially useful for ecological research and should be mutually exclusive, so that their count can be summed.{Environment.NewLine}" +
                $"• Facilitates linking event-based and media-based observations with a permanent identifier.{Environment.NewLine}" +
                $"Required.{Environment.NewLine}" +
                "• e.g., \"event\"",
                StandardsBase.CreateChoiceList(true, ["media", "event"])),


            new(Control.FixedChoice, 0, "unclassified", "Observation type*", CamtrapDPConstants.Observations.ObservationType,
                $"Type of the observation.{Environment.NewLine}" +
                $"All categories in this vocabulary have to be understandable from an AI point of view.{Environment.NewLine}" +
                $"unknown describes classifications with a classificationProbability below some predefined threshold{Environment.NewLine}" +
                $"i.e. neither humans nor AI can say what was recorded.{Environment.NewLine}" +
                $"Required.{Environment.NewLine}" +
                "• e.g., \"animal\"",
                StandardsBase.CreateChoiceList(false, ["animal", "human", "vehicle", "blank", "unknown", "unclassified"])),


            new(Control.FixedChoice, 0, "", "Camera setup type", CamtrapDPConstants.Observations.CameraSetupType,
                $"Type of the camera setup action (if any) associated with the observation.{Environment.NewLine}" +
                $"Optional.{Environment.NewLine}" +
                "• e.g., \"calibration\"",
                StandardsBase.CreateChoiceList(true, ["setup", "calibration"])),


            new(Control.Note, 0, "", "Scientific name", CamtrapDPConstants.Observations.ScientificName,
                $"Scientific name of the observed individual(s).{Environment.NewLine}" +
                $"Optional.{Environment.NewLine}" +
                "• e.g., \"Canis lupus\"",
                null),


            new(Control.Counter, 0, "", "Count", CamtrapDPConstants.Observations.Count,
                $"Number of observed individuals (optionally of given life stage, sex and behavior).{Environment.NewLine}" +
                $"Minimum 1. Optional.{Environment.NewLine}" +
                "• e.g., \"5\"",
                null),


            new(Control.FixedChoice, 0, "", "Life stage", CamtrapDPConstants.Observations.LifeStage,
                $"Age class or life stage of the observed individual(s).{Environment.NewLine}" +
                $"Optional.{Environment.NewLine}" +
                "• e.g., \"adult\"",
                StandardsBase.CreateChoiceList(true, ["adult", "subadult", "juvenile"])),


            new(Control.FixedChoice, 0, "", "Sex", CamtrapDPConstants.Observations.Sex,
                $"Sex of the observed individual(s).{Environment.NewLine}" +
                $"Optional.{Environment.NewLine}" +
                "• e.g., \"female\"",
                StandardsBase.CreateChoiceList(true, ["female", "male"])),


            new(Control.Note, 0, "", "Behavior", CamtrapDPConstants.Observations.Behavior,
                $"Dominant behavior of the observed individual(s), preferably expressed as controlled values.{Environment.NewLine}" +
                $"(e.g. grazing, browsing, rooting, vigilance, running, walking).{Environment.NewLine}" +
                $"Formatted as a pipe (|) separated list for multiple values, with the dominant behavior listed first.{Environment.NewLine}" +
                $"Optional.{Environment.NewLine}" +
                "• e.g., \"vigilance\"",
                null),


            new(Control.Note, 0, "", "Individual Id", CamtrapDPConstants.Observations.IndividualID,
                $"Identifier of the observed individual.{Environment.NewLine}" +
                $"Optional.{Environment.NewLine}" +
                "• e.g., \"RD213\"",
                null),


            new(Control.DecimalAny, 0, "", "Individual position radius", CamtrapDPConstants.Observations.IndividualPositionRadius,
                $"Distance from the camera to the observed individual identified by individualID.{Environment.NewLine} " +
                $"Required for distance analyses (e.g. Howe et al. 2017) and random encounter modelling (e.g. Rowcliffe et al. 2011).{Environment.NewLine}" +
                $"Expressed in meters. Optional.{Environment.NewLine}" +
                "• e.g., \"-6.81\"",
                null),


            new(Control.DecimalAny, 0, "", "Individual position angle", CamtrapDPConstants.Observations.IndividualPositionAngle,
                $"Angular distance from the camera view centerline to the observed individual identified by individualID.{Environment.NewLine}" +
                $"Required for distance analyses (e.g. Howe et al. 2017) and random encounter modelling (e.g. Rowcliffe et al. 2011).{Environment.NewLine}" +
                $"Expressed in degrees, with negative values left, 0 straight ahead and positive values right.{Environment.NewLine}" +
                $"Minimum: -90, Maximum: 90. Optional.{Environment.NewLine}" +
                "• e.g., \"-8.56\"",
                null),


            new(Control.DecimalPositive, 0, "", "Individual position speed", CamtrapDPConstants.Observations.IndividualSpeed,
                $"Average movement speed of the observed individual identified by individualID.{Environment.NewLine}" +
                $"Required for random encounter modelling (e.g. Rowcliffe et al. 2016).{Environment.NewLine}" +
                $"Expressed in degrees, with negative values left, 0 straight ahead and positive values right.{Environment.NewLine}" +
                $"Expressed in meters per second.  Optional.{Environment.NewLine}" +
                "• e.g., \"1.75\"",
                null),


            new(Control.DecimalAny, 0, "", "Bbox X", CamtrapDPConstants.Observations.BboxX,
                $"Horizontal position of the top-left corner of a bounding box that encompasses the observed individual(s) in the media file identified by mediaID.{Environment.NewLine}" +
                $"Or the horizontal position of an object in that media file. {Environment.NewLine}" +
                $"Measured from the left and relative to media file width.{Environment.NewLine}" +
                "Minimum: 0, maximum: 1. Optional." +
                $"Note: Timelapse does not currently let users create bounding boxes.{Environment.NewLine}" +
                "• e.g., \".2\"",
                null, false, false),


            new(Control.DecimalAny, 0, "", "Bbox Y", CamtrapDPConstants.Observations.BboxY,
                $"Vertical position of the top-left corner of a bounding box that encompasses the observed individual(s) in the media file identified by mediaID.{Environment.NewLine}" +
                $"Or the vertical position of an object in that media file.{Environment.NewLine}" +
                $"Measured from the top and relative to media file width.{Environment.NewLine}" +
                "Minimum: 0, maximum: 1. Optional." +
                $"Note: Timelapse does not currently let users create bounding boxes.{Environment.NewLine}" +
                "• e.g., \".25\"",
                null, false, false),


            new(Control.DecimalAny, 0, "", "Bbox width", CamtrapDPConstants.Observations.BboxWidth,
                $"Width of a bounding box that encompasses the observed individual(s) in the media file identified by mediaID.{Environment.NewLine}" +
                $"Measured from the left of the bounding box and relative to the media file width.{Environment.NewLine}" +
                "Minimum: 1e-15, maximum: 1. Optional." +
                $"Note: Timelapse does not currently let users create bounding boxes.{Environment.NewLine}" +
                "• e.g., \"0.4\"",
                null, false, false),


            new(Control.DecimalAny, 0, "", "Bbox height", CamtrapDPConstants.Observations.BboxHeight,
                $"Height of a bounding box that encompasses the observed individual(s) in the media file identified by mediaID.{Environment.NewLine}" +
                $"Measured from the top of the bounding box and relative to the media file height.{Environment.NewLine}" +
                "Minimum: 1e-15, maximum: 1. Optional." +
                "• e.g., \"0.5\"",
                $"Note: Timelapse does not currently let users create bounding boxes.{Environment.NewLine}" +
                null, false, false),


            new(Control.FixedChoice, 0, "", "Classification method", CamtrapDPConstants.Observations.ClassificationMethod,
                $"Method (most recently) used to classify the observation.{Environment.NewLine}" +
                "Optional." +
                "• e.g., \"human\"",
                StandardsBase.CreateChoiceList(true, ["human", "machine"])),


            new(Control.Note, 0, "", "Classified by", CamtrapDPConstants.Observations.ClassifiedBy,
                $"Name or identifier of the person or AI algorithm that (most recently) classified the observation.{Environment.NewLine}" +
                "Optional." +
                "• e.g., \"MegaDetector V5\"",
                null),


            new(Control.DateTime_, 0, "", "Classification timestamp", CamtrapDPConstants.Observations.ClassificationTimestamp,
                $"Date_ and time of the (most recent) classification.{Environment.NewLine}" +
                "Optional." +
                "• e.g., \"2020-08-22 10:25:19\"",
                null, false, false),


            new(Control.IntegerPositive, 0, "", "Classification probability", CamtrapDPConstants.Observations.ClassificationProbability,
                $"Degree of certainty of the (most recent) classification.{Environment.NewLine}" +
                $"Expressed as a probability, with 1 being maximum certainty.{Environment.NewLine}" +
                $"Omit or provide an approximate probability for human classifications.{Environment.NewLine}" +
                "Minimum: 0, maximum: 1. Optional." +
                "• e.g., \"0.95\"",
                null, false, false),


            new(Control.MultiLine, 0, "", "Observation tags", CamtrapDPConstants.Observations.ObservationTags,
                $"Tag(s) associated with the observation.{Environment.NewLine}" +
                $"Formatted as a pipe (|) separated list for multiple values, with values optionally formatted as key:value pairs.{Environment.NewLine}" +
                "Optional." +
                "• e.g., \"travelDirection:left\"",
                null),


            new(Control.MultiLine, 0, "", "Observation comments", CamtrapDPConstants.Observations.ObservationComments,
                $"Comments or notes about the observation..{Environment.NewLine}" +
                "Optional.",
                null)

        ];

        #endregion

        #endregion

        //Not needed, as we have this done explicitly in  CamtrapDPHelpers
        //#region Generate all static member values of CamtrapDPConstants.Observations and CamtrapDPConstants.Media
        //// Property holding all static member values of CamtrapDPConstants.Observations and CamtrapDPConstants.Media (but only does this once)
        //// Used by the template editor to identify and disable data label fields that match the CamtrapDP Observations and Media data labels
        //private static List<string> observationValues = null;
        //public static List<string> MediaAndObservationValues => observationValues ?? (observationValues = GetMediaAndObservationValues());

        //// Actually gets all static member values of CamtrapDPConstants.Observations and CamtrapDPConstants.Media
        //private static List<string> GetMediaAndObservationValues()
        //{
        //    List<string> values = new List<string>();
        //    Type myType = typeof(CamtrapDPConstants.Observations);

        //    // Add all static member values of CamtrapDPConstants.Observations
        //    FieldInfo[] staticFields = myType.GetFields(BindingFlags.Static | BindingFlags.Public);
        //    foreach (FieldInfo field in staticFields)
        //    {
        //        object value = field.GetValue(null);
        //        values.Add(value.ToString());
        //    }

        //    // Add all static member values of CamtrapDPConstants.Media
        //    myType = typeof(CamtrapDPConstants.Media);
        //    staticFields = myType.GetFields(BindingFlags.Static | BindingFlags.Public);
        //    foreach (FieldInfo field in staticFields)
        //    {
        //        object value = field.GetValue(null);
        //        values.Add(value.ToString());
        //    }
        //    return values;
        //}
        //#endregion
    }
}