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
    public static class ComparableSqlParser
    {
        public static ParsedSql ParseAndValidate(string sql)
        {
            var parseResult = Parser.Parse(sql);

            if (parseResult.Errors.Any())
            {
                // TODO, report the errors?
                var sb = new StringBuilder();
                foreach (var error in parseResult.Errors)
                {
                    sb.Append(error.Message + " ");
                }

                return new ParsedSql(sql, false, "SQL Has Errors: " + sb.ToString());
            }
            else if (parseResult.Script.Batches[0].Statements[0].Statement == null)
            {
                return new ParsedSql(sql, false, "Not narsable as SQL.");
            }

            // TODO, when do we get multiple batches? When there are GOs? Can we handle multiple
            // batches (in the output)?
            var batches = parseResult.Script.Batches;

            // All sql is safe (no side effects). Only the last statement is a plain sql statement
            // All columns have names, which are unique and no * selects.
            if (BatchesAreSafe(batches, out var batchMessage) == false)
            {
                return new ParsedSql(sql, false, batchMessage);
            }
            else if (BatchesProduceOneResultSet(batches, out var resultSetMessage) == false)
            {
                return new ParsedSql(sql, false, resultSetMessage);
            }
            else
            {
                var lastStatement = batches.Last().Statements.Last();

                var colList = new List<string>();

                var xmlDoc = new XmlDocument();
                xmlDoc.LoadXml(lastStatement.Xml);

                //Narrow to only the Select. There otherwise could be a CTE or something else containing SqlSelectClause
                try
                {
                    xmlDoc.LoadXml(xmlDoc.GetElementsByTagName("SqlSelectSpecification")[0].OuterXml);
                }
                catch
                {
                    return new ParsedSql(sql, false, "Unable to find a select specification. Shouldn't be possible Please report this error if it occurs");
                }

                var select = xmlDoc.GetElementsByTagName("SqlSelectClause");
                if (select.Count == 0)
                {
                    return new ParsedSql(sql, false, "Unable to find a select clause. Shouldn't be possible Please report this error if it occurs");
                }

                xmlDoc.LoadXml(select[0].OuterXml.ToString());

                foreach (XmlNode node in select[0].ChildNodes)
                {
                    if (node.Name == "SqlSelectScalarExpression")
                    {
                        if (node.Attributes["Alias"]?.Value is string alias)
                        {
                            colList.Add(alias);
                            continue;
                        }
                        else
                        {
                            if (node.FirstChild.NextSibling.Attributes["ColumnOrPropertyName"]?.Value is string col1)
                            {
                                colList.Add(col1.Trim());
                                continue;
                            }
                            else if (node.FirstChild.NextSibling.Attributes["ColumnName"]?.Value is string col2)
                            {
                                colList.Add(col2.Trim());
                                continue;
                            }

                            return new ParsedSql(sql, false, "Error - Select is not comparable because at least column is unnamed in statement");
                        }
                    }
                    else if (node.Name == "SqlSelectStarExpression")
                    {
                        return new ParsedSql(sql, false, "Error - Select is not comparable because at least one * is used in statement");
                    }
                }

                if (colList.Count != colList.Distinct(StringComparer.InvariantCultureIgnoreCase).Count())
                {
                    return new ParsedSql(sql, false, "Error - Select is not comparable because at least one column name is used multiple times");
                }

                return new ParsedSql(sql, true, "Valid SQL", colList);
            }
        }

        internal static bool BatchesAreSafe(SqlBatchCollection batches, out string message)
        {
            for (int i = 0; i < batches.Count; i++)
            {
                var batch = batches[i];

                Console.WriteLine($"Batch {i}");

                for (int j = 0; j < batch.Statements.Count; j++)
                {
                    var statement = batch.Statements[j];

                    Console.WriteLine($"Statement {j} - {statement}");
                    Console.WriteLine(statement.Sql);
                    if (StatementIsSafe(statement, out message) == false)
                    {
                        return false;
                    }
                }
            }

            message = "Batch is safe";
            return true;
        }

        internal static bool BatchesProduceOneResultSet(SqlBatchCollection batches, out string message)
        {
            var selectCount = batches.SelectMany(x => x.Statements).Where(x => IsPlainSelect(x)).Count();

            if (selectCount > 1)
            {
                message = $"SQL contains {selectCount} select statements. There must only be one.";
                return false;
            }
            else if (selectCount == 0)
            {
                message = "SQL does not contain a Select statement";
                return false;
            }

            var lastStatement = batches.Last().Statements.Last();
            if (IsPlainSelect(lastStatement))
            {
                message = string.Empty;
                return true;
            }
            else
            {
                message = "The last SQL statement must be a select";
                return false;
            }
        }

        /// <summary>
        /// Determines if a statement is a select, which is not selecting into a table. Therefore it
        /// returns a result set.
        /// </summary>
        private static bool IsPlainSelect(SqlStatement statement)
        {
            var statementTypeName = statement.GetType().ToString().Split('.').Last();
            if (statementTypeName == "SqlSelectStatement")
            {
                var xmlDoc = new XmlDocument();
                xmlDoc.LoadXml(statement.Xml);
                if (TryParseFirstTagAttribute(xmlDoc, "SqlSelectIntoClause", "IntoTarget", out var _) == false)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool StatementIsSafe(SqlStatement statement, out string message)
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
                        if (selectIntoTarget.Contains('#', StringComparison.InvariantCultureIgnoreCase) == false)
                        {
                            message = "Select Into a real table";
                            return false;
                        }

                        message = "Select Into a temp table";
                        return true;
                    }

                    message = "Plain Select Statement";
                    return true;

                case "SqlDeleteStatement":
                    var delete = xmlDoc.GetElementsByTagName("SqlDeleteStatement")[0];
                    if (TryParseFirstTagAttribute(xmlDoc, "SqlIdentifier", "Value", out string deleteTarget))
                    {
                        if (deleteTarget.Contains('#', StringComparison.InvariantCultureIgnoreCase) == false)
                        {
                            message = "Delete from a real table";
                            return false;
                        }

                        message = "Delete from a temp table";
                        return true;
                    }

                    message = "Error determining if SQL is safe";
                    return false;

                case "SqlDropTableStatement":
                    var drop = xmlDoc.GetElementsByTagName("SqlDropTableStatement")[0];
                    if (TryParseFirstTagAttribute(xmlDoc, "SqlIdentifier", "Value", out string dropTarget))
                    {
                        if (dropTarget.Contains('#', StringComparison.InvariantCultureIgnoreCase) == false)
                        {
                            message = "Dropping a real table";
                            return false;
                        }

                        message = "Dropping a temp table";
                        return true;
                    }

                    message = "Error determining if SQL is safe";
                    return false;

                case "SqlUpdateStatement":
                    var update = xmlDoc.GetElementsByTagName("SqlUpdateStatement")[0];
                    if (TryParseFirstTagAttribute(xmlDoc, "SqlIdentifier", "Value", out string updateTarget))
                    {
                        if (updateTarget.Contains('#', StringComparison.InvariantCultureIgnoreCase) == false)
                        {
                            message = "Updating a real table";
                            return false;
                        }

                        message = "Updating a temp table";
                        return true;
                    }

                    message = "Error determining if SQL is safe";
                    return false;

                case "SqlInsertStatement":
                    var insert = xmlDoc.GetElementsByTagName("SqlInsertStatement")[0];
                    if (TryParseFirstTagAttribute(xmlDoc, "SqlIdentifier", "Value", out string insertTarget))
                    {
                        if (insertTarget.Contains('#', StringComparison.InvariantCultureIgnoreCase) == false)
                        {
                            message = "Insertting to a real table";
                            return false;
                        }

                        message = "Insertting to a temp table";
                        return true;
                    }

                    message = "Error determining if SQL is safe";
                    return false;

                default:
                    message = "Uknown Statement Type";
                    return false;
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
    }
}
