using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.VisualTree;
using Avalonia.LogicalTree;
using Avalonia.Automation;

namespace Avalonia.Diagnostics.Cdp;

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
                        // Ignore other assembly loading issues
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

        // Try casting first for common types to be fast/non-allocating
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
        if (visual is Avalonia.Controls.Presenters.ContentPresenter contentPresenter)
        {
            if (contentPresenter.Content is string contentStr)
            {
                return contentStr.Contains(text, StringComparison.OrdinalIgnoreCase);
            }
        }

        // Reflection fallback for other properties (like custom controls, or properties like Text/Content/Header)
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
        if (visual is Control ctrl)
        {
            var txt = Domains.DomDomain.GetControlTextOrContent(ctrl);
            if (!string.IsNullOrEmpty(txt)) return txt;
        }

        foreach (var child in visual.GetVisualChildren())
        {
            var txt = GetVisualTextContent(child);
            if (!string.IsNullOrEmpty(txt)) return txt;
        }

        return null;
    }

    private static bool TryGetVisualText(Visual visual, out string value)
    {
        value = "";

        if (visual is TextBlock textBlock)
        {
            value = textBlock.Text ?? "";
            return true;
        }

        if (visual is TextBox textBox)
        {
            value = textBox.Text ?? "";
            return true;
        }

        if (visual is HeaderedContentControl headeredContentControl)
        {
            value = headeredContentControl.Header?.ToString() ?? "";
            return true;
        }

        if (visual is HeaderedItemsControl headeredItemsControl)
        {
            value = headeredItemsControl.Header?.ToString() ?? "";
            return true;
        }

        if (visual is ContentControl contentControl)
        {
            value = contentControl.Content?.ToString() ?? "";
            return true;
        }

        if (visual is Avalonia.Controls.Presenters.ContentPresenter contentPresenter)
        {
            value = contentPresenter.Content?.ToString() ?? "";
            return true;
        }

        return false;
    }

    private static bool TryGetAttributeValue(Visual visual, string attributeName, out string value)
    {
        value = "";
        var normalizedName = attributeName.Trim();

        if (normalizedName.Equals("Type", StringComparison.OrdinalIgnoreCase) ||
            normalizedName.Equals("nodeName", StringComparison.OrdinalIgnoreCase) ||
            normalizedName.Equals("localName", StringComparison.OrdinalIgnoreCase))
        {
            value = visual.GetType().Name;
            return true;
        }

        if (normalizedName.Equals("FullType", StringComparison.OrdinalIgnoreCase))
        {
            value = visual.GetType().FullName ?? visual.GetType().Name;
            return true;
        }

        if (normalizedName.Equals("Text", StringComparison.OrdinalIgnoreCase) ||
            normalizedName.Equals("Content", StringComparison.OrdinalIgnoreCase) ||
            normalizedName.Equals("Header", StringComparison.OrdinalIgnoreCase))
        {
            return TryGetVisualText(visual, out value);
        }

        if (visual is not Control control)
        {
            return false;
        }

        if (normalizedName.Equals("id", StringComparison.OrdinalIgnoreCase) ||
            normalizedName.Equals("Id", StringComparison.OrdinalIgnoreCase) ||
            normalizedName.Equals("Name", StringComparison.OrdinalIgnoreCase))
        {
            value = control.Name ?? "";
            return !string.IsNullOrEmpty(value);
        }

        if (normalizedName.Equals("class", StringComparison.OrdinalIgnoreCase) ||
            normalizedName.Equals("Class", StringComparison.OrdinalIgnoreCase))
        {
            value = string.Join(" ", control.Classes.Where(cls => !cls.StartsWith(":", StringComparison.Ordinal)));
            return !string.IsNullOrEmpty(value);
        }

        if (normalizedName.Equals("AccessibilityId", StringComparison.OrdinalIgnoreCase) ||
            normalizedName.Equals("AutomationId", StringComparison.OrdinalIgnoreCase) ||
            normalizedName.Equals("AutomationProperties.AutomationId", StringComparison.OrdinalIgnoreCase) ||
            normalizedName.Equals("automation-id", StringComparison.OrdinalIgnoreCase))
        {
            value = control.GetValue(AutomationProperties.AutomationIdProperty) as string ?? "";
            return !string.IsNullOrEmpty(value);
        }

        if (normalizedName.Equals("AccessibilityName", StringComparison.OrdinalIgnoreCase) ||
            normalizedName.Equals("AutomationName", StringComparison.OrdinalIgnoreCase))
        {
            value = control.GetValue(AutomationProperties.NameProperty) ?? "";
            return !string.IsNullOrEmpty(value);
        }

        if (normalizedName.Equals("AccessibilityHelp", StringComparison.OrdinalIgnoreCase) ||
            normalizedName.Equals("HelpText", StringComparison.OrdinalIgnoreCase))
        {
            value = control.GetValue(AutomationProperties.HelpTextProperty) ?? "";
            return !string.IsNullOrEmpty(value);
        }

        if (normalizedName.Equals("IsEnabled", StringComparison.OrdinalIgnoreCase) ||
            normalizedName.Equals("enabled", StringComparison.OrdinalIgnoreCase))
        {
            value = control.IsEnabled.ToString().ToLowerInvariant();
            return true;
        }

        if (normalizedName.Equals("IsVisible", StringComparison.OrdinalIgnoreCase) ||
            normalizedName.Equals("visible", StringComparison.OrdinalIgnoreCase))
        {
            value = control.IsVisible.ToString().ToLowerInvariant();
            return true;
        }

        if (normalizedName.Equals("IsFocused", StringComparison.OrdinalIgnoreCase) ||
            normalizedName.Equals("focused", StringComparison.OrdinalIgnoreCase))
        {
            value = control.IsFocused.ToString().ToLowerInvariant();
            return true;
        }

        if (normalizedName.Equals("IsChecked", StringComparison.OrdinalIgnoreCase) ||
            normalizedName.Equals("checked", StringComparison.OrdinalIgnoreCase))
        {
            if (control is ToggleButton toggleButton)
            {
                value = (toggleButton.IsChecked == true).ToString().ToLowerInvariant();
                return true;
            }

            var prop = control.GetType().GetProperty("IsChecked", BindingFlags.Public | BindingFlags.Instance);
            if (prop != null)
            {
                var val = prop.GetValue(control);
                value = (val != null && (val.ToString().Equals("True", StringComparison.OrdinalIgnoreCase) || val.ToString().Equals("true", StringComparison.OrdinalIgnoreCase))).ToString().ToLowerInvariant();
                return true;
            }

            return false;
        }

        if (normalizedName.Equals("IsSelected", StringComparison.OrdinalIgnoreCase) ||
            normalizedName.Equals("selected", StringComparison.OrdinalIgnoreCase))
        {
            var selectedProperty = control.GetType().GetProperty("IsSelected", BindingFlags.Public | BindingFlags.Instance);
            if (selectedProperty?.PropertyType == typeof(bool))
            {
                value = ((bool)(selectedProperty.GetValue(control) ?? false)).ToString().ToLowerInvariant();
                return true;
            }

            return false;
        }

        if (normalizedName.Equals("Value", StringComparison.OrdinalIgnoreCase))
        {
            var valueProperty = control.GetType().GetProperty("Value", BindingFlags.Public | BindingFlags.Instance);
            if (valueProperty != null)
            {
                value = valueProperty.GetValue(control)?.ToString() ?? "";
                return true;
            }
            return false;
        }

        if (normalizedName.Equals("SelectedIndex", StringComparison.OrdinalIgnoreCase))
        {
            var prop = control.GetType().GetProperty("SelectedIndex", BindingFlags.Public | BindingFlags.Instance);
            if (prop != null)
            {
                value = prop.GetValue(control)?.ToString() ?? "";
                return true;
            }
            return false;
        }

        if (normalizedName.Equals("IsExpanded", StringComparison.OrdinalIgnoreCase))
        {
            var prop = control.GetType().GetProperty("IsExpanded", BindingFlags.Public | BindingFlags.Instance);
            if (prop != null)
            {
                value = prop.GetValue(control)?.ToString().ToLowerInvariant() ?? "false";
                return true;
            }
            return false;
        }

        if (normalizedName.Equals("SelectedDate", StringComparison.OrdinalIgnoreCase))
        {
            var prop = control.GetType().GetProperty("SelectedDate", BindingFlags.Public | BindingFlags.Instance);
            if (prop != null)
            {
                value = prop.GetValue(control)?.ToString() ?? "";
                return true;
            }
            return false;
        }

        if (normalizedName.Equals("SelectedTime", StringComparison.OrdinalIgnoreCase))
        {
            var prop = control.GetType().GetProperty("SelectedTime", BindingFlags.Public | BindingFlags.Instance);
            if (prop != null)
            {
                value = prop.GetValue(control)?.ToString() ?? "";
                return true;
            }
            return false;
        }

        if (normalizedName.Equals("PlaceholderText", StringComparison.OrdinalIgnoreCase))
        {
            var prop = control.GetType().GetProperty("PlaceholderText", BindingFlags.Public | BindingFlags.Instance);
            if (prop != null)
            {
                value = prop.GetValue(control)?.ToString() ?? "";
                return true;
            }
            return false;
        }

        if (normalizedName.Equals("Width", StringComparison.OrdinalIgnoreCase))
        {
            value = Math.Round(control.Bounds.Width).ToString(System.Globalization.CultureInfo.InvariantCulture);
            return true;
        }

        if (normalizedName.Equals("Height", StringComparison.OrdinalIgnoreCase))
        {
            value = Math.Round(control.Bounds.Height).ToString(System.Globalization.CultureInfo.InvariantCulture);
            return true;
        }

        if (normalizedName.Equals("Traits", StringComparison.OrdinalIgnoreCase) ||
            normalizedName.Equals("traits", StringComparison.OrdinalIgnoreCase))
        {
            var traits = new List<string>();
            if (TryGetVisualText(visual, out var text) && !string.IsNullOrWhiteSpace(text))
            {
                traits.Add("text");
                if (text.Length >= 200)
                {
                    traits.Add("long-text");
                }
            }

            var width = control.Bounds.Width;
            var height = control.Bounds.Height;
            if (width > 0 && height > 0)
            {
                var larger = Math.Max(width, height);
                if (Math.Abs(width - height) / larger <= 0.03)
                {
                    traits.Add("square");
                }
            }

            value = string.Join(" ", traits);
            return traits.Count > 0;
        }

        if (normalizedName.Equals("Bounds", StringComparison.OrdinalIgnoreCase))
        {
            value = $"{control.Bounds.X},{control.Bounds.Y},{control.Bounds.Width},{control.Bounds.Height}";
            return true;
        }

        return false;
    }



    public static Visual? QuerySelector(Visual root, string selector, bool useLogicalTree = false)
    {
        if (string.IsNullOrWhiteSpace(selector)) return null;
        var normalizedSelector = NormalizeSelector(selector);
        var result = QuerySelectorInternal(root, normalizedSelector, useLogicalTree);
        if (result != null) return result;

        // Fallback: search other registered windows/TopLevels only if query root is a TopLevel window
        if (root is TopLevel)
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

        // Fallback: search other registered windows/TopLevels if no matches found in root and root is a TopLevel window
        if (results.Count == 0 && root is TopLevel)
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

    private static bool MatchesSimple(Visual visual, string selector, bool useLogicalTree = false)
    {
        if (selector == "*") return true;

        int nthChildIndex = selector.IndexOf(":nth-child(", StringComparison.OrdinalIgnoreCase);
        if (nthChildIndex >= 0)
        {
            int start = nthChildIndex + ":nth-child(".Length;
            int end = selector.IndexOf(')', start);
            if (end >= 0)
            {
                string indexStr = selector.Substring(start, end - start).Trim();
                if (int.TryParse(indexStr, out int targetIndex))
                {
                    var parent = useLogicalTree ? GetLogicalParent(visual) : visual.GetVisualParent();
                    int actualIndex = 1;
                    if (parent != null)
                    {
                        var siblings = (useLogicalTree ? GetLogicalChildren(parent) : parent.GetVisualChildren()).ToList();
                        actualIndex = siblings.IndexOf(visual) + 1;
                    }
                    if (actualIndex != targetIndex)
                    {
                        return false;
                    }
                }
            }
            string before = selector.Substring(0, nthChildIndex);
            string after = (end >= 0 && end + 1 < selector.Length) ? selector.Substring(end + 1) : "";
            selector = before + after;
        }

        var containsTexts = new List<string>();
        while (true)
        {
            int containsIndex = selector.IndexOf(":contains(", StringComparison.OrdinalIgnoreCase);
            if (containsIndex < 0) break;

            int start = containsIndex + ":contains(".Length;
            int end = selector.IndexOf(')', start);
            string containsText;
            if (end >= 0)
            {
                containsText = selector.Substring(start, end - start);
            }
            else
            {
                containsText = selector.Substring(start);
            }

            // Clean quotes if present
            if (containsText.StartsWith("\"") && containsText.EndsWith("\"") && containsText.Length >= 2)
            {
                containsText = containsText.Substring(1, containsText.Length - 2);
            }
            else if (containsText.StartsWith("'") && containsText.EndsWith("'") && containsText.Length >= 2)
            {
                containsText = containsText.Substring(1, containsText.Length - 2);
            }

            containsTexts.Add(containsText);

            // Remove the :contains(...) part from the selector
            string before = selector.Substring(0, containsIndex);
            string after = (end >= 0 && end + 1 < selector.Length) ? selector.Substring(end + 1) : "";
            selector = before + after;
        }

        // Match attribute selectors like [id], [Name="value"], [AccessibilityId="value"], [class~="primary"].
        var attrMatches = new List<AttributeSelector>();
        while (true)
        {
            int startIdx = selector.IndexOf('[');
            if (startIdx < 0) break;
            int endIdx = selector.IndexOf(']', startIdx);
            if (endIdx < 0) break;

            string attrExpr = selector.Substring(startIdx + 1, endIdx - startIdx - 1);
            if (CssSelectorParser.TryParseAttributeSelector(attrExpr, out var attrSelector))
            {
                attrMatches.Add(attrSelector);
            }
            else
            {
                return false;
            }

            selector = selector.Substring(0, startIdx) + ((endIdx + 1 < selector.Length) ? selector.Substring(endIdx + 1) : "");
        }

        if (string.IsNullOrWhiteSpace(selector))
        {
            selector = "*";
        }

        bool baseMatch = true;
        string type = "";
        string? id = null;
        var classes = new List<string>();

        if (selector != "*")
        {
            int i = 0;
            // Parse type
            while (i < selector.Length && selector[i] != '#' && selector[i] != '.')
            {
                type += selector[i];
                i++;
            }

            while (i < selector.Length)
            {
                if (selector[i] == '#')
                {
                    i++;
                    string idVal = "";
                    while (i < selector.Length && selector[i] != '#' && selector[i] != '.')
                    {
                        idVal += selector[i];
                        i++;
                    }
                    id = idVal;
                }
                else if (selector[i] == '.')
                {
                    i++;
                    string classVal = "";
                    while (i < selector.Length && selector[i] != '#' && selector[i] != '.')
                    {
                        classVal += selector[i];
                        i++;
                    }
                    classes.Add(classVal);
                }
                else
                {
                    i++;
                }
            }

            // Match type
            if (!string.IsNullOrEmpty(type))
            {
                var visualType = visual.GetType();
                bool typeMatches = visualType.Name.Equals(type, StringComparison.OrdinalIgnoreCase) ||
                                   (visualType.FullName != null && visualType.FullName.Equals(type, StringComparison.OrdinalIgnoreCase));
                if (!typeMatches) baseMatch = false;
            }

            // Match ID (Control Name)
            if (baseMatch && id != null)
            {
                if (visual is Control control)
                {
                    if (control.Name != id) baseMatch = false;
                }
                else
                {
                    baseMatch = false;
                }
            }

            // Match Classes
            if (baseMatch && classes.Count > 0)
            {
                if (visual is Control control)
                {
                    foreach (var cls in classes)
                    {
                        if (!control.Classes.Contains(cls))
                        {
                            baseMatch = false;
                            break;
                        }
                    }
                }
                else
                {
                    baseMatch = false;
                }
            }
        }

        // Match Attributes
        if (baseMatch && attrMatches.Count > 0)
        {
            foreach (var attr in attrMatches)
            {
                if (!TryGetAttributeValue(visual, attr.Name, out var attrValue))
                {
                    baseMatch = false;
                    break;
                }

                if (attr.Operator == AttributeSelectorOperator.Exists)
                {
                    continue;
                }

                var expected = attr.Value ?? "";
                if (attr.Operator == AttributeSelectorOperator.Equals &&
                    !string.Equals(attrValue, expected, StringComparison.Ordinal))
                {
                    baseMatch = false;
                    break;
                }

                if (attr.Operator == AttributeSelectorOperator.ContainsWord)
                {
                    var words = attrValue.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    if (!words.Contains(expected, StringComparer.Ordinal))
                    {
                        baseMatch = false;
                        break;
                    }
                }
            }
        }

        if (!baseMatch) return false;

        // Check containsText conditions
        bool hasOtherFilters = (selector != "*" && !string.IsNullOrWhiteSpace(selector)) ||
                               id != null ||
                               classes.Count > 0 ||
                               attrMatches.Count > 0;
        bool recursive = hasOtherFilters;

        foreach (var containsText in containsTexts)
        {
            if (!VisualContainsText(visual, containsText, recursive))
            {
                return false;
            }
        }

        return true;
    }

    public static string GetSelector(Visual visual, bool useLogicalTree = false, bool useAutomation = false)
    {
        var generator = SelectorRegistry.GetGenerator(useAutomation ? "automation" : "dom");
        return generator.GenerateSelector(visual, useLogicalTree);
    }

    internal static IEnumerable<Visual> GetLogicalChildren(Visual visual)
    {
        if (visual is ILogical logical)
        {
            return CdpSession.GetLogicalVisualChildren(logical);
        }
        return Enumerable.Empty<Visual>();
    }

    internal static Visual? GetLogicalParent(Visual visual)
    {
        var current = (visual as ILogical)?.LogicalParent;
        while (current != null)
        {
            if (current is Visual v && 
                (v is not StyledElement se || se.TemplatedParent == null) &&
                (v.GetVisualParent() is not Avalonia.Controls.Presenters.ContentPresenter cp || cp.Content == v))
            {
                return v;
            }
            current = current.LogicalParent;
        }
        return null;
    }
}
