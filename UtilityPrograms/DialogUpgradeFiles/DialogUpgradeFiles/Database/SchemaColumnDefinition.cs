﻿using System;
using System.Globalization;

namespace DialogUpgradeFiles.Database
{
    public class SchemaColumnDefinition
    {
        /// <summary>
        /// ColumnDefinition defines a single column in a SQL Database Schema. 
        /// For example the column definition for Image set log would be stored as
        /// - Log Text "Some text" ;
        /// </summary>
        #region Public Properties
        public string Name { get; }
        public string DefaultValue { get;  }
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
            this.DefaultValue = defaultValue;
            this.Name = name;
            this.Type = type;
        }
        #endregion

        #region Public Methods
        /// <summary>
        /// Convert the data structure to a string usable by the Database definition schema
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            string columnDefinition = $"{this.Name} {this.Type}";
            if (this.DefaultValue != null)
            {
                columnDefinition += " DEFAULT " + Sql.Quote(this.DefaultValue);
            }
            return columnDefinition;
        }
        #endregion
    }
}
