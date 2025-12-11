using Timelapse.ControlsDataEntry;
using Timelapse.DataTables;

namespace Timelapse.ControlsMetadata
{
    // DecimalPositive: Any positive real number as input. Comprises:
    // - a label containing the descriptive label)
    // - a DoubleUpDownControl containing the content
    public class MetadataDataEntryDecimalPositive(MetadataControlRow control, DataEntryControls styleProvider, string tooltip)
        : MetadataDataEntryDecimalBase(control, styleProvider, tooltip, true)
    {
        #region Constructor

        // Base class handles all validation callback configuration

        #endregion
    }
}