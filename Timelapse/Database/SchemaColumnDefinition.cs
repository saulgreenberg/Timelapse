using System;
using System.Globalization;

namespace Timelapse.Database
{
    public class SchemaColumnDefinition
    {
        /// <summary>
        /// ColumnDefinition defines a single column in a SQL Database Schema. 
        /// For example the column definition for Image set log would be stored as
        /// - Log Text "Add text here" ;
        /// </summary>
        #region Public Properties
        public string Name { get; }
        public string DefaultValue { get; }
        public string Type { get; }
        #endregion

        #region Constructors
        // Empty default 
        public SchemaColumnDefinition(string name, string type) : this(name, type, null)
        {
        }

        // Int default converted to string 
        public SchemaColumnDefinition(string name, string type, int defaultValue) : this(name, type, defaultValue.ToString())
        {
        }

        // Bool default converted to string 
        public SchemaColumnDefinition(string name, string type, bool defaultValue) : this(name, type, defaultValue.ToString())
        {
        }

        // Float default converted to string 
        public SchemaColumnDefinition(string name, string type, float defaultValue) : this(name, type, defaultValue.ToString(CultureInfo.InvariantCulture))
        {
        }

        // Full form
        public SchemaColumnDefinition(string name, string type, string defaultValue)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentOutOfRangeException(nameof(name));
            }
            if (string.IsNullOrWhiteSpace(type))
            {
                throw new ArgumentOutOfRangeException(nameof(type));
            }
            DefaultValue = defaultValue;
            Name = name;
            Type = type;
        }
        #endregion

        #region Public Methods
        /// <summary>
        /// Convert the data structure to a string usable by the Database definition schema
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            string columnDefinition = $"{Name} {Type}";
            if (DefaultValue != null)
            {
                columnDefinition += " DEFAULT " + Sql.Quote(DefaultValue);
            }
            return columnDefinition;
        }
        #endregion
    }
}
