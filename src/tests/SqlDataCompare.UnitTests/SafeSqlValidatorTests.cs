using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.SqlServer.Management.SqlParser.Parser;
using Microsoft.SqlServer.Management.SqlParser.SqlCodeDom;
using NUnit.Framework;
using SqlDataCompare.Core;

namespace SqlDataCompare.UnitTests
{
    public class SafeSqlValidatorTests
    {
        [TestCase("/* stuff */ Select * From  T")]
        [TestCase("Select * From  T")]
        [TestCase("Select * From  T Where A = @a")]
        [TestCase("Select * From #T")]
        [TestCase("Select * From Temp")]
        [TestCase("Select * Into #t From Temp")]
        [TestCase("Delete from #t")]
        [TestCase("Drop table #t")]
        [TestCase("INSERT INTO #Alert_ProgramsTemp ([Program]) VALUES('s')")]
        [TestCase("UPDATE #Alert_ProgramsTemp SET [Program] = 'b' WHERE 1 = 0")]
        public void ValidateIsSafe_SingleStatement_IsSafe(string sql)
        {
            var result = ComparableSqlParser.IsSafe(sql);

            Assert.IsTrue(result.Valid);
            Assert.IsNotEmpty(result.ValidationMessage);
        }

        [TestCase("Delete from t")]
        [TestCase("Select * Into t From Temp")]
        [TestCase("Drop Table T")]
        [TestCase("INSERT INTO [dbo].[Alert_ProgramsTemp] ([Program]) VALUES('s')")]
        [TestCase("UPDATE [dbo].[Alert_ProgramsTemp] SET [Program] = 'b' WHERE 1 = 0")]
        public void ValidateIsSafe_SingleStatement_NotSafe(string sql)
        {
            var result = ComparableSqlParser.IsSafe(sql);

            Assert.IsFalse(result.Valid);
            Assert.IsNotEmpty(result.ValidationMessage);
        }

        [TestCase("Delete from")]
        [TestCase("Select * InFrom Temp")]
        public void ValidateIsSafe_SingleStatement_SqlError(string sql)
        {
            var result = ComparableSqlParser.IsSafe(sql);

            Assert.IsFalse(result.Valid);
            Assert.IsTrue(result.ValidationMessage.StartsWith("Sql Has Errors"));
        }

        [TestCase("DropTable T")]
        public void ValidateIsSafe_SingleStatement_NotSQL(string sql)
        {
            var result = ComparableSqlParser.IsSafe(sql);

            Assert.IsFalse(result.Valid);
        }

        [TestCase("Select * From  T", "Select * From  T")]
        [TestCase("Select * From  T", "GO", "Select * From  T")]
        [TestCase("Select * From  T", "Select Distinct * From  T")]
        [TestCase("Select", "field", "From", "T")]
        public void ValidateIsSafe_MultipleStatements_IsSafe(params string[] sqlArray)
        {
            string sql = string.Join("\r\n", sqlArray);

            var result = ComparableSqlParser.IsSafe(sql);

            Assert.IsTrue(result.Valid);
            Assert.IsNotEmpty(result.ValidationMessage);
        }

        [TestCase("Select * From  T", "Delete From  T")]
        [TestCase("Delete From  T", "Select * From  T")]
        public void ValidateIsSafe_MultipleStatements_NotSafe(params string[] sqlArray)
        {
            string sql = string.Join("\r\n", sqlArray);

            var result = ComparableSqlParser.IsSafe(sql);

            Assert.IsFalse(result.Valid);
            Assert.IsNotEmpty(result.ValidationMessage);
        }

        [TestCase(true, "Select a From  T")]
        [TestCase(true, "Select a Into t From Temp", "Select a From  T")]
        [TestCase(true, "Select a Into t From Temp", "Select a From  T", "--comment")]
        [TestCase(false, "Select a From  T", "Select a From  T")]
        [TestCase(false, "Select a From  T", "Select a Into t From Temp")]
        public void ValidateSingleResultSet_MultipleStatements_MatchAssert(bool assert, params string[] sqlArray)
        {
            string sql = string.Join("\r\n", sqlArray);

            var result = ComparableSqlParser.ValidateSingleResultSet(sql);

            Assert.AreEqual(assert, result.Valid);
            Assert.IsNotEmpty(result.ValidationMessage);
        }

        [TestCase(true, "Select a,b", "a", "b")]
        [TestCase(true, "Select a,a as b", "a", "b")]
        [TestCase(true, "Select a,a b", "a", "b")]
        [TestCase(true, "Select a,b from Tab", "a", "b")]
        [TestCase(true, "Select T.a,T.a b from Tab T", "a", "b")]
        [TestCase(true, ";With CTE AS (SELECT TOP (1000) [ABC] FROM [db].[dbo].[tab]) Select ABC From CTE", "ABC")]
        [TestCase(true, "Select a,count(*) n from T", "a", "n")]
        [TestCase(true, "Select a,count(*) from T", "a", null)]
        [TestCase(true, "Select * from Tab T", "*")]
        [TestCase(true, "Select T.* from Tab T", "*")]
        [TestCase(true, "Select a, T.* from Tab T", "a", "*")]
        [TestCase(false, "Delete from #t")]
        [TestCase(false, "Drop table #t")]
        [TestCase(false, "INSERT INTO #Alert_ProgramsTemp ([Program]) VALUES('s')")]
        [TestCase(false, "UPDATE #Alert_ProgramsTemp SET [Program] = 'b' WHERE 1 = 0")]
        [TestCase(true, "Select a,(Select top 1 g from G) TopG From T", "a", "TopG")]
        public void SelectColumns(bool assertCanParse, string sql, params string[] assertCols)
        {
            var statement = ConvertSqlStringToStatement(sql);
            var couldParse = ComparableSqlParser.TryParseSelectColumns(statement, out string[] columns);

            Assert.AreEqual(assertCanParse, couldParse);
            Assert.IsTrue(Enumerable.SequenceEqual(assertCols, columns));
        }

        [Test]
        public void SelectColumns_Difficult()
        {
            var assertColumns = new string[]
            {
                "staff",
                "sales",
            };

            var sql = @"
            WITH cte_sales_amounts (staff, sales, year) AS (
                SELECT
                    first_name + ' ' + last_name,
                    SUM(quantity * list_price * (1 - discount)),
                    YEAR(order_date)
                FROM
                    sales.orders o
                INNER JOIN sales.order_items i ON i.order_id = o.order_id
                INNER JOIN sales.staffs s ON s.staff_id = o.staff_id
                GROUP BY
                    first_name + ' ' + last_name,
                    year(order_date)
            )

            SELECT
                staff,
                sales
            FROM
                cte_sales_amounts
            WHERE
                year = 2018;";

            var statement = ConvertSqlStringToStatement(sql);
            var couldParse = ComparableSqlParser.TryParseSelectColumns(statement, out string[] columns);

            Assert.IsTrue(couldParse);
            Assert.IsTrue(Enumerable.SequenceEqual(assertColumns, columns));
        }

        private SqlStatement ConvertSqlStringToStatement(string sql)
        {
            var parseResult = Parser.Parse(sql);

            return parseResult.Script.Batches[0].Statements.Single();
        }
    }
}
