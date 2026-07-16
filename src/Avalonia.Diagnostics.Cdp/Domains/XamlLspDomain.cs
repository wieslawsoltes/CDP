using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Xaml.Compiler.Ast;
using Xaml.Compiler.Parser;
using Xaml.Compiler.Registry;
using Xaml.Compiler.TypeSystem;
using Xaml.Compiler.TypeSystemImpl;

namespace Avalonia.Diagnostics.Cdp.Domains
{
    public static class XamlLspDomain
    {
        private static readonly XamlSchemaRegistry s_schemaRegistry = new();

        static XamlLspDomain()
        {
            var avaloniaNs = new XamlNamespace(
                "https://github.com/avaloniaui",
                "avalonia",
                new[] { "Avalonia.Controls", "Avalonia.Base", "Avalonia.Layout", "Avalonia.Visuals", "Avalonia.Input", "Avalonia.Markup.Xaml", "Avalonia.Diagnostics" },
                new[] { "Avalonia.Controls", "Avalonia.Layout", "Avalonia.Input", "Avalonia.Media", "Avalonia.Styling", "Avalonia.Controls.Primitives" }
            );
            s_schemaRegistry.RegisterNamespace(avaloniaNs);

            var xamlNs = new XamlNamespace(
                "http://schemas.microsoft.com/winfx/2006/xaml",
                "x",
                new[] { "System.Private.CoreLib" },
                new[] { "System" }
            );
            s_schemaRegistry.RegisterNamespace(xamlNs);
        }

        public static async Task<JsonObject> HandleAsync(CdpSession session, string action, JsonObject @params)
        {
            switch (action)
            {
                case "getCompletions":
                    {
                        var file = @params["file"]?.GetValue<string>() ?? "";
                        var line = @params["line"]?.GetValue<int>() ?? 1;
                        var col = @params["column"]?.GetValue<int>() ?? 1;

                        if (!File.Exists(file)) return new JsonObject { ["completions"] = new JsonArray() };

                        string text = await File.ReadAllTextAsync(file);
                        var doc = XamlParser.Parse(text);

                        var completions = GetCompletions(doc, text, line, col);
                        var compArray = new JsonArray();
                        foreach (var item in completions)
                        {
                            compArray.Add(new JsonObject
                            {
                                ["label"] = item.Label,
                                ["kind"] = item.Kind,
                                ["detail"] = item.Detail,
                                ["insertText"] = item.InsertText,
                                ["documentation"] = item.Documentation
                            });
                        }
                        return new JsonObject { ["completions"] = compArray };
                    }

                case "getHover":
                    {
                        var file = @params["file"]?.GetValue<string>() ?? "";
                        var line = @params["line"]?.GetValue<int>() ?? 1;
                        var col = @params["column"]?.GetValue<int>() ?? 1;

                        if (!File.Exists(file)) return new JsonObject();

                        string text = await File.ReadAllTextAsync(file);
                        var doc = XamlParser.Parse(text);

                        var hover = GetHover(doc, line, col);
                        if (hover == null) return new JsonObject();

                        return new JsonObject
                        {
                            ["contents"] = hover.Contents,
                            ["range"] = new JsonObject
                            {
                                ["start"] = new JsonObject
                                {
                                    ["offset"] = hover.Range.Start.Offset,
                                    ["line"] = hover.Range.Start.Line,
                                    ["column"] = hover.Range.Start.Column
                                },
                                ["end"] = new JsonObject
                                {
                                    ["offset"] = hover.Range.End.Offset,
                                    ["line"] = hover.Range.End.Line,
                                    ["column"] = hover.Range.End.Column
                                }
                            }
                        };
                    }

                default:
                    throw new NotSupportedException($"Method XamlLsp.{action} is not supported");
            }
        }

        private record CompletionItem(
            string Label,
            string Kind,
            string Detail,
            string InsertText,
            string Documentation
        );

        private record HoverResult(
            string Contents,
            SourceSpan Range
        );

        private static List<CompletionItem> GetCompletions(XamlDocumentSyntax doc, string fileText, int line, int column)
        {
            RegisterDocumentNamespaces(doc, s_schemaRegistry);
            var completions = new List<CompletionItem>();
            var node = FindNodeAtPosition(doc, line, column);

            if (node is XamlCommentSyntax || node is XamlCDataSyntax || node is XamlTextSyntax)
            {
                return completions;
            }

            string prefix = GetCurrentPrefix(fileText, line, column);
            if (prefix.Contains(':'))
            {
                prefix = prefix.Substring(prefix.IndexOf(':') + 1);
            }

            bool insideTagStart = IsInsideTagStart(fileText, line, column);

            if (insideTagStart || node is XamlDocumentSyntax)
            {
                var availableTypes = GetAvailableTypes(s_schemaRegistry);
                foreach (var type in availableTypes)
                {
                    if (string.IsNullOrEmpty(prefix) || type.Name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    {
                        completions.Add(new CompletionItem(
                            type.Name,
                            "Class",
                            type.Namespace ?? "",
                            type.Name,
                            $"Represents a {type.Name} control."
                        ));
                    }
                }
            }
            else
            {
                var element = node as XamlElementSyntax ?? FindEnclosingElement(doc, node);
                if (element != null)
                {
                    string nsUri = ResolveNamespaceUri(element, element.Prefix, doc);
                    var xamlType = s_schemaRegistry.ResolveType(nsUri, element.LocalName);
                    if (xamlType != null)
                    {
                        var props = GetAllProperties(xamlType);
                        foreach (var prop in props)
                        {
                            if (string.IsNullOrEmpty(prefix) || prop.Name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                            {
                                completions.Add(new CompletionItem(
                                    prop.Name,
                                    "Property",
                                    prop.PropertyType?.Name ?? "",
                                    $"{prop.Name}=\"$1\"",
                                    $"Gets or sets the {prop.Name} property of type {prop.PropertyType?.Name}."
                                ));
                            }
                        }
                    }
                }

                if (prefix.Contains('.'))
                {
                    var parts = prefix.Split('.');
                    var ownerTypeName = parts[0];
                    var propPrefix = parts[1];

                    var availableTypes = GetAvailableTypes(s_schemaRegistry);
                    var ownerType = availableTypes.FirstOrDefault(t => string.Equals(t.Name, ownerTypeName, StringComparison.OrdinalIgnoreCase));
                    if (ownerType != null)
                    {
                        var nsUri = "https://github.com/avaloniaui";
                        var xamlType = s_schemaRegistry.ResolveType(nsUri, ownerType.Name);
                        if (xamlType != null)
                        {
                            var props = GetAllProperties(xamlType);
                            foreach (var prop in props)
                            {
                                if (prop.IsAttached && (string.IsNullOrEmpty(propPrefix) || prop.Name.StartsWith(propPrefix, StringComparison.OrdinalIgnoreCase)))
                                {
                                    completions.Add(new CompletionItem(
                                        prop.Name,
                                        "Property",
                                        prop.PropertyType?.Name ?? "",
                                        $"{prop.Name}=\"$1\"",
                                        $"Attached property {prop.Name} of type {prop.PropertyType?.Name}."
                                    ));
                                }
                            }
                        }
                    }
                }
            }

            return completions;
        }

        private static bool IsInsideTagStart(string text, int line, int column)
        {
            int offset = GetOffsetAtPosition(text, line, column);
            if (offset <= 0 || offset > text.Length) return false;

            int idx = offset - 1;
            bool foundWhitespace = false;
            while (idx >= 0)
            {
                if (text[idx] == '<')
                {
                    return !foundWhitespace;
                }
                if (text[idx] == '>') return false;
                if (char.IsWhiteSpace(text[idx])) foundWhitespace = true;
                idx--;
            }
            return false;
        }

        private static XamlElementSyntax? FindEnclosingElement(XamlDocumentSyntax doc, XamlSyntaxNode? node)
        {
            if (node == null) return null;
            return FindEnclosingElementInNode(doc.RootElement, node);
        }

        private static XamlElementSyntax? FindEnclosingElementInNode(XamlElementSyntax? parent, XamlSyntaxNode target)
        {
            if (parent == null) return null;
            if (parent == target) return null;

            if (parent.Attributes.Any(a => a == target || a.ValueNode == target))
            {
                return parent;
            }

            foreach (var child in parent.Children)
            {
                if (child == target) return parent;
                if (child is XamlElementSyntax elChild)
                {
                    var res = FindEnclosingElementInNode(elChild, target);
                    if (res != null) return res;
                }
            }
            return null;
        }

        private static string ResolveNamespaceUri(XamlElementSyntax element, string prefix, XamlDocumentSyntax doc)
        {
            var curr = element;
            while (curr != null)
            {
                foreach (var attr in curr.Attributes)
                {
                    if (prefix == "" && attr.LocalName == "xmlns" && string.IsNullOrEmpty(attr.Prefix))
                    {
                        if (attr.ValueNode is XamlLiteralValueSyntax literal)
                        {
                            return literal.Value;
                        }
                    }
                    else if (attr.Prefix == "xmlns" && attr.LocalName == prefix)
                    {
                        if (attr.ValueNode is XamlLiteralValueSyntax literal)
                        {
                            return literal.Value;
                        }
                    }
                }
                curr = FindParentAstElementStatic(doc, curr);
            }
            return "https://github.com/avaloniaui";
        }

        private static XamlElementSyntax? FindParentAstElementStatic(XamlSyntaxNode root, XamlElementSyntax childToFind)
        {
            if (root is XamlElementSyntax el)
            {
                foreach (var child in el.Children)
                {
                    if (child == childToFind) return el;
                    if (child is XamlElementSyntax elChild)
                    {
                        var p = FindParentAstElementStatic(elChild, childToFind);
                        if (p != null) return p;
                    }
                }
            }
            else if (root is XamlDocumentSyntax doc && doc.RootElement != null)
            {
                if (doc.RootElement == childToFind) return null;
                return FindParentAstElementStatic(doc.RootElement, childToFind);
            }
            return null;
        }

        private static HoverResult? GetHover(XamlDocumentSyntax doc, int line, int column)
        {
            var node = FindNodeAtPosition(doc, line, column);
            if (node == null) return null;

            if (node is XamlElementSyntax el)
            {
                string nsUri = ResolveNamespaceUri(el, el.Prefix, doc);
                var xamlType = s_schemaRegistry.ResolveType(nsUri, el.LocalName);
                if (xamlType != null)
                {
                    return new HoverResult(
                        $"**{xamlType.Name}** ({xamlType.Namespace.Uri})\n\nRepresents a {xamlType.Name} control.",
                        el.Span
                    );
                }
            }
            else if (node is XamlAttributeSyntax attr)
            {
                var elParent = FindEnclosingElement(doc, attr);
                if (elParent != null)
                {
                    string nsUri = ResolveNamespaceUri(elParent, elParent.Prefix, doc);
                    var xamlType = s_schemaRegistry.ResolveType(nsUri, elParent.LocalName);
                    if (xamlType != null)
                    {
                        var prop = s_schemaRegistry.ResolveProperty(xamlType, attr.LocalName);
                        if (prop != null)
                        {
                            return new HoverResult(
                                $"**{prop.Name}** ({prop.DeclaringType.Name})\n\nGets or sets the {prop.Name} property of type {prop.PropertyType.Name}.",
                                attr.Span
                            );
                        }
                    }
                }
            }

            return null;
        }

        private static XamlSyntaxNode? FindNodeAtPosition(XamlSyntaxNode node, int line, int column)
        {
            if (IsPositionInSpan(node.Span, line, column))
            {
                if (node is XamlDocumentSyntax doc)
                {
                    if (doc.RootElement != null)
                    {
                        var sub = FindNodeAtPosition(doc.RootElement, line, column);
                        if (sub != null) return sub;
                    }
                }
                else if (node is XamlElementSyntax el)
                {
                    foreach (var attr in el.Attributes)
                    {
                        var sub = FindNodeAtPosition(attr, line, column);
                        if (sub != null) return sub;
                    }
                    foreach (var child in el.Children)
                    {
                        var sub = FindNodeAtPosition(child, line, column);
                        if (sub != null) return sub;
                    }
                }
                else if (node is XamlAttributeSyntax attr)
                {
                    if (attr.ValueNode != null)
                    {
                        var sub = FindNodeAtPosition(attr.ValueNode, line, column);
                        if (sub != null) return sub;
                    }
                }
                return node;
            }
            return null;
        }

        private static bool IsPositionInSpan(SourceSpan span, int line, int column)
        {
            if (line < span.Start.Line || line > span.End.Line) return false;
            if (line == span.Start.Line && column < span.Start.Column) return false;
            if (line == span.End.Line && column > span.End.Column) return false;
            return true;
        }

        private static int GetOffsetAtPosition(string text, int line, int column)
        {
            int currentLine = 1;
            int currentColumn = 1;
            for (int i = 0; i < text.Length; i++)
            {
                if (currentLine == line && currentColumn == column)
                {
                    return i;
                }

                if (text[i] == '\n')
                {
                    currentLine++;
                    currentColumn = 1;
                }
                else
                {
                    currentColumn++;
                }
            }
            return text.Length;
        }

        private static string GetCurrentPrefix(string text, int line, int column)
        {
            int offset = GetOffsetAtPosition(text, line, column);
            if (offset <= 0 || offset > text.Length) return "";

            int start = offset - 1;
            while (start >= 0 && (char.IsLetterOrDigit(text[start]) || text[start] == '.' || text[start] == ':' || text[start] == '-'))
            {
                start--;
            }
            start++;
            if (start < offset)
            {
                return text.Substring(start, offset - start);
            }
            return "";
        }

        private static readonly ConcurrentDictionary<Assembly, bool> s_scannedAssemblies = new();
        private static readonly ConcurrentDictionary<string, List<Type>> s_clrNamespaceCache = new();
        private static readonly object s_scanLock = new();

        private static void UpdateClrNamespaceCache()
        {
            bool hasUnscanned = false;
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (!s_scannedAssemblies.ContainsKey(assembly))
                {
                    hasUnscanned = true;
                    break;
                }
            }

            if (!hasUnscanned)
            {
                return;
            }

            lock (s_scanLock)
            {
                foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    if (s_scannedAssemblies.ContainsKey(assembly))
                    {
                        continue;
                    }

                    Type[] types;
                    try
                    {
                        types = assembly.GetTypes();
                    }
                    catch (ReflectionTypeLoadException ex)
                    {
                        types = ex.Types.Where(t => t != null).ToArray()!;
                    }
                    catch
                    {
                        s_scannedAssemblies.TryAdd(assembly, true);
                        continue;
                    }

                    foreach (var type in types)
                    {
                        if (type == null) continue;
                        if (!type.IsPublic || type.IsAbstract) continue;
                        var clrNs = type.Namespace;
                        if (clrNs == null) continue;

                        s_clrNamespaceCache.AddOrUpdate(
                            clrNs,
                            ns => new List<Type> { type },
                            (ns, list) =>
                            {
                                lock (list)
                                {
                                    list.Add(type);
                                }
                                return list;
                            });
                    }

                    s_scannedAssemblies.TryAdd(assembly, true);
                }
            }
        }

        private static IEnumerable<Type> GetAvailableTypes(XamlSchemaRegistry registry)
        {
            UpdateClrNamespaceCache();

            foreach (var ns in registry.Namespaces)
            {
                var targetAssemblies = ns.TargetAssemblies;
                if (targetAssemblies != null && targetAssemblies.Count > 0)
                {
                    foreach (var assemblyName in targetAssemblies)
                    {
                        try
                        {
                            var assembly = AppDomain.CurrentDomain.GetAssemblies()
                                .FirstOrDefault(a => string.Equals(a.GetName().Name, assemblyName, StringComparison.OrdinalIgnoreCase));
                            if (assembly == null)
                            {
                                assembly = Assembly.Load(new AssemblyName(assemblyName));
                                UpdateClrNamespaceCache();
                            }
                        }
                        catch
                        {
                            // Ignore load errors
                        }
                    }
                }

                foreach (var clrNs in ns.ClrNamespaces)
                {
                    if (s_clrNamespaceCache.TryGetValue(clrNs, out var types))
                    {
                        List<Type> copy;
                        lock (types)
                        {
                            copy = new List<Type>(types);
                        }
                        foreach (var type in copy)
                        {
                            yield return type;
                        }
                    }
                }
            }
        }

        private static List<IXamlProperty> GetAllProperties(IXamlType type)
        {
            var props = new List<IXamlProperty>();
            var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var curr = type;
            while (curr != null)
            {
                foreach (var prop in curr.GetProperties())
                {
                    if (visited.Add(prop.Name))
                    {
                        props.Add(prop);
                    }
                }
                curr = curr.BaseType;
            }
            return props;
        }

        private static void RegisterDocumentNamespaces(XamlDocumentSyntax doc, XamlSchemaRegistry registry)
        {
            if (doc.RootElement != null)
            {
                RegisterNamespacesInElement(doc.RootElement, registry);
            }
        }

        private static void RegisterNamespacesInElement(XamlElementSyntax el, XamlSchemaRegistry registry)
        {
            foreach (var attr in el.Attributes)
            {
                if (attr.Prefix == "xmlns" || (attr.LocalName == "xmlns" && string.IsNullOrEmpty(attr.Prefix)))
                {
                    if (attr.ValueNode is XamlLiteralValueSyntax literal)
                    {
                        var nsUri = literal.Value;
                        if (nsUri.StartsWith("clr-namespace:"))
                        {
                            registry.EnsureNamespaceRegistered(nsUri);
                        }
                    }
                }
            }
            foreach (var child in el.Children)
            {
                if (child is XamlElementSyntax childEl)
                {
                    RegisterNamespacesInElement(childEl, registry);
                }
            }
        }
    }
}
