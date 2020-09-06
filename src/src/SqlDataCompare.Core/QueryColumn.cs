using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SqlDataCompare.Core
{
    public readonly struct QueryColumn : IEquatable<QueryColumn>
    {
        public QueryColumn(string columnName, bool isKey, int sortOrder, bool sortAscending)
        {
            ColumnName = columnName ?? throw new ArgumentNullException(nameof(columnName));
            IsKey = isKey;
            SortOrder = sortOrder;
            SortAscending = sortAscending;
        }

        public string ColumnName { get; }

        public bool IsKey { get; }

        public int SortOrder { get; }

        public bool SortAscending { get; }

        public static bool operator ==(QueryColumn left, QueryColumn right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(QueryColumn left, QueryColumn right)
        {
            return !(left == right);
        }

        public override bool Equals(object? obj)
        {
            return obj is QueryColumn column && Equals(column);
        }

        public bool Equals(QueryColumn other)
        {
            return ColumnName == other.ColumnName &&
                   IsKey == other.IsKey &&
                   SortOrder == other.SortOrder &&
                   SortAscending == other.SortAscending;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(ColumnName, IsKey, SortOrder, SortAscending);
        }
    }
}
