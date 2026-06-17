using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;

namespace Avalonia.Diagnostics.Cdp;

public static class SelectorEngine
{
    public static Visual? QuerySelector(Visual root, string selector)
    {
        if (string.IsNullOrWhiteSpace(selector)) return null;
        return QuerySelectorInternal(root, selector.Trim());
    }

    public static List<Visual> QuerySelectorAll(Visual root, string selector)
    {
        var results = new List<Visual>();
        if (string.IsNullOrWhiteSpace(selector)) return results;
        QuerySelectorAllInternal(root, selector.Trim(), results);
        return results;
    }

    private static Visual? QuerySelectorInternal(Visual current, string selector)
    {
        if (Matches(current, selector))
        {
            return current;
        }

        foreach (var child in current.GetVisualChildren())
        {
            var result = QuerySelectorInternal(child, selector);
            if (result != null) return result;
        }

        return null;
    }

    private static void QuerySelectorAllInternal(Visual current, string selector, List<Visual> results)
    {
        if (Matches(current, selector))
        {
            results.Add(current);
        }

        foreach (var child in current.GetVisualChildren())
        {
            QuerySelectorAllInternal(child, selector, results);
        }
    }

    public static bool Matches(Visual visual, string selector)
    {
        if (string.IsNullOrWhiteSpace(selector)) return false;

        var tokens = TokenizeSelector(selector);
        if (tokens.Count == 0) return false;

        return MatchesTokens(visual, tokens, tokens.Count - 1);
    }

    private static List<string> TokenizeSelector(string selector)
    {
        var tokens = new List<string>();
        var currentToken = "";
        
        for (int i = 0; i < selector.Length; i++)
        {
            char c = selector[i];
            if (c == '>')
            {
                if (!string.IsNullOrWhiteSpace(currentToken))
                {
                    tokens.Add(currentToken.Trim());
                    currentToken = "";
                }
                tokens.Add(">");
            }
            else if (char.IsWhiteSpace(c))
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

    private static bool MatchesTokens(Visual visual, List<string> tokens, int tokenIndex)
    {
        if (tokenIndex < 0) return true;

        var simpleSelector = tokens[tokenIndex];
        if (!MatchesSimple(visual, simpleSelector)) return false;

        if (tokenIndex == 0) return true;

        var combinator = tokens[tokenIndex - 1];

        if (combinator == ">")
        {
            var parent = visual.GetVisualParent();
            if (parent == null) return false;
            return MatchesTokens(parent, tokens, tokenIndex - 2);
        }
        else if (combinator == " ")
        {
            var parent = visual.GetVisualParent();
            while (parent != null)
            {
                if (MatchesTokens(parent, tokens, tokenIndex - 2))
                {
                    return true;
                }
                parent = parent.GetVisualParent();
            }
            return false;
        }

        return false;
    }

    private static bool MatchesSimple(Visual visual, string selector)
    {
        if (selector == "*") return true;

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
            if (!typeMatches) return false;
        }

        // Match ID (Control Name)
        if (id != null)
        {
            if (visual is Control control)
            {
                if (control.Name != id) return false;
            }
            else
            {
                return false;
            }
        }

        // Match Classes
        if (classes.Count > 0)
        {
            if (visual is Control control)
            {
                foreach (var cls in classes)
                {
                    if (!control.Classes.Contains(cls)) return false;
                }
            }
            else
            {
                return false;
            }
        }

        return true;
    }

    public static string GetSelector(Visual visual)
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
            current = current.GetVisualParent();
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
            current = current.GetVisualParent();
        }

        return string.Join(" > ", parts);
    }
}
