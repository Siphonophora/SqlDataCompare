using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using SqlDataCompare.Core.Enums;

namespace SqlDataCompare.UnitTests.Assetts
{
    internal class TestSqlProvider : IEnumerable
    {
        public IEnumerator GetEnumerator()
        {
            var options = new JsonSerializerOptions();
            options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
            var tests = JsonSerializer.Deserialize<List<TestSql>>(File.ReadAllText("Assets\\TestSql.json"), options);

            return tests.Select(x => new object[] { x.ParseResult, x.Comment, x.Sql, x.Columns }).GetEnumerator();
        }

        private class TestSql
        {
            public string Sql { get; set; } = string.Empty;

            public string Comment { get; set; } = string.Empty;

            public ParseResultValue ParseResult { get; set; }

            public List<string> Columns { get; set; } = new List<string>();
        }
    }
}
