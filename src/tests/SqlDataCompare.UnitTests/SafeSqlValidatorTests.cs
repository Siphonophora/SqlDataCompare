using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
            var sut = new SafeSqlValidator();

            var result = sut.ValidateIsSafe(sql);

            Assert.IsTrue(result.Valid);
            Assert.IsNotEmpty(result.Message);
        }

        [TestCase("Delete from t")]
        [TestCase("Select * Into t From Temp")]
        [TestCase("Drop Table T")]
        [TestCase("INSERT INTO [dbo].[Alert_ProgramsTemp] ([Program]) VALUES('s')")]
        [TestCase("UPDATE [dbo].[Alert_ProgramsTemp] SET [Program] = 'b' WHERE 1 = 0")]
        public void ValidateIsSafe_SingleStatement_NotSafe(string sql)
        {
            var sut = new SafeSqlValidator();

            var result = sut.ValidateIsSafe(sql);

            Assert.IsFalse(result.Valid);
            Assert.IsNotEmpty(result.Message);
        }

        [TestCase("Delete from")]
        [TestCase("Select * InFrom Temp")]
        public void ValidateIsSafe_SingleStatement_SqlError(string sql)
        {
            var sut = new SafeSqlValidator();

            var result = sut.ValidateIsSafe(sql);

            Assert.IsFalse(result.Valid);
            Assert.IsTrue(result.Message.StartsWith("Sql Has Errors"));
        }

        [TestCase("DropTable T")]
        public void ValidateIsSafe_SingleStatement_NotSQL(string sql)
        {
            var sut = new SafeSqlValidator();

            var result = sut.ValidateIsSafe(sql);

            Assert.IsFalse(result.Valid);
        }

        [TestCase("Select * From  T", "Select * From  T")]
        [TestCase("Select * From  T", "GO", "Select * From  T")]
        [TestCase("Select * From  T", "Select Distinct * From  T")]
        [TestCase("Select", "field", "From", "T")]
        public void ValidateIsSafe_MultipleStatements_IsSafe(params string[] sqlArray)
        {
            string sql = string.Join("\r\n", sqlArray);
            var sut = new SafeSqlValidator();

            var result = sut.ValidateIsSafe(sql);

            Assert.IsTrue(result.Valid);
            Assert.IsNotEmpty(result.Message);
        }

        [TestCase("Select * From  T", "Delete From  T")]
        [TestCase("Delete From  T", "Select * From  T")]
        public void ValidateIsSafe_MultipleStatements_NotSafe(params string[] sqlArray)
        {
            string sql = string.Join("\r\n", sqlArray);
            var sut = new SafeSqlValidator();

            var result = sut.ValidateIsSafe(sql);

            Assert.IsFalse(result.Valid);
            Assert.IsNotEmpty(result.Message);
        }

        [TestCase(true, "Select a From  T")]
        [TestCase(true, "Select a Into t From Temp", "Select a From  T")]
        [TestCase(true, "Select a Into t From Temp", "Select a From  T", "--comment")]
        [TestCase(false, "Select a From  T", "Select a From  T")]
        [TestCase(false, "Select a From  T", "Select a Into t From Temp")]
        public void ValidateSingleResultSet_MultipleStatements_MatchAssert(bool assert, params string[] sqlArray)
        {
            string sql = string.Join("\r\n", sqlArray);
            var sut = new SafeSqlValidator();

            var result = sut.ValidateSingleResultSet(sql);

            Assert.AreEqual(assert, result.Valid);
            Assert.IsNotEmpty(result.Message);
        }
    }
}
