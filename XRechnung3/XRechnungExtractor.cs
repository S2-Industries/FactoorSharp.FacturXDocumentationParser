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
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using FactoorSharp.FacturXDocumentationParser.Common;

namespace FactoorSharp.FacturXDocumentationParser.XRechnung3
{
    /// <summary>
    /// Syntax binding that controls which XSD schema and schematron rules are extracted.
    /// </summary>
    public enum SyntaxBinding
    {
        /// <summary>
        /// UN/CEFACT Cross Industry Invoice (CII) — used by ZUGFeRD / Factur-X.
        /// XSD: EN16931 FACTUR-X XSD files (e.g. from documentation/zugferd211en/Schema/EN16931).
        /// </summary>
        Cii,

        /// <summary>
        /// OASIS UBL 2.1 Invoice — used by XRechnung UBL.
        /// XSD: OASIS UBL 2.1 Invoice XSD files (UBL-Invoice-2.1.xsd + companions).
        /// Download: http://docs.oasis-open.org/ubl/os-UBL-2.1/UBL-2.1.zip
        /// </summary>
        UblInvoice,

        /// <summary>
        /// OASIS UBL 2.1 CreditNote — used by XRechnung UBL credit notes.
        /// XSD: OASIS UBL 2.1 CreditNote XSD files (UBL-CreditNote-2.1.xsd + companions).
        /// Download: http://docs.oasis-open.org/ubl/os-UBL-2.1/UBL-2.1.zip
        /// </summary>
        UblCreditNote
    }

    /// <summary>
    /// Main entry point for extracting XRechnung specification documentation.
    /// Combines information from EN16931 / UBL XSD schemas and XRechnung Schematron rules.
    /// </summary>
    public class XRechnungExtractor
    {
        /// <summary>
        /// Extracts the XRechnung specification from the given documentation root directory.
        /// </summary>
        /// <param name="xrechnungDocPath">
        /// Path to the XRechnung version folder, e.g.
        /// "documentation/xRechnung/XRechnung 3.0.1"
        /// </param>
        /// <param name="xsdPath">
        /// Path to the XSD directory.
        /// <list type="bullet">
        ///   <item>For CII: path to the EN16931 FACTUR-X XSD directory
        ///         (e.g. "documentation/zugferd211en/Schema/EN16931")</item>
        ///   <item>For UBL: path to the directory containing the OASIS UBL 2.1 XSD files,
        ///         specifically the one that contains <c>UBL-Invoice-2.1.xsd</c> or
        ///         <c>UBL-CreditNote-2.1.xsd</c>.
        ///         Download the full package from
        ///         http://docs.oasis-open.org/ubl/os-UBL-2.1/UBL-2.1.zip and point to the
        ///         <c>xsd/maindoc</c> or <c>xsdrt/maindoc</c> directory.</item>
        /// </list>
        /// </param>
        /// <param name="syntax">Whether to extract CII or UBL structure and rules.</param>
        /// <param name="version">XRechnung version label, e.g. "3.0.1"</param>
        /// <remarks>
        /// The extracted specification combines schema structure and Schematron business rules for the selected syntax.
        /// </remarks>
        /// <returns>The fully populated <see cref="XRechnungSpec"/>.</returns>
        /// <exception cref="ArgumentException">
        /// Thrown when any required path or version value is empty.
        /// </exception>
        /// <example>
        /// <code>
        /// var extractor = new XRechnungExtractor();
        /// XRechnungSpec spec = extractor.Extract("documentation/xRechnung/XRechnung 3.0.1", "documentation/ubl", SyntaxBinding.UblInvoice);
        /// </code>
        /// </example>
        public XRechnungSpecification Extract(
            string xrechnungDocPath,
            string xsdPath,
            SyntaxBinding syntax = SyntaxBinding.Cii,
            string version = "3.0.1")
        {
            if (string.IsNullOrWhiteSpace(xrechnungDocPath))
            {
                throw new ArgumentException("The XRechnung documentation path must not be empty.", nameof(xrechnungDocPath));
            }

            if (string.IsNullOrWhiteSpace(xsdPath))
            {
                throw new ArgumentException("The XSD path must not be empty.", nameof(xsdPath));
            }

            if (string.IsNullOrWhiteSpace(version))
            {
                throw new ArgumentException("The XRechnung version must not be empty.", nameof(version));
            }

            XRechnungSpecification spec = new XRechnungSpecification
            {
                Version = version,
                Syntax = syntax.ToString()
            };

            XRechnungXsdParser xsdParser = new XRechnungXsdParser();

            switch (syntax)
            {
                case SyntaxBinding.UblInvoice:
                    {
                        spec.Elements = xsdParser.ParseUblInvoice(xsdPath);
                        break;
                    }
                case SyntaxBinding.UblCreditNote:
                    {
                        spec.Elements = xsdParser.ParseUblCreditNote(xsdPath);
                        break;
                    }
                default:
                    {
                        xsdParser.ParseCii(xsdPath);
                        break;
                    }
            }

            string schematronRoot = Path.Combine(xrechnungDocPath, $"xrechnung-{version}-schematron-2.0.1", "schematron");
            string commonSchFile = Path.Combine(schematronRoot, "common.sch");
            string ciiSchFile = Path.Combine(schematronRoot, "cii", "XRechnung-CII-validation.sch");
            string ublSchFile = Path.Combine(schematronRoot, "ubl", "XRechnung-UBL-validation.sch");

            XRechnungSchematronParser schParser = new XRechnungSchematronParser();

            if (syntax == SyntaxBinding.Cii && File.Exists(ciiSchFile))
            {
                spec.Rules.AddRange(schParser.Parse(ciiSchFile, "CII", commonSchFile));
            }
            else if ((syntax == SyntaxBinding.UblInvoice || syntax == SyntaxBinding.UblCreditNote) && File.Exists(ublSchFile))
            {
                spec.Rules.AddRange(schParser.Parse(ublSchFile, "UBL", commonSchFile));
            }

            LinkRulesToElements(spec);

            return spec;
        } // !Extract()


        /// <summary>
        /// Returns a flattened list of all elements (depth-first traversal).
        /// </summary>
        /// <remarks>
        /// The traversal order preserves the schema hierarchy from parent elements to child elements.
        /// </remarks>
        /// <param name="spec">The extracted specification to flatten.</param>
        /// <returns>A depth-first list of all schema elements.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="spec"/> is <see langword="null"/>.</exception>
        /// <example>
        /// <code>
        /// var extractor = new XRechnungExtractor();
        /// List&lt;SpecElement&gt; elements = extractor.FlattenElements(spec);
        /// </code>
        /// </example>
        public List<Element> FlattenElements(XRechnungSpecification spec)
        {
            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }

            List<Element> flat = new List<Element>();
            foreach (Element root in spec.Elements)
            {
                FlattenRecursive(root, flat);
            }

            return flat;
        } // !FlattenElements()


        private static void FlattenRecursive(Element element, List<Element> result)
        {
            result.Add(element);
            foreach (Element child in element.Children)
            {
                FlattenRecursive(child, result);
            }
        } // !FlattenRecursive()


        private void LinkRulesToElements(XRechnungSpecification spec)
        {
            List<Element> flat = FlattenElements(spec);

            foreach (SpecificationRule rule in spec.Rules)
            {
                string normalizedContext = NormalizeXPath(rule.Context);

                foreach (Element element in flat)
                {
                    if (element.XPath.EndsWith(normalizedContext, StringComparison.OrdinalIgnoreCase)
                        || normalizedContext.EndsWith(element.XPath, StringComparison.OrdinalIgnoreCase))
                    {
                        if (!element.BusinessRules.Contains(rule.Id))
                        {
                            element.BusinessRules.Add(rule.Id);
                        }
                    }
                }
            }
        } // !LinkRulesToElements()


        private static string NormalizeXPath(string xpath)
        {
            if (string.IsNullOrWhiteSpace(xpath))
            {
                return xpath;
            }

            return Regex.Replace(xpath, @"\[[^\]]*\]", string.Empty).Trim();
        } // !NormalizeXPath()
    }
}
