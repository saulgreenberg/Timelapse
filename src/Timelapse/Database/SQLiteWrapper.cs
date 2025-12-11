using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Data.SQLite;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Timelapse.Constant;
using Timelapse.Controls;
using Timelapse.DataStructures;
using Timelapse.DebuggingSupport;
using Timelapse.Enums;
using Timelapse.Util;
using File = System.IO.File;

namespace Timelapse.Database
{
    // A C# wrapper to interface with SQLite
    // It is NOT a completely generalized wrapper to all SQLite commands, as there is much in SQLite that Timelapse does not use (although these can be added as needed).
    // Importantly, the wrapper is agnostic to the particular schema used by Timelapse i.e., it is a reusable SQL interface.
    // Typically, the FileDatabase acts as an intermediary between Timelapse and this wrapper, where FileDatabase creates the Timelapse-specific queries
    // and invokes them using this SQLite wrapper
    public class SQLiteWrapper
    {
        // A connection string identifying the  database file. Takes the form:
        // "Data Source=filepath" 
        public string ConnectionString { get; }
        public string FilePath { get; }

        #region Constructor
        /// <summary>
        /// Constructor: Create a database file if it does not exist, and then create a connection string to that file
        /// If the database file does not exist,iIt will be created
        /// </summary>
        /// <param name="inputFile">the file containing the database</param>
        public SQLiteWrapper(string inputFile)
        {
            if (!File.Exists(inputFile))
            {
                SQLiteConnection.CreateFile(inputFile);
            }
            SQLiteConnectionStringBuilder connectionStringBuilder = new()
            {
                DataSource = inputFile,
                DateTimeKind = DateTimeKind.Utc,
                ForeignKeys = true // Enable foreign keys
            };
            ConnectionString = connectionStringBuilder.ConnectionString;
            FilePath = inputFile;
        }
        #endregion

        #region Create Table
        // A simplified table creation routine. It expects the column definitions to be supplied
        // as a column_name, data type key value pair. 
        // The table creation syntax supported is:
        // CREATE TABLE table_name (
        //     column1name datatype,       e.g.,   Id INT PRIMARY KEY OT NULL,
        //     column2name datatype,               NAME TEXT NOT NULL,
        //     ...                                 ...
        //     columnNname datatype);              SALARY REAL);
        public void CreateTable(string tableName, List<SchemaColumnDefinition> columnDefinitions)
        {
            // Check the arguments for null 
            ThrowIf.IsNullArgument(columnDefinitions, nameof(columnDefinitions));

            // Just in case the table exists, we will want to remove it before trying to create it.
            if (TableExists(tableName))
            {
                DropTable(tableName);
            }

            string query = $"{Sql.CreateTable} {tableName} {Sql.OpenParenthesis} {Environment.NewLine}"; // CREATE TABLE <tablename> (
            foreach (SchemaColumnDefinition column in columnDefinitions)
            {
                query += $"{column} {Sql.Comma}{Environment.NewLine}";                                 // "columnname TEXT DEFAULT 'value',\n" or similar
            }
            query = query.Remove(query.Length - Sql.Comma.Length - Environment.NewLine.Length);          // remove last comma / new line
            query += $"{Sql.CloseParenthesis} {Sql.Semicolon}";                                          // );
            ExecuteNonQuery(query);
        }
        #endregion

        #region Indexes: Create or Drop (Public)
        // Creates or drops various indexes in table tableName named index name to the column names

        public bool IndexExists(string indexName)
        {
            return 0 != ScalarGetScalarFromSelectAsInt(Sql.SelectCountFromSqliteMasterWhereTypeEqualIndexAndNameEquals + Sql.Quote(indexName));
        }

        // Create a single index named indexName if it doesn't already exist
        public void IndexCreateIfNotExists(string indexName, string tableName, string columnNames)
        {
            if (false == TableExists(tableName))
            {
                // We should only create an index if the table actually exists.
                return;
            }
            // Form: CREATE INDEX IF NOT EXISTS indexName ON tableName  (column1, column2...);
            string query = Sql.CreateIndex + Sql.IfNotExists + indexName + Sql.On + tableName + Sql.OpenParenthesis + columnNames + Sql.CloseParenthesis;
            ExecuteNonQuery(query);
        }

        // Drop a single index named indexName if it exists
        public void IndexDrop(string indexName)
        {
            // Form: DROP INDEX IF EXISTS indexName 
            string query = Sql.DropIndex + Sql.IfExists + indexName;
            ExecuteNonQuery(query);
        }

        // Create multiple indexes wrapped in a begin / end 
        // Each tuple provides the indexName, tableName, and columnNames
        public void IndexCreateMultipleIfNotExists(List<Tuple<string, string, string>> tuples)
        {
            List<string> queries = [];
            foreach (Tuple<string, string, string> tuple in tuples)
            {
                queries.Add(Sql.CreateIndex + Sql.IfNotExists + tuple.Item1 + Sql.On + tuple.Item2 + Sql.OpenParenthesis + tuple.Item3 + Sql.CloseParenthesis);
            }
            ExecuteNonQueryWrappedInBeginEnd(queries);
        }
        #endregion

        #region Event handlers (Private)
        private void DataTableColumns_Changed(object sender, CollectionChangeEventArgs columnChange)
        {
            // DateTime columns default to DataSetDateTime.UnspecifiedLocal, which converts fully qualified DateTimes returned from SQLite to DateTimeKind.Unspecified
            // Since the DATETIME and TIME columns in Timelapse are UTC change this to DataSetDateTime.Utc to get DateTimeKind.Utc.  This must be done before any rows 
            // are added to the table.  This callback is the only way to access the column schema from within DataTable.Load() to make the change.
            DataColumn columnChanged = (DataColumn)columnChange.Element;
            if (columnChanged!.DataType == typeof(DateTime))
            {
                columnChanged.DateTimeMode = DataSetDateTime.Utc;
            }
        }
        #endregion

        #region Select

        public DataTable GetDataTableFromSelect(string query)
        {
            // Debug.Print("GetDataTableFromSelect: " + query);
            DataTable dataTable = new();
            try
            {
                // Open the connection
                using SQLiteConnection connection = GetNewSqliteConnection(ConnectionString);
                connection.Open();

                using SQLiteCommand command = new(connection);
                command.CommandText = query;
                // Debug.Print(query);
                using SQLiteDataReader reader = command.ExecuteReader();
                dataTable.Columns.CollectionChanged += DataTableColumns_Changed;
                dataTable.Load(reader);
                return dataTable;
            }
            catch (Exception exception)
            {
                TracePrint.PrintMessage($"Failure executing query '{query}' in GetDataTableFromSelect. {exception}");
                return dataTable;
            }
        }
        public async Task<DataTable> GetDataTableFromSelectAsync(string query)
        {
            // Debug.Print("GetDataTableFromSelectAsync: " + query);
            return await Task.Run(() =>
            {
                DataTable dataTable = new();
                try
                {
                    // Open the connection
                    using SQLiteConnection connection = GetNewSqliteConnection(ConnectionString);
                    connection.Open();

                    using SQLiteCommand command = new(connection);
                    command.CommandText = query;
                    // Debug.Print(query);
                    using SQLiteDataReader reader = command.ExecuteReader();
                    dataTable.Columns.CollectionChanged += DataTableColumns_Changed;
                    dataTable.Load(reader);
                    return dataTable;
                }
                catch (Exception exception)
                {
                    TracePrint.PrintMessage($"Failure executing query '{query}' in GetDataTableFromSelect. {exception}");
                    return dataTable;
                }
            });
        }


        // TEST CODE: Interruptable GetDataTableFromSelect
        //public DataTable GetDataTableFromSelect(string query, bool interruptable)
        //{
        //    // Debug.Print("GetDataTableFromSelect: " + query);
        //    DataTable dataTable = new DataTable();
        //    try
        //    {
        //        // Open the connection
        //        using (SQLiteConnection connection = SQLiteWrapper.GetNewSqliteConnection(this.connectionString))
        //        {
        //            i = 0;
        //            if (interruptable) { 
        //                connection.ProgressOps =  10;
        //                connection.Progress += Connection_Progress;
        //                Debug.Print("Enabled");
        //            }
        //            else 
        //            {
        //                connection.ProgressOps = 0;
        //                connection.Progress += null;
        //                Debug.Print("Disabled");

        //            }
        //            connection.Open();

        //            using (SQLiteCommand command = new SQLiteCommand(connection))
        //            {
        //                command.CommandText = query;
        //                //Debug.Print(query);
        //                using (SQLiteDataReader reader = command.ExecuteReader())
        //                {
        //                    dataTable.Columns.CollectionChanged += this.DataTableColumns_Changed;
        //                    dataTable.Load(reader);
        //                    return dataTable;
        //                }
        //            }
        //        }
        //    }
        //    catch (Exception exception)
        //    {
        //        TracePrint.PrintMessage(String.Format("Failure executing query '{0}' in GetDataTableFromSelect. {1}", query, exception.ToString()));
        //        return dataTable;
        //    }
        //}

        //static public int i = 0;
        //private void Connection_Progress(object sender, ProgressEventArgs e)
        //{
        //    if (i++ == 10)
        //    {
        //        e.ReturnCode = SQLiteProgressReturnCode.Interrupt;
        //        Debug.Print("Interrupted: " + i.ToString());
        //    }
        //    else Debug.Print ("Going: " + i.ToString());
        //}

        public List<object> GetDistinctValuesInColumn(string tableName, string columnName)
        {
            using SQLiteConnection connection = GetNewSqliteConnection(ConnectionString);
            connection.Open();
            using SQLiteCommand command = new(connection);
            command.CommandText = String.Format(Sql.SelectDistinct + " {0} " + Sql.From + "{1}", columnName, tableName);
            using SQLiteDataReader reader = command.ExecuteReader();
            List<object> distinctValues = [];
            while (reader.Read())
            {
                distinctValues.Add(reader[columnName]);
            }
            return distinctValues;
        }

        /// <summary>
        /// Run a generic Select query against the Database, with a single result returned as an object that must be cast. 
        /// </summary>
        /// <param name="query">The SQL to run</param>
        /// <returns>A value containing the single result.</returns>
        private object GetScalarFromSelect(string query)
        {
            // Debug.Print("Scalar: " + query);
            try
            {
                using SQLiteConnection connection = GetNewSqliteConnection(ConnectionString);
                connection.Open();
                using SQLiteCommand command = new(connection);
                //#pragma warning disable CA2100 // Review SQL queries for security vulnerabilities
                command.CommandText = query;
                //Debug.Print("Count: " + query);
                //#pragma warning restore CA2100 // Review SQL queries for security vulnerabilities
                return command.ExecuteScalar();
            }
            catch (Exception exception)
            {
                TracePrint.PrintMessage($"Failure executing query '{query}' in GetObjectFromSelect: {exception}");
                return null;
            }
        }
        #endregion

        #region Insert
        // Construct each individual query in the form 
        // INSERT INTO table_name
        //      colname1, colname12, ... colnameN VALUES
        //      ('value1', 'value2', ... 'valueN');
        public void Insert(string tableName, List<List<ColumnTuple>> insertionStatements)
        {
            Insert(tableName, insertionStatements, null, string.Empty, 1000);
        }

        public void Insert(string tableName, List<List<ColumnTuple>> insertionStatements, IProgress<ProgressBarArguments> progress, string progressString, int progressFrequency)
        {     // Check the arguments for null 
            ThrowIf.IsNullArgument(insertionStatements, nameof(insertionStatements));


            List<string> queries = [];
            foreach (List<ColumnTuple> columnsToUpdate in insertionStatements)
            {
                Debug.Assert(columnsToUpdate is { Count: > 0 }, "No column updates are specified.");

                string columns = string.Empty;
                string values = string.Empty;
                foreach (ColumnTuple column in columnsToUpdate)
                {
                    columns += String.Format(" {0}" + Sql.Comma, column.Name);      // transform dictionary entries into a string "col1, col2, ... coln"
                    values += String.Format(" {0}" + Sql.Comma, Sql.Quote(column.Value));         // transform dictionary entries into a string "'value1', 'value2', ... 'valueN'"
                }
                if (columns.Length > 0)
                {
                    columns = columns[..^Sql.Comma.Length];     // Remove last comma in the sequence to produce (col1, col2, ... coln)  
                }
                if (values.Length > 0)
                {
                    values = values[..^Sql.Comma.Length];        // Remove last comma in the sequence 
                }

                // Construct the query. The newlines are to format it for pretty printing
                string query = Sql.InsertInto + tableName;               // INSERT INTO table_name
                query += Environment.NewLine;
                query += $"({columns}) ";                         // (col1, col2, ... coln)
                query += Environment.NewLine;
                query += Sql.Values;                                     // VALUES
                query += Environment.NewLine;
                query += $"({values}); ";                         // ('value1', 'value2', ... 'valueN');
                queries.Add(query);
            }

            // Now try to invoke the batch queries
            ExecuteNonQueryWrappedInBeginEnd(queries, progress, progressString, progressFrequency);
        }
        #endregion

        #region Upsert
        // Upsert Row is a limited form of upsert that acts on a single row in a given table.
        // As required by Sqlite upserts, The OnConflict target (i.e, the whereName) must be a Primary Key (or Unique) in the schema
        // The column names and values to update or insert are provided in the column tuples. 
        // The primaryKeyTuple must be the primary key and its value (which are used to detect conflict, and the indicate Where, and to include as name/value in the insert portion)
        public void UpsertRow(string tableName, ColumnTuple primaryKeyTuple, List<ColumnTuple> columnTuples)
        {
            // Check the arguments for null 
            ThrowIf.IsNullArgument(columnTuples, nameof(columnTuples));
            if (columnTuples.Count == 0) return;
            string query = Sql.InsertInto + $" {tableName} ";   // Insert Into <tableName>

            // Construct the Values list of column names surrounded by parenthesis
            string names = $" {primaryKeyTuple.Name} {Sql.Comma}";
            string values = $" {Sql.Quote(primaryKeyTuple.Value)} {Sql.Comma}";
            foreach (ColumnTuple ct in columnTuples)
            {
                names += $" {ct.Name} {Sql.Comma}";
                values += $" {Sql.Quote(ct.Value)} {Sql.Comma}";
            }
            names = names[..^Sql.Comma.Length]; // Remove the last comma
            values = values[..^Sql.Comma.Length]; // Remove the last comma

            query += $"{Sql.OpenParenthesis} {names} {Sql.CloseParenthesis} {Sql.Values} {Sql.OpenParenthesis} {values} {Sql.CloseParenthesis}"; // (columNames) Values (columnValues)
            query += $"{Sql.OnConflict} {Sql.OpenParenthesis} {primaryKeyTuple.Name} {Sql.CloseParenthesis} {Sql.DoUpdate} {Sql.Set}";   // On Conflict (<primaryKeyTuple.Name>) Do Update Set
            foreach (ColumnTuple ct in columnTuples)
            {
                query += $" {ct.Name} = {Sql.Quote(ct.Value)}{Sql.Comma}";
            }
            query = query[..^Sql.Comma.Length]; // Remove the last comma
            query += $"{Sql.Where} {primaryKeyTuple.Name} {Sql.Equal} {primaryKeyTuple.Value}";

            ExecuteNonQuery(query);
        }
        #endregion

        #region Update data
        // Set all rows in a given table's column to a single value
        //  Form, e.g., to set all column values to foobar:
        //  -- Update tableName Set columnName = 'foobar';
        public void SetColumnToACommonValue(string tableName, string columnName, string value)
        {
            string query = Sql.Update + tableName + Sql.Set + columnName + Sql.Equal + Sql.Quote(value);
            ExecuteNonQuery(query);
        }

        // Trims all the white space from the data held in the list of column_names in table_name
        // This allows us to trim data in the database after the fact.
        // Form:
        // -- UPDATE tablename SET columname = TRIM(columnname);
        // ReSharper disable once UnusedMember.Global
        public void TrimWhitespace(string tableName, List<string> columnNames)
        {
            // Check the arguments for null 
            ThrowIf.IsNullArgument(columnNames, nameof(columnNames));

            List<string> queries = [];
            foreach (string columnName in columnNames)
            {
                string query = Sql.Update + tableName + Sql.Set + columnName + Sql.Equal + Sql.Trim + Sql.OpenParenthesis + columnName + Sql.CloseParenthesis + Sql.Semicolon;
                queries.Add(query);
            }
            ExecuteNonQueryWrappedInBeginEnd(queries);
        }

        // Update particular column values in the table, where
        // -- keys in the dictionary indicates which of the current values in the column should be replaced
        // -- each key's value indicates the new value that should replace the current value.
        // Note that this could also be done using ColumnTuplesWithWhere, but this is perhaps a simpler way to compose this
        // Form of each update query generated for each key/value dictionary pair (eg., key="Ok"' value="true" :
        // -- Update tableName Set columnName = 'true' where columnName = 'Ok'
        // ReSharper disable once UnusedMember.Global
        public void UpdateParticularColumnValuesWithNewValues(string tableName, string columnName, Dictionary<string, string> currentValue_newValuePair)
        {
            List<string> queries = [];
            foreach (KeyValuePair<string, string> kvp in currentValue_newValuePair)
            {
                string query = Sql.Update + tableName + Sql.Set + columnName + Sql.Equal + Sql.Quote(kvp.Value) + Sql.Where + columnName + Sql.Equal + Sql.Quote(kvp.Key) + Sql.Semicolon;
                queries.Add(query);
            }
            ExecuteNonQueryWrappedInBeginEnd(queries);
        }

        // Convert all nulls in the list of column_names in table_name
        // Note: this is helpful for cases when the defaults were not set, as that could introduce null values. 
        // Form for each query generated for each provided column
        // -- UPDATE tablename SET columname = '' WHERE columnname IS NULL;
        // ReSharper disable once UnusedMember.Global
        public void ChangeNullToEmptyString(string tableName, List<string> columnNames)
        {
            // Check the arguments for null 
            ThrowIf.IsNullArgument(columnNames, nameof(columnNames));

            List<string> queries = [];
            foreach (string columnName in columnNames)
            {
                string query = Sql.Update + tableName + Sql.Set + columnName + Sql.Equal + "''" + Sql.Where + columnName + Sql.IsNull + Sql.Semicolon; // Form: UPDATE tablename SET columname = '' WHERE columnname IS NULL;
                queries.Add(query);
            }
            ExecuteNonQueryWrappedInBeginEnd(queries);
        }

        public void Update(string tableName, List<ColumnTuplesWithWhere> updateQueryList)
        {
            // Check the arguments for null 
            ThrowIf.IsNullArgument(updateQueryList, nameof(updateQueryList));

            List<string> queries = [];
            foreach (ColumnTuplesWithWhere updateQuery in updateQueryList)
            {
                string query = CreateUpdateQuery(tableName, updateQuery);
                if (string.IsNullOrEmpty(query))
                {
                    continue; // skip non-queries
                }
                queries.Add(query);
            }
            ExecuteNonQueryWrappedInBeginEnd(queries);
        }

        /// <summary>
        /// Update specific rows in the DB as specified in the where clause.
        /// </summary>
        /// <param name="tableName">The table to update.</param>
        /// <param name="columnsToUpdate">The column names and their new values.</param>
        // UPDATE table_name SET 
        // colname1 = value1, 
        // colname2 = value2,
        // ...
        // colnameN = valueN
        // WHERE
        // <condition> e.g., ID=1;
        public void Update(string tableName, ColumnTuplesWithWhere columnsToUpdate)
        {
            // Check the arguments for null 
            ThrowIf.IsNullArgument(columnsToUpdate, nameof(columnsToUpdate));

            string query = CreateUpdateQuery(tableName, columnsToUpdate);
            ExecuteNonQuery(query);
        }

        // UPDATE table_name SET 
        // columnname = value, 
        public void Update(string tableName, ColumnTuple columnToUpdate)
        {
            // Check the arguments for null 
            ThrowIf.IsNullArgument(columnToUpdate, nameof(columnToUpdate));

            string query = Sql.Update + tableName + Sql.Set;
            query += $" {columnToUpdate.Name} = {Sql.Quote(columnToUpdate.Value)}";
            ExecuteNonQuery(query);
        }

        // Return a single update query as a string
        private static string CreateUpdateQuery(string tableName, ColumnTuplesWithWhere columnsToUpdate)
        {
            if (columnsToUpdate.Columns.Count < 1)
            {
                return string.Empty;
            }
            // UPDATE tableName SET 
            // colname1 = value1, 
            // colname2 = value2,
            // ...
            // colnameN = valueN
            // WHERE
            // <condition> e.g., ID=1;
            string query = Sql.Update + tableName + Sql.Set;
            if (columnsToUpdate.Columns.Count < 0)
            {
                return string.Empty;     // No data, so nothing to update. This isn't really an error, so...
            }

            // column_name = 'value'
            foreach (ColumnTuple column in columnsToUpdate.Columns)
            {
                // we have to cater to different formats for integers, NULLS and strings...
                if (column.Value == null)
                {
                    query += $" {column.Name} = {Sql.Null}{Sql.Comma}";
                }
                else
                {
                    query += $" {column.Name} = {Sql.Quote(column.Value)}{Sql.Comma}";
                }
            }
            query = query[..^Sql.Comma.Length]; // Remove the last comma

            if (string.IsNullOrWhiteSpace(columnsToUpdate.Where) == false)
            {
                query += Sql.Where;
                query += columnsToUpdate.Where;
            }
            query += Sql.Semicolon;
            return query;
        }
        #endregion

        #region Delete Rows from Table (Public)

        /// <summary>delete specific rows from the DB where...</summary>
        /// <param name="tableName">The table from which to delete.</param>
        /// <param name="where">The where clause for the delete.</param>
        public void DeleteRows(string tableName, string where = "")
        {
            // DELETE FROM table_name WHERE where
            string query = Sql.DeleteFrom + tableName;        // DELETE FROM table_name
            if (!string.IsNullOrWhiteSpace(where))
            {
                // Add the WHERE clause only when where is not empty
                query += Sql.Where;                   // WHERE
                query += where;                                 // where
            }
            GetDataTableFromSelect(query);
            ExecuteNonQuery(query);
        }

        public DataTable DeleteRowsReturningIds(string tableName, string where, string whatToReturn)
        {
            // DELETE FROM table_name WHERE where RETURNING Id
            string query = Sql.DeleteFrom + tableName;        // DELETE FROM table_name
            if (!string.IsNullOrWhiteSpace(where))
            {
                // Add the WHERE clause only when where is not empty
                query += Sql.Where;                   // WHERE
                query += where;                                 // where
            }

            //query += Sql.Returning + Sql.Quote(whatToReturn);
            query += Sql.Returning + whatToReturn;
            return GetDataTableFromSelect(query);
        }

        /// <summary>
        /// Delete one or more rows from the DB, where each row is specified in the list of where clauses ..
        /// </summary>
        /// <param name="tableName">The table from which to delete</param>
        /// <param name="whereClauses">The where clauses for the row to delete (e.g., ID=1 ID=3 etc</param>
        public void Delete(string tableName, List<string> whereClauses)
        {
            // Check the arguments for null 
            ThrowIf.IsNullArgument(whereClauses, nameof(whereClauses));

            if (false == TableExists(tableName))
            {
                TracePrint.PrintMessage($"CommonDatabase.Delete: Could not delete rows from Table {tableName} as the table does not exist");
                return;
            }
            List<string> queries = [];                      // A list of SQL queries

            // Construct a list containing queries of the form DELETE FROM table_name WHERE where
            foreach (string whereClause in whereClauses)
            {
                // Add the WHERE clause only when uts is not empty
                if (!string.IsNullOrEmpty(whereClause.Trim()))
                {                                                            // Construct each query statement
                    string query = Sql.DeleteFrom + tableName;     // DELETE FROM tablename
                    query += Sql.Where;                            // DELETE FROM tablename WHERE
                    query += whereClause;                                    // DELETE FROM tablename WHERE whereClause
                    query += "; ";                                           // DELETE FROM tablename WHERE whereClause;
                    queries.Add(query);
                }
            }
            // Now try to invoke the batch queries
            if (queries.Count > 0)
            {
                ExecuteNonQueryWrappedInBeginEnd(queries);
            }
        }

        /// <summary>
        /// Delete all the rows in each table in the provided list 
        /// </summary>
        /// <param name="tables"></param>
        public void DeleteAllRowsInTables(List<string> tables)
        {
            if (tables == null || tables.Count == 0)
            {
                return;
            }
            string queries = string.Empty;                      // A list of SQL queries

            // Turn pragma foreign_key off before the delete, as otherwise it takes forever on largish tables
            // Notice that we do not wrap this in a begin / end, as the pragma does not work within that.
            queries += Sql.PragmaForeignKeysOff + "; ";

            // Construct a list containing queries of the form DELETE FROM table_name
            foreach (string table in tables)
            {
                string query = Sql.DeleteFrom + table;     // DELETE FROM tablename
                query += "; ";
                queries += query;
            }

            // Now turn pragma foreign_key on again after the delete
            queries += Sql.PragmaForeignKeysOn + ";";

            // Invoke the batched queries
            ExecuteNonQuery(queries);
        }
        #endregion

        #region Scalars
        /// <summary>
        /// Retrieve a count of items from the DB. Select statement. Must be of the form "Select Count(*) FROM "
        /// </summary>
        /// <param name="query">The query to run.</param>
        /// <returns>The number of items selected.</returns>
        public int ScalarGetScalarFromSelectAsInt(string query)
        {
            object obj = GetScalarFromSelect(query);
            return (obj == DBNull.Value) ? 0 : Convert.ToInt32(obj);
        }

        public long ScalarGetScalarFromSelectAsLong(string query)
        {
            object obj = GetScalarFromSelect(query);
            return (obj == DBNull.Value) ? 0 : Convert.ToInt64(obj);
        }

        // This query is used to transform scalar queries that
        // which returns a 1  or a 0 into true or false respectively         
        // For example, Select EXISTS ( SELECT 1 FROM DataTable WHERE DeleteFlag='true') returnes 1 if any matching row exists else 0
        public bool ScalarBoolFromOneOrZero(string query)
        {
            return (Convert.ToInt32(GetScalarFromSelect(query)) == 1);
        }

        // Get the Maximum value of the field from the datatable  
        // Form: "SELECT MAX(field) From DataTable"
        // The field should contain a long value
        public long ScalarGetMaxValueAsLong(string tableName, string longField)
        {
            return ScalarGetScalarFromSelectAsLong($"{Sql.Select} {Sql.Max} {Sql.OpenParenthesis} {longField} {Sql.CloseParenthesis} {Sql.From} {tableName}");
        }

        // Return a scalar float value, or null if things go wrong.
        public float? ScalarGetFloatValue(string dataTable, string floatfield)
        {
            object obj = GetScalarFromSelect($"{Sql.Select} {floatfield} {Sql.From} {dataTable}");
            if (obj == DBNull.Value)
            {
                return null;
            }
            return Convert.ToSingle(obj);
        }

        #endregion

        #region Execute Non-Queries: one statement, list of statements 
        /// <summary>
        /// Allows the programmer to interact with the database for purposes other than a query.
        /// </summary>
        /// <param name="commandString">The SQL command to be run.</param>
        public void ExecuteNonQuery(string commandString)
        {
            if (string.IsNullOrEmpty(commandString))
            {
                // Nothing to execute
                return;
            }
            try
            {
                using SQLiteConnection connection = GetNewSqliteConnection(ConnectionString);
                connection.Open();
                using SQLiteCommand command = new(connection);
                command.CommandText = commandString;
                command.ExecuteNonQuery();
                //Debug.Print(commandString);
            }
            catch (Exception exception)
            {
                TracePrint.PrintMessage(
                    $"Failure executing statement '{commandString}'. in ExecuteNonQuery:{exception}");
            }
        }

        /// <summary>
        /// Given a list of complete queries, wrap up to 500 of them in a BEGIN/END statement so they are all executed in one go for efficiency
        /// BEGIN
        ///      query1
        ///      query2
        ///      ...
        ///      queryn
        /// END
        /// </summary>
        /// <param name="statements"></param>

        public void ExecuteNonQueryWrappedInBeginEnd(List<string> statements)
        {
            ExecuteNonQueryWrappedInBeginEnd(statements, null, string.Empty, 0);
        }

        public void ExecuteNonQueryWrappedInBeginEnd(List<string> statements, IProgress<ProgressBarArguments> progress, string progressString, int progressFrequency)
        {
            // Check the arguments for null 
            ThrowIf.IsNullArgument(statements, nameof(statements));

            const int MaxStatementCount = 50000;
            // ReSharper disable once RedundantAssignment
            string mostRecentStatement = null;
            try
            {
                using SQLiteConnection connection = GetNewSqliteConnection(ConnectionString);
                connection.Open();
                if (progress != null)
                {
                    progress.Report(new(0, progressString, false, true));
                    Thread.Sleep(ThrottleValues.RenderingBackoffTime);  // Allows the UI thread to update every now and the
                }

                using SQLiteCommand command = new(connection);
                // Invoke each query in the queries list
                // ReSharper disable once NotAccessedVariable
                int rowsUpdated = 0;
                int statementsInQuery = 0;
                int statementsCount = statements.Count;
                int i = 0;
                foreach (string statement in statements)
                {
                    if (progress != null && i % progressFrequency == 0)
                    {
                        int percent = Convert.ToInt32(i * 100.0 / statementsCount);
                        progress.Report(new(percent,
                            $"{progressString} ({i:N0}/{statementsCount:N0})...", false, false));
                        Thread.Sleep(ThrottleValues.RenderingBackoffTime);  // Allows the UI thread to update every now and the

                    }
                    // capture the most recent statement so it's available for debugging
                    // ReSharper disable once RedundantAssignment
                    mostRecentStatement = statement;
                    statementsInQuery++;

                    // Insert a BEGIN if we are at the beginning of the count
                    if (statementsInQuery == 1)
                    {
                        command.CommandText = Sql.BeginTransaction;
                        //Debug.Print(command.CommandText);
                        command.ExecuteNonQuery();
                    }

                    command.CommandText = statement;
                    // Debug.Print(command.CommandText);
                    // Note: Its more efficient to do it this way than to send
                    // a bunch of semicolon-separated statements as a single query
                    rowsUpdated += command.ExecuteNonQuery();

                    // END
                    if (statementsInQuery > MaxStatementCount)
                    {
                        command.CommandText = Sql.EndTransaction;
                        //Debug.Print(command.CommandText);
                        rowsUpdated += command.ExecuteNonQuery();
                        statementsInQuery = 0;

                        if (progress != null)
                        {
                            int percent = Convert.ToInt32(i * 100.0 / statementsCount);
                            progress.Report(new(percent,
                                $"{progressString} ({i:N0}/{statementsCount:N0})...", false, false));
                            Thread.Sleep(ThrottleValues.RenderingBackoffTime);  // Allows the UI thread to update every now and the
                        }
                    }
                    i++;
                }
                // END
                if (statementsInQuery != 0)
                {
                    command.CommandText = Sql.EndTransaction;
                    // ReSharper disable once RedundantAssignment
                    rowsUpdated += command.ExecuteNonQuery();
                }
            }
            catch (Exception exception)
            {
                TracePrint.PrintMessage(
                    $"Failure near executing statement '{mostRecentStatement}' n ExecuteNonQueryWrappedInBeginEnd. {exception}");
            }
        }
        #endregion

        #region Get Schema (Private)
        /// <summary>
        /// Get the Schema for a simple database table 'tableName' from the connected database.
        /// For each column, it can retrieve schema settings including:
        ///     Name, Type, If its the Primary Key, Constraints including its Default Value (if any) and Not Null 
        /// However other constraints that may be set in the table schema are NOT returned, including:
        ///     UNIQUE, CHECK, FOREIGN KEYS, AUTOINCREMENT 
        /// If you use those, the schema may either ignore them or return odd values. So check it!
        /// Usage example: SQLiteDataReader reader = this.GetSchema(connection, "tableName");
        /// </summary>
        /// <param name="connection">the open and valid connection to the database</param>
        /// <param name="tableName">the  name of the table</param>
        /// <returns>
        /// The schema as a SQLiteDataReader.To examine it, do a while loop over reader.Read() to read a column at a time after every read
        /// access the column's attributes, where 
        /// -reader[0] is column number (e.g., 0)
        /// -reader[1] is column name (e.g., Employee)
        /// -reader[2] is type (e.g., STRING)
        /// -reader[3] to [5] also returns values, but not yet sure what they stand for.. maybe 'Primary Key Autoincrement'?
        /// </returns>
        private static SQLiteDataReader GetSchema(SQLiteConnection connection, string tableName)
        {
            string sql = Sql.PragmaTableInfo + Sql.OpenParenthesis + tableName + Sql.CloseParenthesis; // Syntax is: PRAGMA TABLE_INFO (tableName)
            using SQLiteCommand command = new(sql, connection);
            return command.ExecuteReader();
        }

        private static List<string> GetSchemaColumnDefinitions(SQLiteConnection connection, string tableName)
        {
            using SQLiteDataReader reader = GetSchema(connection, tableName);
            List<string> columnDefinitions = [];
            while (reader.Read())
            {
                string existingColumnDefinition = string.Empty;
                for (int field = 0; field < reader.FieldCount; field++)
                {
                    switch (field)
                    {
                        case 0:  // cid (Column Index)
                            break;
                        case 1:  // name (Column Name)
                        case 2:  // type (Column type)
                            existingColumnDefinition += reader[field] + " ";
                            break;
                        case 3:  // notnull (Column has a NOT NULL constraint)
                            if (reader[field].ToString() != "0")
                            {
                                existingColumnDefinition += Sql.NotNull;
                            }
                            break;
                        case 4:  // dflt_value (Column has a default value)
                            string s = reader[field].ToString();
                            if (!string.IsNullOrEmpty(s))
                            {
                                existingColumnDefinition += Sql.Default + reader[field] + " ";
                            }
                            break;
                        case 5:  // pk (Column is part of the primary key)
                            if (reader[field].ToString() != "0")
                            {
                                existingColumnDefinition += Sql.PrimaryKey;
                            }
                            break;
                        default:
                            // This should never happen
                            // But if it does, we just ignore it
                            Debug.Print("Unknown Field: " + field);
                            break;
                    }
                }
                existingColumnDefinition = existingColumnDefinition.TrimEnd(' ');
                columnDefinitions.Add(existingColumnDefinition);
            }
            return columnDefinitions;
        }

        /// <summary>
        /// Return a list of all the column names in the  table named 'tableName' from the connected database.
        /// </summary>
        /// <param name="connection">the open and valid connection to the database</param>
        /// <param name="tableName">the name of the table</param>
        /// <returns>a list of all the column names in the  table</returns>
        private static List<string> GetSchemaColumnNamesAsList(SQLiteConnection connection, string tableName)
        {
            using SQLiteDataReader reader = GetSchema(connection, tableName);
            List<string> columnNames = [];
            while (reader.Read())
            {
                columnNames.Add(reader[1].ToString());
            }
            return columnNames;
        }

        /// <summary>
        /// Return a comma separated string of all the column names in the  table named 'tableName' from the connected database.
        /// </summary>
        /// <param name="connection">the open and valid connection to the database</param>
        /// <param name="tableName">the name of the table</param>
        /// <returns>a comma separated string of all the column names in the table</returns>
        private static string GetSchemaColumnNamesAsString(SQLiteConnection connection, string tableName)
        {
            return String.Join(", ", GetSchemaColumnNamesAsList(connection, tableName));
        }
        #endregion

        #region Copy from source table to destination table
        /// <summary>
        /// Copy all the values from the source table into the destination table. Assumes that both tables are populated with identically-named columns
        /// </summary>
        private static void CopyAllValuesFromTable(SQLiteConnection connection, string schemaFromTable, string dataSourceTable, string dataDestinationTable)
        {
            string commaSeparatedColumns = GetSchemaColumnNamesAsString(connection, schemaFromTable);
            if (string.IsNullOrEmpty(commaSeparatedColumns))
            {
                //Debug.Print("In CopyAllValuesFromTable: comma separated columns is empty. Aborted");
                return;
            }
            string sql = Sql.InsertInto + dataDestinationTable + Sql.OpenParenthesis + commaSeparatedColumns + Sql.CloseParenthesis + Sql.Select + commaSeparatedColumns + Sql.From + dataSourceTable;
            using SQLiteCommand command = new(sql, connection);
            command.ExecuteNonQuery();
        }

        /// <summary>
        /// Copy all the values from the source table into the destination table. Assumes that both tables are populated with identically-named columns
        /// </summary>
        private static void CopyAllValuesBetweenTables(SQLiteConnection connection, string schemaFromSourceTable, string schemaFromDestinationTable, string dataSourceTable, string dataDestinationTable)
        {
            string commaSeparatedColumnsSource = GetSchemaColumnNamesAsString(connection, schemaFromSourceTable);
            string commaSeparatedColumnsDestination = GetSchemaColumnNamesAsString(connection, schemaFromDestinationTable);
            string sql = Sql.InsertInto + dataDestinationTable + Sql.OpenParenthesis + commaSeparatedColumnsDestination + Sql.CloseParenthesis + Sql.Select + commaSeparatedColumnsSource + Sql.From + dataSourceTable;

            using SQLiteCommand command = new(sql, connection);
            command.ExecuteNonQuery();
        }
        #endregion

        #region Schema and Column Changes: Replace Schema, IsColumnInTable / Add / Delete / Rename / 

        // Unused, but may as well keep it
        // // Alter the column schema
        // New attributes are given in the attributes dictionary, where the key indicates the schema field (e.g., Column name, type, NotNull, and Default) and the value is the new value for that field
        // If a field is not specified, jsut keep the old value.  
        // ReSharper disable once UnusedMember.Global
        public void SchemaRenameTable(string originalTableName, string newTableName)
        {
            // Some basic error checking to make sure we can do the operation
            if (false == TableExists(originalTableName))
            {
                return;
            }

            try
            {
                using SQLiteConnection connection = GetNewSqliteConnection(ConnectionString);
                connection.Open();
                // Rename the table
                SchemaRenameTable(connection, originalTableName, newTableName);
            }
            catch (Exception exception)
            {
                TracePrint.PrintMessage($"Failure in SchemaRenameTable. {exception}");
                throw;
            }
        }

        public void SchemaAlterTableWithNewColumnDefinitions(string sourceTable, List<SchemaColumnDefinition> columnDefinitions)
        {
            string destTable = "TempTable";
            try
            {
                // Create an empty table with the schema based on columnDefinitions
                CreateTable(destTable, columnDefinitions);
                using SQLiteConnection connection = GetNewSqliteConnection(ConnectionString);
                connection.Open();

                // copy the contents from the source table to the destination table
                CopyAllValuesFromTable(connection, destTable, sourceTable, destTable);

                // delete the source table, and rename the destination table so it is the same as the source table
                DropTable(connection, sourceTable);
                SchemaRenameTable(connection, destTable, sourceTable);
            }
            catch (Exception exception)
            {
                TracePrint.PrintMessage($"Failure in CopyTableContentsToEmptyTable. {exception}");
                throw;
            }
        }

        // Return a List of strings comprising each column in the schema
        public List<string> SchemaGetColumns(string tableName)
        {
            try
            {
                // Open the connection
                using SQLiteConnection connection = GetNewSqliteConnection(ConnectionString);
                connection.Open();
                using SQLiteDataReader reader = GetSchema(connection, tableName);
                List<string> columnsList = [];
                while (reader.Read())
                {
                    columnsList.Add(reader[1].ToString());
                }
                return columnsList;
            }
            catch (Exception exception)
            {
                TracePrint.PrintMessage($"Failure executing getschema in SchemaGetColumns. {exception}");
                return null;
            }
        }


        // Return a dictionary comprising each column in the schema and its default values (if any)
        public Dictionary<string, string> SchemaGetColumnsAndDefaultValues(string tableName)
        {
            try
            {
                // Open the connection
                using SQLiteConnection connection = GetNewSqliteConnection(ConnectionString);
                connection.Open();
                using SQLiteDataReader reader = GetSchema(connection, tableName);
                Dictionary<string, string> columndefaultsDict = [];
                while (reader.Read())
                {
                    columndefaultsDict.Add(reader[1].ToString() ?? string.Empty, reader[4].ToString());
                }
                return columndefaultsDict;
            }
            catch (Exception exception)
            {
                TracePrint.PrintMessage($"Failure executing getschema in SchemaGetColumnsAndDefaultValues. {exception}");
                return null;
            }
        }

        public bool SchemaIsColumnInTable(string sourceTable, string currentColumnName)
        {
            try
            {
                using SQLiteConnection connection = GetNewSqliteConnection(ConnectionString);
                connection.Open();
                List<string> currentColumnNames = GetSchemaColumnNamesAsList(connection, sourceTable);
                return currentColumnNames.Contains(currentColumnName);
            }
            catch (Exception exception)
            {
                TracePrint.PrintMessage($"Failure in ColumnExists. {exception}");
                return false;
            }
        }

        // This method will create a column in a table of type TEXT, where it is added to its end
        // It assumes that the value, if not empty, should be treated as the default value for that column
        public void SchemaAddColumnToEndOfTable(string tableName, SchemaColumnDefinition columnDefinition)
        {
            // Check the arguments for null 
            ThrowIf.IsNullArgument(columnDefinition, nameof(columnDefinition));

            ExecuteNonQuery(Sql.AlterTable + tableName + Sql.AddColumn + columnDefinition);
        }

        /// <summary>
        /// Add a column to the table named sourceTable at position columnNumber using the provided columnDefinition
        /// The value in columnDefinition is assumed to be the desired default value
        /// </summary>
        // ReSharper disable once UnusedMember.Global
        public void SchemaAddColumnToTable(string tableName, int columnNumber, SchemaColumnDefinition columnDefinition)
        {
            // Check the arguments for null 
            ThrowIf.IsNullArgument(columnDefinition, nameof(columnDefinition));

            try
            {
                using SQLiteConnection connection = GetNewSqliteConnection(ConnectionString);
                connection.Open();

                // Some basic error checking to make sure we can do the operation
                List<string> columnNames = GetSchemaColumnNamesAsList(connection, tableName);

                // Check if a column named Name already exists in the source Table. If so, abort as we cannot add duplicate column names
                if (columnNames.Contains(columnDefinition.Name))
                {
                    throw new ArgumentException(
                        $"Column '{columnDefinition.Name}' is already present in table '{tableName}'.", nameof(columnDefinition));
                }

                // If columnNumber would result in the column being inserted at the end of the table, then use the more efficient method to do so.
                if (columnNumber >= columnNames.Count)
                {
                    SchemaAddColumnToEndOfTable(tableName, columnDefinition);
                    return;
                }

                // We need to add a column elsewhere than the end. This requires us to 
                // create a new schema, create a new table from that schema, copy data over to it, remove the old table
                // and rename the new table to the name of the old one.

                // Get a schema definition identical to the schema in the existing table, 
                // but with a new column definition of type TEXT added at the given position, where the value is assumed to be the default value
                string newSchema = SchemaInsertColumn(connection, tableName, columnNumber, columnDefinition);

                // Create a new table 
                string destTable = tableName + "NEW";
                string sql = Sql.CreateTable + destTable + Sql.OpenParenthesis + newSchema + Sql.CloseParenthesis;
                //#pragma warning disable CA2100 // Review SQL queries for security vulnerabilities
                using (SQLiteCommand command = new(sql, connection))
                {
                    command.ExecuteNonQuery();
                }
                //#pragma warning restore CA2100 // Review SQL queries for security vulnerabilities
                // Copy the old table's contents to the new table
                CopyAllValuesFromTable(connection, tableName, tableName, destTable);

                // Now drop the source table and rename the destination table to that of the source table
                DropTable(connection, tableName);

                // Rename the table
                SchemaRenameTable(connection, destTable, tableName);
            }
            catch (Exception exception)
            {
                TracePrint.PrintMessage($"Failure in AddColumn. {exception}");
                throw;
            }
        }

        public bool SchemaDeleteColumn(string sourceTable, string columnName)
        {
            // Check the arguments for null 
            ThrowIf.IsNullArgument(columnName, nameof(columnName));

            try
            {
                using SQLiteConnection connection = GetNewSqliteConnection(ConnectionString);
                connection.Open();
                // Some basic error checking to make sure we can do the operation
                if (string.IsNullOrEmpty(columnName.Trim()))
                {
                    return false;  // The provided column names= is an empty string
                }
                List<string> columnNames = GetSchemaColumnNamesAsList(connection, sourceTable);
                if (!columnNames.Contains(columnName))
                {
                    return false; // There is no column called columnName in the source Table, so we can't delete ti
                }

                // Get a schema definition identical to the schema in the existing table, 
                // but with the column named columnName deleted from it
                string newSchema = SchemaRemoveColumn(connection, sourceTable, columnName);

                // Create a new table 
                string destTable = sourceTable + "NEW";
                string sql = Sql.CreateTable + destTable + Sql.OpenParenthesis + newSchema + Sql.CloseParenthesis;
                using (SQLiteCommand command = new(sql, connection))
                {
                    command.ExecuteNonQuery();
                }

                // Copy the old table's contents to the new table
                CopyAllValuesFromTable(connection, destTable, sourceTable, destTable);

                // Now drop the source table and rename the destination table to that of the source table
                DropTable(connection, sourceTable);

                // Rename the table
                SchemaRenameTable(connection, destTable, sourceTable);
                return true;
            }
            catch (Exception exception)
            {
                TracePrint.PrintMessage($"Failure in DeleteColumn. {exception}");
                throw;
            }
        }

        // Rename a column in a table. This is just a simpler form of SchemaAlterColumn
        public void SchemaRenameColumn(string sourceTable, string currentColumnName, string newColumnName)
        {
            Dictionary<SchemaAttributesEnum, string> attributes = new()
            {
                { SchemaAttributesEnum.Name, newColumnName }
            };
            SchemaAlterColumn(sourceTable, currentColumnName, attributes);
        }

        // Alter the column schema
        // New attributes are given in the attributes dictionary, where the key indicates the schema field (e.g., Column name, type, NotNull, and Default) and the value is the new value for that field
        // If a field is not specified, jsut keep the old value.  
        public void SchemaAlterColumn(string sourceTable, string currentColumnName, Dictionary<SchemaAttributesEnum, string> attributes)
        {
            // Some basic error checking to make sure we can do the operation
            if (string.IsNullOrWhiteSpace(currentColumnName))
            {
                throw new ArgumentOutOfRangeException(nameof(currentColumnName));
            }
            if (attributes.Count == 0)
            {
                // Nothing to change!
                return;
            }
            try
            {
                string newColumnName = string.Empty;
                if (attributes.TryGetValue(SchemaAttributesEnum.Name, out string key))
                //if (attributes.ContainsKey(SchemaAttributesEnum.Name))
                {
                    newColumnName = key.Trim();
                }

                using SQLiteConnection connection = GetNewSqliteConnection(ConnectionString);
                connection.Open();
                List<string> currentColumnNames = GetSchemaColumnNamesAsList(connection, sourceTable);
                if (currentColumnNames.Contains(currentColumnName) == false)
                {
                    throw new ArgumentException($"No column named '{currentColumnName}' exists to rename.", nameof(currentColumnName));
                }
                if (false == string.IsNullOrEmpty(newColumnName) && currentColumnNames.Contains(newColumnName))
                {
                    // If its a name change, we have to ensure that name is valid and that it doesn't already exit
                    throw new ArgumentException($"Column '{newColumnName}' is already in use.");
                }
                string newSchema = SchemaCloneButAlterColumn(connection, sourceTable, currentColumnName, attributes);

                // Create a new table 
                string destTable = sourceTable + "NEW";
                string sql = Sql.CreateTable + destTable + Sql.OpenParenthesis + newSchema + Sql.CloseParenthesis;
                using (SQLiteCommand command = new(sql, connection))
                {
                    command.ExecuteNonQuery();
                }

                // Copy the old table's contents to the new table
                CopyAllValuesBetweenTables(connection, sourceTable, destTable, sourceTable, destTable);

                // Now drop the source table and rename the destination table to that of the source table
                DropTable(connection, sourceTable);

                // Rename the table
                SchemaRenameTable(connection, destTable, sourceTable);
            }
            catch (Exception exception)
            {
                TracePrint.PrintMessage($"Failure in RenameColumn. {exception}");
                throw;
            }
        }

        /// <summary>
        /// Create a schema cloned from tableName, except with the column definition for columnName deleted
        /// </summary>
        private static string SchemaCloneButAlterColumn(SQLiteConnection connection, string tableName, string existingColumnName, Dictionary<SchemaAttributesEnum, string> attributes)
        {
            string newSchema = string.Empty;
            using (SQLiteDataReader reader = GetSchema(connection, tableName))
            {
                string currentColumnName = string.Empty;
                while (reader.Read())
                {
                    string existingColumnDefinition = string.Empty;

                    // Copy the existing column definition unless its the column named columnNam
                    for (int field = 0; field < reader.FieldCount; field++)
                    {
                        switch (field)
                        {
                            case 0:  // cid (Column Index)
                                break;
                            case 1:  // name (Column Name)
                                     // Rename the column if needed
                                currentColumnName = reader[field].ToString();
                                existingColumnDefinition += (currentColumnName == existingColumnName && attributes.TryGetValue(SchemaAttributesEnum.Name, out var attribute))
                                    ? attribute
                                    : reader[field].ToString();
                                existingColumnDefinition += " ";
                                break;
                            case 2:  // type (Column type)
                                existingColumnDefinition += (currentColumnName == existingColumnName && attributes.TryGetValue(SchemaAttributesEnum.Type, out var attribute1))
                                    ? attribute1
                                    : reader[field].ToString();
                                existingColumnDefinition += " ";
                                break;
                            case 3:  // notnull (Column has a NOT NULL constraint)
                                if (currentColumnName == existingColumnName && attributes.TryGetValue(SchemaAttributesEnum.NotNull, out var attribute2))
                                {
                                    existingColumnDefinition += attribute2;
                                }
                                else if (reader[field].ToString() != "0")
                                {
                                    existingColumnDefinition += Sql.NotNull;
                                }
                                existingColumnDefinition += " ";
                                break;
                            case 4:  // dflt_value (Column has a default value)
                                if (currentColumnName == existingColumnName && attributes.TryGetValue(SchemaAttributesEnum.Default, out var attribute3))
                                {
                                    existingColumnDefinition += Sql.Default + Sql.Quote(attribute3);
                                }
                                else if (false == string.IsNullOrEmpty(reader[field].ToString()))
                                {
                                    // Note that the default is already quoted, so we should not quote it again
                                    existingColumnDefinition += Sql.Default + reader[field] + " ";
                                }
                                existingColumnDefinition += " ";
                                break;
                            case 5:  // pk (Column is part of the primary key)
                                if (reader[field].ToString() != "0")
                                {
                                    existingColumnDefinition += Sql.PrimaryKey;
                                }
                                break;
                            default:
                                Debug.Print(field.ToString());
                                break;
                        }
                    }
                    existingColumnDefinition = existingColumnDefinition.TrimEnd(' ');
                    newSchema += existingColumnDefinition + ", ";
                }
            }
            newSchema = newSchema.TrimEnd(',', ' '); // remove last comma
            return newSchema;
        }
        private static void SchemaAddColumnToEndOfTable(SQLiteConnection connection, string tableName, string columnDefinition)
        {
            string sql = Sql.AlterTable + tableName + Sql.AddColumn + columnDefinition;
            using SQLiteCommand command = new(sql, connection);
            command.ExecuteNonQuery();
        }
        /// <summary>
        /// Add a column definition into the provided schema at the given column location
        /// </summary>
        private static string SchemaInsertColumn(SQLiteConnection connection, string tableName, int newColumnNumber, SchemaColumnDefinition newColumn)
        {
            List<string> columnDefinitions = GetSchemaColumnDefinitions(connection, tableName);
            columnDefinitions.Insert(newColumnNumber, newColumn.ToString());
            return String.Join(", ", columnDefinitions);
        }

        /// <summary>
        /// Create a schema cloned from tableName, except with the column definition for columnName deleted
        /// </summary>
        private static string SchemaRemoveColumn(SQLiteConnection connection, string tableName, string columnName)
        {
            List<string> columnDefinitions = GetSchemaColumnDefinitions(connection, tableName);
            int columnToRemove = -1;
            int columnDefinitionsCount = columnDefinitions.Count;
            for (int column = 0; column < columnDefinitionsCount; ++column)
            {
                string columnDefinition = columnDefinitions[column];
                // Extract the exact column name from the definition
                string definitionColumnName = columnDefinition.Trim().Split()[0];
                if (definitionColumnName == columnName)
                {
                    columnToRemove = column;
                    break;
                }
            }
            if (columnToRemove == -1)
            {
                throw new ArgumentOutOfRangeException($"Column '{columnName}' not found in table '{tableName}'.");
            }

            columnDefinitions.RemoveAt(columnToRemove);
            return String.Join(", ", columnDefinitions);
        }

        /// <summary>
        /// Rename the database table named 'tableName' to 'new_tableName'  
        /// </summary>
        /// <param name="connection">the open and valid connection to the database</param>
        /// <param name="tableName">the current name of the existing table</param> 
        /// <param name="newTableName">the new name of the table</param> 
        private static void SchemaRenameTable(SQLiteConnection connection, string tableName, string newTableName)
        {
            string sql = Sql.AlterTable + tableName + Sql.RenameTo + newTableName;
            using SQLiteCommand command = new(sql, connection);
            command.ExecuteNonQuery();
        }
        #endregion

        #region Drop Table / Vacuum
        /// <summary>
        /// Drop the database table 'tableName' from the connected database.
        /// </summary>
        public void DropTable(string tableName)
        {
            using SQLiteConnection connection = GetNewSqliteConnection(ConnectionString);
            connection.Open();
            DropTable(connection, tableName);
        }
        private static void DropTable(SQLiteConnection connection, string tableName)
        {
            // Turn foreign keys off, do the operaton, then turn it backon. 
            // This is because if we drop a table that has foreign keys in it, we need to make sure foreign keys are off
            // as otherwise it will delete the foreign key table contents.
            PragmaSetForeignKeys(connection, false);

            // Drop the table
            string sql = Sql.DropTable + Sql.IfExists + tableName;
            using (SQLiteCommand command = new(sql, connection))
            {
                command.ExecuteNonQuery();
            }

            PragmaSetForeignKeys(connection, true);
        }

        /// <summary>
        /// Vacuum the connected database.
        /// </summary>
        public void Vacuum()
        {
            using SQLiteConnection connection = GetNewSqliteConnection(ConnectionString);
            connection.Open();
            using SQLiteCommand command = new(Sql.Vacuum, connection);
            command.ExecuteNonQuery();
        }
        #endregion

        #region Utilities
        // Open the SQLite connection
        // Note that the 2nd argument is ParseViaFramework. This is included to resolve an issue that occurs
        // when users try to open a network file on some VPNs, eg., Cisco VPN and perhaps other network file systems
        // Its an obscur bug and solution reported by others: sqlite doesn't really document that argument very well. But it seems to fix it.
        public static SQLiteConnection GetNewSqliteConnection(string connectionString)
        {
            return new(connectionString, true);
        }
        #endregion

        #region Table Exists (and also its not empty)
        public bool TableExists(string tableName)
        {
            string query = Sql.SelectNameFromSqliteMasterWhereTypeEqualTableAndNameEquals + Sql.Quote(tableName) + Sql.Semicolon;
            using DataTable datatable = GetDataTableFromSelect(query);
            bool rowsExist = datatable.Rows.Count != 0;
            return rowsExist;
        }

        public bool TableExistsAndNotEmpty(string tableName)
        {
            string query = Sql.SelectNameFromSqliteMasterWhereTypeEqualTableAndNameEquals + Sql.Quote(tableName) + Sql.Semicolon;
            using DataTable datatable = GetDataTableFromSelect(query);
            if (datatable.Rows.Count == 0)
            {
                // The table does not exists
                return false;
            }

            return TableHasContent(tableName);
        }
        #endregion

        #region Rows Exist
        // Return true iff the table exists, but is empty
        public bool TableHasContent(string tableName)
        {
            string query = $"{Sql.SelectCountStarFrom} {tableName} {Sql.LimitOne}";
            // If > 0 elements, then it both exists and has content so return true otherwise false
            return ScalarGetScalarFromSelectAsInt(query) != 0;
        }
        #endregion

        #region Pragmas
        // PRAGMA Quick_Check
        // Checks for database integrity. Note that if it is really corrupt, it will generate an internal exception that is corrupt.
        // Also note that a zero-length database file will pass this test, so you need to do a further check i.e. to see if a particular table is in the database.
        public bool PragmaGetQuickCheck()
        {
            try
            {
                using DataTable dataTable = new();
                // Open the connection
                using SQLiteConnection connection = GetNewSqliteConnection(ConnectionString);
                connection.Open();
                using SQLiteCommand command = new(connection);
                command.CommandText = Sql.PragmaQuickCheck;
                using SQLiteDataReader reader = command.ExecuteReader();
                dataTable.Columns.CollectionChanged += DataTableColumns_Changed;
                dataTable.Load(reader);
                if (dataTable.Rows.Count == 1 && String.Equals((string)dataTable.Rows[0].ItemArray[0], Sql.Ok, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                return false;
            }
            catch
            {
                // this will be a System.Data.SQLite.SQLiteException
                return false;
            }
        }

        // PRAGMA Turn foreign keys on or off. 
        // For example, if we drop a table that has foreign keys in it, we need to make sure foreign keys are off
        // as otherwise it will delete the foreign key table contents.
        private static void PragmaSetForeignKeys(SQLiteConnection connection, bool state)
        {
            // Syntax is: PRAGMA foreign_keys = OFF;
            // Syntax is: PRAGMA foreign_keys = On;
            string sql = "PRAGMA foreign_keys = ";
            sql += state ? "ON;" : "Off;";
            using SQLiteCommand command = new(sql, connection);
            command.ExecuteNonQuery();
        }

        // PRAGMA Defer foreign keys. 
#pragma warning disable IDE0051 // Remove unused private members
        // ReSharper disable once UnusedMember.Local
        private static void PragmaSetDeferForeignKeys(SQLiteConnection connection, bool state)
        {
            // Syntax is: defer_foreign_keys = 1; True
            //            defer_foreign_keys = 0; False
            string sql = "PRAGMA defer_foreign_keys = ";
            sql += state ? "1;" : "0;";
            using SQLiteCommand command = new(sql, connection);
            command.ExecuteNonQuery();
        }


#pragma warning restore IDE0051 // Remove unused private members
        #endregion

        #region Unused methods
#pragma warning disable IDE0051 // Remove unused private members
        /// <summary>
        /// CURRENTLY UNUSED
        /// Add a column to the end of the database table 
        /// This does NOT require the table to be cloned.
        /// Note: Some of the AddColumnToEndOfTable methods are currently not referenced, but may be handy in the future.
        /// </summary>
        /// <param name="connection">the open and valid connection to the database</param> 
        /// <param name="tableName">the name of the  table</param> 
        /// <param name="name">the name of the new column</param> 
        /// <param name="type">the type of the new column</param> 
        // ReSharper disable once UnusedMember.Local
        private static void AddColumnToEndOfTable(SQLiteConnection connection, string tableName, string name, string type)
        {
            string columnDefinition = name + " " + type;
            SchemaAddColumnToEndOfTable(connection, tableName, columnDefinition);
        }

        /// <summary>
        /// Add a column to the end of the database table. 
        /// This does NOT require the table to be cloned.
        /// </summary>
        /// <param name="connection">the open and valid connection to the database</param> 
        /// <param name="tableName">the name of the  table</param> 
        /// <param name="name">the name of the new column</param> 
        /// <param name="type">the type of the new column</param> 
        /// <param name="otherOptions">space-separated options such as PRIMARY KEY AUTOINCREMENT, NULL or NOT NULL etc</param>
        // ReSharper disable once UnusedMember.Local
        private static void AddColumnToEndOfTable(SQLiteConnection connection, string tableName, string name, string type, string otherOptions)
        {
            string columnDefinition = name + " " + type;
            if (string.IsNullOrEmpty(otherOptions))
            {
                columnDefinition += " " + otherOptions;
            }
            SchemaAddColumnToEndOfTable(connection, tableName, columnDefinition);
        }
#pragma warning restore IDE0051 // Remove unused private members
        #endregion
    }
}