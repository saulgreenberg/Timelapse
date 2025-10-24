using Timelapse.DataTables;

// DecimalPositive: Positive decimal only as input. Comprises:
// - a label containing the descriptive label)
// - a DoubleUpDown control containing the content
namespace Timelapse.ControlsDataEntry
{
    public class DataEntryDecimalPositive : DataEntryDecimalBase
    {
        #region Constructor
        public DataEntryDecimalPositive(ControlRow control, DataEntryControls styleProvider) :
            base(control, styleProvider, true)
        {
            // Base class handles all validation callback configuration
        }
        #endregion
    }
}