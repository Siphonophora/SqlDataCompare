using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SqlDataCompare.Core
{
    public struct Validation
    {
        public Validation(bool valid, string message)
        {
            Valid = valid;
            Message = message ?? throw new ArgumentNullException(nameof(message));
        }

        public bool Valid { get; }

        public string Message { get; }

        public override string ToString()
        {
            return $"Valid: {Valid} - Message: {Message}";
        }
    }
}
