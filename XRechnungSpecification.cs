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
using System.Collections.Generic;

namespace FactoorSharp.FacturXDocumentationParser
{
    /// <summary>
    /// Represents the full extracted documentation of the XRechnung specification.
    /// Contains the element hierarchy from XSD schemas and the business rules from Schematron files.
    /// </summary>
    /// <remarks>
    /// This type is the root aggregate for the extracted schema and rule metadata.
    /// </remarks>
    /// <example>
    /// <code>
    /// var spec = new XRechnungSpec();
    /// spec.Version = "3.0.1";
    /// </code>
    /// </example>
    public class XRechnungSpecification
    {
        /// <summary>
        /// Version of the XRechnung specification (e.g., "3.0.1")
        /// </summary>
        public string Version { get; set; } = string.Empty;

        /// <summary>
        /// Syntax binding used for extraction: "Cii", "UblInvoice", or "UblCreditNote".
        /// </summary>
        public string Syntax { get; set; } = "Cii";

        /// <summary>
        /// All XML elements defined in the EN16931 CII schema, with their
        /// hierarchy, types, and cardinalities.
        /// </summary>
        public List<Element> Elements { get; set; } = new List<Element>();

        /// <summary>
        /// All business rule constraints extracted from the XRechnung schematron files.
        /// Includes both CII and UBL rules.
        /// </summary>
        public List<SpecificationRule> Rules { get; set; } = new List<SpecificationRule>();
    }


    /// <summary>
    /// Represents a single Schematron business rule (assert).
    /// </summary>
    /// <remarks>
    /// Rules describe validation constraints and their human-readable descriptions.
    /// </remarks>
    /// <example>
    /// <code>
    /// var rule = new SpecificationRule();
    /// rule.Id = "BR-DE-1";
    /// </code>
    /// </example>
    public class SpecificationRule
    {
        /// <summary>
        /// Unique identifier of the rule, e.g., "BR-DE-1", "PEPPOL-EN16931-R001".
        /// </summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// The Schematron pattern this rule belongs to (e.g., "cii-pattern", "peppol-cii-pattern-1").
        /// </summary>
        public string Pattern { get; set; } = string.Empty;

        /// <summary>
        /// The syntax binding this rule applies to: "CII", "UBL", or "common".
        /// </summary>
        public string SyntaxBinding { get; set; } = string.Empty;

        /// <summary>
        /// The XPath context of the rule (from the enclosing &lt;rule context="..."&gt;).
        /// </summary>
        public string Context { get; set; } = string.Empty;

        /// <summary>
        /// The XPath test expression (the constraint).
        /// </summary>
        public string Test { get; set; } = string.Empty;

        /// <summary>
        /// Severity: "fatal" (MUST, Pflicht) or "warning" (SHOULD, Soll).
        /// </summary>
        public string Flag { get; set; } = string.Empty;

        /// <summary>
        /// The human-readable description / error message (typically in German for DE-specific rules).
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Business Term (BT) and Business Group (BG) references found in the description.
        /// E.g., ["BT-10", "BG-16"]
        /// </summary>
        public List<string> BtBgReferences { get; set; } = new List<string>();
    }
}
