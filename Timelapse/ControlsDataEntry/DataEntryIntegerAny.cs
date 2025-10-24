using Timelapse.DataTables;

// IntegerAny: Any negative or positive integer as input. Comprises:
// - a label containing the descriptive label)
// - an IntegerControl containing the content
namespace Timelapse.ControlsDataEntry
{
    public class DataEntryIntegerAny : DataEntryIntegerBase
    {
        #region Constructor
        public DataEntryIntegerAny(ControlRow control, DataEntryControls styleProvider) :
            base(control, styleProvider, false)
        {
            // Base class handles all validation callback configuration
        }
        #endregion
    }
}
