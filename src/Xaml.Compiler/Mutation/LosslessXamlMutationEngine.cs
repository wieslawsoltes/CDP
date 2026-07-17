using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xaml.Compiler.Adapters;
using Xaml.Compiler.Ast;
using Xaml.Compiler.Parser;
using Xaml.Compiler.Registry;
using Chrome.DevTools.Protocol;

namespace Xaml.Compiler.Mutation
{
    public class LosslessXamlMutationEngine : IMutationEngine
    {
        private readonly IUiFrameworkAdapter _adapter;
        private readonly INodeMap _nodeMap;
        private readonly XamlSchemaRegistry _schemaRegistry;
        private readonly ConcurrentDictionary<string, string> _classToFileMap = new(StringComparer.Ordinal);
        private readonly object _scanLock = new();
        private bool _scanned = false;
        private static readonly ConcurrentDictionary<string, XamlDocumentSyntax> _documentCache = new(StringComparer.OrdinalIgnoreCase);

        private static readonly ConcurrentDictionary<string, System.Threading.SemaphoreSlim> _fileSemaphores = new(StringComparer.OrdinalIgnoreCase);
        private static System.Threading.SemaphoreSlim GetFileSemaphore(string filePath) => _fileSemaphores.GetOrAdd(filePath, _ => new System.Threading.SemaphoreSlim(1, 1));

        public event Action<string, List<Diagnostic>>? DiagnosticsUpdated;

        public LosslessXamlMutationEngine(IUiFrameworkAdapter adapter, INodeMap nodeMap, Action<string, List<Diagnostic>>? diagnosticsCallback = null)
        {
            _adapter = adapter;
            _nodeMap = nodeMap;
            _schemaRegistry = new XamlSchemaRegistry();
            if (diagnosticsCallback != null)
            {
                DiagnosticsUpdated += diagnosticsCallback;
            }
            Task.Run(() => EnsureScan());
        }

        public LosslessXamlMutationEngine(IUiFrameworkAdapter adapter, INodeMap nodeMap, XamlSchemaRegistry schemaRegistry, Action<string, List<Diagnostic>>? diagnosticsCallback = null)
        {
            _adapter = adapter;
            _nodeMap = nodeMap;
            _schemaRegistry = schemaRegistry;
            if (diagnosticsCallback != null)
            {
                DiagnosticsUpdated += diagnosticsCallback;
            }
            Task.Run(() => EnsureScan());
        }

        public bool CanMutate(object target)
        {
            var logPath = "/Users/wieslawsoltes/GitHub/CDP/mutation_debug.log";
            try
            {
                File.AppendAllText(logPath, $"[MUTATION] CanMutate called for target={_adapter.GetTypeName(target)} (Name={_adapter.GetPropertyValue(target, "Name")})\n");
            }
            catch {}

            if (!_adapter.IsControl(target)) return false;
            var (xamlRoot, filePath) = FindXamlRoot(target);
            try
            {
                File.AppendAllText(logPath, $"[MUTATION] CanMutate result: xamlRoot={(xamlRoot != null ? _adapter.GetTypeName(xamlRoot) : "null")}, filePath={filePath}\n");
            }
            catch {}
            return xamlRoot != null && filePath != null;
        }

        public async Task<bool> SetAttributeAsync(object target, string name, string value)
        {
            if (!_adapter.IsControl(target)) return false;
            var (_, filePath) = FindXamlRoot(target);
            if (filePath == null) return false;
            var sem = GetFileSemaphore(filePath);
            await sem.WaitAsync();
            try
            {
                var (control, resolvedPath, doc) = await ResolveContextAsync(target);
                if (doc == null || resolvedPath == null || control == null) return false;

                var (xamlRoot, _) = FindXamlRoot(control);
                if (xamlRoot == null) return false;

                var astElement = LocateAstElement(doc, control, xamlRoot);
                if (astElement == null) return false;

                string localName = name;
                string prefix = "";
                if (name.Contains(':'))
                {
                    var parts = name.Split(':');
                    prefix = parts[0];
                    localName = parts[1];
                }

                var attr = astElement.Attributes.FirstOrDefault(a => string.Equals(a.LocalName, localName, StringComparison.OrdinalIgnoreCase));
                XamlValueSyntax? oldValNode = attr?.ValueNode;
                bool wasAdded = false;
                XamlAttributeSyntax? newAttr = null;

                if (attr != null)
                {
                    attr.ValueNode = ParseAttributeValue(value);
                }
                else
                {
                    newAttr = new XamlAttributeSyntax
                    {
                        Prefix = prefix,
                        LocalName = localName,
                        QuoteChar = '"',
                        ValueNode = ParseAttributeValue(value)
                    };
                    newAttr.LeadingTrivia.Add(new XamlTrivia(" ", new SourceSpan()));
                    astElement.Attributes.Add(newAttr);
                    wasAdded = true;
                }

                string newText = astElement.ToFullString();

                // Restore AST state
                if (attr != null)
                {
                    attr.ValueNode = oldValNode;
                }
                else if (wasAdded && newAttr != null)
                {
                    astElement.Attributes.Remove(newAttr);
                }

                await ApplyPatchWithNewTextAsync(resolvedPath, doc, astElement, newText);

                // Live UI update
                await _adapter.ApplyAttributeLiveAsync(control, name, value);

                return true;
            }
            finally
            {
                sem.Release();
            }
        }

        public async Task<bool> RemoveAttributeAsync(object target, string name)
        {
            if (!_adapter.IsControl(target)) return false;
            var (_, filePath) = FindXamlRoot(target);
            if (filePath == null) return false;
            var sem = GetFileSemaphore(filePath);
            await sem.WaitAsync();
            try
            {
                var (control, resolvedPath, doc) = await ResolveContextAsync(target);
                if (doc == null || resolvedPath == null || control == null) return false;

                var (xamlRoot, _) = FindXamlRoot(control);
                if (xamlRoot == null) return false;

                var astElement = LocateAstElement(doc, control, xamlRoot);
                if (astElement == null) return false;

                string localName = name;
                if (name.Contains(':'))
                {
                    var parts = name.Split(':');
                    localName = parts[1];
                }

                var attr = astElement.Attributes.FirstOrDefault(a => string.Equals(a.LocalName, localName, StringComparison.OrdinalIgnoreCase));
                if (attr != null)
                {
                    int index = astElement.Attributes.IndexOf(attr);
                    astElement.Attributes.RemoveAt(index);

                    string newText = astElement.ToFullString();

                    // Restore
                    astElement.Attributes.Insert(index, attr);

                    await ApplyPatchWithNewTextAsync(resolvedPath, doc, astElement, newText);
                }

                // Live UI update
                await _adapter.RemoveAttributeLiveAsync(control, name);

                return true;
            }
            finally
            {
                sem.Release();
            }
        }

        public async Task<bool> RemoveNodeAsync(object target)
        {
            if (!_adapter.IsControl(target)) return false;
            var (_, filePath) = FindXamlRoot(target);
            if (filePath == null) return false;
            var sem = GetFileSemaphore(filePath);
            await sem.WaitAsync();
            try
            {
                var (control, resolvedPath, doc) = await ResolveContextAsync(target);
                if (doc == null || resolvedPath == null || control == null) return false;

                var (xamlRoot, _) = FindXamlRoot(control);
                if (xamlRoot == null) return false;

                var astElement = LocateAstElement(doc, control, xamlRoot);
                if (astElement == null) return false;

                var parentElement = FindParentAstElement(doc, astElement);
                if (parentElement == null) return false;

                int index = parentElement.Children.IndexOf(astElement);
                if (index != -1)
                {
                    parentElement.Children.RemoveAt(index);

                    string newText = parentElement.ToFullString();

                    // Restore
                    parentElement.Children.Insert(index, astElement);

                    await ApplyPatchWithNewTextAsync(resolvedPath, doc, parentElement, newText);
                }

                // Live UI update
                await _adapter.RemoveNodeLiveAsync(control);

                return true;
            }
            finally
            {
                sem.Release();
            }
        }

        public async Task<bool> SetOuterHtmlAsync(object target, string outerHtml)
        {
            if (!_adapter.IsControl(target)) return false;
            var (_, filePath) = FindXamlRoot(target);
            if (filePath == null) return false;
            var sem = GetFileSemaphore(filePath);
            await sem.WaitAsync();
            try
            {
                var (control, resolvedPath, doc) = await ResolveContextAsync(target);
                if (doc == null || resolvedPath == null || control == null) return false;

                var (xamlRoot, _) = FindXamlRoot(control);
                if (xamlRoot == null) return false;

                var astElement = LocateAstElement(doc, control, xamlRoot);
                if (astElement == null) return false;

                XamlElementSyntax newElement;
                try
                {
                    var localDiags = new List<Diagnostic>();
                    newElement = XamlParser.ParseElement(outerHtml, localDiags);
                    if (localDiags.Any(d => d.Severity == DiagnosticSeverity.Error))
                    {
                        return false;
                    }
                }
                catch
                {
                    return false;
                }

                var parentElement = FindParentAstElement(doc, astElement);
                if (parentElement == null) return false;

                int idx = parentElement.Children.IndexOf(astElement);
                if (idx == -1) return false;

                string loaderXaml = GetXamlStringForLoader(newElement, astElement, doc);

                // Pre-load validation on the UI thread before writing to disk
                object? newControl = null;
                try
                {
                    var inheritedNamespaces = GetInheritedNamespaces(astElement, doc);
                    newControl = await _adapter.InstantiateXamlFragmentAsync(loaderXaml, inheritedNamespaces);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[LosslessMutationEngine] SetOuterHtmlAsync pre-load validation failed: {ex.Message}");
                    return false;
                }

                if (newControl == null)
                {
                    return false;
                }

                parentElement.Children[idx] = newElement;

                string newText = parentElement.ToFullString();

                // Restore in memory
                parentElement.Children[idx] = astElement;

                await ApplyPatchWithNewTextAsync(resolvedPath, doc, parentElement, newText);

                // Live UI update (now guaranteed to succeed since load passed)
                bool success = false;
                if (_nodeMap.TryGetId(control, out int nodeId))
                {
                    success = await _adapter.ReplaceChildLiveAsync(control, newControl);
                    if (success)
                    {
                        _nodeMap.UpdateNodeMapping(nodeId, newControl);
                    }
                }

                return success;
            }
            finally
            {
                sem.Release();
            }
        }

        public async Task<string?> GetOuterHtmlAsync(object target)
        {
            if (!_adapter.IsControl(target)) return null;
            var (_, filePath) = FindXamlRoot(target);
            if (filePath == null) return null;
            var sem = GetFileSemaphore(filePath);
            await sem.WaitAsync();
            try
            {
                var (control, _, doc) = await ResolveContextAsync(target);
                if (doc == null || control == null) return null;

                var (xamlRoot, _) = FindXamlRoot(control);
                if (xamlRoot == null) return null;

                var astElement = LocateAstElement(doc, control, xamlRoot);
                if (astElement == null) return null;

                return astElement.ToFullString();
            }
            finally
            {
                sem.Release();
            }
        }

        private async Task<(object? control, string? filePath, XamlDocumentSyntax? doc)> ResolveContextAsync(object target)
        {
            var logPath = "/Users/wieslawsoltes/GitHub/CDP/mutation_debug.log";
            if (!_adapter.IsControl(target)) return (null, null, null);
            var (xamlRoot, filePath) = FindXamlRoot(target);
            if (xamlRoot == null || filePath == null) return (target, null, null);

            if (!_documentCache.TryGetValue(filePath, out var doc))
            {
                if (!File.Exists(filePath)) return (target, filePath, null);
                string xamlText = await File.ReadAllTextAsync(filePath);
                doc = XamlParser.Parse(xamlText);
                _documentCache[filePath] = doc;
            }
            else
            {
                if (System.Environment.GetEnvironmentVariable("CDP_E2E_MODE") != "true")
                {
                    string fileText = await File.ReadAllTextAsync(filePath);
                    if (doc.ToFullString() != fileText)
                    {
                        doc = XamlParser.Parse(fileText);
                        _documentCache[filePath] = doc;
                    }
                }
            }
            if (doc != null && doc.Diagnostics != null)
            {
                var errors = doc.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
                if (errors.Any())
                {
                    try
                    {
                        File.AppendAllText(logPath, $"[MUTATION] ResolveContextAsync has {errors.Count} errors:\n");
                        foreach (var err in errors)
                        {
                            File.AppendAllText(logPath, $"  -> ERROR: {err.Message} at offset {err.Span.Start.Offset}\n");
                        }
                    }
                    catch {}
                    return (target, filePath, null);
                }
            }
            return (target, filePath, doc);
        }

        private async Task ApplyPatchWithNewTextAsync(string file, XamlDocumentSyntax doc, XamlSyntaxNode oldNode, string newText)
        {
            string currentText = doc.ToFullString();
            int start = oldNode.Span.Start.Offset;
            int length = oldNode.Span.End.Offset - start;

            string patchedText = currentText.Remove(start, length).Insert(start, newText);

            var change = new TextChange(start, length, newText);
            var updatedDoc = XamlParser.ParseIncremental(doc, patchedText, change);
            _documentCache[file] = updatedDoc;

            if (System.Environment.GetEnvironmentVariable("CDP_E2E_MODE") != "true")
            {
                await File.WriteAllTextAsync(file, patchedText);
            }

            // Push diagnostics if there are any
            PushDiagnostics(file, updatedDoc);
        }

        private void PushDiagnostics(string file, XamlDocumentSyntax doc)
        {
            if (doc.Diagnostics != null)
            {
                DiagnosticsUpdated?.Invoke(file, doc.Diagnostics.ToList());
            }
        }

        private XamlValueSyntax ParseAttributeValue(string value)
        {
            try
            {
                var el = XamlParser.ParseElement($"<Dummy Attribute='{value}' />");
                var attr = el.Attributes.FirstOrDefault();
                if (attr?.ValueNode != null)
                {
                    return attr.ValueNode;
                }
            }
            catch {}

            try
            {
                var el = XamlParser.ParseElement($"<Dummy Attribute=\"{value}\" />");
                var attr = el.Attributes.FirstOrDefault();
                if (attr?.ValueNode != null)
                {
                    return attr.ValueNode;
                }
            }
            catch {}

            return new XamlLiteralValueSyntax(value);
        }

        private XamlElementSyntax? LocateAstElement(XamlDocumentSyntax doc, object target, object xamlRoot)
        {
            var logPath = "/Users/wieslawsoltes/GitHub/CDP/mutation_debug.log";
            try
            {
                File.AppendAllText(logPath, $"[MUTATION] LocateAstElement target={_adapter.GetTypeName(target)} (Name={_adapter.GetPropertyValue(target, "Name")})\n");
            }
            catch {}

            var rootEl = doc.RootElement;
            if (rootEl == null) return null;

            var namedAncestor = FindNamedAncestor(target, xamlRoot);
            try
            {
                File.AppendAllText(logPath, $"[MUTATION] namedAncestor={namedAncestor?.GetType().Name} (Name={_adapter.GetPropertyValue(namedAncestor, "Name")})\n");
            }
            catch {}

            if (namedAncestor != null)
            {
                var ancestorName = _adapter.GetPropertyValue(namedAncestor, "Name") as string;
                if (string.IsNullOrEmpty(ancestorName))
                {
                    ancestorName = _adapter.GetPropertyValue(namedAncestor, "id") as string;
                }
                if (string.IsNullOrEmpty(ancestorName)) return null;

                var startEl = FindElementByName(doc.RootElement, ancestorName);
                try
                {
                    File.AppendAllText(logPath, $"[MUTATION] startEl found={(startEl != null ? startEl.LocalName : "null")}\n");
                }
                catch {}

                if (startEl == null) return null;

                if (target == namedAncestor)
                {
                    return startEl;
                }

                var path = ComputeLogicalPath(namedAncestor, target);
                try
                {
                    File.AppendAllText(logPath, $"[MUTATION] path={string.Join(" -> ", path.Select(p => $"{p.TypeName}[{p.Index}]"))}\n");
                }
                catch {}

                var result = NavigateAstPath(startEl, path);
                try
                {
                    File.AppendAllText(logPath, $"[MUTATION] NavigateAstPath result={(result != null ? result.LocalName : "null")}\n");
                }
                catch {}

                return result;
            }
            else
            {
                if (target == xamlRoot)
                {
                    return rootEl;
                }
                var path = ComputeLogicalPath(xamlRoot, target);
                try
                {
                    File.AppendAllText(logPath, $"[MUTATION] path (no named ancestor)={string.Join(" -> ", path.Select(p => $"{p.TypeName}[{p.Index}]"))}\n");
                }
                catch {}

                var result = NavigateAstPath(rootEl, path);
                try
                {
                    File.AppendAllText(logPath, $"[MUTATION] NavigateAstPath result (no named ancestor)={(result != null ? result.LocalName : "null")}\n");
                }
                catch {}

                return result;
            }
        }

        private object? FindNamedAncestor(object target, object xamlRoot)
        {
            object? current = target;
            while (current != null)
            {
                var nameVal = _adapter.GetPropertyValue(current, "Name") as string;
                if (string.IsNullOrEmpty(nameVal))
                {
                    nameVal = _adapter.GetPropertyValue(current, "id") as string;
                }

                if (!string.IsNullOrEmpty(nameVal))
                {
                    return current;
                }
                if (current == xamlRoot)
                {
                    break;
                }
                current = _adapter.GetParent(current);
            }
            return null;
        }

        private IEnumerable<XamlElementSyntax> Descendants(XamlElementSyntax element)
        {
            foreach (var child in element.Children.OfType<XamlElementSyntax>())
            {
                yield return child;
                foreach (var desc in Descendants(child))
                {
                    yield return desc;
                }
            }
        }

        private XamlElementSyntax? FindElementByName(XamlElementSyntax? container, string name)
        {
            if (container == null) return null;

            foreach (var attr in container.Attributes)
            {
                if (attr.LocalName == "Name" && attr.ValueNode is XamlLiteralValueSyntax literal && literal.Value == name)
                {
                    return container;
                }
            }

            foreach (var element in Descendants(container))
            {
                foreach (var attr in element.Attributes)
                {
                    if (attr.LocalName == "Name" && attr.ValueNode is XamlLiteralValueSyntax literal && literal.Value == name)
                    {
                        return element;
                    }
                }
            }
            return null;
        }

        private List<PathSegment> ComputeLogicalPath(object startControl, object target)
        {
            var path = new List<PathSegment>();
            object? current = target;
            while (current != null && current != startControl)
            {
                var parent = _adapter.GetParent(current);
                if (parent == null) break;

                var siblings = new List<object>();
                foreach (var child in _adapter.GetChildren(parent))
                {
                    if (_adapter.IsControl(child))
                    {
                        siblings.Add(child);
                    }
                }

                int index = siblings.IndexOf(current);
                path.Add(new PathSegment
                {
                    TypeName = _adapter.GetTypeName(current),
                    Index = index
                });

                current = parent;
            }
            path.Reverse();
            return path;
        }

        private XamlElementSyntax? NavigateAstPath(XamlElementSyntax startElement, List<PathSegment> path)
        {
            XamlElementSyntax current = startElement;
            foreach (var segment in path)
            {
                var children = GetLogicalXmlChildElements(current);
                if (segment.Index < 0 || segment.Index >= children.Count)
                {
                    return null;
                }
                var child = children[segment.Index];
                if (!string.Equals(child.LocalName, segment.TypeName, StringComparison.OrdinalIgnoreCase))
                {
                    return null;
                }
                current = child;
            }
            return current;
        }

        private List<XamlElementSyntax> GetLogicalXmlChildElements(XamlElementSyntax parent)
        {
            var list = new List<XamlElementSyntax>();
            CollectLogicalXmlElements(parent, list);
            return list;
        }

        private void CollectLogicalXmlElements(XamlElementSyntax element, List<XamlElementSyntax> results)
        {
            foreach (var child in element.Children.OfType<XamlElementSyntax>())
            {
                string localName = child.LocalName;

                if (localName.Contains('.'))
                {
                    if (localName.EndsWith(".Child", StringComparison.OrdinalIgnoreCase) ||
                        localName.EndsWith(".Children", StringComparison.OrdinalIgnoreCase) ||
                        localName.EndsWith(".Content", StringComparison.OrdinalIgnoreCase) ||
                        localName.EndsWith(".Items", StringComparison.OrdinalIgnoreCase) ||
                        localName.EndsWith(".Header", StringComparison.OrdinalIgnoreCase))
                    {
                        CollectLogicalXmlElements(child, results);
                    }
                    continue;
                }

                if (IsMetadataOrStylingElement(localName))
                {
                    continue;
                }

                results.Add(child);
            }
        }

        private bool IsMetadataOrStylingElement(string localName)
        {
            var skipped = new[]
            {
                "RowDefinition", "ColumnDefinition", "Style", "Setter",
                "ResourceDictionary", "Template", "DataTemplate", "ControlTemplate",
                "SolidColorBrush", "LinearGradientBrush", "RadialGradientBrush",
                "ImageBrush", "VisualBrush"
            };
            return skipped.Contains(localName, StringComparer.OrdinalIgnoreCase);
        }

        private XamlElementSyntax? FindParentAstElement(XamlSyntaxNode root, XamlElementSyntax childToFind)
        {
            if (root is XamlElementSyntax el)
            {
                foreach (var child in el.Children)
                {
                    if (child == childToFind) return el;
                    if (child is XamlElementSyntax elChild)
                    {
                        var p = FindParentAstElement(elChild, childToFind);
                        if (p != null) return p;
                    }
                }
            }
            else if (root is XamlDocumentSyntax doc && doc.RootElement != null)
            {
                if (doc.RootElement == childToFind) return null;
                return FindParentAstElement(doc.RootElement, childToFind);
            }
            return null;
        }

        private Dictionary<string, string> GetInheritedNamespaces(XamlElementSyntax targetElement, XamlDocumentSyntax doc)
        {
            var namespaces = new Dictionary<string, string>();
            var curr = targetElement;
            while (curr != null)
            {
                foreach (var attr in curr.Attributes)
                {
                    if (attr.LocalName == "xmlns" || attr.Prefix == "xmlns")
                    {
                        string key = attr.LocalName == "xmlns" ? "" : attr.LocalName;
                        if (attr.ValueNode is XamlLiteralValueSyntax literal)
                        {
                            if (!namespaces.ContainsKey(key))
                            {
                                namespaces[key] = literal.Value;
                            }
                        }
                    }
                }
                curr = FindParentAstElement(doc, curr);
            }

            if (!namespaces.ContainsKey(""))
            {
                namespaces[""] = _adapter.DefaultXmlNamespace;
            }
            if (!namespaces.ContainsKey("x"))
            {
                namespaces["x"] = "http://schemas.microsoft.com/winfx/2006/xaml";
            }
            return namespaces;
        }

        private string GetXamlStringForLoader(XamlElementSyntax newElement, XamlElementSyntax targetElement, XamlDocumentSyntax doc)
        {
            var namespaces = GetInheritedNamespaces(targetElement, doc);

            var addedAttrs = new List<XamlAttributeSyntax>();
            foreach (var kv in namespaces)
            {
                string prefix = kv.Key == "" ? "" : "xmlns";
                string localName = kv.Key == "" ? "xmlns" : kv.Key;

                bool exists = newElement.Attributes.Any(a =>
                    (a.LocalName == localName && a.Prefix == prefix) ||
                    (a.LocalName == "xmlns" && string.IsNullOrEmpty(a.Prefix) && kv.Key == "")
                );
                if (!exists)
                {
                    var attr = new XamlAttributeSyntax
                    {
                        Prefix = prefix,
                        LocalName = localName,
                        QuoteChar = '"',
                        ValueNode = new XamlLiteralValueSyntax(kv.Value)
                    };
                    attr.LeadingTrivia.Add(new XamlTrivia(" ", new SourceSpan()));
                    newElement.Attributes.Add(attr);
                    addedAttrs.Add(attr);
                }
            }

            string loaderXaml = newElement.ToFullString();

            foreach (var attr in addedAttrs)
            {
                newElement.Attributes.Remove(attr);
            }

            return loaderXaml;
        }

        private void EnsureScan()
        {
            lock (_scanLock)
            {
                if (_scanned) return;

                string root = FindWorkspaceRoot();
                if (root != null)
                {
                    var files = new List<string>();
                    FindXamlFiles(root, files);
                    foreach (var file in files)
                    {
                        var className = ExtractClassName(file);
                        if (!string.IsNullOrEmpty(className))
                        {
                            _classToFileMap[className] = file;
                        }
                    }
                }
                _scanned = true;
            }
        }

        private string FindWorkspaceRoot()
        {
            string? current = Directory.GetCurrentDirectory();
            while (current != null)
            {
                try
                {
                    if (Directory.EnumerateFiles(current, "*.sln").Any() ||
                        Directory.EnumerateFiles(current, "*.slnx").Any() ||
                        Directory.Exists(Path.Combine(current, ".git")))
                    {
                        return current;
                    }
                }
                catch { }
                var parent = Directory.GetParent(current);
                if (parent == null || parent.FullName == current)
                {
                    break;
                }
                current = parent.FullName;
            }
            return Directory.GetCurrentDirectory();
        }

        private void FindXamlFiles(string dir, List<string> results)
        {
            var name = Path.GetFileName(dir);
            if (string.IsNullOrEmpty(name)) return;

            if (string.Equals(name, "bin", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(name, "obj", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(name, ".git", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(name, ".vs", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(name, ".idea", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(name, "node_modules", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            try
            {
                foreach (var ext in _adapter.XamlFileExtensions)
                {
                    string searchPattern = ext.StartsWith('.') ? $"*{ext}" : $"*.{ext}";
                    foreach (var file in Directory.EnumerateFiles(dir, searchPattern))
                    {
                        results.Add(file);
                    }
                }
                foreach (var subDir in Directory.EnumerateDirectories(dir))
                {
                    FindXamlFiles(subDir, results);
                }
            }
            catch
            {
                // Ignore access/security errors
            }
        }

        private (object? xamlRoot, string? filePath) FindXamlRoot(object target)
        {
            var logPath = "/Users/wieslawsoltes/GitHub/CDP/mutation_debug.log";
            EnsureScan();
            object? current = target;
            try
            {
                File.AppendAllText(logPath, $"[MUTATION] FindXamlRoot start target={_adapter.GetTypeName(target)}\n");
            }
            catch {}
            while (current != null)
            {
                var fullName = _adapter.GetClassFullName(current);
                try
                {
                    File.AppendAllText(logPath, $"  -> current={_adapter.GetTypeName(current)} (Name={_adapter.GetPropertyValue(current, "Name")}), class={fullName}\n");
                }
                catch {}
                if (fullName != null && _classToFileMap.TryGetValue(fullName, out var filePath))
                {
                    try
                    {
                        File.AppendAllText(logPath, $"  -> FOUND file: {filePath}\n");
                    }
                    catch {}
                    return (current, filePath);
                }
                var parent = _adapter.GetParent(current);
                try
                {
                    File.AppendAllText(logPath, $"  -> parent resolved to={(parent != null ? _adapter.GetTypeName(parent) : "null")} (Name={(parent != null ? _adapter.GetPropertyValue(parent, "Name") : "null")})\n");
                }
                catch {}
                current = parent;
            }
            try
            {
                File.AppendAllText(logPath, $"  -> NOT FOUND xaml root\n");
            }
            catch {}
            return (null, null);
        }

        private static string? ExtractClassName(string filePath)
        {
            try
            {
                using var reader = new StreamReader(filePath);
                var sb = new StringBuilder();
                int ch;
                bool inTag = false;
                char? quoteChar = null;
                int angleBrackets = 0;

                while ((ch = reader.Read()) != -1)
                {
                    char c = (char)ch;
                    if (!inTag)
                    {
                        if (c == '<')
                        {
                            var next1 = (char)reader.Peek();
                            if (next1 == '?' || next1 == '!')
                            {
                                while ((ch = reader.Read()) != -1)
                                {
                                    if ((char)ch == '>') break;
                                }
                                continue;
                            }
                            inTag = true;
                            sb.Append(c);
                            angleBrackets = 1;
                        }
                    }
                    else
                    {
                        sb.Append(c);
                        if (quoteChar.HasValue)
                        {
                            if (c == quoteChar.Value)
                            {
                                quoteChar = null;
                            }
                        }
                        else
                        {
                            if (c == '"' || c == '\'')
                            {
                                quoteChar = c;
                            }
                            else if (c == '<')
                            {
                                angleBrackets++;
                            }
                            else if (c == '>')
                            {
                                angleBrackets--;
                                if (angleBrackets == 0)
                                {
                                    break;
                                }
                            }
                        }
                    }
                }

                string tag = sb.ToString().Trim();
                if (!tag.StartsWith("<") || !tag.EndsWith(">")) return null;

                if (!tag.EndsWith("/>"))
                {
                    tag = tag.Substring(0, tag.Length - 1) + " />";
                }

                var rootEl = XamlParser.ParseElement(tag);
                var classNameAttr = rootEl.Attributes.FirstOrDefault(a => a.LocalName == "Class" && (a.Prefix == "x" || string.IsNullOrEmpty(a.Prefix)));
                return (classNameAttr?.ValueNode as XamlLiteralValueSyntax)?.Value;
            }
            catch
            {
                return null;
            }
        }

        private class PathSegment
        {
            public string TypeName { get; set; } = "";
            public int Index { get; set; }
        }
    }
}
