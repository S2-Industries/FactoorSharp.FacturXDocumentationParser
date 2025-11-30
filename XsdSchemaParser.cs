using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Schema;

namespace FactoorSharp.FacturXDocumentationParser
{
    internal sealed class XsdSchemaParser
    {
        // Map Namespace URI -> preferred prefix (aus den XSD-Dateien extrahiert)
        private readonly Dictionary<string, string> _nsToPrefix = new Dictionary<string, string>();

        // Parse: Erwartet eine oder mehrere Einstiegspfade. Für jeden Einstiegspfad werden
        // die dort referenzierten Schemas rekursiv nachgeladen (xs:import / xs:include / xs:redefine).
        public List<Element> Parse(params string[] entryXsdPaths)
        {
            if (entryXsdPaths == null || entryXsdPaths.Length == 0)
            {
                throw new ArgumentException("Mindestens ein XSD-Einstiegspfad erforderlich.", nameof(entryXsdPaths));
            }

            // Sammle rekursiv alle zu ladenden XSD-Dateien (resolviere schemaLocation relativ zum referenzierenden File)
            var discoveredSchemaFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var entryPath in entryXsdPaths)
            {
                if (string.IsNullOrWhiteSpace(entryPath))
                {
                    continue;
                }

                var resolvedEntryPath = Path.GetFullPath(entryPath);
                if (File.Exists(resolvedEntryPath))
                {
                    foreach (var referenced in CollectReferencedSchemas(resolvedEntryPath))
                    {
                        discoveredSchemaFiles.Add(referenced);
                    }
                }
            }

            // Lese Prefix-Mapping aus allen gefundenen Dateien
            foreach (var schemaFilePath in discoveredSchemaFiles)
            {
                _TryReadNamespacePrefixes(schemaFilePath, _nsToPrefix);
            }

            var schemaSet = new XmlSchemaSet();

            // Füge alle Dateien dem SchemaSet hinzu
            foreach (var schemaPath in discoveredSchemaFiles)
            {
                try
                {
                    using (var fileStream = File.OpenRead(schemaPath))
                    {
                        using (var xmlReader = XmlReader.Create(fileStream, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit }))
                        {
                            schemaSet.Add(null, xmlReader);
                        }
                    }
                }
                catch
                {
                    // Fehler beim Einlesen einzelner Dateien nicht fatal für komplette Analyse — überspringen
                }
            }

            schemaSet.CompilationSettings = new XmlSchemaCompilationSettings { EnableUpaCheck = false };
            schemaSet.ValidationEventHandler += (sender, e) =>
            {
                // Hier können Sie e.Exception protokollieren (einschließlich e.Message)
                if (e.Severity == XmlSeverityType.Error)
                {
                    // Logging der Fehler, aber die Kompilierung fortsetzen
                    // log.LogError($"XSD Compilation Error: {e.Message}");
                }
            };

            schemaSet.Compile();

            // Map für schnellen Zugriff auf globale Typen und Groups
            var globalTypesByName = new Dictionary<XmlQualifiedName, XmlSchemaType>();
            var globalGroupsByName = new Dictionary<XmlQualifiedName, XmlSchemaGroup>();

            foreach (XmlSchema schema in schemaSet.Schemas())
            {
                foreach (XmlSchemaObject schemaItem in schema.Items)
                {
                    if (schemaItem is XmlSchemaComplexType complexType && complexType.QualifiedName != null && !complexType.QualifiedName.IsEmpty && !globalTypesByName.ContainsKey(complexType.QualifiedName))
                    {
                        globalTypesByName[complexType.QualifiedName] = complexType;
                    }

                    if (schemaItem is XmlSchemaSimpleType simpleType && simpleType.QualifiedName != null && !simpleType.QualifiedName.IsEmpty && !globalTypesByName.ContainsKey(simpleType.QualifiedName))
                    {
                        globalTypesByName[simpleType.QualifiedName] = simpleType;
                    }

                    if (schemaItem is XmlSchemaGroup group && group.QualifiedName != null && !group.QualifiedName.IsEmpty && !globalGroupsByName.ContainsKey(group.QualifiedName))
                    {
                        globalGroupsByName[group.QualifiedName] = group;
                    }
                }
            }

            var roots = new List<Element>();

            foreach (XmlSchemaElement globalElement in schemaSet.GlobalElements.Values.Cast<XmlSchemaElement>())
            {
                var rootNode = _BuildNodeFromElement(globalElement, globalTypesByName, globalGroupsByName, new HashSet<XmlQualifiedName>());
                roots.Add(rootNode);
            }

            return roots;
        }


        // Rekursive Sammlung aller referenzierten Schema-Dateien (inkl. Startdatei).
        // Es werden xs:import, xs:include und xs:redefine Elemente betrachtet.
        private IEnumerable<string> CollectReferencedSchemas(string startPath)
        {
            var collectedFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var toVisitStack = new Stack<string>();
            toVisitStack.Push(Path.GetFullPath(startPath));

            while (toVisitStack.Count > 0)
            {
                var currentPath = toVisitStack.Pop();
                if (string.IsNullOrEmpty(currentPath) || !File.Exists(currentPath))
                {
                    continue;
                }

                var fullPath = Path.GetFullPath(currentPath);
                if (!collectedFiles.Add(fullPath))
                {
                    continue;
                }

                try
                {
                    var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit };
                    using (var reader = XmlReader.Create(fullPath, settings))
                    {
                        while (reader.Read())
                        {
                            if (reader.NodeType != XmlNodeType.Element)
                            {
                                continue;
                            }

                            // localName kann import/include/redefine sein, namespace muss XML Schema namespace sein
                            var localName = reader.LocalName;
                            if (!string.Equals(localName, "import", StringComparison.Ordinal) &&
                                !string.Equals(localName, "include", StringComparison.Ordinal) &&
                                !string.Equals(localName, "redefine", StringComparison.Ordinal))
                            {
                                continue;
                            }

                            if (!string.Equals(reader.NamespaceURI, XmlSchema.Namespace, StringComparison.Ordinal))
                            {
                                continue;
                            }

                            var schemaLocation = reader.GetAttribute("schemaLocation");
                            if (string.IsNullOrEmpty(schemaLocation))
                            {
                                continue;
                            }

                            string resolvedLocation;
                            if (Path.IsPathRooted(schemaLocation))
                            {
                                resolvedLocation = schemaLocation;
                            }
                            else
                            {
                                var directoryOfCurrent = Path.GetDirectoryName(fullPath) ?? string.Empty;
                                resolvedLocation = Path.GetFullPath(Path.Combine(directoryOfCurrent, schemaLocation));
                            }

                            if (File.Exists(resolvedLocation))
                            {
                                toVisitStack.Push(resolvedLocation);
                            }
                            else
                            {
                                // Remote or missing schemaLocation — aktuell ignorieren
                            }
                        }
                    }
                }
                catch
                {
                    // Bei Fehlern lesen wir möglichst viele Dateien weiter; einzelne Dateien überspringen
                }
            }

            return collectedFiles;
        } // !CollectReferencedSchemas()


        // Liest die xmlns:prefix="uri" Deklarationen aus der Root-Element-Attribute-Liste einer XSD-Datei
        private static void _TryReadNamespacePrefixes(string path, IDictionary<string, string> map)
        {
            try
            {
                using (var fileStream = File.OpenRead(path))
                {
                    var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit };
                    using (var reader = XmlReader.Create(fileStream, settings))
                    {
                        // Auf das Root-Element warten
                        while (reader.Read())
                        {
                            if (reader.NodeType != XmlNodeType.Element)
                            {
                                continue;
                            }

                            if (!reader.HasAttributes)
                            {
                                break;
                            }

                            for (int attributeIndex = 0; attributeIndex < reader.AttributeCount; attributeIndex++)
                            {
                                reader.MoveToAttribute(attributeIndex);
                                var attributeName = reader.Name;     // z.B. "xmlns:ram" oder "xmlns"
                                var attributeValue = reader.Value;   // Namespace URI

                                if (attributeName.StartsWith("xmlns:", StringComparison.Ordinal))
                                {
                                    var prefix = attributeName.Substring("xmlns:".Length);
                                    if (!string.IsNullOrEmpty(attributeValue) && !map.ContainsKey(attributeValue))
                                    {
                                        map[attributeValue] = prefix;
                                    }
                                }
                                else if (attributeName == "xmlns")
                                {
                                    if (!string.IsNullOrEmpty(attributeValue) && !map.ContainsKey(attributeValue))
                                    {
                                        map[attributeValue] = string.Empty;
                                    }
                                }
                            }

                            break;
                        }
                    }
                }
            }
            catch
            {
                // Fehler beim Lesen der Prefixes nicht kritisch für die Ausgabe
            }
        }

        // Die übrigen Methoden (_GetCardinality, _BuildNodeFromElement, _ProcessParticle, _PrefixedName) bleiben unverändert.
        private static string _GetCardinality(XmlSchemaElement element)
        {
            try
            {
                long min = (long)element.MinOccurs;
                string maxRaw = element.MaxOccursString;
                string max;

                if (!string.IsNullOrEmpty(maxRaw))
                {
                    max = string.Equals(maxRaw, "unbounded", StringComparison.OrdinalIgnoreCase) ? "*" : maxRaw;
                }
                else
                {
                    decimal maxDec = element.MaxOccurs;
                    if (maxDec == decimal.MaxValue)
                    {
                        max = "*";
                    }
                    else
                    {
                        max = ((long)maxDec).ToString();
                    }
                }

                return $"{min}..{max}";
            }
            catch
            {
                return string.Empty;
            }
        } // !_GetCardinality()


        private Element _BuildNodeFromElement(XmlSchemaElement element, IDictionary<XmlQualifiedName, XmlSchemaType> globalTypes, IDictionary<XmlQualifiedName, XmlSchemaGroup> globalGroups, HashSet<XmlQualifiedName> visitedTypes)
        {
            var nameQualified = !element.RefName.IsEmpty
                ? element.RefName
                : !element.QualifiedName.IsEmpty
                    ? element.QualifiedName
                    : new XmlQualifiedName(element.Name ?? string.Empty, element.QualifiedName.Namespace);

            // at this point, the nameQualified contains a name with the
            // full namespace as prefix. We want to replace this
            // with the preferred prefix from the _nsToPrefix map.
            string displayName = _PrefixedName(nameQualified);

            string displayType = string.Empty;
            if (element.SchemaTypeName != null && !element.SchemaTypeName.IsEmpty)
            {
                displayType = _PrefixedName(element.SchemaTypeName);
            }
            else if (element.SchemaType != null && element.SchemaType.QualifiedName != null && !element.SchemaType.QualifiedName.IsEmpty)
            {
                displayType = _PrefixedName(element.SchemaType.QualifiedName);
            }

            var node = new Element
            {
                Name = displayName,
                TypeName = displayType,
                XsdCardinality = _GetCardinality(element)
            };

            XmlSchemaType resolvedType = element.SchemaType;
            if ((resolvedType == null) && (element.SchemaTypeName != null) && !element.SchemaTypeName.IsEmpty)
            {
                globalTypes.TryGetValue(element.SchemaTypeName, out resolvedType);
            }

            if (resolvedType is XmlSchemaComplexType complex)
            {
                var typeQName = resolvedType.QualifiedName ?? (element.SchemaTypeName ?? new XmlQualifiedName(string.Empty, string.Empty));
                if (!typeQName.IsEmpty && visitedTypes.Contains(typeQName))
                {
                    return node;
                }

                if (!typeQName.IsEmpty)
                {
                    visitedTypes.Add(typeQName);
                }

                // **Wir müssen HIER eine Kopie für den Abstieg in diesen Typ anlegen**, 
                // falls ein anderes Element (Geschwister) später diesen Typ referenziert
                var nextVisited = new HashSet<XmlQualifiedName>(visitedTypes);
                _ProcessParticle(complex.ContentTypeParticle, node, globalTypes, globalGroups, nextVisited);
            }

            return node;
        } // !_BuildNodeFromElement()


        private void _ProcessParticle(XmlSchemaParticle particle, Element parent, IDictionary<XmlQualifiedName, XmlSchemaType> globalTypes, IDictionary<XmlQualifiedName, XmlSchemaGroup> globalGroups, HashSet<XmlQualifiedName> visitedTypes)
        {
            if (particle == null)
            {
                return;
            }

            if (particle is XmlSchemaSequence sequence)
            {
                foreach (XmlSchemaObject sequenceItem in sequence.Items)
                {
                    if (sequenceItem is XmlSchemaElement childElement)
                    {
                        parent.Children.Add(_BuildNodeFromElement(childElement, globalTypes, globalGroups, visitedTypes));
                    }
                    else if (sequenceItem is XmlSchemaChoice choice)
                    {
                        _ProcessParticle(choice, parent, globalTypes, globalGroups, visitedTypes);
                    }
                    else if (sequenceItem is XmlSchemaGroupRef groupRef)
                    {
                        if (!groupRef.RefName.IsEmpty && globalGroups.TryGetValue(groupRef.RefName, out var group) && group.Particle != null)
                        {
                            _ProcessParticle(group.Particle, parent, globalTypes, globalGroups, visitedTypes);
                        }
                    }
                }

                return;
            }

            if (particle is XmlSchemaChoice choiceRoot)
            {
                foreach (XmlSchemaObject choiceItem in choiceRoot.Items)
                {
                    if (choiceItem is XmlSchemaElement choiceElement)
                    {
                        parent.Children.Add(_BuildNodeFromElement(choiceElement, globalTypes, globalGroups, new HashSet<XmlQualifiedName>(visitedTypes)));
                    }
                    else if (choiceItem is XmlSchemaGroupRef groupRef)
                    {
                        if (!groupRef.RefName.IsEmpty && globalGroups.TryGetValue(groupRef.RefName, out var group) && group.Particle != null)
                        {
                            _ProcessParticle(group.Particle, parent, globalTypes, globalGroups, visitedTypes);
                        }
                    }
                }

                return;
            }

            if (particle is XmlSchemaGroupBase groupBase)
            {
                foreach (XmlSchemaObject groupItem in groupBase.Items)
                {
                    if (groupItem is XmlSchemaElement child)
                    {
                        parent.Children.Add(_BuildNodeFromElement(child, globalTypes, globalGroups, new HashSet<XmlQualifiedName>(visitedTypes)));
                    }
                    else if (groupItem is XmlSchemaChoice choice)
                    {
                        _ProcessParticle(choice, parent, globalTypes, globalGroups, visitedTypes);
                    }
                    else if (groupItem is XmlSchemaGroupRef groupRef)
                    {
                        if (!groupRef.RefName.IsEmpty && globalGroups.TryGetValue(groupRef.RefName, out var group) && group.Particle != null)
                        {
                            _ProcessParticle(group.Particle, parent, globalTypes, globalGroups, visitedTypes);
                        }
                    }
                }

                return;
            }
        } // !_ProcessParticle()


        // Liefert "prefix:LocalName" wenn ein Prefix für die Namespace-URI existiert, sonst LocalName.
        private string _PrefixedName(XmlQualifiedName qname)
        {
            if (qname == null || string.IsNullOrEmpty(qname.Name))
            {
                return string.Empty;
            }

            if (string.IsNullOrEmpty(qname.Namespace))
            {
                return qname.Name;
            }

            if (_nsToPrefix.TryGetValue(qname.Namespace, out var prefix))
            {
                if (!string.IsNullOrEmpty(prefix))
                {
                    return prefix + ":" + qname.Name;
                }

                return qname.Name;
            }

            try
            {
                var uri = qname.Namespace;
                var last = uri.TrimEnd('/').Split('/').LastOrDefault() ?? uri;
                var candidate = new string(last.Where(ch => char.IsLetterOrDigit(ch) || ch == '_' || ch == '-').ToArray());
                if (!string.IsNullOrEmpty(candidate))
                {
                    return candidate + ":" + qname.Name;
                }
            }
            catch
            {
                // Fallback weiter unten
            }

            return qname.Name;
        } // !_PrefixedName()
    }
}
