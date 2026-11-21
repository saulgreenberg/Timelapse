using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;
using System.Windows;
using System.Windows.Threading;
using Timelapse.Database;
using Timelapse.SearchingAndSorting;

namespace Timelapse.Dialog
{
    /// <summary>
    /// A console that displays the SQLite statements Timelapse executes, or the WHERE expressions created from the selection criteria.
    /// It is only traced while the console is visible: hiding it detaches it from SQLiteWrapper and CustomSelection.
    /// It runs on its own UI thread (see TimelapseWindow.MenuItemSelectSqliteConsole_Click), so modal dialogs don't block it; hence it has no owner.
    /// Statements may arrive on any thread, so they are queued as is, then formatted and appended to the console on its own thread a few times a second.
    /// This keeps the work done on the threads executing the statements to a minimum.
    /// </summary>
    public partial class SqliteConsole
    {
        #region Private Variables
        private enum ShowWhat
        {
            SelectOnly,
            All,
            WhereOnly
        }

        // Read from the threads that execute statements, so it is volatile
        private volatile ShowWhat showWhat = ShowWhat.All;

        // Statements and expressions not yet shown, unformatted. An empty string stands for an empty WHERE expression
        private readonly ConcurrentQueue<string> pending = new();
        private readonly DispatcherTimer flushTimer;

        // The console keeps only roughly this many of the most recent characters, so it doesn't grow without limit
        private const int MaxConsoleLength = 1_000_000;

        // The number of characters in the console, kept so its (possibly very long) text needn't be read to check its length
        private int consoleLength;

        // Longer statements (e.g., inserts of many rows) are shortened, as formatting and showing them in full would slow the console down
        private const int MaxStatementLength = 5_000;

        private const string EmptyWhereExpression = "(empty: no selection criteria)";
        #endregion

        #region Constructor, Loading, Showing and Closing
        public SqliteConsole()
        {
            InitializeComponent();
            flushTimer = new(DispatcherPriority.Background, Dispatcher)
            {
                Interval = TimeSpan.FromMilliseconds(200)
            };
            flushTimer.Tick += FlushTimer_Tick;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            Message.BuildContentFromProperties();
        }

        // Trace only while the console is visible
        private void Window_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (IsVisible)
            {
                SQLiteWrapper.StatementExecuted = OnStatementExecuted;
                CustomSelection.WhereExpressionCreated = OnWhereExpressionCreated;
                flushTimer.Start();
            }
            else
            {
                SQLiteWrapper.StatementExecuted = null;
                CustomSelection.WhereExpressionCreated = null;
                flushTimer.Stop();
                Flush();
            }
        }

        // Reuse the window by changing closing to hiding, so the console keeps its contents
        private void Window_Closing(object sender, CancelEventArgs e)
        {
            e.Cancel = true;
            Hide();
        }
        #endregion

        #region Receiving statements and expressions
        private void ShowWhat_Checked(object sender, RoutedEventArgs e)
        {
            // Which radio button was checked: a reference comparison, as intended
            showWhat = ReferenceEquals(sender, RBSelectOnly)
                ? ShowWhat.SelectOnly
                : ReferenceEquals(sender, RBWhereOnly)
                    ? ShowWhat.WhereOnly
                    : ShowWhat.All;
        }

        // Called by SQLiteWrapper, on the thread executing the statement. It only filters and queues the statement, which is formatted later
        private void OnStatementExecuted(string statement)
        {
            ShowWhat current = showWhat;
            if (current == ShowWhat.WhereOnly || string.IsNullOrWhiteSpace(statement) || IsSqliteLibrarySchemaQuery(statement))
            {
                return;
            }
            if (current == ShowWhat.SelectOnly && false == statement.TrimStart().StartsWith("SELECT", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }
            pending.Enqueue(statement);
        }

        // Whether the statement is one the SQLite library runs itself to describe a result's columns, which it does when a query's
        // results are loaded into a DataTable (DataTable.Load calls GetSchemaTable): several per column, e.g.,
        // SELECT * FROM [main].[sqlite_master] WHERE [type] LIKE 'table', and PRAGMA [main].table_info([DataTable]).
        // Timelapse never writes SQL in this bracketed form, so these are left out as noise
        private static bool IsSqliteLibrarySchemaQuery(string statement)
        {
            return statement.Contains("[main].[sqlite_master]", StringComparison.OrdinalIgnoreCase)
                   || statement.Contains("[main].[sqlite_temp_master]", StringComparison.OrdinalIgnoreCase)
                   || statement.TrimStart().StartsWith("PRAGMA [main].", StringComparison.OrdinalIgnoreCase);
        }

        // Called by CustomSelection.GetFilesWhere each time it creates a WHERE expression
        private void OnWhereExpressionCreated(string where)
        {
            if (showWhat != ShowWhat.WhereOnly)
            {
                return;
            }
            pending.Enqueue(string.IsNullOrWhiteSpace(where) ? string.Empty : where);
        }
        #endregion

        #region Clearing the console
        // Clear the console, including any statements not yet shown
        private void ButtonClear_Click(object sender, RoutedEventArgs e)
        {
            pending.Clear();
            TextBoxConsole.Clear();
            consoleLength = 0;
        }
        #endregion

        #region Appending to the console
        private void FlushTimer_Tick(object sender, EventArgs e)
        {
            Flush();
        }

        // Format the queued statements and append them, each followed by a blank line, and scroll them into view.
        // If more arrived than the console keeps (e.g., during a bulk operation), the earlier ones are skipped without being formatted
        private void Flush()
        {
            if (pending.IsEmpty)
            {
                return;
            }
            List<string> statements = [];
            while (pending.TryDequeue(out string statement))
            {
                statements.Add(statement);
            }

            // Work back from the most recent statement to find the first one that fits in the console
            int first = statements.Count;
            int length = 0;
            while (first > 0 && length < MaxConsoleLength)
            {
                first--;
                length += Math.Min(statements[first].Length, MaxStatementLength) + 4;
            }

            StringBuilder text = new();
            if (first > 0)
            {
                text.Append($"({first:N0} earlier statements not shown, as there were too many to keep)").Append(Environment.NewLine).Append(Environment.NewLine);
            }
            for (int i = first; i < statements.Count; i++)
            {
                text.Append(statements[i].Length == 0 ? EmptyWhereExpression : FormatSql(statements[i]))
                    .Append(Environment.NewLine).Append(Environment.NewLine);
            }

            TextBoxConsole.AppendText(text.ToString());
            consoleLength += text.Length;

            // While text is selected, leave it alone: don't trim the console (which would lose the selection)
            // or scroll away from it. Both resume once nothing is selected
            if (TextBoxConsole.SelectionLength > 0)
            {
                return;
            }
            if (consoleLength > MaxConsoleLength)
            {
                TextBoxConsole.Text = TextBoxConsole.Text[^MaxConsoleLength..];
                consoleLength = MaxConsoleLength;
            }
            TextBoxConsole.ScrollToEnd();
        }
        #endregion

        #region Formatting
        // Lay out a SQL statement for reading: each clause (FROM, WHERE, ORDER BY...) starts a new line,
        // and AND / OR start new lines when they aren't nested deeper than one level of parentheses (except the AND of BETWEEN x AND y).
        // Runs of white space are collapsed. Text inside quotes is left as is.
        private static string FormatSql(string sql)
        {
            sql = sql.Trim();
            string suffix = string.Empty;
            if (sql.Length > MaxStatementLength)
            {
                suffix = $" ... ({sql.Length - MaxStatementLength:N0} more characters)";
                sql = sql[..MaxStatementLength];
            }

            string[] clauses = ["FROM", "WHERE", "GROUP BY", "HAVING", "ORDER BY", "LIMIT", "OFFSET", "INNER JOIN", "LEFT JOIN", "CROSS JOIN", "UNION", "VALUES", "SET"];
            string[] conjunctions = ["AND", "OR"];

            StringBuilder result = new();
            char quote = '\0';
            int depth = 0;
            bool inBetween = false;
            int i = 0;
            while (i < sql.Length)
            {
                char c = sql[i];

                // Copy quoted text unchanged
                if (quote != '\0')
                {
                    result.Append(c);
                    if (c == quote)
                    {
                        quote = '\0';
                    }
                    i++;
                    continue;
                }
                if (c is '\'' or '"')
                {
                    quote = c;
                    result.Append(c);
                    i++;
                    continue;
                }

                // Collapse white space
                if (char.IsWhiteSpace(c))
                {
                    if (result.Length > 0 && false == char.IsWhiteSpace(result[^1]))
                    {
                        result.Append(' ');
                    }
                    i++;
                    continue;
                }

                if (c == '(')
                {
                    depth++;
                }
                else if (c == ')' && depth > 0)
                {
                    depth--;
                }

                // At the start of a word, break the line before a clause keyword, or before AND / OR near the top level
                bool atWordStart = i == 0 || false == (char.IsLetterOrDigit(sql[i - 1]) || sql[i - 1] == '_');
                if (atWordStart && char.IsLetter(c))
                {
                    if (MatchKeyword(sql, i, ["BETWEEN"]) != null)
                    {
                        inBetween = true;
                    }
                    else if (inBetween && MatchKeyword(sql, i, ["AND"]) != null)
                    {
                        // The AND of BETWEEN x AND y stays on the same line
                        inBetween = false;
                        result.Append(sql, i, 3);
                        i += 3;
                        continue;
                    }
                    string keyword = MatchKeyword(sql, i, clauses);
                    bool isConjunction = false;
                    if (keyword == null && depth <= 1)
                    {
                        keyword = MatchKeyword(sql, i, conjunctions);
                        isConjunction = keyword != null;
                    }
                    if (keyword != null)
                    {
                        // Trim the space before the keyword, then start it on a new line (conjunctions indented under their clause)
                        while (result.Length > 0 && result[^1] == ' ')
                        {
                            result.Length--;
                        }
                        if (result.Length > 0)
                        {
                            result.Append(Environment.NewLine).Append(isConjunction ? "    " : string.Empty);
                        }
                        result.Append(sql, i, keyword.Length);
                        i += keyword.Length;
                        continue;
                    }
                }

                result.Append(c);
                i++;
            }
            return result + suffix;
        }

        // The keyword (as given) found at position start, if it is a whole word there (ignoring case and the white space between its words)
        private static string MatchKeyword(string sql, int start, string[] keywords)
        {
            foreach (string keyword in keywords)
            {
                if (start + keyword.Length <= sql.Length &&
                    string.Compare(sql, start, keyword, 0, keyword.Length, StringComparison.OrdinalIgnoreCase) == 0)
                {
                    int end = start + keyword.Length;
                    if (end == sql.Length || false == (char.IsLetterOrDigit(sql[end]) || sql[end] == '_'))
                    {
                        return keyword;
                    }
                }
            }
            return null;
        }
        #endregion
    }
}
