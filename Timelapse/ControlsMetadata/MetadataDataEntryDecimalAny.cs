﻿using Timelapse.ControlsDataEntry;
using Timelapse.DataTables;

namespace Timelapse.ControlsMetadata
{
    // DecimalAny: Any negative or positive real number as input. Comprises:
    // - a label containing the descriptive label)
    // - a DoubleUpDownControl containing the content
    public class MetadataDataEntryDecimalAny : MetadataDataEntryDecimalBase
    {
        #region Constructor
        public MetadataDataEntryDecimalAny(MetadataControlRow control, DataEntryControls styleProvider, string tooltip) :
            base(control, styleProvider, tooltip, false)
        {
            // Base class handles all validation callback configuration
        }
        #endregion
    }
}
