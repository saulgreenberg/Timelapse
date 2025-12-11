using Timelapse.ControlsDataEntry;
using Timelapse.DataTables;

namespace Timelapse.ControlsMetadata
{
    // IntegerAny: Any negative or positive integer as input. Comprises:
    // - a label containing the descriptive label)
    // - an IntegerControl containing the content
    public class MetadataDataEntryIntegerAny(MetadataControlRow control, DataEntryControls styleProvider, string tooltip)
        : MetadataDataEntryIntegerBase(control, styleProvider, tooltip, false)
    {
        #region Constructor

        // Base class handles all validation callback configuration

        #endregion
    }
}
