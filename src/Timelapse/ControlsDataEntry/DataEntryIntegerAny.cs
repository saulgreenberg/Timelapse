using Timelapse.DataTables;

// IntegerAny: Any negative or positive integer as input. Comprises:
// - a label containing the descriptive label)
// - an IntegerControl containing the content
namespace Timelapse.ControlsDataEntry
{
    public class DataEntryIntegerAny(ControlRow control, DataEntryControls styleProvider) : DataEntryIntegerBase(control, styleProvider, false)
    {
        #region Constructor

        // Base class handles all validation callback configuration

        #endregion
    }
}
