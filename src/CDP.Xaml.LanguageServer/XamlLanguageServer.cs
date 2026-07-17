using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Xaml.Compiler.Ast;
using Xaml.Compiler.Parser;
using Xaml.Compiler.Registry;
using Xaml.Compiler.TypeSystem;
using Xaml.Compiler.TypeSystemImpl;

namespace CDP.Xaml.LanguageServer
{
    public class XamlLanguageServer
    {
        private readonly Dictionary<string, string> _documents = new(StringComparer.OrdinalIgnoreCase);
        private readonly XamlSchemaRegistry _schemaRegistry;

        public XamlLanguageServer(XamlSchemaRegistry schemaRegistry)
        {
            _schemaRegistry = schemaRegistry;
        }

        public XamlLanguageServer() : this(new XamlSchemaRegistry())
        {
            var avaloniaNs = new XamlNamespace(
                "https://github.com/avaloniaui",
                "avalonia",
                new[] { "Avalonia.Controls", "Avalonia.Base", "Avalonia.Layout", "Avalonia.Visuals", "Avalonia.Input", "Avalonia.Markup.Xaml", "Avalonia.Diagnostics" },
                new[] { "Avalonia.Controls", "Avalonia.Layout", "Avalonia.Input", "Avalonia.Media", "Avalonia.Styling", "Avalonia.Controls.Primitives" }
            );
            _schemaRegistry.RegisterNamespace(avaloniaNs);

            var xamlNs = new XamlNamespace(
                "http://schemas.microsoft.com/winfx/2006/xaml",
                "x",
                new[] { "System.Private.CoreLib" },
                new[] { "System" }
            );
            _schemaRegistry.RegisterNamespace(xamlNs);
        }

        public void OpenDocument(string uri, string text)
        {
            _documents[uri] = text;
        }

        public void ChangeDocument(string uri, string text)
        {
            _documents[uri] = text;
        }

        public string? ProcessMessage(string json)
        {
            try
            {
                var node = JsonNode.Parse(json);
                if (node == null) return null;

                var id = node["id"]?.GetValue<int>();
                var method = node["method"]?.GetValue<string>();
                var @params = node["params"];

                if (method == "initialize")
                {
                    return MakeResponse(id, new JsonObject
                    {
                        ["capabilities"] = new JsonObject
                        {
                            ["textDocumentSync"] = 1,
                            ["completionProvider"] = new JsonObject
                            {
                                ["triggerCharacters"] = new JsonArray("<", ".", " ")
                            },
                            ["hoverProvider"] = true
                        }
                    });
                }
                else if (method == "textDocument/didOpen")
                {
                    var docNode = @params?["textDocument"];
                    var uri = docNode?["uri"]?.GetValue<string>();
                    var text = docNode?["text"]?.GetValue<string>();
                    if (uri != null && text != null)
                    {
                        OpenDocument(uri, text);
                    }
                }
                else if (method == "textDocument/didChange")
                {
                    var docNode = @params?["textDocument"];
                    var uri = docNode?["uri"]?.GetValue<string>();
                    var contentChanges = @params?["contentChanges"]?.AsArray();
                    var text = contentChanges?.LastOrDefault()?["text"]?.GetValue<string>();
                    if (uri != null && text != null)
                    {
                        ChangeDocument(uri, text);
                    }
                }
                else if (method == "textDocument/completion")
                {
                    var docNode = @params?["textDocument"];
                    var uri = docNode?["uri"]?.GetValue<string>();
                    var pos = @params?["position"];
                    var line = (pos?["line"]?.GetValue<int>() ?? 0) + 1;
                    var col = (pos?["character"]?.GetValue<int>() ?? 0) + 1;

                    if (uri != null)
                    {
                        var list = GetCompletions(uri, line, col);
                        return MakeResponse(id, new JsonObject
                        {
                            ["items"] = new JsonArray(list.Select(c => new JsonObject
                            {
                                ["label"] = c.Label,
                                ["kind"] = c.Kind == "Class" ? 7 : 10,
                                ["detail"] = c.Detail,
                                ["insertText"] = c.InsertText,
                                ["documentation"] = c.Documentation
                            }).ToArray())
                        });
                    }
                }
                else if (method == "textDocument/hover")
                {
                    var docNode = @params?["textDocument"];
                    var uri = docNode?["uri"]?.GetValue<string>();
                    var pos = @params?["position"];
                    var line = (pos?["line"]?.GetValue<int>() ?? 0) + 1;
                    var col = (pos?["character"]?.GetValue<int>() ?? 0) + 1;

                    if (uri != null)
                    {
                        var hover = GetHover(uri, line, col);
                        if (hover != null)
                        {
                            return MakeResponse(id, new JsonObject
                            {
                                ["contents"] = hover.Contents
                            });
                        }
                    }
                    return MakeResponse(id, null);
                }
            }
            catch
            {
            }
            return null;
        }

        private string MakeResponse(int? id, JsonNode? result)
        {
            var resp = new JsonObject
            {
                ["jsonrpc"] = "2.0"
            };
            if (id != null)
            {
                resp["id"] = id;
            }
            resp["result"] = result;
            return resp.ToJsonString();
        }

        public record CompletionItem(
            string Label,
            string Kind,
            string Detail,
            string InsertText,
            string Documentation
        );

        public record HoverResult(
            string Contents,
            SourceSpan Range
        );

        public record DiagnosticInfo(
            string Code,
            string Message,
            string Severity,
            int StartLine,
            int StartColumn,
            int EndLine,
            int EndColumn
        );

        public List<DiagnosticInfo> GetDiagnostics(string uri)
        {
            var list = new List<DiagnosticInfo>();
            if (!_documents.TryGetValue(uri, out var text)) return list;

            var doc = XamlParser.Parse(text);
            foreach (var diag in doc.Diagnostics)
            {
                list.Add(new DiagnosticInfo(
                    diag.Code,
                    diag.Message,
                    diag.Severity.ToString(),
                    diag.Span.Start.Line,
                    diag.Span.Start.Column,
                    diag.Span.End.Line,
                    diag.Span.End.Column
                ));
            }
            return list;
        }

        public List<CompletionItem> GetCompletions(string uri, int line, int column)
        {
            var completions = new List<CompletionItem>();
            if (!_documents.TryGetValue(uri, out var fileText)) return completions;

            var doc = XamlParser.Parse(fileText);
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
                var availableTypes = GetAvailableTypes(_schemaRegistry);
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
                    var xamlType = _schemaRegistry.ResolveType(nsUri, element.LocalName);
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

                    var availableTypes = GetAvailableTypes(_schemaRegistry);
                    var ownerType = availableTypes.FirstOrDefault(t => string.Equals(t.Name, ownerTypeName, StringComparison.OrdinalIgnoreCase));
                    if (ownerType != null)
                    {
                        var nsUri = "https://github.com/avaloniaui";
                        var xamlType = _schemaRegistry.ResolveType(nsUri, ownerType.Name);
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

        public HoverResult? GetHover(string uri, int line, int column)
        {
            if (!_documents.TryGetValue(uri, out var fileText)) return null;

            var doc = XamlParser.Parse(fileText);
            var node = FindNodeAtPosition(doc, line, column);
            if (node == null) return null;

            if (node is XamlElementSyntax el)
            {
                string nsUri = ResolveNamespaceUri(el, el.Prefix, doc);
                var xamlType = _schemaRegistry.ResolveType(nsUri, el.LocalName);
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
                    var xamlType = _schemaRegistry.ResolveType(nsUri, elParent.LocalName);
                    if (xamlType != null)
                    {
                        var prop = _schemaRegistry.ResolveProperty(xamlType, attr.LocalName);
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

        private static IEnumerable<Type> GetAvailableTypes(XamlSchemaRegistry registry)
        {
            var field = typeof(XamlSchemaRegistry).GetField("_namespaces", BindingFlags.NonPublic | BindingFlags.Instance);
            var namespaces = field?.GetValue(registry) as ConcurrentDictionary<string, IXamlNamespace>;
            if (namespaces == null) yield break;

            foreach (var ns in namespaces.Values)
            {
                foreach (var assemblyName in ns.TargetAssemblies)
                {
                    Assembly? assembly = null;
                    try
                    {
                        assembly = AppDomain.CurrentDomain.GetAssemblies()
                            .FirstOrDefault(a => string.Equals(a.GetName().Name, assemblyName, StringComparison.OrdinalIgnoreCase));
                        if (assembly == null)
                        {
                            assembly = Assembly.Load(new AssemblyName(assemblyName));
                        }
                    }
                    catch
                    {
                        continue;
                    }

                    if (assembly == null) continue;

                    Type[] types;
                    try
                    {
                        types = assembly.GetTypes();
                    }
                    catch
                    {
                        continue;
                    }

                    foreach (var type in types)
                    {
                        if (!type.IsPublic || type.IsAbstract) continue;

                        foreach (var clrNs in ns.ClrNamespaces)
                        {
                            if (type.Namespace == clrNs)
                            {
                                yield return type;
                            }
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
    }
}
