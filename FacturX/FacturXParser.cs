/*
 * Licensed to the Apache Software Foundation (ASF) under one
 * or more contributor license agreements.  See the NOTICE file
 * distributed with this work for additional information
 * regarding copyright ownership.  The ASF licenses this file
 * to you under the Apache License, Version 2.0 (the
 * "License"); you may not use this file except in compliance
 * with the License.  You may obtain a copy of the License at
 *
 *   http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing,
 * software distributed under the License is distributed on an
 * "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY
 * KIND, either express or implied.  See the License for the
 * specific language governing permissions and limitations
 * under the License.
 */
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FactoorSharp.FacturXDocumentationParser.Common;

namespace FactoorSharp.FacturXDocumentationParser.FacturX
{
    public class FacturXParser
    {
        public static async Task<List<Element>> ParseAsync(string xsdPath, string excelPath)
        {
            IReadOnlyList<FacturXElementDocumentation> documentation = await DocumentationExcelParser.ParseAsync(excelPath);
            var documentationLookup = _BuildDocumentationLookup(documentation);

            FacrurXXsdSchemaParser schemaParser = new FacrurXXsdSchemaParser();
            List<Element> elements = schemaParser.Parse(xsdPath);

            XsdXPathBuilder.ComputeAbsoluteXPaths(elements);

            foreach (Element rootElement in elements)
            {
                _AddDocumentationToNodesRecursive(rootElement, documentationLookup);
            }

            return elements;
        } // !ParseAsync()


        private static void _AddDocumentationToNodesRecursive(Element element, Dictionary<string, FacturXElementDocumentation> documentationLookup)
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


        private static void _ApplyDocumentation(Element element, FacturXElementDocumentation elementDocumentation)
        { 
            element.Id = elementDocumentation.Id;
            element.BusinessTerm = elementDocumentation.BusinessTerm;
            element.BusinessRules.Add(elementDocumentation.BusinessRule);
            element.Description = elementDocumentation.Description;
            element.CiiCardinality = elementDocumentation.CiiCardinality;
            element.ProfileSupport = elementDocumentation.ProfileSupport;
        } //!_ApplyDocumentation()


        private static Dictionary<string, FacturXElementDocumentation> _BuildDocumentationLookup(IReadOnlyList<FacturXElementDocumentation> documentation)
        {
            var lookup = new Dictionary<string, FacturXElementDocumentation>();

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
