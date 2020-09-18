using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;
using SqlDataCompare.Core.Enums;
using SqlDataCompare.Core.Models;

namespace SqlDataCompare.UnitTests.Tests
{
    public class ParsedSqlTests
    {
        [Test]
        public void GetSqlWithInto_ValidSql()
        {
            var sut = new ParsedSql("Sql string", ParseResultValue.Valid, string.Empty, new List<string>(), 4);

            sut.GetSqlWithInto("#T");
        }

        [TestCase(ParseResultValue.Error)]
        [TestCase(ParseResultValue.Warning)]
        public void GetSqlWithInto_InvalidValidSql_Throws(ParseResultValue parseResult)
        {
            var sut = new ParsedSql("Sql string", parseResult, string.Empty);

            Assert.Throws<InvalidOperationException>(() => sut.GetSqlWithInto("#T"));
        }

        [TestCase(false, "#TabName")]
        [TestCase(true, "TabName")]
        [TestCase(true, "##TabName")]
        [TestCase(true, "#TabN#ame")]
        [TestCase(true, "TabN#ame")]
        public void GetSqlWithInto_RequiresLocalTempTable_OrThrows(bool throws, string table)
        {
            var sut = new ParsedSql("Sql string", ParseResultValue.Valid, string.Empty, new List<string>(), 4);

            if (throws)
            {
                Assert.Throws<InvalidOperationException>(() => sut.GetSqlWithInto(table));
            }
            else
            {
                sut.GetSqlWithInto(table);
            }
        }
    }
}
