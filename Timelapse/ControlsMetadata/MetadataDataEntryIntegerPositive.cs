using System.Windows;
using Timelapse.ControlsDataEntry;
using Timelapse.DataTables;
using Timelapse.Util;

namespace Timelapse.ControlsMetadata
{
    // IntegerPostive: An integer control allowing only positive integers or 0 as as input. Comprises:
    // - a label containing the descriptive label) 
    // - an IntegerControl containing the content 
    public class MetadataDataEntryIntegerPositive : MetadataDataEntryIntegerBase
    {
        #region Constructor
        public MetadataDataEntryIntegerPositive(MetadataControlRow control, DataEntryControls styleProvider, string tooltip) :
            base(control, styleProvider,  tooltip, true)
        {
            ContentControl.PreviewTextInput += ValidationCallbacks.PreviewInput_IntegerPositiveCharacterOnly;
            DataObject.AddPastingHandler(ContentControl, ValidationCallbacks.Paste_OnlyIfIntegerPositive);
        }
        #endregion
    }
}