using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace FactoorSharp.FacturXDocumentationParser
{
    /// <summary>
    /// Represents a row from the "Factur-X CII D22B EXTENDED" sheet.
    /// All fields are strings — validation and type conversion should be performed externally.
    /// </summary>
    [DebuggerDisplay("{Id}")]
    internal sealed class ElementDocumentation
    {
        public string Id { get; set; } = string.Empty;
        public string IdCtcFrReform { get; set; } = string.Empty;
        public string XsdLevel { get; set; } = string.Empty;
        public string En16931SemanticCardinality { get; set; } = string.Empty;
        public string BusinessTerm { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string UsageNote { get; set; } = string.Empty;
        public string Cius { get; set; } = string.Empty;
        public string BusinessRule { get; set; } = string.Empty;
        public string SemanticDataType { get; set; } = string.Empty;
        public string ExtProfilesCardinality { get; set; } = string.Empty;
        public string XpathXmlNorme1 { get; set; } = string.Empty;
        public string XpathXmlNorme2 { get; set; } = string.Empty;
        public string Dt { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string CiiCardinality { get; set; } = string.Empty;
        public string Match { get; set; } = string.Empty;
        public string Rules { get; set; } = string.Empty;
        public List<string> ProfileSupport { get; internal set; } = new List<string>();

        public string CalculateName()
        {
            if (string.IsNullOrEmpty(XpathXmlNorme1))
            {
                return string.Empty;
            }

            var parts = XpathXmlNorme1.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            return parts.Length == 0 ? string.Empty : parts[parts.Length - 1];
        } // !CalculateName()
    }
}
