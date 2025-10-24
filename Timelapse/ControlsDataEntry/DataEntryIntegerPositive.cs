using Timelapse.DataTables;

namespace Timelapse.ControlsDataEntry
{
    public class DataEntryIntegerPositive : DataEntryIntegerBase
    {
        #region Constructor
        public DataEntryIntegerPositive(ControlRow control, DataEntryControls styleProvider) :
            base(control, styleProvider, true)
        {
            // Base class handles all validation callback configuration
        }
        #endregion
    }
}
