using Timelapse.ControlsDataEntry;
using Timelapse.DataTables;

namespace Timelapse.ControlsMetadata
{
    // DecimalAny: Any negative or positive real number as input. Comprises:
    // - a label containing the descriptive label)
    // - a DoubleUpDownControl containing the content
    public class MetadataDataEntryDecimalAny(MetadataControlRow control, DataEntryControls styleProvider, string tooltip)
        : MetadataDataEntryDecimalBase(control, styleProvider, tooltip, false)
    {
        #region Constructor

        // Base class handles all validation callback configuration

        #endregion
    }
}
