using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using Microsoft.SqlServer.Management.SqlParser.Parser;
using Microsoft.SqlServer.Management.SqlParser.SqlCodeDom;

namespace SqlDataCompare.Core
{
    public class SafeSqlValidator
    {
        public static SqlStatement GetFirstSqlStatement(string sql)
        {
            var parseResult = Parser.Parse(sql);

            return parseResult.Script.Batches[0].Statements[0];
        }

        public Validation ValidateIsSafe(string sql)
        {
            var parseResult = Parser.Parse(sql);
            var val = new Validation(false, "Unknown");

            if (parseResult.Errors.Count() > 0)
            {
                return new Validation(false, "Sql Has Errors");
            }

            if (parseResult.Script.Batches[0].Statements[0].Statement == null)
            {
                return new Validation(false, "Not SQL");
            }

            for (int i = 0; i < parseResult.Script.Batches.Count; i++)
            {
                var batch = parseResult.Script.Batches[i];

                Console.WriteLine();
                Console.WriteLine($"Batch {i}");

                for (int j = 0; j < batch.Statements.Count; j++)
                {
                    var statement = batch.Statements[j];

                    Console.WriteLine();
                    Console.WriteLine($"Statement {j} - {statement}");
                    Console.WriteLine(statement.Sql);
                    val = SqlIsSafe(statement);
                    if (val.Valid == false)
                    {
                        Console.WriteLine(val.Message);
                        return val;
                    }
                }
            }

            return val;
        }

        public Validation ValidateSingleResultSet(string sql)
        {
            var parseResult = Parser.Parse(sql);
            var val = new Validation(false, "Unknown");

            if (parseResult.Errors.Count() > 0)
            {
                return new Validation(false, "Sql Has Errors");
            }

            int selectCount = 0;

            foreach (var batch in parseResult.Script.Batches)
            {
                foreach (var statement in batch.Statements)
                {
                    if (IsPlainSelect(statement))
                    {
                        selectCount++;
                    }
                }
            }

            var lastStatement = parseResult.Script.Batches.Last().Statements.Last();
            if (IsPlainSelect(lastStatement) == false)
            {
                return new Validation(false, "The last SQL statement isn't a select");
            }

            var namedCols = SelectColumnsAreNamed(lastStatement);
            if (namedCols.Valid == false)
            {
                return new Validation(false, $"Error in last select: {namedCols.Message}");
            }

            return new Validation(selectCount == 1, selectCount == 1 ? "Single Select" : $"{selectCount} Selects");
        }

        public List<QueryColumn> GetLastSelectQueryColumns(string sql)
        {
            var parseResult = Parser.Parse(sql);
            var lastStatement = parseResult.Script.Batches.Last().Statements.Last();
            if (lastStatement.TryParseSelectColumns(out string[] cols) == false)
            {
                throw new ArgumentException("SQL coudln't be parsed");
            }

            return cols.Select(x => new QueryColumn(x, false, 0)).ToList();
        }

        private static bool IsPlainSelect(SqlStatement statement)
        {
            var statementTypeName = statement.GetType().ToString().Split('.').Last();
            if (statementTypeName == "SqlSelectStatement")
            {
                var xmlDoc = new XmlDocument();
                xmlDoc.LoadXml(statement.Xml);
                if (TryParseFirstTagAttribute(xmlDoc, "SqlSelectIntoClause", "IntoTarget", out string selectIntoTarget) == false)
                {
                    return true;
                }
            }

            return false;
        }

        private static Validation SelectColumnsAreNamed(SqlStatement statement)
        {
            if (statement.TryParseSelectColumns(out string[] cols))
            {
                if (cols.Length == 0)
                {
                    return new Validation(false, "Could not find any column names for statement");
                }
                foreach (var col in cols)
                {
                    if (col == "*")
                    {
                        return new Validation(false, "Error - Select is not comparable because at least one * is used in statement");
                    }
                    if (col == null)
                    {
                        return new Validation(false, "Error - Select is not comparable because at least column is unnamed in statement");
                    }
                }
                return new Validation(true, "All columns have names");
            }
            else
            {
                return new Validation(false, "Could not parse column names for statement");
            }
        }

        private static Validation SqlIsSafe(SqlStatement statement)
        {
            var statementTypeName = statement.GetType().ToString().Split('.').Last();
            Console.WriteLine(statementTypeName);
            var xmlDoc = new XmlDocument();
            xmlDoc.LoadXml(statement.Xml);

            switch (statementTypeName)
            {
                case "SqlSelectStatement":
                    if (TryParseFirstTagAttribute(xmlDoc, "SqlSelectIntoClause", "IntoTarget", out string selectIntoTarget))
                    {
                        if (selectIntoTarget.Contains('#') == false)
                        {
                            return new Validation(false, "Select Into a real table");
                        }

                        return new Validation(true, "Select Into a temp table");
                    }

                    return new Validation(true, "Plain Select Statement"); //This one isn't an error, unlinke the other ELSE brances below

                case "SqlDeleteStatement":
                    var delete = xmlDoc.GetElementsByTagName("SqlDeleteStatement")[0];
                    if (TryParseFirstTagAttribute(xmlDoc, "SqlIdentifier", "Value", out string deleteTarget))
                    {
                        if (deleteTarget.Contains('#') == false)
                        {
                            return new Validation(false, "Delete from a real table");
                        }

                        return new Validation(true, "Delete from a temp table");
                    }

                    return new Validation(false, "Error determining if SQL is safe");

                case "SqlDropTableStatement":
                    var drop = xmlDoc.GetElementsByTagName("SqlDropTableStatement")[0];
                    if (TryParseFirstTagAttribute(xmlDoc, "SqlIdentifier", "Value", out string dropTarget))
                    {
                        if (dropTarget.Contains('#') == false)
                        {
                            return new Validation(false, "Drop a real table");
                        }

                        return new Validation(true, "Drop a temp table");
                    }

                    return new Validation(false, "Error determining if SQL is safe");

                case "SqlUpdateStatement":
                    var update = xmlDoc.GetElementsByTagName("SqlUpdateStatement")[0];
                    if (TryParseFirstTagAttribute(xmlDoc, "SqlIdentifier", "Value", out string updateTarget))
                    {
                        if (updateTarget.Contains('#') == false)
                        {
                            return new Validation(false, "Update a real table");
                        }

                        return new Validation(true, "Update a temp table");
                    }

                    return new Validation(false, "Error determining if SQL is safe");

                case "SqlInsertStatement":
                    var insert = xmlDoc.GetElementsByTagName("SqlInsertStatement")[0];
                    if (TryParseFirstTagAttribute(xmlDoc, "SqlIdentifier", "Value", out string insertTarget))
                    {
                        if (insertTarget.Contains('#') == false)
                        {
                            return new Validation(false, "Update a real table");
                        }

                        return new Validation(true, "Update a temp table");
                    }

                    return new Validation(false, "Error determining if SQL is safe");

                default:
                    Write("Unknown type", ConsoleColor.Red);
                    return new Validation(false, "Unknown Type");
            }
        }

        private static bool TryParseFirstTagAttribute(XmlDocument doc, string tag, string attribute, out string value)
        {
            value = doc.GetElementsByTagName(tag)[0]?.Attributes[attribute]?.Value;

            if (value == null)
            {
                return false;
            }

            return true;
        }

        private static void Write(string message, ConsoleColor color)
        {
            var oldcolor = Console.BackgroundColor;
            Console.BackgroundColor = color;
            Console.WriteLine(message);
            Console.BackgroundColor = oldcolor;
        }
    }
}
