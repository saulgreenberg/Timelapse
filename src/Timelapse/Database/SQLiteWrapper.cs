using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Data.SQLite;
using System.Diagnostics;
using System.Linq;
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

    // Note. Some methods set an error state, others return an error, which can be examined from a calling method far up the stack.
    // The issue is doing it in the right place, as dialogs and other actions lower down the stack may be triggered.
    // Thus we need to do it quite selectively.
    // This is an example of how we can check it far up the stack
    // if (SqlErrorState.HasError)
    //{
    //    SqlOperationResult.GenerateExceptionDialog(SqlErrorState.SqlOperationResult, "MenuItemLoadImages_ClickAsync");
    //    SqlErrorState.Reset();
    //}
    // This is an example of how we can check it from a returned value
    //SqlOperationResult result = Database.Update(DBTables.FileData, Constant.DatabaseColumn.ID, listOfIDs, dataLabel, value);
    //if (!result.Success)
    //{
    //    SqlOperationResult.GenerateExceptionDialog(result, methodName);
    //}
public class SQLiteWrapper
    {
        // A connection string identifying the  database file. Takes the form:
        // "Data Source=filepath" 
        public string ConnectionString { get; }
        public string FilePath { get; }

        /// <summary>
        /// Optional callback invoked when a read method catches an exception.
        /// Parameters: context (method name), failing SQL statement (may be null).
        /// Subscribers can record the failure for later reporting (e.g. via <see cref="Database.SqlErrorState"/>).
        /// In Debug builds, <c>Debug.Fail</c> fires first; this callback is still invoked afterwards.
        /// </summary>
        public static Action<string, SqlOperationResult> OnReadError { get; set; }

        // Ensures only the first SQL read error across all concurrent threads raises the dialog.
#if !DEBUG
        private static int _errorFired;
#endif

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
        public SqlOperationResult CreateTable(string tableName, List<SchemaColumnDefinition> columnDefinitions)
        {
            // Check the arguments for null
            ThrowIf.IsNullArgument(columnDefinitions, nameof(columnDefinitions));

            // Just in case the table exists, we will want to remove it before trying to create it.
            if (TableExists(tableName))
            {
                SqlOperationResult dropResult = DropTable(tableName);
                if (!dropResult.Success) return dropResult;
            }

            string query = $"{Sql.CreateTable} {tableName} {Sql.OpenParenthesis} {Environment.NewLine}"; // CREATE TABLE <tablename> (
            foreach (SchemaColumnDefinition column in columnDefinitions)
            {
                query += $"{column} {Sql.Comma}{Environment.NewLine}";                                 // "columnname TEXT DEFAULT 'value',\n" or similar
            }
            query = query.Remove(query.Length - Sql.Comma.Length - Environment.NewLine.Length);          // remove last comma / new line
            query += $"{Sql.CloseParenthesis} {Sql.Semicolon}";                                          // );
            return ExecuteNonQueryWithRollback(query);
        }
        #endregion

        #region Indexes: Create or Drop (Public)
        // Creates or drops various indexes in table tableName named index name to the column names

        // ReSharper disable once UnusedMember.Global
        public bool IndexExists(string indexName)
        {
            return 0 != ScalarGetScalarFromSelectAsInt(Sql.SelectCountFromSqliteMasterWhereTypeEqualIndexAndNameEquals + Sql.Quote(indexName));
        }

        // Drop a single index named indexName if it exists
        // ReSharper disable once UnusedMember.Global
        public void IndexDropIfExists(string indexName)
        {
            // Form: DROP INDEX IF EXISTS indexName 
            string query = Sql.DropIndex + Sql.IfExists + indexName;
            ExecuteNonQueryWithRollback(query);
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
            ExecuteNonQueryWithRollback(query);
        }


        // Create multiple indexes wrapped in a begin / end 
        // Each tuple provides the indexName, tableName, and columnNames
        public void IndexCreateIfNotExists(List<Tuple<string, string, string>> tuples)
        {
            List<string> queries = [];
            foreach (Tuple<string, string, string> tuple in tuples)
            {
                queries.Add(Sql.CreateIndex + Sql.IfNotExists + tuple.Item1 + Sql.On + tuple.Item2 + Sql.OpenParenthesis + tuple.Item3 + Sql.CloseParenthesis);
            }
            ExecuteNonQueryWithRollback(queries);
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
#if DEBUG
                Debug.Fail($"SQL read failure in GetDataTableFromSelect: {exception.Message}\nQuery: {query}");
#else
                if (Interlocked.Exchange(ref _errorFired, 1) == 0)
                    OnReadError?.Invoke("GetDataTableFromSelect", new SqlOperationResult
                    {
                        Context="GetDataTableFromSelect",
                        ErrorMessage="SqlStatementFailure",
                        FailingStatement=query,
                        Exception = exception
                    });
#endif
                return dataTable;
            }
        }

        // Async version with cancellation support. Cannot delegate to the sync version because
        // cancellation requires access to the command object to call command.Cancel().
        // When the token fires, Cancel() signals SQLite to abort the current operation and throw
        // a SQLiteException; the exception filter 'when (token.IsCancellationRequested)' catches
        // that silently. Genuine SQL errors fall through to the second catch as normal.
        public async Task<DataTable> GetDataTableFromSelectAsync(string query, CancellationToken token = default)
        {
            return await Task.Run(() =>
            {
                DataTable dataTable = new();
                try
                {
                    using SQLiteConnection connection = GetNewSqliteConnection(ConnectionString);
                    connection.Open();
                    SQLiteCommand command = new(connection);
                    try
                    {
                        command.CommandText = query;
                        // ReSharper disable once AccessToDisposedClosure
                        using (token.Register(() => command.Cancel()))
                        {
                            using SQLiteDataReader reader = command.ExecuteReader();
                            dataTable.Columns.CollectionChanged += DataTableColumns_Changed;
                            dataTable.Load(reader);
                        }
                    }
                    finally
                    {
                        command.Dispose();
                    }
                    return dataTable;
                }
                catch (Exception) when (token.IsCancellationRequested)
                {
                    return dataTable;
                }
                catch (Exception exception)
                {
#if DEBUG
                    Debug.Fail($"SQL read failure in GetDataTableFromSelectAsync: {exception.Message}\nQuery: {query}");
#else
                    if (Interlocked.Exchange(ref _errorFired, 1) == 0)
                        OnReadError?.Invoke("GetDataTableFromSelectAsync", new SqlOperationResult
                        {
                            Context = "GetDataTableFromSelectAsync",
                            ErrorMessage = "SqlStatementFailure",
                            FailingStatement = query,
                            Exception = exception
                        });
#endif
                    return dataTable;
                }
            }, token);
        }

        public List<object> GetDistinctValuesInColumn(string tableName, string columnName)
        {
            List<object> distinctValues = [];
#if !DEBUG
            string lastQuery = string.Empty;
#endif
            try
            {
                using SQLiteConnection connection = GetNewSqliteConnection(ConnectionString);
                connection.Open();
                using SQLiteCommand command = new(connection);
                command.CommandText = String.Format(Sql.SelectDistinct + " {0} " + Sql.From + "{1}", columnName, tableName);
#if !DEBUG
                lastQuery = command.CommandText;
#endif
                using SQLiteDataReader reader = command.ExecuteReader();
                while (reader.Read())
                {
                    distinctValues.Add(reader[columnName]);
                }
                return distinctValues;
            }
            catch (Exception exception)
            {
#if DEBUG
                Debug.Fail($"SQL read failure in GetDistinctValuesInColumn (table '{tableName}', column '{columnName}'): {exception.Message}");
#else
                if (Interlocked.Exchange(ref _errorFired, 1) == 0)
                    OnReadError?.Invoke("GetDistinctValuesInColumn", new SqlOperationResult
                    {
                        Context = "GetDistinctValuesInColumn",
                        ErrorMessage = "SqlStatementFailure",
                        FailingStatement = lastQuery,
                        Exception = exception
                    });
#endif
                return distinctValues;
            }
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
#if DEBUG
                Debug.Fail($"SQL read failure in GetScalarFromSelect: {exception.Message}\nQuery: {query}");
#else
                if (Interlocked.Exchange(ref _errorFired, 1) == 0)
                    OnReadError?.Invoke("GetScalarFromSelect", new SqlOperationResult
                    {
                        Context = "GetScalarFromSelect",
                        ErrorMessage = "SqlStatementFailure",
                        FailingStatement = query,
                        Exception = exception
                    });
#endif
                return null;
            }
        }
#endregion

        #region Insert
        // Construct each individual query in the form 
        // INSERT INTO table_name
        //      colname1, colname12, ... colnameN VALUES
        //      ('value1', 'value2', ... 'valueN');
        public SqlOperationResult Insert(string tableName, List<List<ColumnTuple>> insertionStatements)
        {
            return Insert(tableName, insertionStatements, null, string.Empty, 1000);
        }

        public SqlOperationResult Insert(string tableName, List<List<ColumnTuple>> insertionStatements, IProgress<ProgressBarArguments> progress, string progressString, int progressFrequency)
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
            return ExecuteNonQueryWithRollback(queries, progress, progressString, progressFrequency);
        }
        #endregion

        #region Upsert
        // Upsert Row is a limited form of upsert that acts on a single row in a given table.
        // As required by Sqlite upserts, The OnConflict target (i.e, the whereName) must be a Primary Key (or Unique) in the schema
        // The column names and values to update or insert are provided in the column tuples. 
        // The primaryKeyTuple must be the primary key and its value (which are used to detect conflict, and the indicate Where, and to include as name/value in the insert portion)
        public SqlOperationResult UpsertRow(string tableName, ColumnTuple primaryKeyTuple, List<ColumnTuple> columnTuples)
        {
            // Check the arguments for null
            ThrowIf.IsNullArgument(columnTuples, nameof(columnTuples));
            if (columnTuples.Count == 0) return SqlOperationResult.Ok();
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

            return ExecuteNonQueryWithRollback(query);
        }
        #endregion

        #region Update data
        // Set all rows in a given table's column to a single value
        //  Form, e.g., to set all column values to foobar:
        //  -- Update tableName Set columnName = 'foobar';
        public SqlOperationResult SetColumnToACommonValue(string tableName, string columnName, string value)
        {
            string query = Sql.Update + tableName + Sql.Set + columnName + Sql.Equal + Sql.Quote(value);
            return ExecuteNonQueryWithRollback(query);
        }

        // Trims all the white space from the data held in the list of column_names in table_name
        // This allows us to trim data in the database after the fact.
        // Form:
        // -- UPDATE tablename SET columname = TRIM(columnname);
        // ReSharper disable once UnusedMember.Global
        public SqlOperationResult TrimWhitespace(string tableName, List<string> columnNames)
        {
            // Check the arguments for null 
            ThrowIf.IsNullArgument(columnNames, nameof(columnNames));

            List<string> queries = [];
            foreach (string columnName in columnNames)
            {
                string query = Sql.Update + tableName + Sql.Set + columnName + Sql.Equal + Sql.Trim + Sql.OpenParenthesis + columnName + Sql.CloseParenthesis + Sql.Semicolon;
                queries.Add(query);
            }
            return ExecuteNonQueryWithRollback(queries);
        }

        // Update particular column values in the table, where
        // -- keys in the dictionary indicates which of the current values in the column should be replaced
        // -- each key's value indicates the new value that should replace the current value.
        // Note that this could also be done using ColumnTuplesWithWhere, but this is perhaps a simpler way to compose this
        // Form of each update query generated for each key/value dictionary pair (eg., key="Ok"' value="true" :
        // -- Update tableName Set columnName = 'true' where columnName = 'Ok'
        // ReSharper disable once UnusedMember.Global
        public SqlOperationResult UpdateParticularColumnValuesWithNewValues(string tableName, string columnName, Dictionary<string, string> currentValue_newValuePair)
        {
            List<string> queries = [];
            foreach (KeyValuePair<string, string> kvp in currentValue_newValuePair)
            {
                string query = Sql.Update + tableName + Sql.Set + columnName + Sql.Equal + Sql.Quote(kvp.Value) + Sql.Where + columnName + Sql.Equal + Sql.Quote(kvp.Key) + Sql.Semicolon;
                queries.Add(query);
            }
            return ExecuteNonQueryWithRollback(queries);
        }

        // Convert all nulls in the list of column_names in table_name
        // Note: this is helpful for cases when the defaults were not set, as that could introduce null values. 
        // Form for each query generated for each provided column
        // -- UPDATE tablename SET columname = '' WHERE columnname IS NULL;
        // ReSharper disable once UnusedMember.Global
        public SqlOperationResult ChangeNullToEmptyString(string tableName, List<string> columnNames)
        {
            // Check the arguments for null 
            ThrowIf.IsNullArgument(columnNames, nameof(columnNames));

            List<string> queries = [];
            foreach (string columnName in columnNames)
            {
                string query = Sql.Update + tableName + Sql.Set + columnName + Sql.Equal + "''" + Sql.Where + columnName + Sql.IsNull + Sql.Semicolon; // Form: UPDATE tablename SET columname = '' WHERE columnname IS NULL;
                queries.Add(query);
            }
            return ExecuteNonQueryWithRollback(queries);
        }

        public SqlOperationResult Update(string tableName, List<ColumnTuplesWithWhere> updateQueryList)
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
            return ExecuteNonQueryWithRollback(queries);
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
        public SqlOperationResult Update(string tableName, ColumnTuplesWithWhere columnsToUpdate)
        {
            // Check the arguments for null
            ThrowIf.IsNullArgument(columnsToUpdate, nameof(columnsToUpdate));

            string query = CreateUpdateQuery(tableName, columnsToUpdate);
            return ExecuteNonQueryWithRollback(query);
        }

        // UPDATE table_name SET
        // columnname = value,
        public SqlOperationResult Update(string tableName, ColumnTuple columnToUpdate)
        {
            // Check the arguments for null
            ThrowIf.IsNullArgument(columnToUpdate, nameof(columnToUpdate));

            string query = Sql.Update + tableName + Sql.Set;
            query += $" {columnToUpdate.Name} = {Sql.Quote(columnToUpdate.Value)}";
            return ExecuteNonQueryWithRollback(query);
        }

        // Efficient update for a list of IDs
        // Rather than having an update for every single ID, we look for contiguous and non-contiguous IDs
        // The SQL expression will look something like:
        // Update tableName SET columnName = value
        // WHERE columnName BETWEEN x AND y
        // OR columnName IN (a,b,c)
        // the above are repeated as needed whenever contiguous and non-contiguous ids are found
        // For example:
        // UPDATE DataTable SET CamID = '10'
        // WHERE Id BETWEEN 1 AND 870798
        // OR Id BETWEEN 870800 AND 870801
        // OR Id BETWEEN 870809 AND 909567
        // OR Id IN (870803,870806)
        // To avoid SQLite's expression tree depth limit (~1000 nodes), the WHERE clause is built
        // in clause-count-limited chunks. Each chunk becomes one UPDATE statement, and all
        // statements run inside a single BEGIN/END transaction for atomicity.
        // Crucially, the chunk boundary is based on the number of OR-joined conditions, not the
        // number of IDs — so a single BETWEEN covering 50,000 contiguous IDs still counts as
        // one condition and requires only one query.
        public SqlOperationResult Update(string tableName, string IDColumnName, List<long> listOfIDs, string columnName, string value)
        {
            if (listOfIDs.Count == 0)
            {
                // nothing to update
                return SqlOperationResult.Ok();
            }

            const int maxClausesPerQuery = 500;
            List<long> sorted = listOfIDs.OrderBy(id => id).ToList();
            List<string> queries = [];
            int startIndex = 0;
            while (startIndex < sorted.Count)
            {
                string whereClause = BuildWhereListofIds(IDColumnName, sorted, startIndex, maxClausesPerQuery, out int nextIndex);
                queries.Add($"{Sql.Update}{tableName}{Sql.Set}{columnName} = {Sql.Quote(value)}{Sql.Where}{whereClause}");
                startIndex = nextIndex;
            }

            return ExecuteNonQueryWithRollback(queries);
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
        public SqlOperationResult DeleteRows(string tableName, string where = "")
        {
            // DELETE FROM table_name WHERE where
            string query = Sql.DeleteFrom + tableName;        // DELETE FROM table_name
            if (!string.IsNullOrWhiteSpace(where))
            {
                // Add the WHERE clause only when where is not empty
                query += Sql.Where;                   // WHERE
                query += where;                                 // where
            }
            return ExecuteNonQueryWithRollback(query);
        }

        /// <summary>
        /// Delete one or more rows from the DB, where each row is specified in the list of where clauses ..
        /// </summary>
        /// <param name="tableName">The table from which to delete</param>
        /// <param name="whereClauses">The where clauses for the row to delete (e.g., ID=1 ID=3 etc</param>
        public SqlOperationResult Delete(string tableName, List<string> whereClauses)
        {
            // Check the arguments for null
            ThrowIf.IsNullArgument(whereClauses, nameof(whereClauses));

            if (false == TableExists(tableName))
            {
                TracePrint.PrintMessage($"CommonDatabase.Delete: Could not delete rows from Table {tableName} as the table does not exist");
                return SqlOperationResult.Ok();
            }
            List<string> queries = [];                      // A list of SQL queries

            // Construct a list containing queries of the form DELETE FROM table_name WHERE where
            foreach (string whereClause in whereClauses)
            {
                // Add the WHERE clause only when it is not empty
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
                return ExecuteNonQueryWithRollback(queries);
            }
            return SqlOperationResult.Ok();
        }

        /// <summary>
        /// Delete all the rows in each table in the provided list 
        /// </summary>
        /// <param name="tables"></param>
        public SqlOperationResult DeleteAllRowsInTables(List<string> tables)
        {
            if (tables == null || tables.Count == 0)
            {
                return SqlOperationResult.Ok();
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
            return ExecuteNonQueryWithRollback(queries);
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
        public bool ScalarBoolFromOneOrZero(string query)
        {
            object obj = GetScalarFromSelect(query);
            if (obj == null || obj == DBNull.Value)
            {
                return false;
            }
            return Convert.ToInt32(obj) == 1;
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

        #region Execute Non-Queries (unified: single statement or list, always with rollback)

        /// <summary>
        /// Executes a single SQL statement wrapped in a transaction, rolling back on failure.
        /// If busyTimeoutMs is greater than zero, SQLite will retry for that many milliseconds
        /// when the database is locked instead of failing immediately. Only use busyTimeoutMs
        /// for standalone operations (e.g. DROP TABLE cleanup) with no dependent follow-on
        /// statements, as the delay breaks ordering guarantees for callers that execute a logical
        /// sequence outside of a transaction.
        /// </summary>
        /// <returns>
        /// <see cref="SqlOperationResult.Ok()"/> on success, or
        /// <see cref="SqlOperationResult.Fail"/> carrying the error message, exception, and the
        /// failing SQL statement on failure.
        /// </returns>
        public SqlOperationResult ExecuteNonQueryWithRollback(string commandString, int busyTimeoutMs = 0)
        {
            if (string.IsNullOrWhiteSpace(commandString))
            {
                return SqlOperationResult.Ok();
            }
            return ExecuteNonQueryWithRollbackCore([commandString], null, string.Empty, 0, busyTimeoutMs);
        }

        /// <summary>
        /// Executes a list of SQL statements in a single transaction, rolling back all of them on failure.
        /// </summary>
        /// <returns>
        /// <see cref="SqlOperationResult.Ok()"/> on success, or
        /// <see cref="SqlOperationResult.Fail"/> carrying the error message, exception, and the
        /// last-executing SQL statement on failure.
        /// </returns>
        public SqlOperationResult ExecuteNonQueryWithRollback(List<string> statements)
        {
            ThrowIf.IsNullArgument(statements, nameof(statements));
            return ExecuteNonQueryWithRollbackCore(statements);
        }

        /// <summary>
        /// Executes a list of SQL statements in a single transaction with progress reporting,
        /// rolling back all of them on failure. Progress is reported every progressFrequency statements.
        /// </summary>
        /// <returns>
        /// <see cref="SqlOperationResult.Ok()"/> on success, or
        /// <see cref="SqlOperationResult.Fail"/> carrying the error message, exception, and the
        /// last-executing SQL statement on failure.
        /// </returns>
        public SqlOperationResult ExecuteNonQueryWithRollback(List<string> statements, IProgress<ProgressBarArguments> progress, string progressString, int progressFrequency)
        {
            ThrowIf.IsNullArgument(statements, nameof(statements));
            return ExecuteNonQueryWithRollbackCore(statements, progress, progressString, progressFrequency);
        }

        /// <summary>
        /// Core implementation shared by all ExecuteNonQueryWithRollback overloads.
        /// Opens a connection, begins a single transaction, executes all statements, and commits.
        /// On failure, rolls back the entire transaction — nothing is committed — and returns a
        /// <see cref="SqlOperationResult"/> that includes the error details and the SQL statement
        /// that was executing when the failure occurred. The failing statement is preserved in full
        /// (untruncated) so it can be included in a bug report emailed by the user.
        /// The connection is declared outside the try block so the catch block can call Rollback
        /// on the still-open connection. SQLite auto-detaches any attached databases when the
        /// connection closes, so no explicit DETACH is needed on failure.
        /// </summary>
        private SqlOperationResult ExecuteNonQueryWithRollbackCore(
            IReadOnlyList<string> statements,
            IProgress<ProgressBarArguments> progress = null,
            string progressString = "",
            int progressFrequency = 0,
            int busyTimeoutMs = 0)
        {
            if (statements == null || statements.Count == 0)
            {
                return SqlOperationResult.Ok();
            }
            // ReSharper disable once RedundantAssignment
            string mostRecentStatement = null;
            // Declared outside try so the catch block can call Rollback on the still-open connection
            // if an exception is thrown mid-transaction.
            using SQLiteConnection connection = GetNewSqliteConnection(ConnectionString);
            connection.Open();
            if (busyTimeoutMs > 0)
            {
                connection.BusyTimeout = busyTimeoutMs;
            }
            SQLiteTransaction transaction = null;
            try
            {
                transaction = connection.BeginTransaction();

                if (progress != null)
                {
                    progress.Report(new(0, progressString, false, true));
                    Thread.Sleep(ThrottleValues.RenderingBackoffTime);  // Allows the UI thread to update every now and then
                }

                using SQLiteCommand command = new(connection);
                command.Transaction = transaction;
                int statementsCount = statements.Count;
                int i = 0;
                foreach (string statement in statements)
                {
                    if (progress != null && progressFrequency > 0 && i % progressFrequency == 0)
                    {
                        int percent = Convert.ToInt32(i * 100.0 / statementsCount);
                        progress.Report(new(percent,
                            $"{progressString} ({i:N0}/{statementsCount:N0})...", false, false));
                        Thread.Sleep(ThrottleValues.RenderingBackoffTime);  // Allows the UI thread to update every now and then
                    }
                    // Track the current statement so it is available in the catch block.
                    // For single-statement calls this is the statement itself; for multi-statement
                    // transactions it is the last statement executed before the exception, which is
                    // almost always the one that caused the failure.
                    // ReSharper disable once RedundantAssignment
                    mostRecentStatement = statement;
                    command.CommandText = statement;
                    // Note: It is more efficient to do it this way than to send
                    // a bunch of semicolon-separated statements as a single query
                    command.ExecuteNonQuery();
                    i++;
                }
                transaction.Commit();
                transaction.Dispose();
                return SqlOperationResult.Ok();
            }
            catch (Exception exception)
            {
                try { transaction?.Rollback(); }
                catch { /* connection may already be broken; rollback best-effort only */ }
                finally { transaction?.Dispose(); }
                // Truncate only for the live-debugging log message; the full statement is
                // preserved untruncated in SqlOperationResult.FailingStatement for bug reports.
                string excerpt = mostRecentStatement?.Length > 500
                    ? mostRecentStatement[..500] + "…"
                    : mostRecentStatement ?? "(none)";
                TracePrint.PrintMessage(
                    $"Failure near executing statement '{excerpt}' in ExecuteNonQueryWithRollbackCore: {exception}");
                return SqlOperationResult.Fail(
                    $"Failure near executing statement '{excerpt}'",
                    exception,
                    mostRecentStatement);
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
        /// Copy all the values from the source table into the destination table using a single schema for both sides.
        /// Use this when source and destination columns have the same names — the same column list appears in both
        /// the INSERT and SELECT clauses. Contrast with CopyAllValuesBetweenTables, which uses two separate schemas
        /// and matches columns positionally, allowing columns with different names (e.g. a rename) to be mapped.
        /// Note: silently aborts if the schema table has no columns.
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
        /// Copy all the values from the source table into the destination table using two separate schemas.
        /// The INSERT column list is derived from schemaFromDestinationTable and the SELECT column list from
        /// schemaFromSourceTable. SQLite matches them positionally, so this correctly handles cases where the
        /// source and destination have columns with different names at the same position (e.g. a column rename).
        /// Use CopyAllValuesFromTable instead when source and destination column names are identical.
        /// Note: unlike CopyAllValuesFromTable, this method has no empty-schema guard — an empty schema will
        /// produce malformed SQL and throw. This is intentional: an empty schema here indicates a genuine bug.
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
        public SqlOperationResult SchemaRenameTable(string originalTableName, string newTableName)
        {
            // Some basic error checking to make sure we can do the operation
            if (false == TableExists(originalTableName))
            {
                return SqlOperationResult.Ok();
            }

            try
            {
                using SQLiteConnection connection = GetNewSqliteConnection(ConnectionString);
                connection.Open();
                // Rename the table
                SchemaRenameTable(connection, originalTableName, newTableName);
                return SqlOperationResult.Ok();
            }
            catch (Exception exception)
            {
                TracePrint.PrintMessage($"Failure in SchemaRenameTable. {exception}");
                return SqlOperationResult.Fail("Failure in SchemaRenameTable", exception);
            }
        }

        public SqlOperationResult SchemaAlterTableWithNewColumnDefinitions(string sourceTable, List<SchemaColumnDefinition> columnDefinitions)
        {
            // A GUID suffix guarantees uniqueness regardless of the user's schema.
            string destTable = $"_TempTable_{Guid.NewGuid():N}";
            try
            {
                // Create an empty table with the schema based on columnDefinitions
                SqlOperationResult createResult = CreateTable(destTable, columnDefinitions);
                if (!createResult.Success) return createResult;

                using SQLiteConnection connection = GetNewSqliteConnection(ConnectionString);
                connection.Open();

                // copy the contents from the source table to the destination table
                CopyAllValuesFromTable(connection, destTable, sourceTable, destTable);

                // delete the source table, and rename the destination table so it is the same as the source table
                DropTable(connection, sourceTable);
                SchemaRenameTable(connection, destTable, sourceTable);
                return SqlOperationResult.Ok();
            }
            catch (Exception exception)
            {
                TracePrint.PrintMessage($"Failure in SchemaAlterTableWithNewColumnDefinitions. {exception}");
                return SqlOperationResult.Fail("Failure in SchemaAlterTableWithNewColumnDefinitions", exception);
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
#if DEBUG
                Debug.Fail($"SQL read failure in SchemaGetColumns (table '{tableName}'): {exception.Message}");
#else
                if (Interlocked.Exchange(ref _errorFired, 1) == 0)
                    OnReadError?.Invoke("SchemaGetColumns", new SqlOperationResult
                    {
                        Context = "SchemaGetColumns",
                        ErrorMessage = "SqlStatementFailure",
                        FailingStatement = string.Empty,
                        Exception = exception
                    });
#endif
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
#if DEBUG
                Debug.Fail($"SQL read failure in SchemaGetColumnsAndDefaultValues (table '{tableName}'): {exception.Message}");
#else
                if (Interlocked.Exchange(ref _errorFired, 1) == 0)
                    OnReadError?.Invoke("SchemaGetColumnsAndDefaultValues", new SqlOperationResult
                    {
                        Context = "SchemaGetColumnsAndDefaultValues",
                        ErrorMessage = "SqlStatementFailure",
                        FailingStatement = string.Empty,
                        Exception = exception
                    });
#endif
                return null;
            }
        }

        public bool SchemaIsColumnInTable(string sourceTable, string currentColumnName)
        {
#pragma warning disable CS0168 // Variable is declared but never used
            try
            {
                using SQLiteConnection connection = GetNewSqliteConnection(ConnectionString);
                connection.Open();
                List<string> currentColumnNames = GetSchemaColumnNamesAsList(connection, sourceTable);
                return currentColumnNames.Contains(currentColumnName);
            }
            catch (Exception exception)
            {
#if DEBUG
                Debug.Fail($"SQL read failure in SchemaIsColumnInTable (table '{sourceTable}', column '{currentColumnName}'): {exception.Message}");
#else
                if (Interlocked.Exchange(ref _errorFired, 1) == 0)
                    OnReadError?.Invoke("SchemaIsColumnInTable", null);
#endif
                return false;
            }
#pragma warning restore CS0168 // Variable is declared but never used
        }

        // This method will create a column in a table of type TEXT, where it is added to its end
        // It assumes that the value, if not empty, should be treated as the default value for that column
        public SqlOperationResult SchemaAddColumnToEndOfTable(string tableName, SchemaColumnDefinition columnDefinition)
        {
            // Check the arguments for null
            ThrowIf.IsNullArgument(columnDefinition, nameof(columnDefinition));

            return ExecuteNonQueryWithRollback(Sql.AlterTable + tableName + Sql.AddColumn + columnDefinition);
        }

        /// <summary>
        /// Add a column to the table named sourceTable at position columnNumber using the provided columnDefinition
        /// The value in columnDefinition is assumed to be the desired default value
        /// </summary>
        // ReSharper disable once UnusedMember.Global
        public SqlOperationResult SchemaAddColumnToTable(string tableName, int columnNumber, SchemaColumnDefinition columnDefinition)
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
                    SchemaAddColumnToEndOfTable(connection, tableName, columnDefinition.ToString());
                    return SqlOperationResult.Ok();
                }

                // We need to add a column elsewhere than the end. This requires us to
                // create a new schema, create a new table from that schema, copy data over to it, remove the old table
                // and rename the new table to the name of the old one.

                // Get a schema definition identical to the schema in the existing table,
                // but with a new column definition of type TEXT added at the given position, where the value is assumed to be the default value
                string newSchema = SchemaInsertColumn(connection, tableName, columnNumber, columnDefinition);

                // A GUID suffix guarantees the temp name is unique.
                string destTable = $"{tableName}_NEW_{Guid.NewGuid():N}";
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
                return SqlOperationResult.Ok();
            }
            catch (Exception exception)
            {
                TracePrint.PrintMessage($"Failure in SchemaAddColumnToTable. {exception}");
                return SqlOperationResult.Fail("Failure in SchemaAddColumnToTable", exception);
            }
        }

        public SqlOperationResult SchemaDeleteColumn(string sourceTable, string columnName)
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
                    return SqlOperationResult.Ok(); // The provided column name is empty — nothing to delete
                }
                List<string> columnNames = GetSchemaColumnNamesAsList(connection, sourceTable);
                if (!columnNames.Contains(columnName))
                {
                    return SqlOperationResult.Ok(); // Column doesn't exist — nothing to delete
                }

                // Get a schema definition identical to the schema in the existing table,
                // but with the column named columnName deleted from it
                string newSchema = SchemaRemoveColumn(connection, sourceTable, columnName);

                // Guarantees that the table name is unique
                string destTable = $"{sourceTable}_NEW_{Guid.NewGuid():N}";
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
                return SqlOperationResult.Ok();
            }
            catch (Exception exception)
            {
                TracePrint.PrintMessage($"Failure in SchemaDeleteColumn. {exception}");
                return SqlOperationResult.Fail("Failure in SchemaDeleteColumn", exception);
            }
        }

        // Rename a column in a table. This is just a simpler form of SchemaAlterColumn
        public SqlOperationResult SchemaRenameColumn(string sourceTable, string currentColumnName, string newColumnName)
        {
            Dictionary<SchemaAttributesEnum, string> attributes = new()
            {
                { SchemaAttributesEnum.Name, newColumnName }
            };
            return SchemaAlterColumn(sourceTable, currentColumnName, attributes);
        }

        // Alter the column schema
        // New attributes are given in the attributes dictionary, where the key indicates the schema field (e.g., Column name, type, NotNull, and Default) and the value is the new value for that field
        // If a field is not specified, jsut keep the old value.  
        public SqlOperationResult SchemaAlterColumn(string sourceTable, string currentColumnName, Dictionary<SchemaAttributesEnum, string> attributes)
        {
            // Pre-condition: empty column name is a programming error, not a runtime SQL failure
            if (string.IsNullOrWhiteSpace(currentColumnName))
            {
                throw new ArgumentOutOfRangeException(nameof(currentColumnName));
            }
            if (attributes.Count == 0)
            {
                // Nothing to change
                return SqlOperationResult.Ok();
            }
            try
            {
                string newColumnName = string.Empty;
                if (attributes.TryGetValue(SchemaAttributesEnum.Name, out string key))
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
                    // If its a name change, we have to ensure that name is valid and that it doesn't already exist
                    throw new ArgumentException($"Column '{newColumnName}' is already in use.");
                }
                string newSchema = SchemaCloneButAlterColumn(connection, sourceTable, currentColumnName, attributes);

                // Guarantees that the table name is unique
                string destTable = $"{sourceTable}_NEW_{Guid.NewGuid():N}";
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
                return SqlOperationResult.Ok();
            }
            catch (Exception exception)
            {
                TracePrint.PrintMessage($"Failure in SchemaAlterColumn. {exception}");
                return SqlOperationResult.Fail("Failure in SchemaAlterColumn", exception);
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
        public SqlOperationResult DropTable(string tableName)
        {
            try
            {
                using SQLiteConnection connection = GetNewSqliteConnection(ConnectionString);
                connection.Open();
                DropTable(connection, tableName);
                return SqlOperationResult.Ok();
            }
            catch (Exception exception)
            {
                TracePrint.PrintMessage($"Failure in DropTable for table '{tableName}'. {exception}");
                return SqlOperationResult.Fail($"Failure dropping table '{tableName}'", exception);
            }
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
        public SqlOperationResult Vacuum()
        {
            try
            {
                using SQLiteConnection connection = GetNewSqliteConnection(ConnectionString);
                connection.Open();
                using SQLiteCommand command = new(Sql.Vacuum, connection);
                command.ExecuteNonQuery();
                return SqlOperationResult.Ok();
            }
            catch (Exception exception)
            {
                TracePrint.PrintMessage($"Failure in Vacuum. {exception}");
                return SqlOperationResult.Fail("Failure in Vacuum", exception);
            }
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

        #region Query string Helpers
        // Build a WHERE clause by collapsing contiguous IDs into BETWEEN ranges
        // and collecting isolated IDs into an IN list.
        // For example: (assuming IDColumnName is Id
        // Id BETWEEN 237 AND 240 OR Id BETWEEN 253 AND 255 OR Id IN (244,248)
        // Handles edge cases,
        // e.g., if no continguous IDs exist, only the In clause is used
        //       if all are contiguos, only the Between clause is used
        public static string BuildWhereListofIds(string IDColumnName, List<long> listOfIDs)
        {
            List<long> sorted = listOfIDs.OrderBy(id => id).ToList();
            if (sorted.Count == 0)
            {
                return string.Empty;
            }

            // Build WHERE clause by collapsing contiguous IDs into BETWEEN ranges
            // and collecting isolated IDs into an IN list.
            List<string> conditions = [];
            List<long> singles = [];

            int i = 0;
            while (i < sorted.Count)
            {
                long rangeStart = sorted[i];
                long rangeEnd = rangeStart;
                while (i + 1 < sorted.Count && sorted[i + 1] == sorted[i] + 1)
                {
                    i++;
                    rangeEnd = sorted[i];
                }

                if (rangeEnd > rangeStart)
                {
                    conditions.Add($"{IDColumnName} BETWEEN {rangeStart} AND {rangeEnd}");
                }
                else
                {
                    singles.Add(rangeStart);
                }
                i++;
            }

            if (singles.Count == 1)
            {
                conditions.Add($"{IDColumnName} = {singles[0]}");
            }
            else if (singles.Count > 1)
            {
                conditions.Add($"{IDColumnName} IN ({string.Join(",", singles)})");
            }

            return $"{string.Join(" OR ", conditions)}";
        }

        // Chunked variant of BuildWhereListofIds for use when the full list may produce a WHERE
        // clause too large for SQLite's expression tree depth limit (~1000 nodes).
        // Works on a pre-sorted list and processes IDs starting at startIndex, emitting at most
        // maxClauses OR-joined conditions. Each BETWEEN range and each isolated Id = x counts as
        // one clause, so the expression tree cost is exactly conditions.Count OR-nodes deep.
        // Sets nextIndex to the first unprocessed position (== sortedIDs.Count when all done).
        public static string BuildWhereListofIds(string IDColumnName, List<long> sortedIDs, int startIndex, int maxClauses, out int nextIndex)
        {
            List<string> conditions = [];
            int i = startIndex;

            while (i < sortedIDs.Count && conditions.Count < maxClauses)
            {
                long rangeStart = sortedIDs[i];
                long rangeEnd = rangeStart;
                while (i + 1 < sortedIDs.Count && sortedIDs[i + 1] == sortedIDs[i] + 1)
                {
                    i++;
                    rangeEnd = sortedIDs[i];
                }

                conditions.Add(rangeEnd > rangeStart
                    ? $"{IDColumnName} BETWEEN {rangeStart} AND {rangeEnd}"
                    : $"{IDColumnName} = {rangeStart}");
                i++;
            }

            nextIndex = i;
            return string.Join(" OR ", conditions);
        }
        #endregion
    }
}