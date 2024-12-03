using System.Windows;
using Timelapse.ControlsDataEntry;
using Timelapse.DataTables;
using Timelapse.Util;

namespace Timelapse.ControlsMetadata
{
    // DecimalPositive: Any positive real number as input. Comprises:
    // - a label containing the descriptive label) 
    // - a DoubleUpDownControl containing the content 
    // Identical to DecimalAny except it sets a minimum value
    public class MetadataDataEntryDecimalPositive : MetadataDataEntryDecimalBase
    {
        #region Constructor
        public MetadataDataEntryDecimalPositive(MetadataControlRow control, DataEntryControls styleProvider, string tooltip) :
            base(control, styleProvider, tooltip, true)
        {
            ContentControl.PreviewTextInput += ValidationCallbacks.PreviewInput_DecimalPositiveCharacterOnly;
            DataObject.AddPastingHandler(ContentControl, ValidationCallbacks.Paste_OnlyIfDecimalPositive);
        }
        #endregion
    }
}