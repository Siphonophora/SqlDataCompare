using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using SqlDataCompare.Core.Models;

namespace SqlDataCompare.Core.Services
{
    public static class ComparisonTemplator
    {
        private enum ErrorType
        {
            Fatal,
            Warning,
        }

        public static string Create(ParsedSql assertSql, ParsedSql testSql, IEnumerable<ComparableColumn> comparableColumns)
        {
            var headerWidth = 100;
            var indentWidth = 4;
            var assertTable = "#Assert";
            var testTable = "#Test";
            var matchedTable = "#Matched";
            var discrepantTable = "#Discrepant";
            var extraTable = "#Extra";
            var missingTable = "#Missing";

            var keys = comparableColumns.Where(x => x.IsKey).OrderBy(x => x.ColumnOrder).Select(x => x.ColumnName);
            var commaKeys = string.Join(", ", keys);
            var orderedKeys = string.Join(", ", comparableColumns.Where(x => x.IsKey).OrderBy(x => x.ColumnOrder).Select(x => $"{x.ColumnName} {(x.SortDescending ? "desc" : string.Empty)}"));
            var columns = comparableColumns.Where(x => !x.IsKey).OrderBy(x => x.ColumnOrder).Select(x => x.ColumnName);
            var commaColuns = string.Join(", ", columns);

            var header = new StringBuilder();
            var body = new StringBuilder();

            AddBlockComment(
                header,
                "Sql Data Comparison Script - https://siphonophora.github.io/SqlDataCompare/",
                WrapLine("This script compares the results of the two queries below. The first query, called the 'Assert' query is assumed to be correct. The second query, called 'Test' is the new query we are testing. There are two main steps to this comparison:"),
                string.Empty,
                WrapLine($"  1. First, we check that the key(s) which were defined for this comparison are unique for each row. If this comparison fails, you will recieve an error and the output will show a summary of which key values existed multiple times.", 5),
                WrapLine($"  2. Once we have verified the keys are properly defined, we compare rows based on the keys. The report will define several output tables", 5),
                string.Empty,
                WrapLine($"     {matchedTable,-25} - Rows where all columns are identical."),
                WrapLine($"     {missingTable,-25} - Rows which exist in 'Assert' but are missing from 'Test'"),
                WrapLine($"     {extraTable,-25} - Rows which do not exist in 'Assert' but exist in 'Test'"),
                WrapLine($"     {discrepantTable,-25} - Rows where one or more columns are not equal"),
                WrapLine($"     {discrepantTable + "__ColumnName",-25} - Rows where a specific column is not equal."),
                string.Empty,
                $"Compared Keys: {commaKeys}",
                string.Empty,
                "Assert:",
                "-------",
                assertSql.Sql,
                string.Empty,
                "Test:",
                "-----",
                testSql.Sql);

            body.AppendLine("BEGIN TRAN -- Begin a transaction which is rolled back. This is done to guarentee no database changes occur.");

            AddBlockComment(
                body,
                "User Settings",
                "Any variables in this section can be edited to adjust the output of the results.",
                WrapLine("If your queries need to share variables, declare and initalize them here. Additionally, if they require the same temp table, that may be created in this section as well."));
            body.AppendLine("Declare @VerboseOutput bit = 0; --Set to 1 to see additional output.  \r\n\r\n");

            AddBlockComment(body, "Setup", "Scroll down to add 'into' to the selects");
            body.AppendLine(CreateErrorsTable());
            body.AppendLine(CreateStatsTable(assertTable, testTable));

            body.AppendLine("Declare @ErrorCount int; --Helper for later");

            body.AppendLine(GetIntoStatements(assertSql, assertTable));
            body.AppendLine();
            body.AppendLine(GetIntoStatements(testSql, testTable));

            AddBlockComment(
                body,
                "Perform Base Analysis",
                "In this we ensure the provided keys will allow the main analysis to work.",
                "If there are duplicate keys we cannot proceed because we might get many to many joins.");
            body.AppendLine("BEGIN TRY");

            body.AppendLine();
            body.AppendLine(CalculateStats(assertTable, keys));
            body.AppendLine();
            body.AppendLine(CalculateStats(testTable, keys));

            body.AppendLine(HaltOnErrors());

            AddBlockComment(
                body,
                "Perform Main Analysis",
                "In this section, we compare the two sets of selected results.");

            body.AppendLine(CreateSummaryTable());
            var outputTables = new List<(string Table, string OrderBy)>();

            string assert = "Assert";
            string test = "Test";

            outputTables.Add((matchedTable, orderedKeys));
            body.AppendLine();
            body.AppendLine($"Select {AliasColumns(assert, keys, true)}");
            body.AppendLineIf(columns.Any(), $"     , '' [ ] \r\n     , {AliasColumns(assert, columns, true)}");
            body.AppendLine($"Into {matchedTable}");
            body.AppendLine($"From {testTable} {test}                ");
            body.AppendLine($"Join {assertTable} {assert} on {JoinON(assert, test, keys)} ");
            body.AppendLineIf(columns.Any(), $"Where {WhereMatch(assert, test, columns)}");
            body.AppendLine();

            outputTables.Add((missingTable, orderedKeys));
            body.AppendLine();
            body.AppendLine($"Select {AliasColumns(assert, keys, true)}");
            body.AppendLineIf(columns.Any(), $", '' [ ] \r\n     , {AliasColumns(assert, columns)}");
            body.AppendLine($"Into {missingTable}");
            body.AppendLine($"From {assertTable} {assert}                ");
            body.AppendLine($"Left Join {testTable} {test} on {JoinON(assert, test, keys)} ");
            body.AppendLine($"Where {test}.{keys.First()} IS NULL");

            outputTables.Add((extraTable, orderedKeys));
            body.AppendLine();
            body.AppendLine($"Select {AliasColumns(test, keys, true)}");
            body.AppendLineIf(columns.Any(), $", '' [ ] \r\n     , {AliasColumns(test, columns)}");
            body.AppendLine($"Into {extraTable}");
            body.AppendLine($"From {testTable} {test}                ");
            body.AppendLine($"Left Join {assertTable} {assert} on {JoinON(assert, test, keys)} ");
            body.AppendLine($"Where {assert}.{keys.First()} IS NULL");
            body.AppendLine();

            if (columns.Any())
            {
                outputTables.Add((discrepantTable, orderedKeys));
                body.AppendLine();
                body.AppendLine($"Select {AliasColumns(assert, keys, true)}\r\n     , '' [ ] \r\n     , {CompareColumns(assert, test, columns)}");
                body.AppendLine($"Into {discrepantTable}");
                body.AppendLine($"From {testTable} {test}                ");
                body.AppendLine($"Join {assertTable} {assert} on {JoinON(assert, test, keys)} ");
                body.AppendLine($"Where {WhereNotMatch(assert, test, columns)}");
                body.AppendLine();

                foreach (var column in columns)
                {
                    var discrepantDetailTable = $"{discrepantTable}__{column}";
                    var columnDisplay = columns.Where(x => x != column).ToList();
                    columnDisplay.Insert(0, column);

                    var desc = comparableColumns.Single(x => x.ColumnName == column).SortDescending ? " desc" : string.Empty;
                    outputTables.Add((discrepantDetailTable, $"[{assert} {column}]{desc}, [{test} {column}]{desc}"));
                    body.AppendLine();
                    body.AppendLine($"Select {AliasColumns(assert, keys, true)}\r\n     , '' [ ] \r\n     , {CompareColumns(assert, test, columnDisplay.ToArray())}");
                    body.AppendLine($"Into {discrepantDetailTable}");
                    body.AppendLine($"From {testTable} {test}                ");
                    body.AppendLine($"Join {assertTable} {assert} on {JoinON(assert, test, keys)} ");
                    body.AppendLine($"Where {WhereNotMatch(assert, test, new string[] { column })}");
                    body.AppendLine();
                }
            }
            else
            {
                body.AppendLine("Print('All columns selected are keys, so rows cannot be discrepant. They can only match, be extra or missing')");
            }

            body.AppendLine(SummarizeOutputs(outputTables));

            body.AppendLine($"     {SelectTable("#Stats", "Stats")}");
            body.AppendLine($"     {SelectTable(assertTable, "Assert Results", commaKeys)}");
            body.AppendLine($"     {SelectTable(testTable, "Test Results", commaKeys)}");
            body.AppendLine("     ROLLBACK TRAN -- Rollback transaction which surrounds the whole comparison script. This is done to guarentee no database changes occur.");
            body.AppendLine("END TRY");
            body.AppendLine("BEGIN CATCH");
            body.AppendLine($"     Select * From #Errors Order By Fatal desc");
            body.AppendLine($"     {SelectTable("#Stats", "Stats")}");
            body.AppendLine($"     {SelectTable(assertTable, "Assert Results", commaKeys)}");
            body.AppendLine($"     {SelectTable(testTable, "Test Results", commaKeys)}");
            body.AppendLine();
            body.AppendLine("     ROLLBACK TRAN -- Rollback transaction which surrounds the whole comparison script. This is done to guarentee no database changes occur.");
            body.AppendLine("     Declare @Message varchar(255) = ERROR_MESSAGE(), @Severity int = ERROR_Severity();");
            body.AppendLine("     RAISERROR(@Message,@Severity,1);");
            body.AppendLine("END CATCH");

            // Produce final result in correct order.
            string result = header.ToString() + body.ToString();
            return result;

            void AddBlockComment(StringBuilder sb, string title, params string[] lines)
            {
                sb.AppendLine();
                sb.AppendLine($"/*{new string('-', headerWidth - 2)}");
                var titlePad = (headerWidth - title.Length) / 2;
                sb.AppendLine($"{new string(' ', titlePad)}{title}");
                sb.AppendLine();

                lines.ToList()
                    .ForEach(x => sb.AppendLine(x));

                sb.AppendLine($"{new string('-', headerWidth - 2)}*/");
                sb.AppendLine();
            }

            string AliasColumns(string alias, IEnumerable<string> columns, bool simpleName = false)
            {
                return string.Join("\r\n     , ", columns.Select(x => $"{alias}.{x} {(simpleName ? string.Empty : $"[{alias} {x}]")}"));
            }

            string SummarizeOutputs(List<(string Table, string OrderBy)> outputTables)
            {
                var summarize = new StringBuilder();

                AddBlockComment(
                    summarize,
                    "Output Results",
                    WrapLine("This section determines whether the comparison passed or failed. If the comparison failed, all discrepant rows are displayed to the user. "));

                summarize.AppendLine();
                foreach (var (table, _) in outputTables)
                {
                    summarize.AppendLine($"Insert Into #Summary (TableName, Records) Values ('{table}',(Select count(*) From {table}))");
                }

                summarize.AppendLine();
                summarize.AppendLine($"Declare @RecordCount int = (Select sum(records) From #Summary Where TableName <> '#Matched')");
                summarize.AppendLine($"IF @RecordCount = 0");
                summarize.AppendLine($"     Select 'Passed' Result");
                summarize.AppendLine($"ELSE");
                summarize.AppendLine($"     Select 'Failed' Result");

                summarize.AppendLine($"Select * From #Summary");

                foreach (var (table, orderBy) in outputTables)
                {
                    summarize.AppendLine($"IF (Select count(*) From {table}) > 0 OR @VerboseOutput = 1");
                    summarize.AppendLine("    " + SelectTable(table, table, orderBy));
                    summarize.AppendLine();
                }

                return summarize.ToString();
            }

            string JoinON(string aliasA, string aliasB, IEnumerable<string> keys)
            {
                return string.Join(" and ", keys.Select(x => $"{aliasA}.{x} = {aliasB}.{x}"));
            }

            string WhereMatch(string aliasA, string aliasB, IEnumerable<string> compareColumns)
            {
                return string.Join("\r\n  and ", compareColumns.Select(x => $"(({aliasA}.{x} = {aliasB}.{x}) ".PadRight(70, ' ') + $"OR ({aliasA}.{x} IS NULL and {aliasB}.{x} IS NULL))"));
            }

            string WhereNull(IEnumerable<string> columns)
            {
                return string.Join(" or ", columns.Select(x => $"{x} IS NULL"));
            }

            string WhereNotMatch(string aliasA, string aliasB, IEnumerable<string> compareColumns)
            {
                return string.Join("\r\n   OR ", compareColumns.Select(x => $"(({aliasA}.{x} <> {aliasB}.{x}) ".PadRight(70, ' ') + $"OR ({aliasA}.{x} IS NULL and {aliasB}.{x} IS NOT NULL) OR ({aliasA}.{x} IS NOT NULL and {aliasB}.{x} IS NULL))"));
            }

            static string CompareColumns(string aliasA, string aliasB, IEnumerable<string> compareColumns)
            {
                return string.Join("\r\n     , ", compareColumns.Select(x => $"{aliasA}.{x} [{aliasA} {x}]".PadRight(80, ' ') + $", {aliasB}.{x} [{aliasB} {x}]"));
            }

            static string UpdateStat(string table, string stat, string param) =>
                $"Update #Stats Set {stat} = {param} where TableName = '{table}'";

            static string HaltOnErrors()
            {
                var halt = new StringBuilder();

                halt.AppendLine();
                halt.AppendLine("Select @ErrorCount = count(*) From #Errors Where Fatal = 1");
                halt.AppendLine($"IF @ErrorCount > 0");
                halt.AppendLine("Begin");
                halt.AppendLine($"    RAISERROR('One or more fatal errors occured. Please see output errors table',16,1)");
                halt.AppendLine("End");
                halt.AppendLine();

                return halt.ToString();
            }

            static string SelectTable(string table, string label, string? orderBy = null) =>
                $"Select '{label}' [{label}], * From {table} {(orderBy == null ? string.Empty : $"Order By {orderBy}")}";

            static string IfConditionInsertError(string condition, bool fatal, ErrorType errorType, string message, string? extraStatements = null)
            {
                var conditionalInsert = new StringBuilder();

                conditionalInsert.AppendLine();
                conditionalInsert.AppendLine($"IF {condition}");
                conditionalInsert.AppendLine("Begin");
                conditionalInsert.AppendLine("     Insert Into #Errors (Fatal, ErrorType, ErrorInfo)");
                conditionalInsert.AppendLine($"     Values( {(fatal ? "1" : "0")} , '{Enum.GetName(typeof(ErrorType), errorType)}' , '{message}' )");
                if (extraStatements != null)
                {
                    conditionalInsert.AppendLine(extraStatements);
                }

                conditionalInsert.AppendLine("End");

                return conditionalInsert.ToString();
            }

            string CreateErrorsTable()
            {
                var errors = new StringBuilder();

                errors.AppendLine("Create Table #Errors ");
                errors.AppendLine("     (Fatal bit not null");
                errors.AppendLine("     ,ErrorType varchar(100) null");
                errors.AppendLine("     ,ErrorInfo varchar(255) null)");

                return errors.ToString();
            }

            string CreateStatsTable(string assertTable, string testTable)
            {
                var stats = new StringBuilder();

                stats.AppendLine("Create Table #Stats ");
                stats.AppendLine("     (TableName varchar(100) not null");
                stats.AppendLine("     ,DurationMS int null");
                stats.AppendLine("     ,Records int null");
                stats.AppendLine("     ,DuplicateRecords int null");
                stats.AppendLine("     ,DuplicateKeys int null");
                stats.AppendLine("     ,NullKeys int null)");
                stats.AppendLine(string.Empty);
                stats.AppendLine($"Insert Into #Stats (TableName) Values ('{assertTable}')");
                stats.AppendLine($"Insert Into #Stats (TableName) Values ('{testTable}')");

                return stats.ToString();
            }

            string CreateSummaryTable()
            {
                var summary = new StringBuilder();

                summary.AppendLine("Create Table #Summary ");
                summary.AppendLine("     (TableName varchar(100) not null");
                summary.AppendLine("     ,Records int not null)");

                return summary.ToString();
            }

            string CalculateStats(string table, IEnumerable<string> keys)
            {
                var stats = new StringBuilder();
                var commaKeys = string.Join(", ", keys);

                var param = new ParamNamer(table);
                string records = param.Name("RecordCount");
                string distinctRecords = param.Name("DistinctRecords");
                string duplicateRecords = param.Name("DuplicateRecords");
                string duplicateKeys = param.Name("DuplicateKeys");
                string nullKeys = param.Name("NullKeys");

                stats.AppendLine($"Declare {records} int;");
                stats.AppendLine($"Declare {distinctRecords} int;");
                stats.AppendLine($"Declare {duplicateRecords} int;");
                stats.AppendLine($"Declare {duplicateKeys} int;");
                stats.AppendLine($"Declare {nullKeys} int;");

                stats.AppendLine($"Select {records} = count(*) From {table};");
                stats.AppendLine($"Select {distinctRecords} = count(*) From (Select Distinct * From {table}) T");
                stats.AppendLine($"Set {duplicateRecords} = {records} - {distinctRecords};");

                stats.AppendLine(IfConditionInsertError($"{duplicateRecords} > 0", false, ErrorType.Warning, $"{table} has duplicate records"));

                stats.AppendLine($";With Duplicatekeys");
                stats.AppendLine("AS");
                stats.AppendLine($"(Select {commaKeys}, count(*) [Number of Duplicates] from {table} Group By {commaKeys} Having count(*) > 1)");
                stats.AppendLine($"Select {duplicateKeys} = count(*) From Duplicatekeys;");
                stats.AppendLine(IfConditionInsertError(
                    $"{duplicateKeys} > 0",
                    true,
                    ErrorType.Fatal,
                    $"{table} has duplicate keys. Please redefine your keys",
                    $"Select 'Duplicate Keys for {table}' Label, {commaKeys}, count(*) n from {table} Group By {commaKeys} Having count(*) > 1 Order By {commaKeys}"));

                stats.AppendLine($"Select {nullKeys} = count(*) From  {table} Where {WhereNull(keys)}; ");
                stats.AppendLine(IfConditionInsertError(
                    $"{nullKeys} > 0",
                    true,
                    ErrorType.Fatal,
                    $"{table} has null keys. Please redefine your keys or add ISNULL to supply a defualt",
                    $"Select 'Null Keys for {table}' Label, {commaKeys} From  {table} Where {WhereNull(keys)} Order By {commaKeys}"));

                stats.AppendLine(UpdateStat(table, "Records", records));
                stats.AppendLine(UpdateStat(table, "DuplicateRecords", duplicateRecords));
                stats.AppendLine(UpdateStat(table, "DuplicateKeys", duplicateKeys));
                stats.AppendLine(UpdateStat(table, "NullKeys", nullKeys));

                string result = stats.ToString();
                return result;
            }

            string GetIntoStatements(ParsedSql sql, string intoTable)
            {
                var statements = new StringBuilder();

                var param = new ParamNamer(intoTable);
                string start = param.Name("StartTime");
                string duration = param.Name("DurationMS");
                statements.AppendLine($"Declare {start} datetime = GetDate();");

                AddBlockComment(statements, $"Select Data 'Into {intoTable}'", "Select the data into a temp table.");
                statements.AppendLine(sql.GetSqlWithInto(intoTable));

                statements.AppendLine($"\r\nDeclare {duration} int = Datediff(MS, {start}, GetDate());");
                statements.AppendLine(UpdateStat(intoTable, "DurationMS", duration));

                return statements.ToString();
            }

            string WrapLine(string line, int extraIndent = 0)
            {
                var sb = new StringBuilder();
                var wrapped = new StringBuilder();
                foreach (var word in line.Split(' '))
                {
                    if (wrapped.Length + word.Length > headerWidth)
                    {
                        sb.AppendLine(wrapped.ToString());
                        wrapped.Clear();
                        wrapped.Append(new string(' ', indentWidth + extraIndent));
                    }

                    wrapped.Append($"{word} ");
                }

                sb.Append(wrapped);

                return sb.ToString();
            }
        }

        private class ParamNamer
        {
            private string table;

            public ParamNamer(string table)
            {
                this.table = table.Remove(0, 1);
            }

            public string Name(string paramName)
            {
                paramName = paramName.Trim();
                return $"@{table}{paramName}";
            }
        }
    }
}
