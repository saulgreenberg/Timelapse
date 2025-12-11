using Timelapse.ControlsDataEntry;
using Timelapse.DataTables;

namespace Timelapse.ControlsMetadata
{
    // IntegerPositive: An integer control allowing only positive integers or 0 as input. Comprises:
    // - a label containing the descriptive label)
    // - an IntegerControl containing the content
    public class MetadataDataEntryIntegerPositive(MetadataControlRow control, DataEntryControls styleProvider, string tooltip)
        : MetadataDataEntryIntegerBase(control, styleProvider, tooltip, true)
    {
        #region Constructor

        // Base class handles all validation callback configuration

        #endregion
    }
}