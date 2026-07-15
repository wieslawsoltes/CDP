using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CDP.CSharp.LanguageServer
{
    public class CSharpLanguageServer
    {
        private readonly Dictionary<string, string> _documents = new(StringComparer.OrdinalIgnoreCase);

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
                                ["triggerCharacters"] = new JsonArray(".", " ")
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
                                ["kind"] = c.Kind == "Method" ? 2 : (c.Kind == "Class" ? 7 : 14), // 2 = Method, 7 = Class, 14 = Keyword in LSP
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
            string Contents
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

            var syntaxTree = CSharpSyntaxTree.ParseText(text);
            var diagnostics = syntaxTree.GetDiagnostics();

            foreach (var diag in diagnostics)
            {
                var lineSpan = diag.Location.GetLineSpan();
                list.Add(new DiagnosticInfo(
                    diag.Id,
                    diag.GetMessage(),
                    diag.Severity.ToString(),
                    lineSpan.StartLinePosition.Line + 1,
                    lineSpan.StartLinePosition.Character + 1,
                    lineSpan.EndLinePosition.Line + 1,
                    lineSpan.EndLinePosition.Character + 1
                ));
            }
            return list;
        }

        public List<CompletionItem> GetCompletions(string uri, int line, int column)
        {
            var completions = new List<CompletionItem>();
            if (!_documents.TryGetValue(uri, out var text)) return completions;

            var syntaxTree = CSharpSyntaxTree.ParseText(text);
            var root = syntaxTree.GetRoot();

            // Find current prefix
            string prefix = GetCurrentPrefix(text, line, column);

            // Add standard C# keywords
            var keywords = new[]
            {
                "using", "namespace", "class", "public", "private", "protected", "internal",
                "static", "void", "string", "int", "bool", "double", "float", "object",
                "return", "if", "else", "for", "foreach", "while", "new", "var", "true", "false",
                "null", "this", "override", "virtual", "async", "await", "task"
            };

            foreach (var kw in keywords)
            {
                if (string.IsNullOrEmpty(prefix) || kw.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    completions.Add(new CompletionItem(kw, "Keyword", "C# Keyword", kw, $"C# keyword: {kw}"));
                }
            }

            // Find classes, methods and variables declared in the file
            var classDeclarations = root.DescendantNodes().OfType<ClassDeclarationSyntax>();
            foreach (var cls in classDeclarations)
            {
                var name = cls.Identifier.Text;
                if (string.IsNullOrEmpty(prefix) || name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    completions.Add(new CompletionItem(name, "Class", "Local Class", name, $"Class declared locally: {name}"));
                }
            }

            var methodDeclarations = root.DescendantNodes().OfType<MethodDeclarationSyntax>();
            foreach (var method in methodDeclarations)
            {
                var name = method.Identifier.Text;
                if (string.IsNullOrEmpty(prefix) || name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    completions.Add(new CompletionItem(name, "Method", "Local Method", name, $"Method declared locally: {name}"));
                }
            }

            // Context-specific completions (e.g. Console.)
            if (prefix.EndsWith("."))
            {
                var target = prefix.Substring(0, prefix.Length - 1).Trim();
                if (string.Equals(target, "Console", StringComparison.OrdinalIgnoreCase))
                {
                    var consoleMethods = new[] { "WriteLine", "Write", "ReadLine", "Clear", "ReadKey" };
                    foreach (var m in consoleMethods)
                    {
                        completions.Add(new CompletionItem(m, "Method", "System.Console Method", m, $"Writes or reads from standard output/input."));
                    }
                }
            }

            return completions;
        }

        public HoverResult? GetHover(string uri, int line, int column)
        {
            if (!_documents.TryGetValue(uri, out var text)) return null;

            var syntaxTree = CSharpSyntaxTree.ParseText(text);
            var root = syntaxTree.GetRoot();

            // Find node at cursor
            int offset = GetOffsetAtPosition(text, line, column);
            var node = root.FindToken(offset).Parent;
            if (node == null) return null;

            if (node is ClassDeclarationSyntax cls)
            {
                return new HoverResult($"**class {cls.Identifier.Text}**\n\nLocally declared class.");
            }
            if (node is MethodDeclarationSyntax method)
            {
                return new HoverResult($"**{method.ReturnType} {method.Identifier.Text}()**\n\nLocally declared method.");
            }

            return null;
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
            while (start >= 0 && (char.IsLetterOrDigit(text[start]) || text[start] == '.' || text[start] == '_'))
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
    }
}
