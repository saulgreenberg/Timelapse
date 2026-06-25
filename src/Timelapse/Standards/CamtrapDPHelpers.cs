using System;
using System.Collections.Generic;
using Timelapse.Database;
using Timelapse.DataTables;

namespace Timelapse.Standards
{
    public static class CamtrapDPHelpers
    {
        // true if the field is a Media or Observations field
        public static bool IsMediaOrObservationField(string fieldName)
        {
            return IsMediaField(fieldName) || IsObservationsField(fieldName);
        }

        public static bool IsDataPackageDeploymentField(string fieldName)
        {
            return IsDataPackageField(fieldName) || IsDeploymentField(fieldName);
        }

        public static bool IsMediaField(string fieldName)
        {
            return fieldName switch
            {
                CamtrapDPConstants.Media.MediaID or 
                    CamtrapDPConstants.Media.DeploymentID or 
                    CamtrapDPConstants.Media.CaptureMethod or 
                    CamtrapDPConstants.Media.Timestamp or 
                    CamtrapDPConstants.Media.FilePath or 
                    CamtrapDPConstants.Media.FilePublic or 
                    CamtrapDPConstants.Media.FileName or 
                    CamtrapDPConstants.Media.FileMediatype or 
                    CamtrapDPConstants.Media.ExifData or 
                    CamtrapDPConstants.Media.Favorite or 
                    CamtrapDPConstants.Media.MediaComments => true,
                _ => false,
            };
        }

        // true if the Observations's datalabel should not be editable in the template
        public static bool IsObservationsField(string fieldName)
        {
            return fieldName switch
            {
                CamtrapDPConstants.Observations.ObservationID or 
                    CamtrapDPConstants.Observations.DeploymentID or 
                    CamtrapDPConstants.Observations.MediaID or 
                    CamtrapDPConstants.Observations.EventID or 
                    CamtrapDPConstants.Observations.EventStart or 
                    CamtrapDPConstants.Observations.EventEnd or 
                    CamtrapDPConstants.Observations.ObservationLevel or 
                    CamtrapDPConstants.Observations.ObservationType or 
                    CamtrapDPConstants.Observations.CameraSetupType or 
                    CamtrapDPConstants.Observations.ScientificName or 
                    CamtrapDPConstants.Observations.Count or 
                    CamtrapDPConstants.Observations.LifeStage or 
                    CamtrapDPConstants.Observations.Sex or 
                    CamtrapDPConstants.Observations.Behavior or 
                    CamtrapDPConstants.Observations.IndividualID or 
                    CamtrapDPConstants.Observations.IndividualPositionRadius or 
                    CamtrapDPConstants.Observations.IndividualPositionAngle or 
                    CamtrapDPConstants.Observations.IndividualSpeed or 
                    CamtrapDPConstants.Observations.BboxX or 
                    CamtrapDPConstants.Observations.BboxY or 
                    CamtrapDPConstants.Observations.BboxWidth or 
                    CamtrapDPConstants.Observations.BboxHeight or 
                    CamtrapDPConstants.Observations.ClassificationMethod or 
                    CamtrapDPConstants.Observations.ClassificationTimestamp or 
                    CamtrapDPConstants.Observations.ClassificationProbability or 
                    CamtrapDPConstants.Observations.ClassifiedBy or 
                    CamtrapDPConstants.Observations.ObservationTags or 
                    CamtrapDPConstants.Observations.ObservationComments => true,
                _ => false,
            };
        }
        public static bool IsMediaObservationsFieldNonEditableDefault(string dataLabel)
        {
            return dataLabel switch
            {
                CamtrapDPConstants.Media.DeploymentID or 
                    CamtrapDPConstants.Media.MediaID or 
                    CamtrapDPConstants.Media.Timestamp or 
                    CamtrapDPConstants.Media.FilePath or 
                    CamtrapDPConstants.Media.FileName or 
                    CamtrapDPConstants.Media.FileMediatype => true,
                _ => false,
            };
        }

        // true if its a  data package's datalabel 
        public static bool IsDataPackageField(string dataLabel)
        {
            return dataLabel switch
            {
                CamtrapDPConstants.DataPackage.Resources.Deployment_name 
                    or CamtrapDPConstants.DataPackage.Resources.Deployment_path
                    or CamtrapDPConstants.DataPackage.Resources.Deployment_schema 
                    or CamtrapDPConstants.DataPackage.Resources.Media_name
                    or CamtrapDPConstants.DataPackage.Resources.Media_path 
                    or CamtrapDPConstants.DataPackage.Resources.Media_schema
                    or CamtrapDPConstants.DataPackage.Resources.Observations_name 
                    or CamtrapDPConstants.DataPackage.Resources.Observations_path
                    or CamtrapDPConstants.DataPackage.Resources.Observations_schema 
                    or CamtrapDPConstants.DataPackage.Resources.Resource_profile
                    or CamtrapDPConstants.DataPackage.Project.Acronym 
                    or CamtrapDPConstants.DataPackage.Project.CaptureMethod 
                    or CamtrapDPConstants.DataPackage.Project.Description
                    or CamtrapDPConstants.DataPackage.Project.Id 
                    or CamtrapDPConstants.DataPackage.Project.IndividualAnimals
                    or CamtrapDPConstants.DataPackage.Project.ObservationLevel 
                    or CamtrapDPConstants.DataPackage.Project.Path
                    or CamtrapDPConstants.DataPackage.Project.SamplingDesign 
                    or CamtrapDPConstants.DataPackage.Project.Title 
                    or CamtrapDPConstants.DataPackage.Temporal.End
                    or CamtrapDPConstants.DataPackage.Temporal.Start 
                    or CamtrapDPConstants.DataPackage.BibliographicCitation 
                    or CamtrapDPConstants.DataPackage.Contributors
                    or CamtrapDPConstants.DataPackage.CoordinatePrecision 
                    or CamtrapDPConstants.DataPackage.Created 
                    or CamtrapDPConstants.DataPackage.Description
                    or CamtrapDPConstants.DataPackage.Homepage 
                    or CamtrapDPConstants.DataPackage.IdAlias 
                    or CamtrapDPConstants.DataPackage.Image
                    or CamtrapDPConstants.DataPackage.Keywords 
                    or CamtrapDPConstants.DataPackage.Licenses 
                    or CamtrapDPConstants.DataPackage.Name
                    or CamtrapDPConstants.DataPackage.Profile 
                    or CamtrapDPConstants.DataPackage.References 
                    or CamtrapDPConstants.DataPackage.RelatedIdentifiers
                    or CamtrapDPConstants.DataPackage.Sources 
                    or CamtrapDPConstants.DataPackage.Spatial or CamtrapDPConstants.DataPackage.Taxonomic
                    or CamtrapDPConstants.DataPackage.Title 
                    or CamtrapDPConstants.DataPackage.Version => true,
                _ => false
            };
        }

        // true if the data package's Default value should not be editable in the template
        public static bool IsDataPackageFieldNonEditableDefault(string dataLabel)
        {
            return dataLabel switch
            {
                CamtrapDPConstants.DataPackage.Resources.Deployment_name 
                    or CamtrapDPConstants.DataPackage.Resources.Deployment_path 
                    or CamtrapDPConstants.DataPackage.Resources.Deployment_schema 
                    or CamtrapDPConstants.DataPackage.Resources.Media_name 
                    or CamtrapDPConstants.DataPackage.Resources.Media_path 
                    or CamtrapDPConstants.DataPackage.Resources.Media_schema 
                    or CamtrapDPConstants.DataPackage.Resources.Observations_name 
                    or CamtrapDPConstants.DataPackage.Resources.Observations_path 
                    or CamtrapDPConstants.DataPackage.Resources.Observations_schema 
                    or CamtrapDPConstants.DataPackage.Resources.Resource_profile 
                    or CamtrapDPConstants.DataPackage.Project.Id 
                    or CamtrapDPConstants.DataPackage.Contributors 
                    or CamtrapDPConstants.DataPackage.IdAlias 
                    or CamtrapDPConstants.DataPackage.Licenses 
                    or CamtrapDPConstants.DataPackage.Profile 
                    or CamtrapDPConstants.DataPackage.RelatedIdentifiers 
                    or CamtrapDPConstants.DataPackage.References 
                    or CamtrapDPConstants.DataPackage.Sources 
                    or CamtrapDPConstants.DataPackage.Spatial 
                    or CamtrapDPConstants.DataPackage.Taxonomic 
                    or CamtrapDPConstants.DataPackage.Version => true,
                _ => false,
            };
        }

        // true if its a  data package's datalabel 
        public static bool IsDeploymentField(string dataLabel)
        {
            return dataLabel switch
            {
                CamtrapDPConstants.Deployment.BaitUse 
                    or CamtrapDPConstants.Deployment.CameraDelay 
                    or CamtrapDPConstants.Deployment.CameraDepth 
                    or CamtrapDPConstants.Deployment.CameraHeading 
                    or CamtrapDPConstants.Deployment.CameraHeight 
                    or CamtrapDPConstants.Deployment.CameraID 
                    or CamtrapDPConstants.Deployment.CameraModel 
                    or CamtrapDPConstants.Deployment.CameraTilt 
                    or CamtrapDPConstants.Deployment.CoordinateUncertainty 
                    or CamtrapDPConstants.Deployment.DeploymentComments 
                    or CamtrapDPConstants.Deployment.DeploymentEnd 
                    or CamtrapDPConstants.Deployment.DeploymentGroups 
                    or CamtrapDPConstants.Deployment.DeploymentID 
                    or CamtrapDPConstants.Deployment.DeploymentStart 
                    or CamtrapDPConstants.Deployment.DeploymentTags 
                    or CamtrapDPConstants.Deployment.DetectionDistance 
                    or CamtrapDPConstants.Deployment.FeatureType 
                    or CamtrapDPConstants.Deployment.Habitat 
                    or CamtrapDPConstants.Deployment.Latitude 
                    or CamtrapDPConstants.Deployment.LocationID 
                    or CamtrapDPConstants.Deployment.LocationName 
                    or CamtrapDPConstants.Deployment.Longitude 
                    or CamtrapDPConstants.Deployment.SetupBy 
                    or CamtrapDPConstants.Deployment.TimestampIssues => true,
                _ => false,
            };
        }
        public static bool IsDeploymentFieldNonEditableDefault(string dataLabel)
        {
            return dataLabel switch
            {
                CamtrapDPConstants.Deployment.DeploymentID => true,
                _ => false,
            };
        }

        #region Get a bounding box around the various deployment's lat/long coordinates
        public static string CalculateLatLongBoundingBoxFromDeployments(FileDatabase fileDatabase)
        {
            DataTableBackedList<MetadataRow> rows = fileDatabase.MetadataTablesByLevel.GetValueOrDefault(2);
            if (rows == null) return null;

            decimal illegalCoordinate = 200;
            decimal minLatitude = illegalCoordinate;
            decimal maxLatitude = -illegalCoordinate;
            decimal minLongitude = illegalCoordinate;
            decimal maxLongitude = -illegalCoordinate;
            int pointCount = 0;
            foreach (MetadataRow row in rows)
            {
                string latitudeStr = row[CamtrapDPConstants.Deployment.Latitude];
                string longitudeStr = row[CamtrapDPConstants.Deployment.Longitude];

                // Skip invalid lat/longs
                if (false == decimal.TryParse(latitudeStr, out decimal latitude) || false == decimal.TryParse(longitudeStr, out decimal longitude))
                {
                    continue;
                }
                // Valid decimal lat/long must be between these ranges
                if (Math.Abs(latitude) > 90 || Math.Abs(longitude) > 180)
                {
                    continue;
                }

                // Expand the bounding box as needed to contain the lat/long coordinate
                minLatitude = Math.Min(minLatitude, latitude);
                minLongitude = Math.Min(minLongitude, longitude);

                maxLatitude = Math.Max(maxLatitude, latitude);
                maxLongitude = Math.Max(maxLongitude, longitude);
                pointCount++;
            }

            if (pointCount == 0)
            {
                // No points, so return an empty geojson
                return "{\"type\": \"FeatureCollection\",\"features\": []}";
            }

            if (pointCount == 1)
            {
                // A single point, so return a single point geojson (i.e., a waypoint)
                return
                    $"{{\"type\": \"FeatureCollection\",\"features\": [{{\"type\": \"Feature\",\"properties\": {{}},\"geometry\": {{\"coordinates\": [{minLongitude},{minLatitude}],\"type\": \"Point\"}}}}]}}";
            }
            // multiple points, so return the bounding box containing all of them
            return "{\"type\": \"FeatureCollection\",\"features\": " +
                   $"{Environment.NewLine}[{{\"type\": \"Feature\"," +
                   $"{Environment.NewLine}\"properties\": {{}}," +
                   $"{Environment.NewLine}\"geometry\": {{\"coordinates\": [[" +
                   $"[{Environment.NewLine}{minLongitude},{minLatitude}]," +
                   $"[{Environment.NewLine}{minLongitude},{maxLatitude}]," +
                   $"[{Environment.NewLine}{maxLongitude},{maxLatitude}]," +
                   $"[{Environment.NewLine}{maxLongitude},{minLatitude}]," +
                   $"[{Environment.NewLine}{minLongitude},{minLatitude}]" +
                   $"{Environment.NewLine}]],\"type\": \"Polygon\"}}}}]}}";
        }
        #endregion
    }
}
