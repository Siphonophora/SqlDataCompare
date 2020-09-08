using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SqlDataCompare.Core.Enums;
using SqlDataCompare.Core.Models;

namespace SqlDataCompare.Core.Services
{
    public class ComprableColumnService
    {
        private readonly Dictionary<string, ComparableColumn> definedColumns = new Dictionary<string, ComparableColumn>();
        private readonly List<ComparableColumn> comparableColumns = new List<ComparableColumn>();

        public IOrderedEnumerable<ComparableColumn> ComparableColumns =>
            comparableColumns
                .OrderByDescending(x => x.IsKey)
                .ThenBy(x => x.ColumnOrder)
                .ThenBy(x => x.ColumnName);

        public ParsedSql AssertParsedSql { get; private set; } = new ParsedSql(string.Empty, ParseResultValue.Warning, "Not entered yet");

        public ParsedSql TestParsedSql { get; private set; } = new ParsedSql(string.Empty, ParseResultValue.Warning, "Not entered yet");

        public string ErrorMessage { get; private set; } = string.Empty;

        public bool Comparable { get; private set; }

        public void UpdateParsedSql(ParsedSql? assertSql = null, ParsedSql? testSql = null)
        {
            Comparable = false;
            ErrorMessage = string.Empty;

            AssertParsedSql = assertSql ?? AssertParsedSql;
            TestParsedSql = testSql ?? TestParsedSql;

            if (string.IsNullOrWhiteSpace(AssertParsedSql.Sql) && string.IsNullOrWhiteSpace(TestParsedSql.Sql))
            {
                ErrorMessage = string.Empty;
            }
            else if (string.IsNullOrWhiteSpace(AssertParsedSql.Sql))
            {
                ErrorMessage = "Enter assert sql";
            }
            else if (string.IsNullOrWhiteSpace(TestParsedSql.Sql))
            {
                ErrorMessage = "Enter test sql";
            }
            else if (AssertParsedSql.ParseResult == ParseResultValue.Valid && TestParsedSql.ParseResult == ParseResultValue.Valid)
            {
                var extraAssertCols = AssertParsedSql
                    .ColumnNames
                    .Except(TestParsedSql.ColumnNames, StringComparer.InvariantCultureIgnoreCase);

                var extraTestCols = TestParsedSql
                    .ColumnNames
                    .Except(AssertParsedSql.ColumnNames, StringComparer.InvariantCultureIgnoreCase);

                if (!extraAssertCols.Any() && !extraTestCols.Any())
                {
                    // This is comparable, so we need to setup the columns
                    Comparable = true;
                    comparableColumns.Clear();

                    for (int i = 0; i < AssertParsedSql.ColumnNames.Count(); i++)
                    {
                        var colName = AssertParsedSql.ColumnNames.ElementAt(i);

                        if (definedColumns.TryGetValue(colName.ToUpperInvariant(), out var col))
                        {
                            col.ColumnOrder = i;
                        }
                        else
                        {
                            col = new ComparableColumn(colName, i);
                            definedColumns.Add(colName.ToUpperInvariant(), col);
                        }

                        comparableColumns.Add(col);
                    }
                }
                else
                {
                    ErrorMessage = ExtraColumnMessage("Assert SQL", "Test SQL", extraAssertCols);
                    ErrorMessage += ExtraColumnMessage("Test SQL", "Assert SQL", extraTestCols);
                }
            }

            string ExtraColumnMessage(string setName, string otherSet, IEnumerable<string> cols) =>
                cols.Any() ?
                $"{setName} has column(s) ({string.Join(", ", cols.OrderBy(x => x))}) which are not present in {otherSet}. " :
                string.Empty;
        }
    }
}
