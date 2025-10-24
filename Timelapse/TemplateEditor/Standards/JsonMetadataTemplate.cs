using System;
using System.Collections.Generic;


#pragma warning disable IDE1006 // Naming Style - we are using lower case names to match the json structure, we  mute the warning
namespace TimelapseTemplateEditor.Standards
{
    /// <summary>
    /// This class holds data produced by camtrap-DP schemas
    /// Property names and structures in the camtrap-DP attribute names
    /// in order to allow the JSON data to be deserialized into the data structure
    /// see
    /// https://camtrap-dp.tdwg.org/ for description
    /// https://github.com/tdwg/camtrap-dp for camtrap-dp github
    ///
    /// When getting schema files from a URL, use the github source which contains the version(1.0), instead of the one on the documentation website
    /// https://raw.githubusercontent.com/tdwg/camtrap-dp/1.0/deployments-table-schema.json:
    /// </summary>
    // UNUSED
    //public class MetadataJsonImporter
    //{
    //    //public async Task<Recognizer> JsonDeserializeMetadataFileAsync(string path)
    //    public static JsonMetadataTemplate JsonDeserializeMetadataFileAsync(string path)
    //    {
    //        if (File.Exists(path) == false)
    //        {
    //            return null;
    //        }

    //        JsonMetadataTemplate jsonMetadata;
    //        using TextReader sr = new StreamReader(path);
    //        TextReader capturedSr = sr;
    //        try
    //        {
    //            using JsonReader reader = new JsonTextReader(capturedSr);
    //            JsonSerializer serializer = new JsonSerializer();
    //            jsonMetadata = serializer.Deserialize<JsonMetadataTemplate>(reader);
    //        }

    //        catch (Exception e)
    //        {
    //            //GlobalReferences.CancelTokenSource = new CancellationTokenSource();
    //            jsonMetadata = e is TaskCanceledException 
    //                ? new JsonMetadataTemplate() 
    //                : null; // signal cancel by returning a non-null recognizer where info is null

    //        }
    //        return jsonMetadata;
    //    }
    //}

    public class JsonMetadataTemplate : IDisposable
    {
        #region Public Properties

        public string name { get; set; }
        public string title { get; set; }
        public string description { get; set; }

        public List<field> fields { get; set; }

        #endregion
        
        #region Disposing
        // Dispose implemented to follow pattern described in CA1816
        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                this.fields = null;
            }
        }
        #endregion
    }


    /// <summary>
    /// The Info class holds extra information produced by Microsoft's Megadetector
    /// </summary>    
    public class field
    {
        #region Public Properties
        // The detector is a string that should include the detector filename, which will likely include
        // the detector verision in the form md_v*
        // for example the file name md_v4.1.0.pb says it used the megadetector version 5
        public string name { get; set; }
        public string description { get; set; }

        // skos:broadMatch, skos:exactMatch, skos:narrowMatch: less important for implementors
        public string type { get; set; }
        public string format { get; set; }
        public constraints constraints { get; set; }
        public string example { get; set; }
        public string unit { get; set; }

        //Table-level  properties:
        // missingValues
        // primaryKey
        // foreignKeys
    }
    #endregion
    public class constraints
    {
        public bool? required { get; set; }
        public bool? unique { get; set; }
        public string minimum { get; set; }
        public string maximum { get; set; }
        public string pattern { get; set; }

        // SHOULD BE ENUM BUT THAT IS A RESERVED WORD
        public List<string> @enum { get; set; }
    }
}
