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
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace FactoorSharp.FacturXDocumentationParser
{
    /// <summary>
    /// Parses XRechnung Schematron (.sch) files and extracts all business rules.
    /// Handles both CII and UBL syntax bindings, including the common.sch patterns.
    /// </summary>
    internal class XRechnungSchematronParser
    {
        private static readonly XNamespace _Sch = "http://purl.oclc.org/dsdl/schematron";

        private static readonly Regex _BtBgRegex = new Regex(
            @"\b(BT-\d+[a-z]?|BG-\d+)\b",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        /// <summary>
        /// Parses one or more Schematron files and returns all extracted business rules.
        /// </summary>
        /// <remarks>
        /// This method merges rules from the syntax-specific Schematron file with optional shared rules
        /// from <c>common.sch</c>.
        /// </remarks>
        /// <param name="schFile">Path to the Schematron .sch file.</param>
        /// <param name="syntaxBinding">Human-readable syntax label, e.g. "CII" or "UBL".</param>
        /// <param name="commonSchFile">Optional path to the common.sch file to include.</param>
        /// <returns>A list of extracted specification rules.</returns>
        /// <exception cref="ArgumentException">
        /// Thrown when <paramref name="schFile"/> or <paramref name="syntaxBinding"/> is empty.
        /// </exception>
        /// <example>
        /// <code>
        /// var parser = new SchematronParser();
        /// List&lt;SpecificationRule&gt; rules = parser.Parse("XRechnung-UBL-validation.sch", "UBL", "common.sch");
        /// </code>
        /// </example>
        public List<SpecificationRule> Parse(string schFile, string syntaxBinding, string commonSchFile = null)
        {
            if (string.IsNullOrWhiteSpace(schFile))
            {
                throw new ArgumentException("The Schematron file path must not be empty.", nameof(schFile));
            }

            if (string.IsNullOrWhiteSpace(syntaxBinding))
            {
                throw new ArgumentException("The syntax binding must not be empty.", nameof(syntaxBinding));
            }

            List<SpecificationRule> rules = new List<SpecificationRule>();

            XDocument document = XDocument.Load(schFile);
            XElement schemaRoot = document.Root;

            if (schemaRoot == null)
            {
                return rules;
            }

            List<XElement> patterns = schemaRoot.Elements(_Sch + "pattern").ToList();

            if (!string.IsNullOrWhiteSpace(commonSchFile) && File.Exists(commonSchFile))
            {
                XDocument commonDocument = XDocument.Load(commonSchFile);
                List<XElement> commonPatterns = commonDocument.Root?.Elements(_Sch + "pattern").ToList() ?? new List<XElement>();
                patterns.AddRange(commonPatterns);
            }

            foreach (XElement pattern in patterns)
            {
                string patternId = pattern.Attribute("id")?.Value ?? string.Empty;

                foreach (XElement rule in pattern.Elements(_Sch + "rule"))
                {
                    string context = NormalizeWhitespace(rule.Attribute("context")?.Value ?? string.Empty);

                    foreach (XElement assert in rule.Elements(_Sch + "assert"))
                    {
                        string description = NormalizeWhitespace(ExtractDescription(assert));

                        rules.Add(new SpecificationRule
                        {
                            Id = assert.Attribute("id")?.Value ?? string.Empty,
                            Pattern = patternId,
                            SyntaxBinding = syntaxBinding,
                            Context = context,
                            Test = NormalizeWhitespace(assert.Attribute("test")?.Value ?? string.Empty),
                            Flag = assert.Attribute("flag")?.Value ?? string.Empty,
                            Description = description,
                            BtBgReferences = ExtractBtBgReferences(description)
                        });
                    }
                }
            }

            return rules;
        } // !Parse()

        private static string ExtractDescription(XElement assert)
        {
            StringBuilder builder = new StringBuilder();

            foreach (XNode node in assert.Nodes())
            {
                if (node is XText text)
                {
                    builder.Append(text.Value);
                }
                else if (node is XElement child)
                {
                    string localName = child.Name.LocalName;
                    if (localName == "name")
                    {
                        builder.Append("{element}");
                    }
                    else if (localName == "value-of")
                    {
                        string select = child.Attribute("select")?.Value ?? string.Empty;
                        builder.Append($"{{value-of:{select}}}");
                    }
                }
            }

            return builder.ToString().Trim();
        } // !ExtractDescription()

        private static List<string> ExtractBtBgReferences(string description)
        {
            HashSet<string> references = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (Match match in _BtBgRegex.Matches(description))
            {
                references.Add(match.Value.ToUpperInvariant());
            }

            return references.OrderBy(reference => reference).ToList();
        } // !ExtractBtBgReferences()

        private static string NormalizeWhitespace(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return value;
            }

            return Regex.Replace(value.Trim(), @"\s+", " ");
        } // !NormalizeWhitespace()
    }
}
