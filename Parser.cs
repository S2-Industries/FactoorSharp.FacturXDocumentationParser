using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FactoorSharp.FacturXDocumentationParser
{
    public class Parser
    {
        public static async Task<List<Element>> ParseAsync(string xsdPath, string excelPath)
        {
            IReadOnlyList<ElementDocumentation> documentation = await DocumentationExcelParser.ParseAsync(excelPath);
            var documentationLookup = _BuildDocumentationLookup(documentation);

            XsdSchemaParser schemaParser = new XsdSchemaParser();
            List<Element> elements = schemaParser.Parse(xsdPath);

            XsdXPathBuilder.ComputeAbsoluteXPaths(elements);

            foreach (Element rootElement in elements)
            {
                _AddDocumentationToNodesRecursive(rootElement, documentationLookup);
            }

            return elements;
        } // !ParseAsync()


        private static void _AddDocumentationToNodesRecursive(Element element, Dictionary<string, ElementDocumentation> documentationLookup)
        {
            if (documentationLookup.TryGetValue(element.XPath, out var elementDocumentation))
            {
                _ApplyDocumentation(element, elementDocumentation);
            }
            
            foreach (Element child in element.Children)
            {
                _AddDocumentationToNodesRecursive(child, documentationLookup);
            }
        } // !_AddDocumentationToNodesRecursive()


        private static void _ApplyDocumentation(Element element, ElementDocumentation elementDocumentation)
        { 
            element.Id = elementDocumentation.Id;
            element.BusinessTerm = elementDocumentation.BusinessTerm;
            element.BusinessRule = elementDocumentation.BusinessRule;
            element.Description = elementDocumentation.Description;
            element.CiiCardinality = elementDocumentation.CiiCardinality;
            element.ProfileSupport = elementDocumentation.ProfileSupport;
        } //!_ApplyDocumentation()


        private static Dictionary<string, ElementDocumentation> _BuildDocumentationLookup(IReadOnlyList<ElementDocumentation> documentation)
        {
            var lookup = new Dictionary<string, ElementDocumentation>();

            foreach (var doc in documentation)
            {
                // Füge den ersten XPath hinzu, falls er noch nicht existiert
                if (!string.IsNullOrWhiteSpace(doc.XpathXmlNorme1) && !lookup.ContainsKey(doc.XpathXmlNorme1))
                {
                    lookup.Add(doc.XpathXmlNorme1, doc);
                }

                // Füge den zweiten XPath hinzu, falls er noch nicht existiert und sich vom ersten unterscheidet
                if (!string.IsNullOrWhiteSpace(doc.XpathXmlNorme2) && doc.XpathXmlNorme2 != doc.XpathXmlNorme1 && !lookup.ContainsKey(doc.XpathXmlNorme2))
                {
                    lookup.Add(doc.XpathXmlNorme2, doc);
                }
            }
            return lookup;
        } //!_BuildDocumentationLookup()
    }
}
