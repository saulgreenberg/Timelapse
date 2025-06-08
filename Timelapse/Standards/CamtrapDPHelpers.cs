using System;
using Timelapse.Database;
using Timelapse.DataTables;

namespace Timelapse.Standards
{
    public static class CamtrapDPHelpers
    {
        public static bool IsMediaField(string fieldName)
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

        // Unusued, as not actually needed: the inverse of IsMediaField should give the same result
        // However, keep it for now in case we want a more explicit check (e.g., if a field name is not in either)
        //public static bool IsObservationsField(string fieldName)
        //{
        //    switch (fieldName)
        //    {
        //        case CamtrapDPConstants.Observations.ObservationID:
        //        case CamtrapDPConstants.Observations.DeploymentID:
        //        case CamtrapDPConstants.Observations.MediaID:
        //        case CamtrapDPConstants.Observations.EventID:
        //        case CamtrapDPConstants.Observations.EventStart:
        //        case CamtrapDPConstants.Observations.EventEnd:
        //        case CamtrapDPConstants.Observations.ObservationLevel:
        //        case CamtrapDPConstants.Observations.ObservationType:
        //        case CamtrapDPConstants.Observations.CameraSetupType:
        //        case CamtrapDPConstants.Observations.ScientificName:
        //        case CamtrapDPConstants.Observations.Count:
        //        case CamtrapDPConstants.Observations.LifeStage:
        //        case CamtrapDPConstants.Observations.Sex:
        //        case CamtrapDPConstants.Observations.Behavior:
        //        case CamtrapDPConstants.Observations.IndividualID:
        //        case CamtrapDPConstants.Observations.IndividualPositionRadius:
        //        case CamtrapDPConstants.Observations.IndividualPositionAngle:
        //        case CamtrapDPConstants.Observations.IndividualSpeed:
        //        case CamtrapDPConstants.Observations.BboxX:
        //        case CamtrapDPConstants.Observations.BboxY:
        //        case CamtrapDPConstants.Observations.BboxWidth:
        //        case CamtrapDPConstants.Observations.BboxHeight:
        //        case CamtrapDPConstants.Observations.ClassificationMethod:
        //        case CamtrapDPConstants.Observations.ClassificationTimestamp:
        //        case CamtrapDPConstants.Observations.ClassificationProbability:
        //        case CamtrapDPConstants.Observations.ObservationTags:
        //        case CamtrapDPConstants.Observations.ObservationComments:
        //            return true;
        //    }

        //    return false;
        //}

        // true if the data package's datalabel should not be editable in the template
        public static bool IsDataPackageFieldNonEditable(string dataLabel)
        {
            switch (dataLabel)
            {
                case CamtrapDPConstants.DataPackage.Resources.Deployment_name:
                case CamtrapDPConstants.DataPackage.Resources.Deployment_path:
                case CamtrapDPConstants.DataPackage.Resources.Deployment_schema:
                case CamtrapDPConstants.DataPackage.Resources.Media_name:
                case CamtrapDPConstants.DataPackage.Resources.Media_path:
                case CamtrapDPConstants.DataPackage.Resources.Media_schema:
                case CamtrapDPConstants.DataPackage.Resources.Observations_name:
                case CamtrapDPConstants.DataPackage.Resources.Observations_path:
                case CamtrapDPConstants.DataPackage.Resources.Observations_schema:
                case CamtrapDPConstants.DataPackage.Resources.Resource_profile:
                case CamtrapDPConstants.DataPackage.Profile:
                case CamtrapDPConstants.DataPackage.IdAlias:
                case CamtrapDPConstants.DataPackage.Project.Id:
                case CamtrapDPConstants.DataPackage.Spatial:
                case CamtrapDPConstants.DataPackage.Contributors:
                case CamtrapDPConstants.DataPackage.Sources:
                case CamtrapDPConstants.DataPackage.Licenses:
                case CamtrapDPConstants.DataPackage.Taxonomic:
                case CamtrapDPConstants.DataPackage.RelatedIdentifiers:
                case CamtrapDPConstants.DataPackage.References:
                    return true;
            }

            return false;
        }

        public static bool IsDeploymentFieldNonEditable(string dataLabel)
        {
            switch (dataLabel)
            {
                case CamtrapDPConstants.Deployment.DeploymentID:
                    return true;
            }

            return false;
        }

        public static bool IsMediaObservationsFieldNonEditable(string dataLabel)
        {
            switch (dataLabel)
            {
                case CamtrapDPConstants.Media.DeploymentID:
                case CamtrapDPConstants.Media.MediaID:
                case CamtrapDPConstants.Media.Timestamp:
                case CamtrapDPConstants.Media.FilePath:
                case CamtrapDPConstants.Media.FileName:
                case CamtrapDPConstants.Media.FileMediatype:
                    return true;
            }

            return false;
        }

        #region Get a bounding box around the various deployment's lat/long coordinates
        public static string CalculateLatLongBoundingBoxFromDeployments(FileDatabase fileDatabase)
        {
            DataTableBackedList<MetadataRow> rows = false == fileDatabase.MetadataTablesByLevel.TryGetValue(2, out var value)
                ? null
                : value;
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
