using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.VisualTree;
using Avalonia.LogicalTree;

namespace Avalonia.Diagnostics.Cdp;

public static class SelectorEngine
{
    private static readonly HashSet<string> s_knownVisualTypes = new(StringComparer.OrdinalIgnoreCase);
    private static readonly object s_lock = new();
    private static bool s_initializedTypes = false;

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
        if (string.IsNullOrWhiteSpace(selector)) return false;

        // Quoted strings are not standard CSS (they are text literals)
        if (selector.Length >= 2)
        {
            if ((selector.StartsWith("\"") && selector.EndsWith("\"")) ||
                (selector.StartsWith("'") && selector.EndsWith("'")))
            {
                return false;
            }
        }

        // If it contains typical CSS characters, it is standard CSS
        if (selector.Contains('#') ||
            selector.Contains('.') ||
            selector.Contains('>') ||
            selector.Contains(':') ||
            selector.Contains('[') ||
            selector.Contains(']') ||
            selector.Contains('*'))
        {
            return true;
        }

        // It has no typical CSS characters.
        // We check if all parts are known visual type names.
        EnsureInitializedTypes();
        var parts = selector.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return false;

        foreach (var part in parts)
        {
            if (!s_knownVisualTypes.Contains(part))
            {
                return false;
            }
        }

        return true;
    }

    private static string NormalizeSelector(string selector)
    {
        if (string.IsNullOrWhiteSpace(selector)) return selector;
        selector = selector.Trim();

        if (!IsStandardCss(selector))
        {
            // If it is quoted, strip the quotes first
            if (selector.Length >= 2)
            {
                if ((selector.StartsWith("\"") && selector.EndsWith("\"")) ||
                    (selector.StartsWith("'") && selector.EndsWith("'")))
                {
                    selector = selector.Substring(1, selector.Length - 2);
                }
            }
            return $":contains(\"{selector}\")";
        }

        return selector;
    }

    private static bool VisualContainsText(Visual visual, string text)
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

        return false;
    }

    public static Visual? QuerySelector(Visual root, string selector, bool useLogicalTree = false)
    {
        if (string.IsNullOrWhiteSpace(selector)) return null;
        var normalizedSelector = NormalizeSelector(selector);
        return QuerySelectorInternal(root, normalizedSelector, useLogicalTree);
    }

    public static List<Visual> QuerySelectorAll(Visual root, string selector, bool useLogicalTree = false)
    {
        var results = new List<Visual>();
        if (string.IsNullOrWhiteSpace(selector)) return results;
        var normalizedSelector = NormalizeSelector(selector);
        QuerySelectorAllInternal(root, normalizedSelector, results, useLogicalTree);
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
        var tokens = TokenizeSelector(normalizedSelector);
        if (tokens.Count == 0) return false;

        return MatchesTokens(visual, tokens, tokens.Count - 1, useLogicalTree);
    }

    private static List<string> TokenizeSelector(string selector)
    {
        var tokens = new List<string>();
        var currentToken = "";
        bool inDoubleQuotes = false;
        bool inSingleQuotes = false;
        int parenDepth = 0;
        
        for (int i = 0; i < selector.Length; i++)
        {
            char c = selector[i];

            if (c == '"' && !inSingleQuotes)
            {
                inDoubleQuotes = !inDoubleQuotes;
                currentToken += c;
            }
            else if (c == '\'' && !inDoubleQuotes)
            {
                inSingleQuotes = !inSingleQuotes;
                currentToken += c;
            }
            else if (c == '(' && !inDoubleQuotes && !inSingleQuotes)
            {
                parenDepth++;
                currentToken += c;
            }
            else if (c == ')' && !inDoubleQuotes && !inSingleQuotes)
            {
                if (parenDepth > 0) parenDepth--;
                currentToken += c;
            }
            else if (c == '>' && !inDoubleQuotes && !inSingleQuotes && parenDepth == 0)
            {
                if (!string.IsNullOrWhiteSpace(currentToken))
                {
                    tokens.Add(currentToken.Trim());
                    currentToken = "";
                }
                tokens.Add(">");
            }
            else if (char.IsWhiteSpace(c) && !inDoubleQuotes && !inSingleQuotes && parenDepth == 0)
            {
                if (!string.IsNullOrWhiteSpace(currentToken))
                {
                    tokens.Add(currentToken.Trim());
                    currentToken = "";
                }
                if (tokens.Count > 0 && tokens[^1] != ">" && tokens[^1] != " ")
                {
                    tokens.Add(" ");
                }
            }
            else
            {
                currentToken += c;
            }
        }
        
        if (!string.IsNullOrWhiteSpace(currentToken))
        {
            tokens.Add(currentToken.Trim());
        }
        
        if (tokens.Count > 0 && tokens[^1] == " ")
        {
            tokens.RemoveAt(tokens.Count - 1);
        }
        
        for (int i = 0; i < tokens.Count - 1; i++)
        {
            if (tokens[i] == " " && tokens[i + 1] == ">")
            {
                tokens.RemoveAt(i);
                i--;
            }
            else if (tokens[i] == ">" && tokens[i + 1] == " ")
            {
                tokens.RemoveAt(i + 1);
                i--;
            }
        }
        
        return tokens;
    }

    private static bool MatchesTokens(Visual visual, List<string> tokens, int tokenIndex, bool useLogicalTree)
    {
        if (tokenIndex < 0) return true;

        var simpleSelector = tokens[tokenIndex];
        if (!MatchesSimple(visual, simpleSelector)) return false;

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

    private static bool MatchesSimple(Visual visual, string selector)
    {
        if (selector == "*") return true;

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

        if (string.IsNullOrWhiteSpace(selector))
        {
            selector = "*";
        }

        bool baseMatch = true;
        if (selector != "*")
        {
            string type = "";
            string? id = null;
            var classes = new List<string>();

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

        if (!baseMatch) return false;

        // Check containsText conditions
        foreach (var containsText in containsTexts)
        {
            if (!VisualContainsText(visual, containsText))
            {
                return false;
            }
        }

        return true;
    }

    public static string GetSelector(Visual visual, bool useLogicalTree = false)
    {
        if (visual is Control c && !string.IsNullOrEmpty(c.Name) && !c.Name.StartsWith("PART_"))
        {
            return $"#{c.Name}";
        }

        // Walk up to find if there is any named ancestor (ignoring PART_ names)
        Visual? current = visual;
        while (current != null)
        {
            if (current is Control ctrl && !string.IsNullOrEmpty(ctrl.Name) && !ctrl.Name.StartsWith("PART_"))
            {
                return $"#{ctrl.Name}";
            }
            current = useLogicalTree ? GetLogicalParent(current) : current.GetVisualParent();
        }

        // Fallback to structural path if no named ancestor is found
        var parts = new List<string>();
        current = visual;
        while (current != null)
        {
            string part = current.GetType().Name;
            if (current is Control ctrlWithClasses)
            {
                var validClasses = ctrlWithClasses.Classes.Where(cls => !cls.StartsWith(":")).ToList();
                if (validClasses.Count > 0)
                {
                    part += "." + string.Join(".", validClasses);
                }
            }

            parts.Insert(0, part);
            current = useLogicalTree ? GetLogicalParent(current) : current.GetVisualParent();
        }

        return string.Join(" > ", parts);
    }

    private static IEnumerable<Visual> GetLogicalChildren(Visual visual)
    {
        if (visual is ILogical logical)
        {
            return CdpSession.GetLogicalVisualChildren(logical);
        }
        return Enumerable.Empty<Visual>();
    }

    private static Visual? GetLogicalParent(Visual visual)
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
