using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Navigation;

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

        public static bool IsObservationsField(string fieldName)
        {
            switch (fieldName)
            {
                case CamtrapDPConstants.Observations.ObservationID:
                case CamtrapDPConstants.Observations.DeploymentID:
                case CamtrapDPConstants.Observations.MediaID:
                case CamtrapDPConstants.Observations.EventID:
                case CamtrapDPConstants.Observations.EventStart:
                case CamtrapDPConstants.Observations.EventEnd:
                case CamtrapDPConstants.Observations.ObservationLevel:
                case CamtrapDPConstants.Observations.ObservationType:
                case CamtrapDPConstants.Observations.CameraSetupType:
                case CamtrapDPConstants.Observations.ScientificName:
                case CamtrapDPConstants.Observations.Count:
                case CamtrapDPConstants.Observations.LifeStage:
                case CamtrapDPConstants.Observations.Sex:
                case CamtrapDPConstants.Observations.Behavior:
                case CamtrapDPConstants.Observations.IndividualID:
                case CamtrapDPConstants.Observations.IndividualPositionRadius:
                case CamtrapDPConstants.Observations.IndividualPositionAngle:
                case CamtrapDPConstants.Observations.IndividualSpeed:
                case CamtrapDPConstants.Observations.BboxX:
                case CamtrapDPConstants.Observations.BboxY:
                case CamtrapDPConstants.Observations.BboxWidth:
                case CamtrapDPConstants.Observations.BboxHeight:
                case CamtrapDPConstants.Observations.ClassificationMethod:
                case CamtrapDPConstants.Observations.ClassificationTimestamp:
                case CamtrapDPConstants.Observations.ClassificationProbability:
                case CamtrapDPConstants.Observations.ObservationTags:
                case CamtrapDPConstants.Observations.ObservationComments:
                    return true;
            }

            return false;
        }
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
    }
}
