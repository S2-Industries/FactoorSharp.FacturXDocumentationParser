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
        // Map Namespace URI -> preferred prefix (extracted from the XSD files)
        private readonly Dictionary<string, string> _nsToPrefix = new Dictionary<string, string>();

        // Parse: Expects one or more entry XSD paths. For each entry path,
        // the referenced schemas are recursively loaded (xs:import / xs:include / xs:redefine).
        public List<Element> Parse(params string[] entryXsdPaths)
        {
            if (entryXsdPaths == null || entryXsdPaths.Length == 0)
            {
                throw new ArgumentException("At least one XSD entry path is required.", nameof(entryXsdPaths));
            }

            // Collect all XSD files recursively (resolve schemaLocation relative to referencing file)
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

            // Read prefix mappings from all discovered files
            foreach (var schemaFilePath in discoveredSchemaFiles)
            {
                _TryReadNamespacePrefixes(schemaFilePath, _nsToPrefix);
            }

            var schemaSet = new XmlSchemaSet();

            // Add all files to the SchemaSet
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
                    // Errors while loading individual files are not fatal — skip them
                }
            }

            schemaSet.CompilationSettings = new XmlSchemaCompilationSettings { EnableUpaCheck = false };
            schemaSet.ValidationEventHandler += (sender, e) =>
            {
                // You may log e.Exception here (including e.Message)
                if (e.Severity == XmlSeverityType.Error)
                {
                    // Log the error but continue compilation
                }
            };

            schemaSet.Compile();

            // Maps for fast access to global types and groups
            var globalTypesByName = new Dictionary<XmlQualifiedName, XmlSchemaType>();
            var globalGroupsByName = new Dictionary<XmlQualifiedName, XmlSchemaGroup>();

            foreach (XmlSchema schema in schemaSet.Schemas())
            {
                foreach (XmlSchemaObject schemaItem in schema.Items)
                {
                    if (schemaItem is XmlSchemaComplexType complexType &&
                        complexType.QualifiedName != null &&
                        !complexType.QualifiedName.IsEmpty &&
                        !globalTypesByName.ContainsKey(complexType.QualifiedName))
                    {
                        globalTypesByName[complexType.QualifiedName] = complexType;
                    }

                    if (schemaItem is XmlSchemaSimpleType simpleType &&
                        simpleType.QualifiedName != null &&
                        !simpleType.QualifiedName.IsEmpty &&
                        !globalTypesByName.ContainsKey(simpleType.QualifiedName))
                    {
                        globalTypesByName[simpleType.QualifiedName] = simpleType;
                    }

                    if (schemaItem is XmlSchemaGroup group &&
                        group.QualifiedName != null &&
                        !group.QualifiedName.IsEmpty &&
                        !globalGroupsByName.ContainsKey(group.QualifiedName))
                    {
                        globalGroupsByName[group.QualifiedName] = group;
                    }
                }
            }

            var roots = new List<Element>();

            foreach (XmlSchemaElement globalElement in schemaSet.GlobalElements.Values.Cast<XmlSchemaElement>())
            {
                // IMPORTANT: each root starts with a fresh visited set
                var rootNode = _BuildNodeFromElement(globalElement, globalTypesByName, globalGroupsByName, new HashSet<XmlQualifiedName>());
                roots.Add(rootNode);
            }

            return roots;
        }


        // Recursive collection of all referenced schema files (including the starting file).
        // Considers xs:import, xs:include, and xs:redefine.
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

                            // localName may be import/include/redefine; namespace must be the XML Schema namespace
                            var localName = reader.LocalName;
                            if (!string.Equals(localName, "import") &&
                                !string.Equals(localName, "include") &&
                                !string.Equals(localName, "redefine"))
                            {
                                continue;
                            }

                            if (!string.Equals(reader.NamespaceURI, XmlSchema.Namespace))
                            {
                                continue;
                            }

                            var schemaLocation = reader.GetAttribute("schemaLocation");
                            if (string.IsNullOrEmpty(schemaLocation))
                            {
                                continue;
                            }

                            string resolvedLocation =
                                Path.IsPathRooted(schemaLocation)
                                    ? schemaLocation
                                    : Path.GetFullPath(Path.Combine(Path.GetDirectoryName(fullPath) ?? "", schemaLocation));

                            if (File.Exists(resolvedLocation))
                            {
                                toVisitStack.Push(resolvedLocation);
                            }
                        }
                    }
                }
                catch
                {
                    // Continue reading other files; skip the failing one
                }
            }

            return collectedFiles;
        } // !CollectReferencedSchemas()



        // Reads xmlns:prefix="uri" declarations from the root element's attributes of an XSD file
        private static void _TryReadNamespacePrefixes(string path, IDictionary<string, string> map)
        {
            try
            {
                using (var fileStream = File.OpenRead(path))
                {
                    var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit };
                    using (var reader = XmlReader.Create(fileStream, settings))
                    {
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
                                var attributeName = reader.Name;
                                var attributeValue = reader.Value;

                                if (attributeName.StartsWith("xmlns:"))
                                {
                                    var prefix = attributeName.Substring("xmlns:".Length);
                                    if (!map.ContainsKey(attributeValue))
                                    {
                                        map[attributeValue] = prefix;
                                    }
                                }
                                else if (attributeName == "xmlns")
                                {
                                    if (!map.ContainsKey(attributeValue))
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
                // Not critical for output
            }
        }



        // Returns X..Y cardinality range text
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
                    max = maxDec == decimal.MaxValue ? "*" : ((long)maxDec).ToString();
                }

                return $"{min}..{max}";
            }
            catch
            {
                return string.Empty;
            }
        } // !_GetCardinality()



        private Element _BuildNodeFromElement(XmlSchemaElement element,
            IDictionary<XmlQualifiedName, XmlSchemaType> globalTypes,
            IDictionary<XmlQualifiedName, XmlSchemaGroup> globalGroups,
            HashSet<XmlQualifiedName> visitedTypes)
        {
            var nameQualified = !element.RefName.IsEmpty
                ? element.RefName
                : !element.QualifiedName.IsEmpty
                    ? element.QualifiedName
                    : new XmlQualifiedName(element.Name ?? "", element.QualifiedName.Namespace);

            string displayName = _PrefixedName(nameQualified);

            string displayType = string.Empty;
            if (element.SchemaTypeName != null && !element.SchemaTypeName.IsEmpty)
            {
                displayType = _PrefixedName(element.SchemaTypeName);
            }
            else if (element.SchemaType?.QualifiedName != null &&
                     !element.SchemaType.QualifiedName.IsEmpty)
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
            if (resolvedType == null &&
                element.SchemaTypeName != null &&
                !element.SchemaTypeName.IsEmpty)
            {
                globalTypes.TryGetValue(element.SchemaTypeName, out resolvedType);
            }

            if (resolvedType is XmlSchemaComplexType complex)
            {
                var typeQName = resolvedType.QualifiedName ??
                                element.SchemaTypeName ??
                                new XmlQualifiedName("", "");

                // Prevent only true recursive cycles.
                if (!typeQName.IsEmpty && visitedTypes.Contains(typeQName))
                {
                    return node;
                }

                if (!typeQName.IsEmpty)
                {
                    visitedTypes.Add(typeQName);
                }

                // Descend into children with a COPY of visited types
                var nextVisited = new HashSet<XmlQualifiedName>(visitedTypes);
                _ProcessParticle(complex.ContentTypeParticle, node,
                    globalTypes, globalGroups, nextVisited);
            }

            return node;
        } // !_BuildNodeFromElement()



        private void _ProcessParticle(XmlSchemaParticle particle,
            Element parent,
            IDictionary<XmlQualifiedName, XmlSchemaType> globalTypes,
            IDictionary<XmlQualifiedName, XmlSchemaGroup> globalGroups,
            HashSet<XmlQualifiedName> visitedTypes)
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
                        // FIX: siblings get their own visitedTypes copy
                        parent.Children.Add(
                            _BuildNodeFromElement(childElement, globalTypes, globalGroups,
                                new HashSet<XmlQualifiedName>(visitedTypes))
                        );
                    }
                    else if (sequenceItem is XmlSchemaChoice choice)
                    {
                        _ProcessParticle(choice, parent, globalTypes, globalGroups,
                            new HashSet<XmlQualifiedName>(visitedTypes)); // FIX
                    }
                    else if (sequenceItem is XmlSchemaGroupRef groupRef)
                    {
                        if (!groupRef.RefName.IsEmpty &&
                            globalGroups.TryGetValue(groupRef.RefName, out var group) &&
                            group.Particle != null)
                        {
                            _ProcessParticle(group.Particle, parent, globalTypes, globalGroups,
                                new HashSet<XmlQualifiedName>(visitedTypes)); // FIX
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
                        // FIX: separate visitedTypes for each choice option
                        parent.Children.Add(
                            _BuildNodeFromElement(choiceElement, globalTypes, globalGroups,
                                new HashSet<XmlQualifiedName>(visitedTypes))
                        );
                    }
                    else if (choiceItem is XmlSchemaGroupRef groupRef)
                    {
                        if (!groupRef.RefName.IsEmpty &&
                            globalGroups.TryGetValue(groupRef.RefName, out var group) &&
                            group.Particle != null)
                        {
                            _ProcessParticle(group.Particle, parent, globalTypes, globalGroups,
                                new HashSet<XmlQualifiedName>(visitedTypes)); // FIX
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
                        // FIX: separate visitedTypes for each group element
                        parent.Children.Add(
                            _BuildNodeFromElement(child, globalTypes, globalGroups,
                                new HashSet<XmlQualifiedName>(visitedTypes))
                        );
                    }
                    else if (groupItem is XmlSchemaChoice choice)
                    {
                        _ProcessParticle(choice, parent, globalTypes, globalGroups,
                            new HashSet<XmlQualifiedName>(visitedTypes)); // FIX
                    }
                    else if (groupItem is XmlSchemaGroupRef groupRef)
                    {
                        if (!groupRef.RefName.IsEmpty &&
                            globalGroups.TryGetValue(groupRef.RefName, out var group) &&
                            group.Particle != null)
                        {
                            _ProcessParticle(group.Particle, parent, globalTypes, globalGroups,
                                new HashSet<XmlQualifiedName>(visitedTypes)); // FIX
                        }
                    }
                }

                return;
            }
        } // !_ProcessParticle()



        // Returns "prefix:LocalName" if a prefix exists, otherwise LocalName.
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
                // fallback below
            }

            return qname.Name;
        } // !_PrefixedName()
    }
}
