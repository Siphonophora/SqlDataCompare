using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace System.Text
{
    public static class StringBuilderExtensions
    {
        public static void AppendLineIf(this StringBuilder sb, bool logical, string text)
        {
            sb = sb ?? throw new ArgumentNullException(nameof(sb));

            if (logical)
            {
                sb.AppendLine(text);
            }
        }
    }
}
