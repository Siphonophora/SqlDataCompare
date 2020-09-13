using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.SqlServer.Management.SqlParser.Parser;
using NUnit.Framework;
using SqlDataCompare.Core.Enums;
using SqlDataCompare.Core.Models;
using SqlDataCompare.Core.Services;

namespace SqlDataCompare.UnitTests
{
    [TestFixtureSource(typeof(Assetts.TestSqlProvider))]
    public class ComparisonTemplatorTests
    {
        private readonly ParseResultValue parseResult;
        private readonly string comment;
        private readonly string sql;
        private readonly List<string> columns;
        private readonly ParsedSql actualResult;

        /// <summary>
        /// Initializes a new instance of the <see cref="ComparisonTemplatorTests"/> class. Test the
        /// <see cref="ComparisonTemplator"/> using a single sql for test and assert. It should be
        /// OK that we compare the same sql to itself because that doens't really add to the
        /// complexity of the templator's work.
        /// </summary>
        public ComparisonTemplatorTests(ParseResultValue parseResult, string comment, string sql, List<string> columns)
        {
            this.parseResult = parseResult;
            this.comment = comment;
            this.sql = sql;
            this.columns = columns;
            this.actualResult = ComparableSqlParser.ParseAndValidate(sql);
        }

        [Test]
        public void Template_WithOneKey_IsValidSql()
        {
            if (actualResult.ParseResult == ParseResultValue.Valid)
            {
                var cols = actualResult.ColumnNames.Select(x => new ComparableColumn(x, 1)).ToList();
                cols.First().IsKey = true;

                var template = ComparisonTemplator.Create(actualResult, actualResult, cols);
                Assert.AreEqual(0, Parser.Parse(template).Errors.Count());
            }
        }

        [Test]
        public void Template_WithAllKeys_IsValidSql()
        {
            if (actualResult.ParseResult == ParseResultValue.Valid)
            {
                var cols = actualResult.ColumnNames.Select(x => new ComparableColumn(x, 1)).ToList();
                cols.ForEach(x => x.IsKey = true);

                var template = ComparisonTemplator.Create(actualResult, actualResult, cols);
                Assert.AreEqual(0, Parser.Parse(template).Errors.Count());
            }
        }
    }
}
