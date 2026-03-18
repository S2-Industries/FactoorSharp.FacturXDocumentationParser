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
using System.Diagnostics;
using System.IO;
using FactoorSharp.FacturXDocumentationParser.Common;

namespace FactoorSharp.FacturXDocumentationParser.FacturX
{
    /// <summary>
    /// Represents a row from the "Factur-X CII D22B EXTENDED" sheet.
    /// All fields are strings — validation and type conversion should be performed externally.
    /// </summary>
    [DebuggerDisplay("{Id}")]
    internal sealed class FacturXElementDocumentation
    {
        public string Id { get; set; } = string.Empty;
        public string IdCtcFrReform { get; set; } = string.Empty;
        public string XsdLevel { get; set; } = string.Empty;
        public Cardinality En16931SemanticCardinality { get; set; }
        public string BusinessTerm { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string UsageNote { get; set; } = string.Empty;
        public string Cius { get; set; } = string.Empty;
        public string BusinessRule { get; set; } = string.Empty;
        public string SemanticDataType { get; set; } = string.Empty;
        public Cardinality ExtProfilesCardinality { get; set; }
        public string XpathXmlNorme1 { get; set; } = string.Empty;
        public string XpathXmlNorme2 { get; set; } = string.Empty;
        public string Dt { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public Cardinality CiiCardinality { get; set; }
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
