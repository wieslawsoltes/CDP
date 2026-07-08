using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;

namespace Wpf.Diagnostics.Cdp;

public static class VisualExtensions
{
    public static IEnumerable<Visual> GetVisualChildren(this Visual visual)
    {
        int count = VisualTreeHelper.GetChildrenCount(visual);
        for (int i = 0; i < count; i++)
        {
            if (VisualTreeHelper.GetChild(visual, i) is Visual child)
            {
                yield return child;
            }
        }
    }

    public static Visual? GetVisualParent(this Visual visual)
    {
        return VisualTreeHelper.GetParent(visual) as Visual;
    }
}

public static class SelectorEngine
{
    private static readonly HashSet<string> s_knownVisualTypes = new(StringComparer.OrdinalIgnoreCase);
    private static readonly object s_lock = new();
    private static bool s_initializedTypes = false;

    [UnconditionalSuppressMessage("Trimming", "IL2026:RequiresUnreferencedCode", Justification = "Assembly scanning to discover loaded visual types for CSS selector parsing")]
    private static void EnsureInitializedTypes()
    {
        if (s_initializedTypes) return;
        lock (s_lock)
        {
            if (s_initializedTypes) return;
            try
            {
                foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    try
                    {
                        foreach (var type in assembly.GetTypes())
                        {
                            if (type.IsClass && !type.IsAbstract && typeof(Visual).IsAssignableFrom(type))
                            {
                                s_knownVisualTypes.Add(type.Name);
                                if (type.FullName != null)
                                {
                                    s_knownVisualTypes.Add(type.FullName);
                                }
                            }
                        }
                    }
                    catch (ReflectionTypeLoadException ex)
                    {
                        if (ex.Types != null)
                        {
                            foreach (var type in ex.Types)
                            {
                                if (type != null && type.IsClass && !type.IsAbstract && typeof(Visual).IsAssignableFrom(type))
                                {
                                    s_knownVisualTypes.Add(type.Name);
                                    if (type.FullName != null)
                                    {
                                        s_knownVisualTypes.Add(type.FullName);
                                    }
                                }
                            }
                        }
                    }
                    catch
                    {
                        // Ignore assembly loading issues
                    }
                }
            }
            catch
            {
                // Ignore general errors
            }
            s_initializedTypes = true;
        }
    }

    private static bool IsStandardCss(string selector)
    {
        EnsureInitializedTypes();
        return CssSelectorParser.IsStandardCss(selector, part => s_knownVisualTypes.Contains(part));
    }

    private static string NormalizeSelector(string selector)
    {
        EnsureInitializedTypes();
        if (selector.Contains(":has-text(", StringComparison.OrdinalIgnoreCase))
        {
            selector = selector.Replace(":has-text(", ":contains(", StringComparison.OrdinalIgnoreCase);
        }
        return CssSelectorParser.NormalizeSelector(selector, part => s_knownVisualTypes.Contains(part));
    }

    private static bool VisualContainsText(Visual visual, string text, bool recursive = true)
    {
        if (string.IsNullOrEmpty(text)) return false;

        if (visual is TextBlock textBlock)
        {
            return textBlock.Text != null && textBlock.Text.Contains(text, StringComparison.OrdinalIgnoreCase);
        }
        if (visual is TextBox textBox)
        {
            return textBox.Text != null && textBox.Text.Contains(text, StringComparison.OrdinalIgnoreCase);
        }
        if (visual is ContentControl contentControl)
        {
            if (contentControl.Content is string contentStr)
            {
                return contentStr.Contains(text, StringComparison.OrdinalIgnoreCase);
            }
        }
        if (visual is HeaderedContentControl headeredContentControl)
        {
            if (headeredContentControl.Header is string headerStr)
            {
                return headerStr.Contains(text, StringComparison.OrdinalIgnoreCase);
            }
        }
        if (visual is HeaderedItemsControl headeredItemsControl)
        {
            if (headeredItemsControl.Header is string headerStr)
            {
                return headerStr.Contains(text, StringComparison.OrdinalIgnoreCase);
            }
        }
        if (visual is ContentPresenter contentPresenter)
        {
            if (contentPresenter.Content is string contentStr)
            {
                return contentStr.Contains(text, StringComparison.OrdinalIgnoreCase);
            }
        }

        // Reflection fallback for other properties (Text/Content/Header)
        var properties = visual.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);
        foreach (var prop in properties)
        {
            if (prop.Name.Equals("Text", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    var val = prop.GetValue(visual);
                    if (val is string valStr && valStr.Contains(text, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
                catch { }
            }
            else if (prop.Name.Equals("Content", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    var val = prop.GetValue(visual);
                    if (val is string valStr && valStr.Contains(text, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
                catch { }
            }
            else if (prop.Name.Equals("Header", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    var val = prop.GetValue(visual);
                    if (val is string valStr && valStr.Contains(text, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
                catch { }
            }
        }

        if (recursive)
        {
            foreach (var child in visual.GetVisualChildren())
            {
                if (VisualContainsText(child, text, true)) return true;
            }
        }

        return false;
    }

    public static string? GetVisualTextContent(Visual visual)
    {
        if (visual is FrameworkElement fe)
        {
            var txt = Domains.DomDomain.GetControlTextOrContent(fe);
            if (!string.IsNullOrEmpty(txt)) return txt;
        }

        foreach (var child in visual.GetVisualChildren())
        {
            var txt = GetVisualTextContent(child);
            if (!string.IsNullOrEmpty(txt)) return txt;
        }

        return null;
    }

    public static Visual? QuerySelector(Visual root, string selector, bool useLogicalTree = false)
    {
        if (string.IsNullOrWhiteSpace(selector)) return null;
        var normalizedSelector = NormalizeSelector(selector);
        var result = QuerySelectorInternal(root, normalizedSelector, useLogicalTree);
        if (result != null) return result;

        if (root is Window)
        {
            foreach (var win in CdpServer.GetWindows().Select(x => x.Window))
            {
                if (win != root)
                {
                    result = QuerySelectorInternal(win, normalizedSelector, useLogicalTree);
                    if (result != null) return result;
                }
            }
        }

        return null;
    }

    public static List<Visual> QuerySelectorAll(Visual root, string selector, bool useLogicalTree = false)
    {
        var results = new List<Visual>();
        if (string.IsNullOrWhiteSpace(selector)) return results;
        var normalizedSelector = NormalizeSelector(selector);
        QuerySelectorAllInternal(root, normalizedSelector, results, useLogicalTree);

        if (results.Count == 0 && root is Window)
        {
            foreach (var win in CdpServer.GetWindows().Select(x => x.Window))
            {
                if (win != root)
                {
                    QuerySelectorAllInternal(win, normalizedSelector, results, useLogicalTree);
                }
            }
        }

        return results;
    }

    private static Visual? QuerySelectorInternal(Visual current, string selector, bool useLogicalTree)
    {
        if (MatchesInternal(current, selector, useLogicalTree))
        {
            return current;
        }

        var children = useLogicalTree ? GetLogicalChildren(current) : current.GetVisualChildren();
        foreach (var child in children)
        {
            var result = QuerySelectorInternal(child, selector, useLogicalTree);
            if (result != null) return result;
        }

        return null;
    }

    private static void QuerySelectorAllInternal(Visual current, string selector, List<Visual> results, bool useLogicalTree)
    {
        if (MatchesInternal(current, selector, useLogicalTree))
        {
            results.Add(current);
        }

        var children = useLogicalTree ? GetLogicalChildren(current) : current.GetVisualChildren();
        foreach (var child in children)
        {
            QuerySelectorAllInternal(child, selector, results, useLogicalTree);
        }
    }

    public static bool Matches(Visual visual, string selector, bool useLogicalTree = false)
    {
        if (string.IsNullOrWhiteSpace(selector)) return false;
        var normalizedSelector = NormalizeSelector(selector);
        return MatchesInternal(visual, normalizedSelector, useLogicalTree);
    }

    private static bool MatchesInternal(Visual visual, string normalizedSelector, bool useLogicalTree)
    {
        var tokens = CssSelectorParser.TokenizeSelector(normalizedSelector);
        if (tokens.Count == 0) return false;

        return MatchesTokens(visual, tokens, tokens.Count - 1, useLogicalTree);
    }

    private static bool MatchesTokens(Visual visual, List<string> tokens, int tokenIndex, bool useLogicalTree)
    {
        if (tokenIndex < 0) return true;

        var simpleSelector = tokens[tokenIndex];
        if (!MatchesSimple(visual, simpleSelector, useLogicalTree)) return false;

        if (tokenIndex == 0) return true;

        var combinator = tokens[tokenIndex - 1];

        if (combinator == ">")
        {
            var parent = useLogicalTree ? GetLogicalParent(visual) : visual.GetVisualParent();
            if (parent == null) return false;
            return MatchesTokens(parent, tokens, tokenIndex - 2, useLogicalTree);
        }
        else if (combinator == " ")
        {
            var parent = useLogicalTree ? GetLogicalParent(visual) : visual.GetVisualParent();
            while (parent != null)
            {
                if (MatchesTokens(parent, tokens, tokenIndex - 2, useLogicalTree))
                {
                    return true;
                }
                parent = useLogicalTree ? GetLogicalParent(parent) : parent.GetVisualParent();
            }
            return false;
        }

        return false;
    }

    private static bool MatchesSimple(Visual visual, string simpleSelector, bool useLogicalTree)
    {
        var parts = CssSelectorParser.SplitSimpleSelector(simpleSelector);
        foreach (var part in parts)
        {
            if (part.StartsWith('#'))
            {
                string id = part.Substring(1);
                if (visual is FrameworkElement ctrl)
                {
                    if (!string.Equals(ctrl.Name, id, StringComparison.OrdinalIgnoreCase))
                    {
                        return false;
                    }
                }
                else
                {
                    return false;
                }
            }
            else if (part.StartsWith('.'))
            {
                // WPF doesn't have classes. Style keys or type names can act as fallback.
                return false; 
            }
            else if (part.StartsWith('['))
            {
                if (!MatchesAttribute(visual, part)) return false;
            }
            else if (part.StartsWith(':'))
            {
                if (part.StartsWith(":contains(", StringComparison.OrdinalIgnoreCase))
                {
                    int start = part.IndexOf('(');
                    int end = part.LastIndexOf(')');
                    if (start >= 0 && end > start)
                    {
                        string text = part.Substring(start + 1, end - start - 1);
                        if (text.StartsWith('"') && text.EndsWith('"') && text.Length >= 2)
                        {
                            text = text.Substring(1, text.Length - 2);
                        }
                        if (!VisualContainsText(visual, text, false))
                        {
                            return false;
                        }
                    }
                }
                else if (part.Equals(":first-child", StringComparison.OrdinalIgnoreCase))
                {
                    var parent = useLogicalTree ? GetLogicalParent(visual) : visual.GetVisualParent();
                    if (parent == null) return false;
                    var children = useLogicalTree ? GetLogicalChildren(parent) : parent.GetVisualChildren();
                    if (children.FirstOrDefault() != visual) return false;
                }
                else if (part.Equals(":last-child", StringComparison.OrdinalIgnoreCase))
                {
                    var parent = useLogicalTree ? GetLogicalParent(visual) : visual.GetVisualParent();
                    if (parent == null) return false;
                    var children = useLogicalTree ? GetLogicalChildren(parent) : parent.GetVisualChildren();
                    if (children.LastOrDefault() != visual) return false;
                }
                else if (part.StartsWith(":nth-child(", StringComparison.OrdinalIgnoreCase))
                {
                    int start = part.IndexOf('(');
                    int end = part.LastIndexOf(')');
                    if (start >= 0 && end > start)
                    {
                        string indexStr = part.Substring(start + 1, end - start - 1);
                        if (int.TryParse(indexStr, out int index))
                        {
                            var parent = useLogicalTree ? GetLogicalParent(visual) : visual.GetVisualParent();
                            if (parent == null) return false;
                            var siblings = (useLogicalTree ? GetLogicalChildren(parent) : parent.GetVisualChildren()).ToList();
                            int actualIndex = siblings.IndexOf(visual) + 1;
                            if (actualIndex != index) return false;
                        }
                    }
                }
            }
            else
            {
                string tag = part;
                string visualTag = visual.GetType().Name;
                string visualFullTag = visual.GetType().FullName ?? "";

                if (!string.Equals(visualTag, tag, StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(visualFullTag, tag, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
            }
        }

        return true;
    }

    private static bool MatchesAttribute(Visual visual, string part)
    {
        int eqIndex = part.IndexOf('=');
        if (eqIndex < 0)
        {
            string attrName = part.Substring(1, part.Length - 2);
            if (attrName.Equals("id", StringComparison.OrdinalIgnoreCase) || attrName.Equals("Name", StringComparison.OrdinalIgnoreCase))
            {
                return visual is FrameworkElement ctrl && !string.IsNullOrEmpty(ctrl.Name);
            }
            if (attrName.Equals("AccessibilityId", StringComparison.OrdinalIgnoreCase) || attrName.Equals("AutomationId", StringComparison.OrdinalIgnoreCase))
            {
                return !string.IsNullOrEmpty(AutomationProperties.GetAutomationId(visual));
            }
            return false;
        }

        string key = part.Substring(1, eqIndex - 1).Trim();
        string val = part.Substring(eqIndex + 1, part.Length - eqIndex - 2).Trim();

        if (val.StartsWith('"') && val.EndsWith('"') && val.Length >= 2)
        {
            val = val.Substring(1, val.Length - 2);
        }

        if (key.Equals("id", StringComparison.OrdinalIgnoreCase) ||
            key.Equals("Name", StringComparison.OrdinalIgnoreCase) ||
            key.Equals("[id]", StringComparison.OrdinalIgnoreCase))
        {
            return visual is FrameworkElement ctrl && string.Equals(ctrl.Name, val, StringComparison.OrdinalIgnoreCase);
        }

        if (key.Equals("AccessibilityId", StringComparison.OrdinalIgnoreCase) ||
            key.Equals("AutomationId", StringComparison.OrdinalIgnoreCase) ||
            key.Equals("AutomationProperties.AutomationId", StringComparison.OrdinalIgnoreCase))
        {
            var accessId = AutomationProperties.GetAutomationId(visual);
            return string.Equals(accessId, val, StringComparison.OrdinalIgnoreCase);
        }

        // Custom properties via reflection
        var prop = visual.GetType().GetProperty(key, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
        if (prop != null)
        {
            try
            {
                var propVal = prop.GetValue(visual);
                if (propVal != null)
                {
                    return string.Equals(propVal.ToString(), val, StringComparison.OrdinalIgnoreCase);
                }
            }
            catch { }
        }

        return false;
    }

    public static string GetSelector(Visual visual, bool useLogicalTree = false, bool useAutomation = false)
    {
        var generator = SelectorRegistry.GetGenerator(useAutomation ? "automation" : "dom");
        return generator.GenerateSelector(visual, useLogicalTree);
    }

    internal static IEnumerable<Visual> GetLogicalChildren(Visual visual)
    {
        return CdpSession.GetLogicalVisualChildren(visual);
    }

    internal static Visual? GetLogicalParent(Visual visual)
    {
        var current = LogicalTreeHelper.GetParent(visual);
        while (current != null)
        {
            if (current is Visual v)
            {
                return v;
            }
            current = LogicalTreeHelper.GetParent(current);
        }
        return null;
    }
}
