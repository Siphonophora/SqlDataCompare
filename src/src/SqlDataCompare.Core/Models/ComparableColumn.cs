using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SqlDataCompare.Core.Models
{
    public class ComparableColumn
    {
        public ComparableColumn(string columnName, int columnOrder)
        {
            ColumnName = columnName ?? throw new ArgumentNullException(nameof(columnName));
            ColumnOrder = columnOrder;
        }

        public string ColumnName { get; }

        public bool IsKey { get; set; }

        /// <summary>
        /// Defines the order the keys are sorted in the output.
        /// </summary>
        public int KeySortOrder { get; set; }

        /// <summary>
        /// Defines the display order of the columns.
        /// </summary>
        public int ColumnOrder { get; set; }

        public bool SortDescending { get; set; }
    }
}
