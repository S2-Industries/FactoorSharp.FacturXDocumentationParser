using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml.Linq;
using MiniExcelLibs;

namespace FactoorSharp.FacturXDocumentationParser
{
    /// <summary>
    /// Reads the worksheet "Factur-X CII D22B EXTENDED" with MiniExcel and returns a list of <see cref="ElementDocumentation"/>.
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


        public static async Task<IReadOnlyList<ElementDocumentation>> ParseAsync(string excelFilePath)
        {
            var profileDataMap = new Dictionary<string, IReadOnlyList<ElementDocumentation>>();

            // read all available profile tabs and mark support in the main list
            foreach (KeyValuePair<string, string> profileWorksheet in _WorksheetNames)
            {
                IReadOnlyList<ElementDocumentation> elements =
                    await _ParseAsync(excelFilePath, profileWorksheet.Value);

                profileDataMap[profileWorksheet.Key] = elements;
            }

            if (!_WorksheetNames.ContainsKey(_DefaultProfile))
            {
                return new List<ElementDocumentation>();
            }

            // use default profile as the base, seek in the other profile tabs
            // if the respective element is supported and add it to ProfileSupport dictionary
            IReadOnlyList<ElementDocumentation> result = profileDataMap[_DefaultProfile];
            foreach(KeyValuePair<string, IReadOnlyList<ElementDocumentation>> profileEntry in profileDataMap)
            {
                var profileLookup = profileEntry.Value.ToLookup(e => e.XpathXmlNorme1); // O(N) Erstellung

                foreach (ElementDocumentation element in result) // Iteration over default list (EXTENDED)
                {
                    if (profileLookup.Contains(element.XpathXmlNorme1))
                    {
                        element.ProfileSupport.Add(profileEntry.Key);
                    }
                }
            }

            return result;
        } // !ParseAsync()


        private static async Task<IReadOnlyList<ElementDocumentation>> _ParseAsync(string excelFilePath, string worksheetName)
        {

            if (string.IsNullOrWhiteSpace(excelFilePath))
            {
                throw new ArgumentException("Excel file path must be provided.", nameof(excelFilePath));
            }

            if (!File.Exists(excelFilePath))
            {
                throw new FileNotFoundException("Excel file not found.", excelFilePath);
            }

            List<ElementDocumentation> result = new List<ElementDocumentation>();

            var rows = MiniExcel.Query(excelFilePath, sheetName: worksheetName).ToList();
            for (int r = 4; r < rows.Count; r++)
            {
                var x = rows[r];
                var row = (IDictionary<string, object>)rows[r];

                // Wenn alle Zellen D–W leer sind → Ende.
                bool isEmpty = Enumerable.Range(3, 20)
                    .All(i => string.IsNullOrWhiteSpace(row.Values.ElementAt(i)?.ToString()));

                if (isEmpty)
                    break;

                var values = Enumerable.Range(3, 20)
                    .Select(i => row.Values.ElementAt(i))
                    .ToList();


                result.Add(_MapRow(values));
            }

            return result.AsReadOnly();
        } //!_ParseAsync()


        private static ElementDocumentation _MapRow(IEnumerable<object> row)
        {
            List<string> stringValues = row.Select(r => r?.ToString()).ToList();

            ElementDocumentation result = new ElementDocumentation()
            {
                Id = stringValues[1],
                IdCtcFrReform = stringValues[2],
                XsdLevel = stringValues[3],
                En16931SemanticCardinality = stringValues[4],
                BusinessTerm = stringValues[5],
                Description = stringValues[6],
                UsageNote = stringValues[7],
                Cius = stringValues[8],
                BusinessRule = stringValues[9],
                SemanticDataType = stringValues[10],
                //
                ExtProfilesCardinality = stringValues[12],
                XpathXmlNorme1 = stringValues[13],
                XpathXmlNorme2 = stringValues[14],
                Dt = stringValues[15],
                Type = stringValues[16],
                CiiCardinality = stringValues[17],
                Match = stringValues[18],
                Rules = stringValues[19]
            };

            return result;
        } // !_MapRow()
    } // !class DocumentationExcelParser
}
