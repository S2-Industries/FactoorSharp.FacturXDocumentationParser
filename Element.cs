using System.Collections.Generic;
using System.Diagnostics;

namespace FactoorSharp.FacturXDocumentationParser
{
    [DebuggerDisplay("{Name}, XPath: {XPath}")]
    public sealed class Element
    {
        public string Name { get; set; } = string.Empty;         // enthält ggf. Prefix (z.B. "ram:ExchangedDocumentContext")
        public string TypeName { get; set; } = string.Empty;     // enthält ggf. Prefix (z.B. "ram:ExchangedDocumentContextType")
        public string XsdCardinality { get; set; } = string.Empty;
        public List<Element> Children { get; } = new List<Element>();

        // Neuer Property zum Speichern des berechneten absoluten XPath
        // Wird vom XsdXPathBuilder gesetzt.
        public string XPath { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public List<string> ProfileSupport { get; internal set; }
        public string BusinessTerm { get; internal set; }
        public string Id { get; internal set; }
        public string BusinessRule { get; internal set; }
        public Dictionary<string, string> AdditionalData { get; set; } = new Dictionary<string, string>();
    }
}
