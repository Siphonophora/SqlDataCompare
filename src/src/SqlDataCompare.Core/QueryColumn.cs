using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SqlDataCompare.Core
{
    public readonly struct QueryColumn : IEquatable<QueryColumn>
    {
        public QueryColumn(string name, bool isKey, int sortOrder, bool sortAscending)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            IsKey = isKey;
            SortOrder = sortOrder;
            SortAscending = sortAscending;
        }

        public string Name { get; }

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
            return Name == other.Name &&
                   IsKey == other.IsKey &&
                   SortOrder == other.SortOrder &&
                   SortAscending == other.SortAscending;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Name, IsKey, SortOrder, SortAscending);
        }
    }
}
