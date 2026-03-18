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
using System.Threading.Tasks;
using System.Xml.Linq;
using FactoorSharp.FacturXDocumentationParser.Common;
using MiniExcelLibs;

namespace FactoorSharp.FacturXDocumentationParser.FacturX
{
    /// <summary>
    /// Reads the worksheet "Factur-X CII D22B EXTENDED" with MiniExcel and returns a list of <see cref="FacturXElementDocumentation"/>.
    /// Reading starts after the row that contains "EN16931 Semantic Cardinality" in any column.
    /// </summary>
    internal sealed class DocumentationExcelParser
    {
        private const string _DefaultProfile = "EXTENDED";
        private readonly static Dictionary<string, string> _WorksheetNames = new Dictionary<string, string>()
        {
            { "BASIC", "Factur-X CII D22B BASIC" },
            { "BASIC WL", "Factur-X CII D22B BASIC WL" },
            { "MINIMUM", "Factur-X CII D22B MINIMUM" },
            { "EN16931", "Factur-X CII D22B EN16931" },
            { "EXTENDED", "Factur-X CII D22B EXTENDED" }
        };


        public static async Task<IReadOnlyList<FacturXElementDocumentation>> ParseAsync(string excelFilePath)
        {
            var profileDataMap = new Dictionary<string, IReadOnlyList<FacturXElementDocumentation>>();

            // read all available profile tabs and mark support in the main list
            foreach (KeyValuePair<string, string> profileWorksheet in _WorksheetNames)
            {
                IReadOnlyList<FacturXElementDocumentation> elements =
                    await _ParseAsync(excelFilePath, profileWorksheet.Value);

                profileDataMap[profileWorksheet.Key] = elements;
            }

            if (!_WorksheetNames.ContainsKey(_DefaultProfile))
            {
                return new List<FacturXElementDocumentation>();
            }

            // use default profile as the base, seek in the other profile tabs
            // if the respective element is supported and add it to ProfileSupport dictionary
            IReadOnlyList<FacturXElementDocumentation> result = profileDataMap[_DefaultProfile];
            foreach(KeyValuePair<string, IReadOnlyList<FacturXElementDocumentation>> profileEntry in profileDataMap)
            {
                var profileLookup = profileEntry.Value.ToLookup(e => e.XpathXmlNorme1); // O(N) Erstellung

                foreach (FacturXElementDocumentation element in result) // Iteration over default list (EXTENDED)
                {
                    if (profileLookup.Contains(element.XpathXmlNorme1))
                    {
                        element.ProfileSupport.Add(profileEntry.Key);
                    }
                }
            }

            return result;
        } // !ParseAsync()


        private static async Task<IReadOnlyList<FacturXElementDocumentation>> _ParseAsync(string excelFilePath, string worksheetName)
        {

            if (string.IsNullOrWhiteSpace(excelFilePath))
            {
                throw new ArgumentException("Excel file path must be provided.", nameof(excelFilePath));
            }

            if (!File.Exists(excelFilePath))
            {
                throw new FileNotFoundException("Excel file not found.", excelFilePath);
            }

            List<FacturXElementDocumentation> result = new List<FacturXElementDocumentation>();

            var rows = MiniExcel.Query(excelFilePath, sheetName: worksheetName).ToList();
            for (int r = 4; r < rows.Count; r++)
            {
                var x = rows[r];
                var row = (IDictionary<string, object>)rows[r];

                // Wenn alle Zellen D–W leer sind → Ende.
                bool isEmpty = Enumerable.Range(4, 21)
                    .All(i => string.IsNullOrWhiteSpace(row.Values.ElementAt(i)?.ToString()));

                if (isEmpty)
                    break;

                var values = Enumerable.Range(4, 21)
                    .Select(i => row.Values.ElementAt(i))
                    .ToList();


                result.Add(_MapRow(values));
            }

            return result.AsReadOnly();
        } //!_ParseAsync()


        private static FacturXElementDocumentation _MapRow(IEnumerable<object> row)
        {
            List<string> stringValues = row.Select(r => r?.ToString()).ToList();

            FacturXElementDocumentation result = new FacturXElementDocumentation()
            {
                Id = stringValues[1],
                IdCtcFrReform = stringValues[2],
                XsdLevel = stringValues[3],
                En16931SemanticCardinality = Cardinality.FromString(stringValues[4]),
                BusinessTerm = stringValues[5],
                Description = stringValues[6],
                UsageNote = stringValues[7],
                Cius = stringValues[8],
                BusinessRule = stringValues[9],
                SemanticDataType = stringValues[10],
                //
                ExtProfilesCardinality = Cardinality.FromString(stringValues[12]),
                XpathXmlNorme1 = stringValues[13],
                XpathXmlNorme2 = stringValues[14],
                Dt = stringValues[15],
                Type = stringValues[16],
                CiiCardinality = Cardinality.FromString(stringValues[17]),
                Match = stringValues[18],
                Rules = stringValues[19]
            };

            return result;
        } // !_MapRow()
    } // !class DocumentationExcelParser
}
