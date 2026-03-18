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
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using FactoorSharp.FacturXDocumentationParser.Common;

namespace FactoorSharp.FacturXDocumentationParser.XRechnung3
{
    /// <summary>
    /// Generic XSD parser that extracts the element hierarchy and cardinalities
    /// from any W3C XML Schema set.
    /// </summary>
    /// <remarks>
    /// The parser supports both CII schemas with inline element declarations and UBL schemas that use
    /// global element references.
    /// </remarks>
    /// <example>
    /// <code>
    /// var parser = new XRechnungXsdParser();
    /// List&lt;SpecElement&gt; elements = parser.ParseCii("documentation/zugferd211en/Schema/EN16931");
    /// </code>
    /// </example>
    public class XRechnungXsdParser
    {
        private static readonly XNamespace _Xs = "http://www.w3.org/2001/XMLSchema";

        /// <summary>
        /// Options for parsing the EN16931 CII (Factur-X / ZUGFeRD) schema.
        /// </summary>
        public static readonly XsdParserOptions CiiOptions = new XsdParserOptions
        {
            RootXsdFileName = "FACTUR-X_EN16931.xsd",
            RootElementPrefix = "rsm",
            NamespacePrefixes = new Dictionary<string, string>
        {
            { "urn:un:unece:uncefact:data:standard:CrossIndustryInvoice:100", "rsm" },
            { "urn:un:unece:uncefact:data:standard:ReusableAggregateBusinessInformationEntity:100", "ram" },
            { "urn:un:unece:uncefact:data:standard:UnqualifiedDataType:100", "udt" },
            { "urn:un:unece:uncefact:data:standard:QualifiedDataType:100", "qdt" }
        }
        };

        /// <summary>
        /// Options for parsing the OASIS UBL 2.1 Invoice schema.
        /// </summary>
        public static readonly XsdParserOptions UblInvoiceOptions = new XsdParserOptions
        {
            RootXsdFileName = "UBL-Invoice-2.1.xsd",
            RootElementPrefix = "ubl",
            NamespacePrefixes = new Dictionary<string, string>
        {
            { "urn:oasis:names:specification:ubl:schema:xsd:Invoice-2", "ubl" },
            { "urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2", "cac" },
            { "urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2", "cbc" },
            { "urn:oasis:names:specification:ubl:schema:xsd:QualifiedDataTypes-2", "qdt" },
            { "urn:oasis:names:specification:ubl:schema:xsd:UnqualifiedDataTypes-2", "udt" },
            { "urn:oasis:names:specification:ubl:schema:xsd:CommonExtensionComponents-2", "ext" }
        }
        };

        /// <summary>
        /// Options for parsing the OASIS UBL 2.1 CreditNote schema.
        /// </summary>
        public static readonly XsdParserOptions UblCreditNoteOptions = new XsdParserOptions
        {
            RootXsdFileName = "UBL-CreditNote-2.1.xsd",
            RootElementPrefix = "cn",
            NamespacePrefixes = new Dictionary<string, string>
        {
            { "urn:oasis:names:specification:ubl:schema:xsd:CreditNote-2", "cn" },
            { "urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2", "cac" },
            { "urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2", "cbc" },
            { "urn:oasis:names:specification:ubl:schema:xsd:QualifiedDataTypes-2", "qdt" },
            { "urn:oasis:names:specification:ubl:schema:xsd:UnqualifiedDataTypes-2", "udt" },
            { "urn:oasis:names:specification:ubl:schema:xsd:CommonExtensionComponents-2", "ext" }
        }
        };

        private readonly Dictionary<string, XElement> _ComplexTypes = new Dictionary<string, XElement>(StringComparer.Ordinal);
        private readonly Dictionary<string, string> _TypeNamespace = new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly Dictionary<string, XElement> _GlobalElements = new Dictionary<string, XElement>(StringComparer.Ordinal);
        private readonly Dictionary<string, string> _GlobalElementNamespace = new Dictionary<string, string>(StringComparer.Ordinal);

        /// <summary>
        /// Parses the XSD files in the given directory using preconfigured CII options.
        /// </summary>
        /// <remarks>
        /// Use this overload for Factur-X or ZUGFeRD EN16931 schemas.
        /// </remarks>
        /// <param name="xsdDirectory">Directory containing the EN16931 CII XSD files.</param>
        /// <returns>A hierarchical list of schema elements rooted at the invoice document element.</returns>
        /// <exception cref="ArgumentException">Thrown when <paramref name="xsdDirectory"/> is empty.</exception>
        /// <exception cref="FileNotFoundException">Thrown when the configured root XSD file cannot be found.</exception>
        /// <example>
        /// <code>
        /// var parser = new XRechnungXsdParser();
        /// List&lt;SpecElement&gt; elements = parser.ParseCii("documentation/zugferd211en/Schema/EN16931");
        /// </code>
        /// </example>
        public List<Element> ParseCii(string xsdDirectory)
        {
            return Parse(xsdDirectory, CiiOptions);
        } // !ParseCii()

        /// <summary>
        /// Parses the XSD files in the given directory using preconfigured UBL 2.1 Invoice options.
        /// </summary>
        /// <remarks>
        /// Use this overload when the schema set contains <c>UBL-Invoice-2.1.xsd</c>.
        /// </remarks>
        /// <param name="xsdDirectory">Directory containing the OASIS UBL 2.1 Invoice XSD files.</param>
        /// <returns>A hierarchical list of schema elements rooted at the invoice document element.</returns>
        /// <exception cref="ArgumentException">Thrown when <paramref name="xsdDirectory"/> is empty.</exception>
        /// <exception cref="FileNotFoundException">Thrown when the configured root XSD file cannot be found.</exception>
        /// <example>
        /// <code>
        /// var parser = new XRechnungXsdParser();
        /// List&lt;SpecElement&gt; elements = parser.ParseUblInvoice("documentation/ubl/xsd/maindoc");
        /// </code>
        /// </example>
        public List<Element> ParseUblInvoice(string xsdDirectory)
        {
            return Parse(xsdDirectory, UblInvoiceOptions);
        } // !ParseUblInvoice()

        /// <summary>
        /// Parses the XSD files in the given directory using preconfigured UBL 2.1 CreditNote options.
        /// </summary>
        /// <remarks>
        /// Use this overload when the schema set contains <c>UBL-CreditNote-2.1.xsd</c>.
        /// </remarks>
        /// <param name="xsdDirectory">Directory containing the OASIS UBL 2.1 CreditNote XSD files.</param>
        /// <returns>A hierarchical list of schema elements rooted at the credit note document element.</returns>
        /// <exception cref="ArgumentException">Thrown when <paramref name="xsdDirectory"/> is empty.</exception>
        /// <exception cref="FileNotFoundException">Thrown when the configured root XSD file cannot be found.</exception>
        /// <example>
        /// <code>
        /// var parser = new XRechnungXsdParser();
        /// List&lt;SpecElement&gt; elements = parser.ParseUblCreditNote("documentation/ubl/xsd/maindoc");
        /// </code>
        /// </example>
        public List<Element> ParseUblCreditNote(string xsdDirectory)
        {
            return Parse(xsdDirectory, UblCreditNoteOptions);
        } // !ParseUblCreditNote()

        /// <summary>
        /// Parses the XSD files in the given directory using the supplied options.
        /// </summary>
        /// <remarks>
        /// This overload lets callers provide custom schema roots and namespace mappings.
        /// </remarks>
        /// <param name="xsdDirectory">Directory that contains the XSD files to parse.</param>
        /// <param name="options">Configuration describing the root XSD file and namespace mappings.</param>
        /// <returns>A hierarchical list of schema elements rooted at the configured document element.</returns>
        /// <exception cref="ArgumentException">Thrown when <paramref name="xsdDirectory"/> is empty.</exception>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="options"/> is <see langword="null"/>.</exception>
        /// <exception cref="FileNotFoundException">Thrown when the configured root XSD file cannot be found.</exception>
        /// <exception cref="InvalidOperationException">Thrown when a schema file cannot be loaded.</exception>
        /// <example>
        /// <code>
        /// var parser = new XRechnungXsdParser();
        /// List&lt;SpecElement&gt; elements = parser.Parse("documentation/schemas", XRechnungXsdParser.CiiOptions);
        /// </code>
        /// </example>
        public List<Element> Parse(string xsdDirectory, XsdParserOptions options)
        {
            if (string.IsNullOrWhiteSpace(xsdDirectory))
            {
                throw new ArgumentException("The XSD directory must not be empty.", nameof(xsdDirectory));
            }

            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            _ComplexTypes.Clear();
            _TypeNamespace.Clear();
            _GlobalElements.Clear();
            _GlobalElementNamespace.Clear();

            LoadSchemas(xsdDirectory);

            string rootXsdPath = Path.Combine(xsdDirectory, options.RootXsdFileName);
            if (!File.Exists(rootXsdPath))
            {
                throw new FileNotFoundException(
                    $"Root XSD not found: {rootXsdPath}. Ensure the XSD directory contains '{options.RootXsdFileName}'.",
                    rootXsdPath);
            }

            XDocument rootXsd = XDocument.Load(rootXsdPath);
            XElement rootElementDeclaration = rootXsd.Root?.Elements(_Xs + "element").FirstOrDefault();
            if (rootElementDeclaration == null)
            {
                return new List<Element>();
            }

            string rootName = rootElementDeclaration.Attribute("name")?.Value ?? string.Empty;
            string rootTypeName = StripPrefix(rootElementDeclaration.Attribute("type")?.Value ?? string.Empty);
            string rootNamespacePrefix = options.RootElementPrefix;

            Element root = new Element
            {
                Name = $"{rootNamespacePrefix}:{rootName}",
                XPath = $"/{rootNamespacePrefix}:{rootName}",
                TypeName = rootTypeName,
                XsdCardinality = new Cardinality("1", "1")
            };

            ExpandChildren(root, rootTypeName, options, new HashSet<string>());

            return new List<Element> { root };
        } // !Parse()

        private void LoadSchemas(string xsdDirectory)
        {
            foreach (string xsdFile in Directory.GetFiles(xsdDirectory, "*.xsd"))
            {
                XDocument document;

                try
                {
                    document = XDocument.Load(xsdFile);
                }
                catch (IOException ex)
                {
                    throw new InvalidOperationException($"Failed to load schema '{xsdFile}'.", ex);
                }
                catch (UnauthorizedAccessException ex)
                {
                    throw new InvalidOperationException($"Failed to load schema '{xsdFile}'.", ex);
                }
                catch (XmlException ex)
                {
                    throw new InvalidOperationException($"Failed to load schema '{xsdFile}'.", ex);
                }

                if (document.Root == null)
                {
                    continue;
                }

                string targetNamespace = document.Root.Attribute("targetNamespace")?.Value ?? string.Empty;

                foreach (XElement complexType in document.Root.Elements(_Xs + "complexType"))
                {
                    string name = complexType.Attribute("name")?.Value ?? string.Empty;
                    if (name.Length > 0 && !_ComplexTypes.ContainsKey(name))
                    {
                        _ComplexTypes[name] = complexType;
                        _TypeNamespace[name] = targetNamespace;
                    }
                }

                foreach (XElement element in document.Root.Elements(_Xs + "element"))
                {
                    string name = element.Attribute("name")?.Value ?? string.Empty;
                    if (name.Length > 0 && !_GlobalElements.ContainsKey(name))
                    {
                        _GlobalElements[name] = element;
                        _GlobalElementNamespace[name] = targetNamespace;
                    }
                }
            }
        } // !LoadSchemas()

        private void ExpandChildren(
            Element parent,
            string typeName,
            XsdParserOptions options,
            HashSet<string> visitedTypes)
        {
            if (string.IsNullOrEmpty(typeName) || !_ComplexTypes.TryGetValue(typeName, out XElement complexType))
            {
                return;
            }

            if (!visitedTypes.Add(typeName))
            {
                return;
            }

            IEnumerable<XElement> elementDeclarations = CollectElementDeclarations(complexType);

            foreach (XElement elementDeclaration in elementDeclarations)
            {
                string childName;
                string childTypeName;
                string namespacePrefix;
                int minOccurs = ParseOccurs(elementDeclaration.Attribute("minOccurs")?.Value, 1);
                int maxOccurs = ParseOccurs(elementDeclaration.Attribute("maxOccurs")?.Value, 1);

                string referencedElement = elementDeclaration.Attribute("ref")?.Value ?? string.Empty;
                if (referencedElement.Length > 0)
                {
                    string referencedLocalName = StripPrefix(referencedElement);
                    string referencedPrefix = GetPrefixFromQName(referencedElement);

                    if (!string.IsNullOrEmpty(referencedPrefix))
                    {
                        namespacePrefix = referencedPrefix;
                    }
                    else if (_GlobalElementNamespace.TryGetValue(referencedLocalName, out string elementNamespace)
                        && !string.IsNullOrEmpty(elementNamespace))
                    {
                        namespacePrefix = ResolvePrefix(elementNamespace, options);
                    }
                    else
                    {
                        namespacePrefix = options.RootElementPrefix;
                    }

                    childName = referencedLocalName;
                    childTypeName = string.Empty;

                    if (_GlobalElements.TryGetValue(referencedLocalName, out XElement globalElement))
                    {
                        string globalType = globalElement.Attribute("type")?.Value;
                        childTypeName = globalType == null ? string.Empty : StripPrefix(globalType);
                    }
                }
                else
                {
                    childName = elementDeclaration.Attribute("name")?.Value;
                    if (string.IsNullOrEmpty(childName))
                    {
                        continue;
                    }

                    string rawType = elementDeclaration.Attribute("type")?.Value;
                    childTypeName = rawType == null ? string.Empty : StripPrefix(rawType);

                    if (!string.IsNullOrEmpty(childTypeName)
                        && _TypeNamespace.TryGetValue(childTypeName, out string typeNamespace)
                        && !string.IsNullOrEmpty(typeNamespace))
                    {
                        namespacePrefix = ResolvePrefix(typeNamespace, options);
                    }
                    else
                    {
                        string parentTypeNamespace = _TypeNamespace.TryGetValue(typeName, out string parentNamespace)
                            ? parentNamespace
                            : string.Empty;
                        namespacePrefix = ResolvePrefix(parentTypeNamespace, options);
                    }
                }

                if (string.IsNullOrEmpty(childName))
                {
                    continue;
                }

                string childXPath = string.Concat(parent.XPath, "/", namespacePrefix, ":", childName);

                Element child = new Element
                {
                    Name = $"{namespacePrefix}:{childName}",
                    XPath = childXPath,
                    TypeName = childTypeName ?? string.Empty,
                    XsdCardinality = new Cardinality(minOccurs.ToString(), maxOccurs.ToString())
                };

                parent.Children.Add(child);

                if (!string.IsNullOrEmpty(childTypeName))
                {
                    ExpandChildren(child, childTypeName, options, new HashSet<string>(visitedTypes));
                }
            }

            visitedTypes.Remove(typeName);
        } // !ExpandChildren()

        private static IEnumerable<XElement> CollectElementDeclarations(XElement complexType)
        {
            XElement container = complexType.Elements(_Xs + "sequence").FirstOrDefault()
                ?? complexType.Elements(_Xs + "all").FirstOrDefault()
                ?? complexType.Elements(_Xs + "choice").FirstOrDefault();

            if (container == null)
            {
                XElement extension = complexType.Descendants(_Xs + "extension").FirstOrDefault();
                if (extension != null)
                {
                    container = extension.Elements(_Xs + "sequence").FirstOrDefault()
                        ?? extension.Elements(_Xs + "all").FirstOrDefault();
                }
            }

            if (container == null)
            {
                return Enumerable.Empty<XElement>();
            }

            List<XElement> result = new List<XElement>();
            foreach (XNode node in container.Nodes())
            {
                if (node is XElement child)
                {
                    string localName = child.Name.LocalName;
                    if (localName == "element")
                    {
                        result.Add(child);
                    }
                    else if (localName == "sequence" || localName == "all" || localName == "choice")
                    {
                        result.AddRange(child.Elements(_Xs + "element"));
                    }
                }
            }

            return result;
        } // !CollectElementDeclarations()

        private static string ResolvePrefix(string namespaceUri, XsdParserOptions options)
        {
            if (!string.IsNullOrEmpty(namespaceUri)
                && options.NamespacePrefixes.TryGetValue(namespaceUri, out string prefix))
            {
                return prefix;
            }

            return options.RootElementPrefix;
        } // !ResolvePrefix()

        private static string StripPrefix(string qualifiedName)
        {
            int colon = qualifiedName.IndexOf(':');
            return colon >= 0 ? qualifiedName.Substring(colon + 1) : qualifiedName;
        } // !StripPrefix()

        private static string GetPrefixFromQName(string qualifiedName)
        {
            int colon = qualifiedName.IndexOf(':');
            return colon >= 0 ? qualifiedName.Substring(0, colon) : string.Empty;
        } // !GetPrefixFromQName()

        private static int ParseOccurs(string value, int defaultValue)
        {
            if (string.IsNullOrEmpty(value))
            {
                return defaultValue;
            }

            if (value == "unbounded")
            {
                return -1;
            }

            return int.TryParse(value, out int result) ? result : defaultValue;
        } // !ParseOccurs()

        private static string BuildCardinality(int min, int max)
        {
            string maxValue = max == -1 ? "n" : max.ToString();
            return string.Concat(min.ToString(), "..", maxValue);
        } // !BuildCardinality()
    }

    /// <summary>
    /// Configuration options for <see cref="XRechnungXsdParser"/>.
    /// </summary>
    /// <remarks>
    /// These options define the root schema document and namespace prefix mapping used for XPath generation.
    /// </remarks>
    /// <example>
    /// <code>
    /// var options = new XsdParserOptions();
    /// options.RootXsdFileName = "UBL-Invoice-2.1.xsd";
    /// </code>
    /// </example>
    public class XsdParserOptions
    {
        /// <summary>
        /// File name of the root (main document) XSD within the schema directory.
        /// E.g., <c>FACTUR-X_EN16931.xsd</c> or <c>UBL-Invoice-2.1.xsd</c>.
        /// </summary>
        public string RootXsdFileName { get; set; } = string.Empty;

        /// <summary>
        /// Namespace prefix to use for the root element in XPath expressions.
        /// E.g., <c>rsm</c> for CII or <c>ubl</c> for UBL Invoice.
        /// </summary>
        public string RootElementPrefix { get; set; } = string.Empty;

        /// <summary>
        /// Maps namespace URIs to their conventional short prefixes.
        /// Used when deriving element XPaths from the schema.
        /// </summary>
        public Dictionary<string, string> NamespacePrefixes { get; set; } = new Dictionary<string, string>(StringComparer.Ordinal);
    }
}
