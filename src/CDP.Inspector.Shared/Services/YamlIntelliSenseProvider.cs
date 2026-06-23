using System;
using System.Collections.Generic;
using System.Linq;
using CdpInspectorApp.Models;
using CdpInspectorApp.ViewModels;

namespace CdpInspectorApp.Services;

public static class YamlIntelliSenseProvider
{
    public static List<string> GetSuggestions(string yamlText, int caretOffset, MainWindowViewModel? mainVm)
    {
        var suggestions = new List<string>();
        if (string.IsNullOrEmpty(yamlText))
        {
            return GetCommands();
        }

        // 1. Get text up to caret
        string textUpToCaret = yamlText.Substring(0, Math.Clamp(caretOffset, 0, yamlText.Length));
        string[] lines = textUpToCaret.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
        if (lines.Length == 0)
        {
            return GetCommands();
        }

        string currentLine = lines[^1];
        int currentLineIndentation = currentLine.TakeWhile(char.IsWhiteSpace).Count();

        // 2. Detect context
        // Check if there is a colon in the current line (typing a value/argument)
        int colonIndex = currentLine.IndexOf(':');
        if (colonIndex >= 0)
        {
            string keyPart = currentLine.Substring(0, colonIndex).Trim();
            
            // If the key starts with '-' (e.g. "- tapOn" or "- scroll")
            if (keyPart.StartsWith("-"))
            {
                string command = keyPart.Substring(1).Trim();
                if (IsSelectorCommand(command))
                {
                    return GetLiveSelectors(mainVm);
                }
                var inlineValues = FlowCommandCatalog.GetValueCompletions(command, command);
                if (inlineValues.Count > 0)
                {
                    return inlineValues.ToList();
                }
            }
            else
            {
                // Typing property values indented under a mapping (e.g. selector: , direction: )
                if (FlowCommandCatalog.IsSelectorKey(keyPart) ||
                    keyPart.Equals("targetSelector", StringComparison.OrdinalIgnoreCase))
                {
                    return GetLiveSelectors(mainVm);
                }
                if (keyPart.Equals("while", StringComparison.OrdinalIgnoreCase))
                {
                    return new List<string> { "visible:", "notVisible:", "assertTrue:", "assertFalse:", "assertVisible:", "assertNotVisible:" };
                }

                string parentCommand = FindParentCommand(lines, currentLineIndentation);
                var valueSuggestions = FlowCommandCatalog.GetValueCompletions(parentCommand, keyPart);
                if (valueSuggestions.Count > 0)
                {
                    return valueSuggestions.ToList();
                }
            }

            // After a colon, return empty if no specific rule matched (do not show commands)
            return suggestions;
        }

        // Case A: User is typing a command list item (e.g. line starts with '-' or is empty at sequence level)
        if (currentLine.TrimStart().StartsWith("-"))
        {
            return GetCommands();
        }

        // Case B: Indented under a parent command (suggest command parameters)
        if (currentLineIndentation > 0)
        {
            string parentCommand = FindParentCommand(lines, currentLineIndentation);
            if (!string.IsNullOrEmpty(parentCommand))
            {
                return GetParametersForCommand(parentCommand);
            }
        }

        // Default: suggest root level keys and commands
        var list = new List<string> { "appId:", "description:" };
        list.AddRange(GetCommands());
        return list;
    }

    private static bool IsSelectorCommand(string command)
    {
        return FlowCommandCatalog.IsSelectorCommand(command);
    }

    private static string FindParentCommand(string[] lines, int currentLineIndentation)
    {
        for (int i = lines.Length - 2; i >= 0; i--)
        {
            string line = lines[i];
            if (string.IsNullOrWhiteSpace(line)) continue;

            int indent = line.TakeWhile(char.IsWhiteSpace).Count();
            if (indent < currentLineIndentation)
            {
                string trimmed = line.Trim();
                if (trimmed.StartsWith("-"))
                {
                    string actionPart = trimmed.Substring(1).Trim();
                    int colonIdx = actionPart.IndexOf(':');
                    return colonIdx >= 0 ? actionPart.Substring(0, colonIdx).Trim() : actionPart;
                }
                else
                {
                    int colonIdx = trimmed.IndexOf(':');
                    if (colonIdx >= 0)
                    {
                        return trimmed.Substring(0, colonIdx).Trim();
                    }
                }
            }
        }
        return "";
    }

    public static bool IsCommand(string suggestion)
    {
        return GetCommands().Contains(suggestion);
    }

    public static int GetProperIndentation(string yamlText, int caretOffset)
    {
        string textUpToCaret = yamlText.Substring(0, Math.Clamp(caretOffset, 0, yamlText.Length));
        string[] lines = textUpToCaret.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
        if (lines.Length <= 1)
        {
            return 0;
        }

        // Search upwards from the current line (lines.Length - 2)
        for (int i = lines.Length - 2; i >= 0; i--)
        {
            string line = lines[i];
            if (string.IsNullOrWhiteSpace(line)) continue;

            int lineIndent = line.TakeWhile(char.IsWhiteSpace).Count();
            string trimmed = line.Trim();

            if (trimmed == "---")
            {
                return 0;
            }

            // If we find a sequence parent line (like commands:) before any sequence item
            if (trimmed.EndsWith(":") && trimmed.Substring(0, trimmed.Length - 1).Trim() == "commands")
            {
                return lineIndent + 2;
            }

            if (trimmed.StartsWith("-"))
            {
                return lineIndent;
            }
        }

        return 0;
    }

    private static List<string> GetCommands()
    {
        return FlowCommandCatalog.GetCommandCompletions().ToList();
    }

    private static List<string> GetParametersForCommand(string command)
    {
        var suggestions = FlowCommandCatalog.GetParameterCompletions(command).ToList();
        if (FlowCommandCatalog.IsSelectorCommand(command))
        {
            suggestions.AddRange(FlowCommandCatalog.SelectorKeys.Select(key => $"{key}:"));
        }

        return suggestions.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static List<string> GetLiveSelectors(MainWindowViewModel? mainVm)
    {
        var selectors = new HashSet<string>();

        // Fallback defaults
        selectors.Add("\"#btnTarget\"");
        selectors.Add("\"#txtTarget\"");
        selectors.Add("\"#btnClickMe\"");
        selectors.Add("text: \"Visible text\"");
        selectors.Add("id: \"automation_id\"");
        selectors.Add("css: \"#controlName\"");
        selectors.Add("point: \"50%, 50%\"");
        selectors.Add("enabled: true");
        selectors.Add("checked: true");
        selectors.Add("focused: true");
        selectors.Add("selected: true");
        selectors.Add("traits: text");
        selectors.Add("traits: long-text");
        selectors.Add("traits: square");
        selectors.Add("width: 48");
        selectors.Add("height: 48");
        selectors.Add("tolerance: 2");

        if (mainVm?.Elements != null)
        {
            foreach (var node in mainVm.Elements.RootNodes)
            {
                CollectSelectors(node, selectors);
            }
        }

        return selectors.OrderBy(s => s).ToList();
    }

    private static void CollectSelectors(DomNodeModel node, HashSet<string> selectors)
    {
        foreach (var attr in node.AttributesList)
        {
            if (attr.Name.Equals("id", System.StringComparison.OrdinalIgnoreCase) ||
                attr.Name.Equals("Name", System.StringComparison.OrdinalIgnoreCase) ||
                attr.Name.Equals("AutomationId", System.StringComparison.OrdinalIgnoreCase))
            {
                if (!string.IsNullOrEmpty(attr.Value))
                {
                    selectors.Add($"\"#{attr.Value}\"");
                    selectors.Add($"id: \"{attr.Value}\"");
                }
            }
            if (attr.Name.Equals("AccessibilityId", System.StringComparison.OrdinalIgnoreCase))
            {
                if (!string.IsNullOrEmpty(attr.Value))
                {
                    selectors.Add($"\"[AccessibilityId=\\\"{attr.Value}\\\"]\"");
                    selectors.Add($"id: \"{attr.Value}\"");
                }
            }
            if (attr.Name.Equals("text", System.StringComparison.OrdinalIgnoreCase) ||
                attr.Name.Equals("Text", System.StringComparison.OrdinalIgnoreCase))
            {
                if (!string.IsNullOrEmpty(attr.Value))
                {
                    selectors.Add($"text: \"{attr.Value}\"");
                    selectors.Add($"\"{attr.Value}\"");
                }
            }
        }
        foreach (var child in node.Children)
        {
            CollectSelectors(child, selectors);
        }
    }
}
