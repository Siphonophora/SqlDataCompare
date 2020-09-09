using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SqlDataCompare.Core.Enums;

namespace SqlDataCompare.Core.Models
{
    public readonly struct ParsedSql : IEquatable<ParsedSql>
    {
        private readonly List<string> columnNames;

        public ParsedSql(string sql, ParseResultValue parseResult, string validationMessage)
            : this(sql, parseResult, validationMessage, new List<string>(), default)
        {
            if (parseResult == ParseResultValue.Valid)
            {
                throw new ArgumentException("Valid parsed sql must supply columns");
            }
        }

        public ParsedSql(string sql, ParseResultValue parseResult, string validationMessage, List<string> columnNames, int intoIndex)
        {
            this.columnNames = columnNames ?? throw new ArgumentNullException(nameof(columnNames));
            Sql = sql ?? throw new ArgumentNullException(nameof(sql));
            ParseResult = parseResult;
            ValidationMessage = validationMessage ?? throw new ArgumentNullException(nameof(validationMessage));
            IntoIndex = intoIndex;
        }

        public string Sql { get; }

        public int IntoIndex { get; }

        /// <summary>
        /// Valid should mean both that the SQL is valid, and that we consider it safe (side effect free).
        /// </summary>
        public ParseResultValue ParseResult { get; }

        public string ValidationMessage { get; }

        public IEnumerable<string> ColumnNames => columnNames.AsReadOnly();

        /// <summary>
        /// TODO unit test for single # in param
        /// </summary>
        /// <param name="intoTable"></param>
        /// <returns></returns>
        public string GetSqlWithInto(string intoTable)
        {
            if (ParseResult != ParseResultValue.Valid)
            {
                throw new InvalidOperationException($"Unable to produce for sql with a parse result of: {ParseResult}");
            }

            return Sql.Insert(IntoIndex, $"{Environment.NewLine}Into {intoTable}{Environment.NewLine}");
        }

        public override bool Equals(object? obj)
        {
            return obj is ParsedSql sql && Equals(sql);
        }

        public bool Equals(ParsedSql other)
        {
            return Sql == other.Sql &&
                   ParseResult == other.ParseResult &&
                   ValidationMessage == other.ValidationMessage &&
                   EqualityComparer<IEnumerable<string>>.Default.Equals(ColumnNames, other.ColumnNames);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Sql, ParseResult, ValidationMessage, ColumnNames);
        }
    }
}
