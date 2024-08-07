using System.Windows;
using Timelapse.ControlsDataEntry;
using Timelapse.DataTables;
using Timelapse.Util;

namespace Timelapse.ControlsMetadata
{

    // IntegerAny: Any negative or positive integer as input. Comprises:
    // - a label containing the descriptive label) 
    // - an IntegerControl containing the content 
    public class MetadataDataEntryIntegerAny : MetadataDataEntryIntegerBase
    {
        #region Constructor
        public MetadataDataEntryIntegerAny(MetadataControlRow control, DataEntryControls styleProvider, string tooltip) :
            base(control, styleProvider, tooltip, false)
        {
            ContentControl.PreviewTextInput += ValidationCallbacks.PreviewInput_IntegerCharacterOnly;
            DataObject.AddPastingHandler(ContentControl, ValidationCallbacks.Paste_OnlyIfIntegerAny);
        }
        #endregion
    }
}

