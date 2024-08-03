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
