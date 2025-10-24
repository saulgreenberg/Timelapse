using Timelapse.ControlsDataEntry;
using Timelapse.DataTables;

namespace Timelapse.ControlsMetadata
{
    // IntegerAny: Any negative or positive integer as input. Comprises:
    // - a label containing the descriptive label)
    // - an IntegerControl containing the content
    public class MetadataDataEntryIntegerAny : MetadataDataEntryIntegerBase
    {
        #region Constructor
        public MetadataDataEntryIntegerAny(MetadataControlRow control, DataEntryControls styleProvider, string tooltip) :
            base(control, styleProvider, tooltip, false)
        {
            // Base class handles all validation callback configuration
        }
        #endregion
    }
}
