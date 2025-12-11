namespace Timelapse.Standards
{
    public class CamtrapDPConstants
    {
        // The names of the camtrapDP levels used by Timelapse
        public class ResourceLevels
        {
            // ReSharper disable once MemberHidesStaticFromOuterClass
            public const string DataPackage = "DataPackage";
            public const string Deployments = "Deployments";
            // ReSharper disable once MemberHidesStaticFromOuterClass
            public const string Media = "Media";
            // ReSharper disable once UnusedMember.Global
            // ReSharper disable once MemberHidesStaticFromOuterClass
            public const string Observations = "Observations";
        }

        public class DateTimeFormats
        {
            public static readonly string[] TimelapseFullDateTimeFormat =
            [
                "dd-MMM-yyyy HH:mm:ss",
                "yyyy-MM-dd HH:mm:ss"
            ];
            public static readonly string[] TimelapseDateOnlyFormat =
            [
                "dd-MMM-yyyy",
                "yyyy-MM-dd"
            ];
            public const string CamtrapDateTimeFormat = "yyyy-MM-ddTHH:mm:ssZ";
            public const string CamtrapDateOnlyFormat = "yyyy-MM-dd";
        }

        public class Deployment
        {
            public const string DeploymentID = "deploymentID";  // same as Media.DemploymentID and Observations.DeploymentID
            public const string LocationID = "locationID";
            public const string LocationName = "locationName";
            public const string Latitude = "latitude";
            public const string Longitude = "longitude";
            public const string CoordinateUncertainty = "coordinateUncertainty";
            public const string DeploymentStart = "deploymentStart";
            public const string DeploymentEnd = "deploymentEnd";
            public const string SetupBy = "setupBy";
            public const string CameraID = "cameraID";
            public const string CameraModel = "cameraModel";
            public const string CameraDelay = "cameraDelay";
            public const string CameraHeight = "cameraHeight";
            public const string CameraDepth = "cameraDepth";
            public const string CameraTilt = "cameraTilt";
            public const string CameraHeading = "cameraHeading";
            public const string DetectionDistance = "detectionDistance";
            public const string TimestampIssues = "timestampIssues";
            public const string BaitUse = "baitUse";
            public const string FeatureType = "featureType";
            public const string Habitat = "habitat";
            public const string DeploymentGroups = "deploymentGroups";
            public const string DeploymentTags = "deploymentTags";
            public const string DeploymentComments = "deploymentComments";
        }

        public class Media
        {
            public const string MediaID = "mediaID";             // same as Observations.MediaID
            public const string DeploymentID = "deploymentID";   // same as Deployment.DemploymentID
            public const string CaptureMethod = "captureMethod";
            public const string Timestamp = "timestamp";
            public const string FilePath = "filePath";
            public const string FilePublic = "filePublic";
            public const string FileName = "fileName";
            public const string FileMediatype = "fileMediatype";
            public const string ExifData = "exifData";
            public const string Favorite = "favorite";
            public const string MediaComments = "mediaComments";
        }

        public class Observations
        {
            public const string ObservationID = "observationID";
            public const string DeploymentID = "deploymentID"; // same as Deployment.DemploymentID
            public const string MediaID = "mediaID";           // same as Media.MediaID
            public const string EventID = "eventID";
            public const string EventStart = "eventStart";
            public const string EventEnd = "eventEnd";
            public const string ObservationLevel = "observationLevel";
            public const string ObservationType = "observationType";
            public const string CameraSetupType = "cameraSetupType";
            public const string ScientificName = "scientificName";
            public const string Count = "count";
            public const string LifeStage = "lifeStage";
            public const string Sex = "sex";
            public const string Behavior = "behavior";
            public const string IndividualID = "individualID";
            public const string IndividualPositionRadius = "individualPositionRadius";
            public const string IndividualPositionAngle = "individualPositionAngle";
            public const string IndividualSpeed = "individualSpeed";
            public const string BboxX = "bboxX";
            public const string BboxY = "bboxY";
            public const string BboxWidth = "bboxWidth";
            public const string BboxHeight = "bboxHeight";
            public const string ClassificationMethod = "classificationMethod";
            public const string ClassifiedBy = "classifiedBy";
            public const string ClassificationTimestamp = "classificationTimestamp";
            public const string ClassificationProbability = "classificationProbability";
            public const string ObservationTags = "observationTags";
            public const string ObservationComments = "observationComments";
        }

        public class DataPackage
        {
            public static class Resources
            {
                public const string Deployment_name = "resource_deployment_name";
                public const string Deployment_path = "resource_deployment_path";
                public const string Deployment_schema = "resource_deployment_schema";
                public const string Media_name = "resource_media_name";
                public const string Media_path = "resource_media_path";
                public const string Media_schema = "resource_media_schema";
                public const string Observations_name = "resource_observations_name";
                public const string Observations_path = "resource_observations_path";
                public const string Observations_schema = "resource_observations_schema";
                public const string Resource_profile = "resource_common_profile";
            }

            public const string Profile = "profile";
            public const string Name = "name";
            public const string IdAlias = "guid_id"; // we can't use id as a datacolumn in SQLite as its already been used, so we use an alias instead.
            public const string Created = "created";
            public const string Title = "title";
            public const string Contributors = "contributors";
            public const string Description = "description";
            public const string Version = "version";
            public const string Keywords = "keywords";
            public const string Image = "image";
            public const string Homepage = "homepage";

            public const string Sources = "sources";

            public const string Licenses = "licenses";

            public const string BibliographicCitation = "bibliographicCitation";

            public static class Project
            {
                public const string Id = "project_id";
                // ReSharper disable once MemberHidesStaticFromOuterClass
                public const string Title = "project_title";
                public const string Acronym = "project_acronym";
                // ReSharper disable once MemberHidesStaticFromOuterClass
                public const string Description = "project_description";
                public const string SamplingDesign = "project_samplingDesign";
                public const string Path = "project_path";
                public const string CaptureMethod = "captureMethod";
                public const string IndividualAnimals = "individualAnimals";
                public const string ObservationLevel = "observationLevel";
            }

            public const string CoordinatePrecision = "coordinatePrecision";
            public const string Spatial = "spatial";

            public static class Temporal
            {
                public const string Start = "start";
                public const string End = "end";
            }

            public const string Taxonomic = "taxonomic";

            public const string RelatedIdentifiers = "relatedIdentifiers";

            public const string References = "references_";
        }
    }
}
