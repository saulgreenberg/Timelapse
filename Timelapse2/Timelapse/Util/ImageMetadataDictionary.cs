using MetadataExtractor;
using System;
using System.Collections.Generic;

namespace Timelapse.Util
{
    /// <summary>
    /// Returns a dictionary listing the metadata found in the given file
    /// If the file cannot be read for metadata, it returns null
    /// Keys are in the form  Directory.Name, e.g., "Reconyx Maker Notes.Ambient Temperature"
    /// Values are instances of the class Metadata, i.e., Key, Directory, Name, Value
    /// </summary>
    public static class ImageMetadataDictionary
    {
        public static Dictionary<string, ImageMetadata> LoadMetadata(string filePath)
        {
            Dictionary<string, ImageMetadata> metadataDictionary = new Dictionary<string, ImageMetadata>();
            try
            {
                foreach (Directory metadataDirectory in ImageMetadataReader.ReadMetadata(filePath))
                {
                    // TraceDebug.PrintMessage(String.Format("metadataDirectory is: {0}", metadataDirectory.Name));
                    foreach (Tag metadataTag in metadataDirectory.Tags)
                    {
                        ImageMetadata metadata = new ImageMetadata(metadataTag.DirectoryName, metadataTag.Name, metadataTag.Description);

                        // Check if the metadata name is already in the dictionary.
                        // If so, just skip it as its not clear what else to do with it
                        // Note that Quicktime mp4s appear to have multiple directories called Quicktime Track Headers. 
                        // Exiftool seems to handle this properly but MetadataDetector generates duplicates.
                        // So only the first Quicktime track header is added to the dictionary.
                        if (!metadataDictionary.ContainsKey(metadata.Key))
                        {
                            metadataDictionary.Add(metadata.Key, metadata);
                        }
                        else
                        {
                            // If you want to see if any Quicktime multiple Track Headers exist but where not added, just uncomment this line.
                            // TracePrint.PrintMessage(String.Format("ImageMetadata Dictionary: Duplicate metadata key: {0}:{1} (Note that Quicktime may have multiple Track Headers)", metadataDirectory.Name, metadata.Key));
                        }
                    }
                }
            }
            catch
            {
                // Likely a corrupt file, Just return the empty dictionary
                metadataDictionary.Clear();
            }
            return metadataDictionary;
        }
    }
}
