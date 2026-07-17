using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Chrome.DevTools.Protocol;
using Chrome.DevTools.Protocol.Domains;
using Xaml.Compiler.Mutation;
using Avalonia.Diagnostics.Cdp.Domains;

using Microsoft.Extensions.Logging;

namespace Avalonia.Diagnostics.Cdp;

public class AvaloniaXamlMutationEngine : IMutationEngine
{
    private static readonly ILogger Logger = CdpLogging.CreateLogger<AvaloniaXamlMutationEngine>();
    private readonly CdpSession _session;
    private readonly object _scanLock = new();
    private bool _scanned = false;
    private readonly ConcurrentDictionary<string, string> _classToFileMap = new(StringComparer.Ordinal);

    public AvaloniaXamlMutationEngine(CdpSession session)
    {
        _session = session;
    }

    public bool CanMutate(object target)
    {
        if (target is not Control control)
        {
            Logger.LogWarning($"[MUTATION DEBUG] CanMutate false: target is not Control. Type: {target?.GetType().FullName}");
            return false;
        }
        var (xamlRoot, filePath) = FindXamlRoot(control);
        Logger.LogWarning($"[MUTATION DEBUG] CanMutate for {control.GetType().Name} (Name={control.Name}): xamlRoot={xamlRoot?.GetType().Name}, filePath={filePath}");
        return xamlRoot != null && filePath != null;
    }

    public async Task<bool> SetAttributeAsync(object target, string name, string value)
    {
        if (target is not Control control) return false;

        var (xamlRoot, filePath) = FindXamlRoot(control);
        if (xamlRoot == null || filePath == null) return false;

        XDocument doc;
        XElement? targetEl;
        try
        {
            doc = XDocument.Load(filePath, LoadOptions.PreserveWhitespace);
            targetEl = LocateXmlElementInDoc(doc, control, xamlRoot);
            if (targetEl == null) return false;

            SetXmlAttribute(targetEl, name, value);

            await SaveDocumentAsync(doc, filePath);
        }
        catch
        {
            return false;
        }

        // Live UI update
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            ApplyAttributeValue(control, name, value);
        });

        return true;
    }

    public async Task<bool> RemoveAttributeAsync(object target, string name)
    {
        if (target is not Control control) return false;

        var (xamlRoot, filePath) = FindXamlRoot(control);
        if (xamlRoot == null || filePath == null) return false;

        XDocument doc;
        XElement? targetEl;
        try
        {
            doc = XDocument.Load(filePath, LoadOptions.PreserveWhitespace);
            targetEl = LocateXmlElementInDoc(doc, control, xamlRoot);
            if (targetEl == null) return false;

            RemoveXmlAttribute(targetEl, name);

            await SaveDocumentAsync(doc, filePath);
        }
        catch
        {
            return false;
        }

        // Live UI update
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            if (name.Equals("class", StringComparison.OrdinalIgnoreCase))
            {
                control.Classes.Clear();
            }
            else if (name.Equals("name", StringComparison.OrdinalIgnoreCase) || name.Equals("id", StringComparison.OrdinalIgnoreCase))
            {
                control.Name = null;
            }
        });

        return true;
    }

    public async Task<bool> RemoveNodeAsync(object target)
    {
        if (target is not Control control) return false;

        var (xamlRoot, filePath) = FindXamlRoot(control);
        if (xamlRoot == null || filePath == null) return false;

        XDocument doc;
        XElement? targetEl;
        try
        {
            doc = XDocument.Load(filePath, LoadOptions.PreserveWhitespace);
            targetEl = LocateXmlElementInDoc(doc, control, xamlRoot);
            if (targetEl == null) return false;

            targetEl.Remove();

            await SaveDocumentAsync(doc, filePath);
        }
        catch
        {
            return false;
        }

        // Live UI update
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            var parent = control.Parent;
            if (parent is Panel panel)
            {
                panel.Children.Remove(control);
            }
            else if (parent is ContentControl contentControl)
            {
                if (contentControl.Content == control) contentControl.Content = null;
            }
            else if (parent is Decorator decorator)
            {
                if (decorator.Child == control) decorator.Child = null;
            }
            else if (parent is HeaderedContentControl headeredControl)
            {
                if (headeredControl.Content == control) headeredControl.Content = null;
                else if (headeredControl.Header == control) headeredControl.Header = null;
            }
            else if (parent is HeaderedItemsControl headeredItemsControl)
            {
                if (headeredItemsControl.Header == control) headeredItemsControl.Header = null;
            }
            else if (control.Parent == null && control.GetVisualParent() is Panel visualPanel)
            {
                visualPanel.Children.Remove(control);
            }
        });

        return true;
    }

    public async Task<bool> SetOuterHtmlAsync(object target, string outerHtml)
    {
        if (target is not Control control) return false;

        var (xamlRoot, filePath) = FindXamlRoot(control);
        if (xamlRoot == null || filePath == null) return false;

        XDocument doc;
        XElement? targetEl;
        XElement newEl;
        try
        {
            doc = XDocument.Load(filePath, LoadOptions.PreserveWhitespace);
            targetEl = LocateXmlElementInDoc(doc, control, xamlRoot);
            if (targetEl == null) return false;

            newEl = ParseFragment(outerHtml, targetEl);
            targetEl.ReplaceWith(newEl);

            await SaveDocumentAsync(doc, filePath);
        }
        catch
        {
            return false;
        }

        string loaderXaml = GetXamlStringForLoader(newEl, targetEl);

        // Live UI update
        bool success = false;
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            try
            {
                var newControlObj = Avalonia.Markup.Xaml.AvaloniaRuntimeXamlLoader.Load(loaderXaml);
                if (newControlObj is Control newControl)
                {
                    if (_session.NodeMap.TryGetId(control, out int nodeId))
                    {
                        ReplaceControlInParent(control, newControl);
                        _session.NodeMap.UpdateNodeMapping(nodeId, newControl);
                        success = true;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogErrorMessage("MutationEngine", "SetOuterHtmlAsync live UI update failed", ex);
            }
        });

        return success;
    }

    public async Task<string?> GetOuterHtmlAsync(object target)
    {
        if (target is not Control control) return null;

        var (xamlRoot, filePath) = FindXamlRoot(control);
        if (xamlRoot == null || filePath == null) return null;

        try
        {
            var doc = XDocument.Load(filePath, LoadOptions.PreserveWhitespace);
            var targetEl = LocateXmlElementInDoc(doc, control, xamlRoot);
            return targetEl?.ToString();
        }
        catch
        {
            return null;
        }
    }

    private string GetXamlStringForLoader(XElement parsedElement, XElement targetElement)
    {
        var elementToSerialize = new XElement(parsedElement);
        XElement? curr = targetElement;
        while (curr != null)
        {
            foreach (var attr in curr.Attributes())
            {
                if (attr.IsNamespaceDeclaration)
                {
                    if (elementToSerialize.Attribute(attr.Name) == null)
                    {
                        elementToSerialize.Add(attr);
                    }
                }
            }
            curr = curr.Parent;
        }

        var hasDefaultXmlns = elementToSerialize.Attributes().Any(a => a.Name.LocalName == "xmlns" && string.IsNullOrEmpty(a.Name.NamespaceName));
        if (!hasDefaultXmlns)
        {
            elementToSerialize.SetAttributeValue("xmlns", "https://github.com/avaloniaui");
        }
        var hasXXmlns = elementToSerialize.Attributes().Any(a => a.Name.LocalName == "x" && a.Name.NamespaceName == "http://www.w3.org/2000/xmlns/");
        if (!hasXXmlns)
        {
            elementToSerialize.SetAttributeValue(XNamespace.Xmlns + "x", "http://schemas.microsoft.com/winfx/2006/xaml");
        }

        return elementToSerialize.ToString(SaveOptions.DisableFormatting);
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
                FindAxamlFiles(root, files);
                foreach (var file in files)
                {
                    try
                    {
                        using var stream = File.OpenRead(file);
                        var xdoc = XDocument.Load(stream);
                        var rootEl = xdoc.Root;
                        if (rootEl != null)
                        {
                            var className = rootEl.Attributes().FirstOrDefault(a => a.Name.LocalName == "Class")?.Value;
                            if (!string.IsNullOrEmpty(className))
                            {
                                _classToFileMap[className] = file;
                            }
                        }
                    }
                    catch
                    {
                        // Ignore malformed XML files during scan
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

    private void FindAxamlFiles(string dir, List<string> results)
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
            foreach (var file in Directory.EnumerateFiles(dir, "*.axaml"))
            {
                results.Add(file);
            }
            foreach (var subDir in Directory.EnumerateDirectories(dir))
            {
                FindAxamlFiles(subDir, results);
            }
        }
        catch
        {
            // Ignore access/security errors
        }
    }

    private (Control? xamlRoot, string? filePath) FindXamlRoot(Control target)
    {
        EnsureScan();
        ILogical? current = target;
        while (current != null)
        {
            if (current is Control control)
            {
                var fullName = control.GetType().FullName;
                if (fullName != null && _classToFileMap.TryGetValue(fullName, out var filePath))
                {
                    return (control, filePath);
                }
            }
            current = current.LogicalParent;
        }
        return (null, null);
    }

    private XElement? LocateXmlElementInDoc(XDocument doc, Control target, Control xamlRoot)
    {
        var rootEl = doc.Root;
        if (rootEl == null)
        {
            Logger.LogWarning($"[MUTATION DEBUG] LocateXmlElementInDoc: doc.Root is null");
            return null;
        }

        var namedAncestor = FindNamedAncestor(target, xamlRoot);
        Logger.LogWarning($"[MUTATION DEBUG] LocateXmlElementInDoc: target={target.GetType().Name} (Name={target.Name}), namedAncestor={namedAncestor?.GetType().Name} (Name={namedAncestor?.Name})");
        if (namedAncestor != null)
        {
            var startEl = FindElementByName(doc, namedAncestor.Name!);
            if (startEl == null)
            {
                Logger.LogWarning($"[MUTATION DEBUG] LocateXmlElementInDoc: startEl not found for name {namedAncestor.Name}");
                return null;
            }

            if (target == namedAncestor)
            {
                return startEl;
            }

            var path = ComputeLogicalPath(namedAncestor, target);
            Logger.LogWarning($"[MUTATION DEBUG] LocateXmlElementInDoc path: {string.Join(" -> ", path.Select(p => $"{p.TypeName}[{p.Index}]"))}");
            var result = NavigatePath(startEl, path);
            Logger.LogWarning($"[MUTATION DEBUG] LocateXmlElementInDoc NavigatePath result: {(result != null ? result.Name.ToString() : "null")}");
            return result;
        }
        else
        {
            if (target == xamlRoot)
            {
                return rootEl;
            }
            var path = ComputeLogicalPath(xamlRoot, target);
            Logger.LogWarning($"[MUTATION DEBUG] LocateXmlElementInDoc path (no named ancestor): {string.Join(" -> ", path.Select(p => $"{p.TypeName}[{p.Index}]"))}");
            var result = NavigatePath(rootEl, path);
            Logger.LogWarning($"[MUTATION DEBUG] LocateXmlElementInDoc NavigatePath result (no named ancestor): {(result != null ? result.Name.ToString() : "null")}");
            return result;
        }
    }

    private Control? FindNamedAncestor(Control target, Control xamlRoot)
    {
        ILogical? current = target;
        while (current != null)
        {
            if (current is Control control && !string.IsNullOrEmpty(control.Name))
            {
                return control;
            }
            if (current == xamlRoot)
            {
                break;
            }
            current = current.LogicalParent;
        }
        return null;
    }

    private XElement? FindElementByName(XContainer container, string name)
    {
        foreach (var element in container.Descendants())
        {
            foreach (var attr in element.Attributes())
            {
                if (attr.Name.LocalName == "Name" && attr.Value == name)
                {
                    return element;
                }
            }
        }
        return null;
    }

    private List<PathSegment> ComputeLogicalPath(Control startControl, Control target)
    {
        var path = new List<PathSegment>();
        ILogical? current = target;
        while (current != null && current != startControl)
        {
            var parent = current.LogicalParent;
            if (parent == null) break;

            var siblings = parent.LogicalChildren
                .OfType<object>()
                .Where(child => child is Visual || child is StyledElement)
                .ToList();

            int index = siblings.IndexOf(current);
            path.Add(new PathSegment
            {
                TypeName = current.GetType().Name,
                Index = index
            });

            current = parent;
        }
        path.Reverse();
        return path;
    }

    private XElement? NavigatePath(XElement startElement, List<PathSegment> path)
    {
        XElement current = startElement;
        foreach (var segment in path)
        {
            var children = GetLogicalXmlChildElements(current);
            if (segment.Index < 0 || segment.Index >= children.Count)
            {
                return null;
            }
            current = children[segment.Index];
        }
        return current;
    }

    private List<XElement> GetLogicalXmlChildElements(XElement parent)
    {
        var list = new List<XElement>();
        CollectLogicalXmlElements(parent, list);
        return list;
    }

    private void CollectLogicalXmlElements(XElement element, List<XElement> results)
    {
        foreach (var child in element.Elements())
        {
            string localName = child.Name.LocalName;

            if (localName.Contains('.'))
            {
                if (localName.EndsWith(".Child", StringComparison.OrdinalIgnoreCase) ||
                    localName.EndsWith(".Children", StringComparison.OrdinalIgnoreCase) ||
                    localName.EndsWith(".Content", StringComparison.OrdinalIgnoreCase) ||
                    localName.EndsWith(".Items", StringComparison.OrdinalIgnoreCase))
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

    private void SetXmlAttribute(XElement element, string name, string value)
    {
        XAttribute? attr = null;
        if (name.Contains(':'))
        {
            var parts = name.Split(':');
            var localName = parts[1];
            attr = element.Attributes().FirstOrDefault(a => string.Equals(a.Name.LocalName, localName, StringComparison.OrdinalIgnoreCase));
        }
        else
        {
            attr = element.Attributes().FirstOrDefault(a => string.Equals(a.Name.LocalName, name, StringComparison.OrdinalIgnoreCase));
        }

        if (attr != null)
        {
            attr.Value = value;
        }
        else
        {
            if (name.Contains(':'))
            {
                var parts = name.Split(':');
                var prefix = parts[0];
                var localName = parts[1];
                var ns = element.GetNamespaceOfPrefix(prefix);
                if (ns == null && prefix == "x")
                {
                    ns = "http://schemas.microsoft.com/winfx/2006/xaml";
                }

                if (ns != null)
                {
                    element.SetAttributeValue(ns + localName, value);
                }
                else
                {
                    element.SetAttributeValue(name, value);
                }
            }
            else
            {
                element.SetAttributeValue(name, value);
            }
        }
    }

    private void RemoveXmlAttribute(XElement element, string name)
    {
        XAttribute? attr = null;
        if (name.Contains(':'))
        {
            var parts = name.Split(':');
            var localName = parts[1];
            attr = element.Attributes().FirstOrDefault(a => string.Equals(a.Name.LocalName, localName, StringComparison.OrdinalIgnoreCase));
        }
        else
        {
            attr = element.Attributes().FirstOrDefault(a => string.Equals(a.Name.LocalName, name, StringComparison.OrdinalIgnoreCase));
        }

        attr?.Remove();
    }

    private XElement ParseFragment(string outerHtml, XElement targetElement)
    {
        var namespaces = new Dictionary<string, string>();
        XElement? curr = targetElement;
        while (curr != null)
        {
            foreach (var attr in curr.Attributes())
            {
                if (attr.IsNamespaceDeclaration)
                {
                    if (!namespaces.ContainsKey(attr.Name.LocalName))
                    {
                        namespaces[attr.Name.LocalName] = attr.Value;
                    }
                }
            }
            curr = curr.Parent;
        }

        if (!namespaces.ContainsKey("x"))
        {
            namespaces["x"] = "http://schemas.microsoft.com/winfx/2006/xaml";
        }
        if (!namespaces.ContainsKey("d"))
        {
            namespaces["d"] = "http://schemas.openxmlformats.org/markup-compatibility/2006"; // or expression blend
        }

        var sb = new System.Text.StringBuilder();
        sb.Append("<Wrapper");
        foreach (var kvp in namespaces)
        {
            if (kvp.Key == "xmlns")
            {
                sb.Append($" xmlns=\"{kvp.Value}\"");
            }
            else
            {
                sb.Append($" xmlns:{kvp.Key}=\"{kvp.Value}\"");
            }
        }
        sb.Append(">");
        sb.Append(outerHtml);
        sb.Append("</Wrapper>");

        var wrapperDoc = XDocument.Parse(sb.ToString(), LoadOptions.PreserveWhitespace);
        var wrapperRoot = wrapperDoc.Root;
        var parsedElement = wrapperRoot?.Elements().FirstOrDefault();
        if (parsedElement == null)
        {
            throw new Exception("Failed to parse XAML fragment");
        }

        parsedElement.Remove();
        return parsedElement;
    }

    private async Task SaveDocumentAsync(XDocument doc, string filePath)
    {
        using var writer = new StreamWriter(filePath, false, System.Text.Encoding.UTF8);
        await Task.Run(() => doc.Save(writer, SaveOptions.DisableFormatting));
    }

    private void ReplaceControlInParent(Control oldControl, Control newControl)
    {
        var parent = oldControl.Parent;
        if (parent is Panel panel)
        {
            int index = panel.Children.IndexOf(oldControl);
            if (index != -1)
            {
                panel.Children[index] = newControl;
            }
        }
        else if (parent is ContentControl contentControl)
        {
            if (contentControl.Content == oldControl)
            {
                contentControl.Content = newControl;
            }
        }
        else if (parent is Decorator decorator)
        {
            if (decorator.Child == oldControl)
            {
                decorator.Child = newControl;
            }
        }
        else if (parent is HeaderedContentControl headeredControl)
        {
            if (headeredControl.Content == oldControl)
            {
                headeredControl.Content = newControl;
            }
            else if (headeredControl.Header == oldControl)
            {
                headeredControl.Header = newControl;
            }
        }
        else if (parent is HeaderedItemsControl headeredItemsControl)
        {
            if (headeredItemsControl.Header == oldControl)
            {
                headeredItemsControl.Header = newControl;
            }
        }
        else if (oldControl.Parent == null && oldControl.GetVisualParent() is Panel visualPanel)
        {
            int index = visualPanel.Children.IndexOf(oldControl);
            if (index != -1)
            {
                visualPanel.Children[index] = newControl;
            }
        }
    }

    private static void ApplyAttributeValue(Control control, string name, string value)
    {
        if (name.Equals("class", StringComparison.OrdinalIgnoreCase))
        {
            control.Classes.Clear();
            var classes = value.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var cls in classes)
            {
                control.Classes.Add(cls);
            }
        }
        else if (name.Equals("name", StringComparison.OrdinalIgnoreCase) || name.Equals("id", StringComparison.OrdinalIgnoreCase))
        {
            control.Name = value;
        }
        else if (name.Equals("text", StringComparison.OrdinalIgnoreCase))
        {
            if (control is TextBlock textBlock)
            {
                textBlock.Text = value;
            }
            else if (control is TextBox textBox)
            {
                textBox.Text = value;
            }
            else
            {
                if (control is HeaderedContentControl headeredControl)
                {
                    if (headeredControl.Header is string || headeredControl.Content is not string)
                    {
                        headeredControl.Header = value;
                    }
                    else
                    {
                        headeredControl.Content = value;
                    }
                }
                else if (control is ContentControl contentControl)
                {
                    contentControl.Content = value;
                }
                else if (control is HeaderedItemsControl headeredItemsControl)
                {
                    headeredItemsControl.Header = value;
                }
            }
        }
        else
        {
            CssDomain.SetControlProperty(control, name, value);
        }
    }

    private class PathSegment
    {
        public string TypeName { get; set; } = "";
        public int Index { get; set; }
    }
}
