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
using System.Linq;
using System.Text;
using FactoorSharp.FacturXDocumentationParser.Common;


namespace FactoorSharp.FacturXDocumentationParser.FacturX
{
    internal static class XsdXPathBuilder
    {
        // Liefert für alle Knoten in den Wurzeln ein Mapping Node -> absoluter XPath.
        // Zusätzlich wird der ermittelte XPath in die jeweilige XsdElementNode.XPath geschrieben.
        // Beispiel: "/ram:ExchangedDocumentContext/ram:TestElement[2]/ram:Child"
        public static Dictionary<Element, string> ComputeAbsoluteXPaths(IEnumerable<Element> roots)
        {
            var result = new Dictionary<Element, string>();
            var rootList = roots?.ToList() ?? new List<Element>();

            // Gruppiere Root-Nodes nach Name, damit ggf. Positions-Prädikate gesetzt werden.
            var rootGroups = rootList.GroupBy(r => r.Name).ToDictionary(g => g.Key, g => g.ToList());

            for (int i = 0; i < rootList.Count; i++)
            {
                var root = rootList[i];
                var rootPath = "/" + (string.IsNullOrEmpty(root.Name) ? "*" : root.Name);

                if (rootGroups.TryGetValue(root.Name, out var siblings) && siblings.Count > 1)
                {
                    int pos = siblings.IndexOf(root) + 1;
                    rootPath += $"[{pos}]";
                }

                _Traverse(root, rootPath, result);
            }

            return result;
        } // !ComputeAbsoluteXPaths()


        private static void _Traverse(Element node, string currentPath, Dictionary<Element, string> map)
        {
            // In Node schreiben und gleichzeitig im zurückgegebenen Map ablegen
            node.XPath = currentPath;
            map[node] = currentPath;

            if (node.Children == null || node.Children.Count == 0)
            {
                return;
            }

            // Gruppiere Kinder nach Name, um bei mehrfachen gleichen Namen Positionen anzuhängen.
            var groups = node.Children.GroupBy(c => c.Name).ToDictionary(g => g.Key, g => g.ToList());

            foreach (var child in node.Children)
            {
                var childPath = new StringBuilder(currentPath);
                childPath.Append("/");
                childPath.Append(string.IsNullOrEmpty(child.Name) ? "*" : child.Name);

                if (groups.TryGetValue(child.Name, out var siblings) && siblings.Count > 1)
                {
                    int pos = siblings.IndexOf(child) + 1;
                    childPath.Append($"[{pos}]");
                }

                _Traverse(child, childPath.ToString(), map);
            }
        } //!_Traverse()
    }
}
