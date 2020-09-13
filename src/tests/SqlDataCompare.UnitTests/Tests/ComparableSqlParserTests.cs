using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;
using SqlDataCompare.Core.Enums;
using SqlDataCompare.Core.Models;
using SqlDataCompare.Core.Services;

namespace SqlDataCompare.UnitTests
{
    [TestFixtureSource(typeof(Assetts.TestSqlProvider))]
    public class ComparableSqlParserTests
    {
        private readonly ParseResultValue parseResult;
        private readonly string comment;
        private readonly string sql;
        private readonly List<string> columns;
        private readonly ParsedSql actualResult;

        public ComparableSqlParserTests(ParseResultValue parseResult, string comment, string sql, List<string> columns)
        {
            this.parseResult = parseResult;
            this.comment = comment;
            this.sql = sql;
            this.columns = columns;
            this.actualResult = ComparableSqlParser.ParseAndValidate(sql);
        }

        [Test]
        public void ParseAndValidate_ParseResult_MatchesExpected()
        {
            Assert.AreEqual(parseResult, actualResult.ParseResult, $"Message: {actualResult.ValidationMessage}");
        }

        [Test]
        public void ParseAndValidate_Columns_MatchesExpected()
        {
            // Not all cases need to have defined columns, but if we have them, they must match.
            if (columns.Any())
            {
                Assert.AreEqual(columns.OrderBy(x => x), actualResult.ColumnNames.OrderBy(x => x));
            }
        }

        [Test]
        public void TestDataAreInternallyConsistent()
        {
            if (columns.Any())
            {
                Assert.AreEqual(parseResult, ParseResultValue.Valid, "Only valid sql can have columns defined");
            }
        }
    }
}
