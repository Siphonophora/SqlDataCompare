using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.SqlServer.Management.SqlParser.Parser;

namespace SqlDataCompare.Core
{
    public class ComparisonTemplator
    {
        private enum ErrorType { Fatal, Warning }

        public StringBuilder DropTable { get; set; } = new StringBuilder();

        public string Create(string assertSql, string testSql, string[] keys, bool addIntoStatement)
        {
            assertSql = assertSql ?? throw new ArgumentNullException(nameof(assertSql));
            testSql = testSql ?? throw new ArgumentNullException(nameof(testSql));

            DropTable.Clear();
            string assertTable = "#Assert";
            string testTable = "#Test";
            string matchedTable = "#Matched";
            string discrepantTable = "#Discrepant";
            string extraTable = "#Extra";
            string missingTable = "#Missing";
            string commaKeys = string.Join(", ", keys);

            var template = new StringBuilder();

            template.AppendLine("-- =====================================================================================================================");
            template.AppendLine("--                                       Start of Setup Block  ");
            template.AppendLine("--");
            template.AppendLine("-- Scroll down to add 'into' to the selects  ");
            template.AppendLine("-- =====================================================================================================================");
            template.AppendLine("Declare @VerboseOutput bit = 0; --Change to see all tables  \r\n\r\n");

            template.AppendLine(CreateErrorsTable());
            template.AppendLine(CreateStatsTable(assertTable, testTable));

            template.AppendLine("Declare @ErrorCount int; --Helper for later");

            template.AppendLine(GetIntoStatements(assertSql, assertTable, addIntoStatement));
            template.AppendLine();
            template.AppendLine(GetIntoStatements(testSql, testTable, addIntoStatement));

            template.AppendLine("-- =====================================================================================================================");
            template.AppendLine("--                                       Perform Base Analysis  ");
            template.AppendLine("--");
            template.AppendLine("-- In this we ensure the provided keys will allow the main analysis to work.");
            template.AppendLine("-- If there are duplicate keys we cannot proceed because we might get many to many joins.");
            template.AppendLine("-- =====================================================================================================================");
            template.AppendLine("BEGIN TRY");

            template.AppendLine();
            template.AppendLine(CalculateStats(assertTable, keys));
            template.AppendLine();
            template.AppendLine(CalculateStats(testTable, keys));

            template.AppendLine(SelectTable("#Stats", "Stats"));
            template.AppendLine(HaltOnErrors(assertTable, testTable, keys));

            template.AppendLine("-- =====================================================================================================================");
            template.AppendLine("--                                       Perform Main Analysis  ");
            template.AppendLine("--");
            template.AppendLine("-- In this section, we compare the two sets of selected results");
            template.AppendLine("-- =====================================================================================================================");

            template.AppendLine(CreateSummaryTable());
            var outputTables = new List<string>();

            //Get the Select Columns and create the ordered list of columns.
            var statement = Parser.Parse(assertSql).Script.Batches.Last().Statements.Last();
            if (SafeSqlValidator.TryParseSelectColumns(statement, out string[] columns) == false)
            {
                throw new ArgumentException("Somehow provided SQL which couldn't parse columns");
            }
            columns = columns.Where(x => keys.Contains(x) == false).ToArray();
            string commaColumns = string.Join(", ", columns);

            string assert = "Assert";
            string test = "Test";

            DropTempTableIfExists(matchedTable);
            outputTables.Add(matchedTable);
            template.AppendLine();
            template.AppendLine($"Select { AliasColumns(assert, keys, true)}\r\n     , '' [ ] \r\n     , {AliasColumns(assert, columns, true)}");
            template.AppendLine($"Into {matchedTable}");
            template.AppendLine($"From {testTable} {test}                ");
            template.AppendLine($"Join {assertTable} {assert} on {JoinON(assert, test, keys)} ");
            template.AppendLine($"Where {WhereMatch(assert, test, columns)}");
            template.AppendLine();

            DropTempTableIfExists(missingTable);
            outputTables.Add(missingTable);
            template.AppendLine();
            template.AppendLine($"Select { AliasColumns(assert, keys, true)}\r\n     , '' [ ] \r\n     , {AliasColumns(assert, columns)}");
            template.AppendLine($"Into {missingTable}");
            template.AppendLine($"From {assertTable} {assert}                ");
            template.AppendLine($"Left Join {testTable} {test} on {JoinON(assert, test, keys)} ");
            template.AppendLine($"Where {test}.{keys[0]} IS NULL");

            DropTempTableIfExists(extraTable);
            outputTables.Add(extraTable);
            template.AppendLine();
            template.AppendLine($"Select { AliasColumns(test, keys, true)}\r\n     , '' [ ] \r\n     , {AliasColumns(test, columns)}");
            template.AppendLine($"Into {extraTable}");
            template.AppendLine($"From {testTable} {test}                ");
            template.AppendLine($"Left Join {assertTable} {assert} on {JoinON(assert, test, keys)} ");
            template.AppendLine($"Where {assert}.{keys[0]} IS NULL");
            template.AppendLine();

            DropTempTableIfExists(discrepantTable);
            outputTables.Add(discrepantTable);
            template.AppendLine();
            template.AppendLine($"Select { AliasColumns(assert, keys, true)}\r\n     , '' [ ] \r\n     , {CompareColumns(assert, test, columns)}");
            template.AppendLine($"Into {discrepantTable}");
            template.AppendLine($"From {testTable} {test}                ");
            template.AppendLine($"Join {assertTable} {assert} on {JoinON(assert, test, keys)} ");
            template.AppendLine($"Where {WhereNotMatch(assert, test, columns)}");
            template.AppendLine();

            foreach (var column in columns)
            {
                var discrepantDetailTable = $"{discrepantTable}__{column}";
                var columnDisplay = columns.Where(x => x != column).ToList();
                columnDisplay.Insert(0, column);

                DropTempTableIfExists(discrepantDetailTable);
                outputTables.Add(discrepantDetailTable);
                template.AppendLine();
                template.AppendLine($"Select { AliasColumns(assert, keys, true)}\r\n     , '' [ ] \r\n     , {CompareColumns(assert, test, columnDisplay.ToArray())}");
                template.AppendLine($"Into {discrepantDetailTable}");
                template.AppendLine($"From {testTable} {test}                ");
                template.AppendLine($"Join {assertTable} {assert} on {JoinON(assert, test, keys)} ");
                template.AppendLine($"Where {WhereNotMatch(assert, test, new string[] { column })}");
                template.AppendLine();
            }

            template.AppendLine("Declare @RecordCount int");
            template.AppendLine(SummarizeOutputs(outputTables, commaKeys));

            template.AppendLine($"     {SelectTable(assertTable, "Assert Results", commaKeys)}");
            template.AppendLine($"     {SelectTable(testTable, "Test Results", commaKeys)}");
            template.AppendLine("END TRY");
            template.AppendLine("BEGIN CATCH");
            template.AppendLine("     Select * From #Errors Order By Fatal desc");
            template.AppendLine($"     {SelectTable(assertTable, "Assert Results", commaKeys)}");
            template.AppendLine($"     {SelectTable(testTable, "Test Results", commaKeys)}");
            template.AppendLine();
            template.AppendLine("     Declare @Message varchar(255) = ERROR_MESSAGE(), @Severity int = ERROR_Severity();");
            template.AppendLine("     RAISERROR(@Message,@Severity,1);");
            template.AppendLine("END CATCH");

            //Drop table first, to clean up issues with changing the query and rerunning in the same session
            string result = DropTable.ToString() + template.ToString();
            return result;
        }

        private object AliasColumns(string alias, string[] columns, bool simpleName = false)
        {
            return string.Join("\r\n     , ", columns.Select(x => $"{alias}.{x} {(simpleName ? "" : $"[{alias} {x}]")}"));
        }

        private string SummarizeOutputs(List<string> outputTables, string commakeys)
        {
            var summarize = new StringBuilder();

            foreach (var table in outputTables)
            {
                summarize.AppendLine();
                summarize.AppendLine($"Select @RecordCount = count(*) From {table}");
                summarize.AppendLine($"Insert Into #Summary (TableName, Records) Values ('{table}',@RecordCount)");
            }
            summarize.AppendLine();
            summarize.AppendLine($"Select @RecordCount = sum(records) From #Summary Where TableName <> '#Matched'");
            summarize.AppendLine($"IF @RecordCount = 0");
            summarize.AppendLine($"     Select 'Passed' Result");
            summarize.AppendLine($"ELSE");
            summarize.AppendLine($"     Select 'Failed' Result");

            summarize.AppendLine($"Select * From #Summary");

            foreach (var table in outputTables)
            {
                summarize.AppendLine($"Select @RecordCount = count(*) From {table}");
                summarize.AppendLine($"IF @RecordCount > 0 OR @VerboseOutput = 1");

                summarize.AppendLine("    " + SelectTable(table, table, commakeys));
            }

            return summarize.ToString();
        }

        private string JoinON(string aliasA, string aliasB, string[] keys)
        {
            return string.Join(" and ", keys.Select(x => $"{aliasA}.{x} = {aliasB}.{x}"));
        }

        private string WhereMatch(string aliasA, string aliasB, string[] compareColumns)
        {
            return string.Join("\r\n  and ", compareColumns.Select(x => $"(({aliasA}.{x} = {aliasB}.{x}) ".PadRight(70, ' ') + $"OR ({aliasA}.{x} IS NULL and {aliasB}.{x} IS NULL))"));
        }

        private string WhereNull(string[] columns)
        {
            return string.Join(" or ", columns.Select(x => $"{x} IS NULL"));
        }

        private string WhereNotMatch(string aliasA, string aliasB, string[] compareColumns)
        {
            return string.Join("\r\n  and ", compareColumns.Select(x => $"(({aliasA}.{x} <> {aliasB}.{x}) ".PadRight(70, ' ') + $"OR ({aliasA}.{x} IS NULL and {aliasB}.{x} IS NOT NULL) OR ({aliasA}.{x} IS NOT NULL and {aliasB}.{x} IS NULL))"));
        }

        private string CompareColumns(string aliasA, string aliasB, string[] compareColumns)
        {
            return string.Join("\r\n     , ", compareColumns.Select(x => $"{aliasA}.{x} [{aliasA} {x}]".PadRight(80, ' ') + $", {aliasB}.{x} [{aliasB} {x}]"));
        }

        private string CreateErrorsTable()
        {
            var errors = new StringBuilder();

            DropTempTableIfExists("#Errors");
            errors.AppendLine("Create Table #Errors ");
            errors.AppendLine("     (Fatal bit not null");
            errors.AppendLine("     ,ErrorType varchar(100) null");
            errors.AppendLine("     ,ErrorInfo varchar(255) null)");

            return errors.ToString();
        }

        private string CreateStatsTable(string assertTable, string testTable)
        {
            var stats = new StringBuilder();

            DropTempTableIfExists("#Stats");
            stats.AppendLine("Create Table #Stats ");
            stats.AppendLine("     (TableName varchar(100) not null");
            stats.AppendLine("     ,DurationMS int null");
            stats.AppendLine("     ,Records int null");
            stats.AppendLine("     ,DuplicateRecords int null");
            stats.AppendLine("     ,DuplicateKeys int null");
            stats.AppendLine("     ,NullKeys int null)");
            stats.AppendLine("");
            stats.AppendLine($"Insert Into #Stats (TableName) Values ('{assertTable}')");
            stats.AppendLine($"Insert Into #Stats (TableName) Values ('{testTable}')");

            return stats.ToString();
        }

        private string CreateSummaryTable()
        {
            var summary = new StringBuilder();

            DropTempTableIfExists("#Summary");
            summary.AppendLine("Create Table #Summary ");
            summary.AppendLine("     (TableName varchar(100) not null");
            summary.AppendLine("     ,Records int not null)");

            return summary.ToString();
        }

        private string UpdateStat(string table, string stat, string param)
        {
            var updateStat = new StringBuilder();

            updateStat.AppendLine($"Update #Stats Set {stat} = {param} where TableName = '{table}'");

            return updateStat.ToString();
        }

        private string HaltOnErrors(string assertTable, string testTable, string[] keys)
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

        private string SelectTable(string table, string label, string orderBy = null)
        {
            var select = new StringBuilder();

            select.AppendLine($"Select '{label}' [{label}], * From {table} {(orderBy == null ? "" : $"Order By {orderBy}")}");

            return select.ToString();
        }

        private string CalculateStats(string table, string[] keys)
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
            stats.AppendLine($"(Select {commaKeys}, count(*) n from {table} Group By {commaKeys} Having count(*) > 1)");
            stats.AppendLine($"Select {duplicateKeys} = count(*) From Duplicatekeys;");
            stats.AppendLine(IfConditionInsertError($"{duplicateKeys} > 0"
                                                    , true
                                                    , ErrorType.Fatal
                                                    , $"{table} has duplicate keys. Please redefine your keys"
                                                    , $"Select 'Duplicate Keys for {table}' Label, {commaKeys}, count(*) n from {table} Group By {commaKeys} Having count(*) > 1 Order By {commaKeys}"));

            stats.AppendLine($"Select {nullKeys} = count(*) From  {table} Where {WhereNull(keys)}; ");
            stats.AppendLine(IfConditionInsertError($"{nullKeys} > 0"
                                                    , true
                                                    , ErrorType.Fatal
                                                    , $"{table} has null keys. Please redefine your keys or add ISNULL to supply a defualt"
                                                    , $"Select 'Null Keys for {table}' Label, {commaKeys} From  {table} Where {WhereNull(keys)} Order By {commaKeys}"));

            stats.AppendLine(UpdateStat(table, "Records", records));
            stats.AppendLine(UpdateStat(table, "DuplicateRecords", duplicateRecords));
            stats.AppendLine(UpdateStat(table, "DuplicateKeys", duplicateKeys));
            stats.AppendLine(UpdateStat(table, "NullKeys", nullKeys));

            string result = stats.ToString();
            return result;
        }

        private void DropTempTableIfExists(string table)
        {
            DropTable.AppendLine($"IF OBJECT_ID('tempdb..{table}') IS NOT NULL");
            DropTable.AppendLine($"     DROP TABLE {table}");
            DropTable.AppendLine("GO\r\n");
        }

        private string GetIntoStatements(string sql, string intoTable, bool addIntoStatement)
        {
            var statements = new StringBuilder();
            DropTempTableIfExists(intoTable);

            var param = new ParamNamer(intoTable);
            string start = param.Name("StartTime");
            string duration = param.Name("DurationMS");
            statements.AppendLine($"Declare {start} datetime = GetDate();");

            //TODO revisit this. There are cases where there is a from in a where clause that breaks this.
            if (addIntoStatement)
            {
                var parsedResult = Parser.Parse(sql);
                var fromStart = parsedResult.Script.Tokens.Where(x => x.Type.ToString() == "TOKEN_FROM").Last().StartLocation;
                var stringIndex = fromStart.Offset;
                sql = sql.Insert(stringIndex - 1, $"\r\nInto {intoTable}\r\n");
            }

            statements.AppendLine($"--==============================================  Add 'Into {intoTable}' to the query =================================");
            statements.AppendLine(sql);

            statements.AppendLine($"\r\nDeclare {duration} int = Datediff(MS, {start}, GetDate());");
            statements.AppendLine(UpdateStat(intoTable, "DurationMS", duration));

            return statements.ToString();
        }

        private string IfConditionInsertError(string condition, bool fatal, ErrorType errorType, string message, string extraStatements = null)
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
