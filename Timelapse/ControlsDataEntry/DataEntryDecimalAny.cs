using Timelapse.DataTables;

// DecimalAny: Any negative or positive decimal as input. Comprises:
// - a label containing the descriptive label)
// - a DoubleUpDown control containing the content
namespace Timelapse.ControlsDataEntry
{
    public class DataEntryDecimalAny(ControlRow control, DataEntryControls styleProvider) : DataEntryDecimalBase(control, styleProvider, false)
    {
        #region Constructor

        // Base class handles all validation callback configuration

        #endregion
    }
}