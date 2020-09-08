using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SqlDataCompare.Core.Enums
{
    public enum ParseResultValue
    {
        /// <summary>
        /// Something is fatally wrong with the sql.
        /// </summary>
        Error = 0,

        /// <summary>
        /// Something less severe is wrong, probably the query is empty.
        /// </summary>
        Warning,

        /// <summary>
        /// Sql is valid.
        /// </summary>
        Valid,
    }
}
