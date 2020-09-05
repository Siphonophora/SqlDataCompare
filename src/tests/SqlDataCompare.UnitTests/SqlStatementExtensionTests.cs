using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;
using SqlDataCompare.Core;

namespace SqlDataCompare.UnitTests
{
    public class SqlStatementExtensionTests
    {
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
            var sut = SafeSqlValidator.GetFirstSqlStatement(sql);
            var couldParse = sut.TryParseSelectColumns(out string[] columns);

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

            var sut = SafeSqlValidator.GetFirstSqlStatement(sql);
            var couldParse = sut.TryParseSelectColumns(out string[] columns);

            Assert.IsTrue(couldParse);
            Assert.IsTrue(Enumerable.SequenceEqual(assertColumns, columns));
        }
    }
}
