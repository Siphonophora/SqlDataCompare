using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.SqlServer.Management.SqlParser.Parser;
using NUnit.Framework;

namespace SqlDataCompare.UnitTests
{
    public class ComparisonTemplatorTests
    {
        [TestCase("SELECT Distinct ID,\r\n Program,[AlertType] \r\n,[AlertStatus] FROM [BWHInfo].[dbo].[AlertQueue]", "SELECT Distinct ID, Program,[AlertType] ,[AlertStatus] FROM [BWHInfo].[dbo].[AlertQueue]", "ID", "Program")]
        [TestCase("SELECT Program,[AlertType] ,[AlertStatus] FROM [BWHInfo].[dbo].[AlertQueue]", "SELECT Distinct Program,[AlertType] ,[AlertStatus] FROM [BWHInfo].[dbo].[AlertQueue]", "Program")]
        public void TemplateIsValid(string assertSQL, string testSQL, params string[] keys)
        {
            var sut = new ComparisonTemplator();

            var template = sut.Create(assertSQL, testSQL, keys, true);

            Assert.AreEqual(0, Parser.Parse(template).Errors.Count());
        }
    }
}
