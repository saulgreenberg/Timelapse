using DialogUpgradeFiles.Util;
using System;

namespace DialogUpgradeFiles.Database
{
    /// <summary>
    /// A column name and a value to assign (or assigned) to that column. 
    /// Typically used 
    /// - as a list element in ColumnTuples, 
    /// - sometimes included in a ColumnTupleWithWhere data structure
    /// Used in part to create a database query 
    /// For example, if we were doing an update, individual columnTuples are each line below (excluding the where)
    /// UPDATE table_name SET 
    /// colname1 = value1, 
    /// colname2 = value2,
    /// ...
    /// colnameN = valueN
    /// WHERE
    /// <condition> e.g., ID=1;
    /// </summary>
    /// </summary>
    public class ColumnTuple
    {
        #region Public Properties
        public string Name { get; private set; }
        public string Value { get; private set; }
        #endregion

        #region Constructors - different forms for different types
        // Bool value
        public ColumnTuple(string column, bool value)
            : this(column, value ? Constant.BooleanValue.True : Constant.BooleanValue.False)
        {
        }

        // DateTime value
        public ColumnTuple(string column, DateTime value)
            : this(column, DateTimeHandler.ToStringDatabaseDateTime(value))
        {
            if (value.Kind != DateTimeKind.Utc)
            {
                //throw new ArgumentOutOfRangeException(nameof(value));
            }
        }

        // Int value
        public ColumnTuple(string column, int value)
            : this(column, value.ToString())
        {
        }

        // Long value
        public ColumnTuple(string column, long value)
            : this(column, value.ToString())
        {
        }

        // String value
        public ColumnTuple(string column, string value)
        {
            this.Name = column;
            this.Value = value;
        }

        // Float value
        public ColumnTuple(string column, float value)
        {
            this.Name = column;
            this.Value = value.ToString();
        }

        // TimeSpan value
        public ColumnTuple(string column, TimeSpan utcOffset)
        {
            if ((utcOffset < Constant.Time.MinimumUtcOffset) ||
                (utcOffset > Constant.Time.MaximumUtcOffset))
            {
                throw new ArgumentOutOfRangeException(nameof(utcOffset),
                    $"UTC offset must be between {DateTimeHandler.ToStringDatabaseUtcOffset(Constant.Time.MinimumUtcOffset)} and {DateTimeHandler.ToStringDatabaseUtcOffset(Constant.Time.MinimumUtcOffset)}, inclusive.");
            }
            if (utcOffset.Ticks % Constant.Time.UtcOffsetGranularity.Ticks != 0)
            {
                throw new ArgumentOutOfRangeException(nameof(utcOffset),
                    $"UTC offset must be an exact multiple of {DateTimeHandler.ToStringDatabaseUtcOffset(Constant.Time.UtcOffsetGranularity)} ({DateTimeHandler.ToStringDisplayUtcOffset(Constant.Time.UtcOffsetGranularity)}).");
            }

            this.Name = column;
            this.Value = DateTimeHandler.ToStringDatabaseUtcOffset(utcOffset);
        }
        #endregion
    }
}
