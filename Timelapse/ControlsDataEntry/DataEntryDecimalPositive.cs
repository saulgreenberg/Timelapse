using Timelapse.DataTables;

// DecimalPositive: Positive decimal only as input. Comprises:
// - a label containing the descriptive label)
// - a DoubleUpDown control containing the content
namespace Timelapse.ControlsDataEntry
{
    public class DataEntryDecimalPositive(ControlRow control, DataEntryControls styleProvider) : DataEntryDecimalBase(control, styleProvider, true)
    {
        #region Constructor

        // Base class handles all validation callback configuration

        #endregion
    }
}