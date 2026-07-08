using System;
using System.Collections.Generic;
using System.Linq;

namespace Chrome.DevTools.Protocol;

public enum AttributeSelectorOperator
{
    Exists,
    Equals,
    ContainsWord
}

public struct AttributeSelector
{
    public AttributeSelector(string name, AttributeSelectorOperator op, string? value)
    {
        Name = name;
        Operator = op;
        Value = value;
    }

    public string Name { get; }
    public AttributeSelectorOperator Operator { get; }
    public string? Value { get; }
}

public static class CssSelectorParser
{
    public static bool IsStandardCss(string selector, Func<string, bool> isKnownType)
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
        var parts = selector.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return false;

        foreach (var part in parts)
        {
            if (!isKnownType(part))
            {
                return false;
            }
        }

        return true;
    }

    public static string NormalizeSelector(string selector, Func<string, bool> isKnownType)
    {
        if (string.IsNullOrWhiteSpace(selector)) return selector;
        selector = selector.Trim();

        if (!IsStandardCss(selector, isKnownType))
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

    public static bool TryParseAttributeSelector(string attributeExpression, out AttributeSelector selector)
    {
        selector = default;
        var expression = attributeExpression.Trim();
        if (expression.Length == 0)
        {
            return false;
        }

        var op = AttributeSelectorOperator.Exists;
        var operatorIndex = expression.IndexOf("~=", StringComparison.Ordinal);
        var operatorLength = 2;
        if (operatorIndex >= 0)
        {
            op = AttributeSelectorOperator.ContainsWord;
        }
        else
        {
            operatorIndex = expression.IndexOf('=');
            operatorLength = 1;
            if (operatorIndex >= 0)
            {
                op = AttributeSelectorOperator.Equals;
            }
        }

        string name;
        string? value = null;
        if (operatorIndex >= 0)
        {
            name = expression.Substring(0, operatorIndex).Trim();
            value = expression.Substring(operatorIndex + operatorLength).Trim();
            if ((value.StartsWith("\"") && value.EndsWith("\"") && value.Length >= 2) ||
                (value.StartsWith("'") && value.EndsWith("'") && value.Length >= 2))
            {
                value = value.Substring(1, value.Length - 2);
            }
        }
        else
        {
            name = expression;
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        selector = new AttributeSelector(name, op, value);
        return true;
    }

    public static List<string> TokenizeSelector(string selector)
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

    public static List<string> SplitSimpleSelector(string selector)
    {
        var parts = new List<string>();
        if (string.IsNullOrEmpty(selector)) return parts;

        int i = 0;
        int start = 0;
        bool inBrackets = false;
        bool inQuotes = false;
        char quoteChar = '\0';

        while (i < selector.Length)
        {
            char c = selector[i];

            if (inQuotes)
            {
                if (c == quoteChar)
                {
                    inQuotes = false;
                }
                i++;
                continue;
            }

            if (c == '"' || c == '\'')
            {
                inQuotes = true;
                quoteChar = c;
                i++;
                continue;
            }

            if (inBrackets)
            {
                if (c == ']')
                {
                    inBrackets = false;
                    parts.Add(selector.Substring(start, i - start + 1));
                    start = i + 1;
                }
                i++;
                continue;
            }

            if (c == '[')
            {
                if (i > start)
                {
                    parts.Add(selector.Substring(start, i - start));
                }
                start = i;
                inBrackets = true;
                i++;
                continue;
            }

            if (c == '#' || c == '.' || c == ':')
            {
                if (i > start)
                {
                    parts.Add(selector.Substring(start, i - start));
                    start = i;
                }
            }

            i++;
        }

        if (i > start)
        {
            parts.Add(selector.Substring(start, i - start));
        }

        return parts;
    }
}
