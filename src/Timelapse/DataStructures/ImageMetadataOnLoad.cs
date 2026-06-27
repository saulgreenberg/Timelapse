using System.Collections.Generic;
using Timelapse.Enums;

namespace Timelapse.DataStructures
{
    // A class representing a selected metadata item with tag, data label, and type
    public class SelectedMetadataItem(string metadataTag, string dataLabel, string type)
    {
        public string MetadataTag { get; set; } = metadataTag;
        public string DataLabel { get; set; } = dataLabel;
        public string Type { get; set; } = type;
    }

    // State data.
    // This data structure is filled in only when the user has specified metadata / data field pairs
    // that should be populated when loading images for the first time. 
    // Population on load is triggered via the Preferences dialog, which sets the state boolean 'ImageMetadataAskOnLoad' (saved in the Registry)
    // If that flag is true, Timelapse will raise the dialog FileMetadataPopulateAllOnLoad
    // whenever a new image set is being created or added to (via the File menu)
    public class ImageMetadataOnLoad
    {
        // Whether the MetadataExtractor or ExifTool was used to get the metadata fields
        public MetadataToolEnum MetadataToolSelected { get; set; }

        // Contains the metadata / data field / type triplets selected by the user that should be populated
        // MetadataTag is the metadata name, which could include Directory information depending on which metadata tool was selected.
        //     If we only want the tag, then we have to parse that out of the key ie. by checking and getting everything after the last '.'
        // DataLabel is the data field that should be populated
        // Type is the control type of the data field
        public List<SelectedMetadataItem> SelectedImageMetadataDataLabels { get; set; }

        // Returns the metadata tags as an array.
        public string[] Tags
        {
            get
            {
                List<string> tagList = [];
                foreach (SelectedMetadataItem item in SelectedImageMetadataDataLabels)
                {
                    tagList.Add(item.MetadataTag);
                }
                return tagList.ToArray();
            }
        }
    }
}