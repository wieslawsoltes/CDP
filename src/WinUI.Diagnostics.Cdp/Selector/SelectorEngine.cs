using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace WinUI.Diagnostics.Cdp;

public static class VisualExtensions
{
    public static IEnumerable<UIElement> GetVisualChildren(this UIElement visual)
    {
        int count = VisualTreeHelper.GetChildrenCount(visual);
        for (int i = 0; i < count; i++)
        {
            if (VisualTreeHelper.GetChild(visual, i) is UIElement child)
            {
                yield return child;
            }
        }
    }

    public static UIElement? GetVisualParent(this UIElement visual)
    {
        return VisualTreeHelper.GetParent(visual) as UIElement;
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
                            if (type.IsClass && !type.IsAbstract && typeof(UIElement).IsAssignableFrom(type))
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
                                if (type != null && type.IsClass && !type.IsAbstract && typeof(UIElement).IsAssignableFrom(type))
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
                    }
                }
            }
            catch
            {
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

    private static bool VisualContainsText(UIElement visual, string text, bool recursive = true)
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
        if (visual is ContentPresenter contentPresenter)
        {
            if (contentPresenter.Content is string contentStr)
            {
                return contentStr.Contains(text, StringComparison.OrdinalIgnoreCase);
            }
        }

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
        }

        if (recursive)
        {
            foreach (var child in CdpVisualTreeHelper.GetChildren(visual, false))
            {
                if (VisualContainsText(child, text, true)) return true;
            }
        }

        return false;
    }

    public static string? GetVisualTextContent(UIElement visual)
    {
        if (visual is FrameworkElement fe)
        {
            var txt = Domains.DomDomain.GetControlTextOrContent(fe);
            if (!string.IsNullOrEmpty(txt)) return txt;
        }

        foreach (var child in CdpVisualTreeHelper.GetChildren(visual, false))
        {
            var txt = GetVisualTextContent(child);
            if (!string.IsNullOrEmpty(txt)) return txt;
        }

        return null;
    }

    public static UIElement? QuerySelector(UIElement root, string selector, bool useLogicalTree = false)
    {
        if (string.IsNullOrWhiteSpace(selector)) return null;
        var normalizedSelector = NormalizeSelector(selector);
        var result = QuerySelectorInternal(root, normalizedSelector, useLogicalTree);
        if (result != null) return result;

        if (CdpServer.GetWindows().Any(w => w.Window.Content == root))
        {
            foreach (var win in CdpServer.GetWindows().Select(x => x.Window))
            {
                if (win.Content != root && win.Content != null)
                {
                    result = QuerySelectorInternal(win.Content, normalizedSelector, useLogicalTree);
                    if (result != null) return result;
                }
            }
        }

        return null;
    }

    public static List<UIElement> QuerySelectorAll(UIElement root, string selector, bool useLogicalTree = false)
    {
        var results = new List<UIElement>();
        if (string.IsNullOrWhiteSpace(selector)) return results;
        var normalizedSelector = NormalizeSelector(selector);
        QuerySelectorAllInternal(root, normalizedSelector, results, useLogicalTree);

        if (results.Count == 0 && CdpServer.GetWindows().Any(w => w.Window.Content == root))
        {
            foreach (var win in CdpServer.GetWindows().Select(x => x.Window))
            {
                if (win.Content != root && win.Content != null)
                {
                    QuerySelectorAllInternal(win.Content, normalizedSelector, results, useLogicalTree);
                }
            }
        }

        return results;
    }

    private static UIElement? QuerySelectorInternal(UIElement current, string selector, bool useLogicalTree)
    {
        if (MatchesInternal(current, selector, useLogicalTree))
        {
            return current;
        }

        var children = CdpVisualTreeHelper.GetChildren(current, useLogicalTree);
        foreach (var child in children)
        {
            var result = QuerySelectorInternal(child, selector, useLogicalTree);
            if (result != null) return result;
        }

        return null;
    }

    private static void QuerySelectorAllInternal(UIElement current, string selector, List<UIElement> results, bool useLogicalTree)
    {
        if (MatchesInternal(current, selector, useLogicalTree))
        {
            results.Add(current);
        }

        var children = CdpVisualTreeHelper.GetChildren(current, useLogicalTree);
        foreach (var child in children)
        {
            QuerySelectorAllInternal(child, selector, results, useLogicalTree);
        }
    }

    public static bool Matches(UIElement visual, string selector, bool useLogicalTree = false)
    {
        if (string.IsNullOrWhiteSpace(selector)) return false;
        var normalizedSelector = NormalizeSelector(selector);
        return MatchesInternal(visual, normalizedSelector, useLogicalTree);
    }

    private static bool MatchesInternal(UIElement visual, string normalizedSelector, bool useLogicalTree)
    {
        var tokens = CssSelectorParser.TokenizeSelector(normalizedSelector);
        if (tokens.Count == 0) return false;

        return MatchesTokens(visual, tokens, tokens.Count - 1, useLogicalTree);
    }

    private static bool MatchesTokens(UIElement visual, List<string> tokens, int tokenIndex, bool useLogicalTree)
    {
        if (tokenIndex < 0) return true;

        var simpleSelector = tokens[tokenIndex];
        if (!MatchesSimple(visual, simpleSelector, useLogicalTree)) return false;

        if (tokenIndex == 0) return true;

        var combinator = tokens[tokenIndex - 1];

        if (combinator == ">")
        {
            var parent = CdpVisualTreeHelper.GetParent(visual, useLogicalTree);
            if (parent == null) return false;
            return MatchesTokens(parent, tokens, tokenIndex - 2, useLogicalTree);
        }
        else if (combinator == " ")
        {
            var parent = CdpVisualTreeHelper.GetParent(visual, useLogicalTree);
            while (parent != null)
            {
                if (MatchesTokens(parent, tokens, tokenIndex - 2, useLogicalTree))
                {
                    return true;
                }
                parent = CdpVisualTreeHelper.GetParent(parent, useLogicalTree);
            }
            return false;
        }

        return false;
    }

    private static bool MatchesSimple(UIElement visual, string simpleSelector, bool useLogicalTree)
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
                    var parent = CdpVisualTreeHelper.GetParent(visual, useLogicalTree);
                    if (parent == null) return false;
                    var children = CdpVisualTreeHelper.GetChildren(parent, useLogicalTree);
                    if (children.FirstOrDefault() != visual) return false;
                }
                else if (part.Equals(":last-child", StringComparison.OrdinalIgnoreCase))
                {
                    var parent = CdpVisualTreeHelper.GetParent(visual, useLogicalTree);
                    if (parent == null) return false;
                    var children = CdpVisualTreeHelper.GetChildren(parent, useLogicalTree);
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
                            var parent = CdpVisualTreeHelper.GetParent(visual, useLogicalTree);
                            if (parent == null) return false;
                            var siblings = CdpVisualTreeHelper.GetChildren(parent, useLogicalTree).ToList();
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
                    if (!tag.Contains('.'))
                    {
                        return false;
                    }
                }
            }
        }

        return true;
    }

    private static bool MatchesAttribute(UIElement visual, string part)
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

    public static string GetSelector(UIElement visual, bool useLogicalTree = false, bool useAutomation = false)
    {
        foreach (var gen in SelectorRegistry.Generators)
        {
            if (useAutomation && gen is AutomationSelectorGenerator)
            {
                var s = gen.Generate(visual);
                if (!string.IsNullOrEmpty(s)) return s;
            }
            else if (!useAutomation && gen is DomSelectorGenerator)
            {
                var s = gen.Generate(visual);
                if (!string.IsNullOrEmpty(s)) return s;
            }
        }

        if (visual is FrameworkElement fe && !string.IsNullOrEmpty(fe.Name))
        {
            return $"#{fe.Name}";
        }
        return visual.GetType().Name;
    }

    internal static IEnumerable<UIElement> GetLogicalChildren(UIElement visual)
    {
        return CdpSession.GetLogicalVisualChildren(visual);
    }

    internal static UIElement? GetLogicalParent(UIElement visual)
    {
        if (visual is FrameworkElement fe)
        {
            var parent = fe.Parent;
            while (parent != null)
            {
                if (parent is UIElement v) return v;
                parent = parent is FrameworkElement pfe ? pfe.Parent : null;
            }
        }
        return null;
    }
}
