namespace Timelapse.DataStructures
{
    /// <summary>
    /// Captures a single metadata record entry as extracted by MetaDataExtractor. 
    /// Each record is Directory.Name Value, where Directory.Name is the Key
    /// </summary>
    public class ImageMetadata
    {
        #region Public Properties
        /// <summary>
        /// Directory in the record Directory.Name Value
        /// </summary>
        public string Directory { get; set; }

        /// <summary>
        /// Key in the record Directory.Name Value, where Key is Directory.Name
        /// </summary>
        public string Key => Directory + "." + Name;

        /// <summary>
        /// Name in the record Directory.Name Value
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// The Metadata Value in the record Directory.Name Value
        /// </summary>
        public string Value { get; set; }
        #endregion

        #region Constructor
        /// <summary>
        /// Constructor: Either and empty or full metadata record in the form Directory.Name Value
        /// </summary>
        public ImageMetadata()
        {
            Initialize(string.Empty, string.Empty, string.Empty);
        }

        /// <summary>
        /// Constructor: A  metadata record in the form Directory.Name Value
        /// </summary>
        public ImageMetadata(string directory, string name, string value)
        {
            Initialize(directory, name, value);
        }
        #endregion

        #region Private methods
        /// <summary>
        /// Set a metadata record to its values in the form Directory.Name Value
        /// </summary>
        private void Initialize(string directory, string name, string value)
        {
            Directory = directory;
            Name = name;
            Value = value;
        }
        #endregion 
    }
}
