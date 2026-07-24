using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Xml.Linq;
using Chrome.DevTools.Protocol;
using Chrome.DevTools.Protocol.Domains;
using Xaml.Compiler.Mutation;
using Wpf.Diagnostics.Cdp.Domains;
using Microsoft.Extensions.Logging;

namespace Wpf.Diagnostics.Cdp;

public class WpfXamlMutationEngine : IMutationEngine
{
    private static readonly ILogger Logger = CdpLogging.CreateLogger<WpfXamlMutationEngine>();
    private readonly CdpSession _session;
    private readonly object _scanLock = new();
    private bool _scanned = false;
    private readonly ConcurrentDictionary<string, string> _classToFileMap = new(StringComparer.Ordinal);

    public WpfXamlMutationEngine(CdpSession session)
    {
        _session = session;
    }

    public bool CanMutate(object target)
    {
        if (target is not FrameworkElement control)
        {
            Logger.LogWarning($"[MUTATION DEBUG] CanMutate false: target is not FrameworkElement. Type: {target?.GetType().FullName}");
            return false;
        }
        var (xamlRoot, filePath) = FindXamlRoot(control);
        Logger.LogWarning($"[MUTATION DEBUG] CanMutate for {control.GetType().Name} (Name={control.Name}): xamlRoot={xamlRoot?.GetType().Name}, filePath={filePath}");
        return xamlRoot != null && filePath != null;
    }

    public async Task<bool> SetAttributeAsync(object target, string name, string value)
    {
        if (target is not FrameworkElement control) return false;

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
        if (_session.Window != null)
        {
            await _session.Window.Dispatcher.InvokeAsync(() =>
            {
                ApplyAttributeValue(control, name, value);
            });
        }

        return true;
    }

    public async Task<bool> RemoveAttributeAsync(object target, string name)
    {
        if (target is not FrameworkElement control) return false;

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
        if (_session.Window != null)
        {
            await _session.Window.Dispatcher.InvokeAsync(() =>
            {
                if (name.Equals("name", StringComparison.OrdinalIgnoreCase) || name.Equals("id", StringComparison.OrdinalIgnoreCase))
                {
                    control.Name = string.Empty;
                }
            });
        }

        return true;
    }

    public async Task<bool> RemoveNodeAsync(object target)
    {
        if (target is not FrameworkElement control) return false;

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
        if (_session.Window != null)
        {
            await _session.Window.Dispatcher.InvokeAsync(() =>
            {
                var parent = VisualTreeHelper.GetParent(control);
                if (parent is Panel panel)
                {
                    panel.Children.Remove(control);
                }
                else if (parent is ContentControl contentControl)
                {
                    if (contentControl.Content == control) contentControl.Content = null;
                }
                else if (parent is Border border)
                {
                    if (border.Child == control) border.Child = null;
                }
            });
        }

        return true;
    }

    public async Task<bool> SetOuterHtmlAsync(object target, string outerHtml)
    {
        if (target is not FrameworkElement control) return false;

        var (xamlRoot, filePath) = FindXamlRoot(control);
        if (xamlRoot == null || filePath == null) return false;

        try
        {
            var doc = XDocument.Load(filePath, LoadOptions.PreserveWhitespace);
            var targetEl = LocateXmlElementInDoc(doc, control, xamlRoot);
            if (targetEl == null) return false;

            var newEl = ParseFragment(outerHtml, targetEl);
            targetEl.ReplaceWith(newEl);

            await SaveDocumentAsync(doc, filePath);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task<string?> GetOuterHtmlAsync(object target)
    {
        if (target is not FrameworkElement control) return null;

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
            foreach (var file in Directory.EnumerateFiles(dir, "*.xaml"))
            {
                results.Add(file);
            }
            foreach (var subDir in Directory.EnumerateDirectories(dir))
            {
                FindXamlFiles(subDir, results);
            }
        }
        catch
        {
            // Ignore access errors
        }
    }

    private (FrameworkElement? xamlRoot, string? filePath) FindXamlRoot(FrameworkElement target)
    {
        EnsureScan();
        DependencyObject? current = target;
        while (current != null)
        {
            if (current is FrameworkElement control)
            {
                var fullName = control.GetType().FullName;
                if (fullName != null && _classToFileMap.TryGetValue(fullName, out var filePath))
                {
                    return (control, filePath);
                }
            }
            current = VisualTreeHelper.GetParent(current);
        }
        return (null, null);
    }

    private XElement? LocateXmlElementInDoc(XDocument doc, FrameworkElement target, FrameworkElement xamlRoot)
    {
        var rootEl = doc.Root;
        if (rootEl == null) return null;

        var namedAncestor = FindNamedAncestor(target, xamlRoot);
        if (namedAncestor != null)
        {
            var startEl = FindElementByName(doc, namedAncestor.Name!);
            if (startEl == null) return null;
            if (target == namedAncestor) return startEl;

            var path = ComputeLogicalPath(namedAncestor, target);
            return NavigatePath(startEl, path);
        }
        else
        {
            if (target == xamlRoot) return rootEl;
            var path = ComputeLogicalPath(xamlRoot, target);
            return NavigatePath(rootEl, path);
        }
    }

    private FrameworkElement? FindNamedAncestor(FrameworkElement target, FrameworkElement xamlRoot)
    {
        DependencyObject? current = target;
        while (current != null)
        {
            if (current is FrameworkElement control && !string.IsNullOrEmpty(control.Name))
            {
                return control;
            }
            if (current == xamlRoot) break;
            current = VisualTreeHelper.GetParent(current);
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

    private List<PathSegment> ComputeLogicalPath(FrameworkElement startControl, FrameworkElement target)
    {
        var path = new List<PathSegment>();
        DependencyObject? current = target;
        while (current != null && current != startControl)
        {
            var parent = VisualTreeHelper.GetParent(current);
            if (parent == null) break;

            int childrenCount = VisualTreeHelper.GetChildrenCount(parent);
            int index = -1;
            int visualIndex = 0;
            for (int i = 0; i < childrenCount; i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child == current)
                {
                    index = visualIndex;
                    break;
                }
                visualIndex++;
            }

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
            if (segment.Index < 0 || segment.Index >= children.Count) return null;
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
                    localName.EndsWith(".Content", StringComparison.OrdinalIgnoreCase))
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
            "ResourceDictionary", "ControlTemplate", "DataTemplate",
            "SolidColorBrush", "LinearGradientBrush"
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
            element.SetAttributeValue(name, value);
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

    private static void ApplyAttributeValue(FrameworkElement control, string name, string value)
    {
        if (name.Equals("name", StringComparison.OrdinalIgnoreCase) || name.Equals("id", StringComparison.OrdinalIgnoreCase))
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
            else if (control is ContentControl contentControl)
            {
                contentControl.Content = value;
            }
        }
    }

    private class PathSegment
    {
        public string TypeName { get; set; } = "";
        public int Index { get; set; }
    }
}
