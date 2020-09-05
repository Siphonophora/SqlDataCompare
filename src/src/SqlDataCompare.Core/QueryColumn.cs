using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SqlDataCompare.Core
{
    public class QueryColumn
    {
        public QueryColumn(string name, bool isKey, int sortOrder)
        {
            Name = name;
            IsKey = isKey;
            SortOrder = sortOrder;
        }

        public string Name { get; private set; }

        public bool IsKey { get; set; }

        public int SortOrder { get; set; }
    }
}
