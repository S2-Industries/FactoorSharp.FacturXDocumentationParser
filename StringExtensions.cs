using System;
using System.Collections.Generic;
using System.Text;

namespace FactoorSharp.FacturXDocumentationParser
{
    internal static class StringExtensions
    {
        // Small helper extension to call ToString with invariant culture (more readable code)
        public static string ToStringInvariant(this int value)
        {
            return value.ToString(System.Globalization.CultureInfo.InvariantCulture);
        } // !ToStringInvariant()
    }
}
