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
                if (command.Equals("scroll", StringComparison.OrdinalIgnoreCase))
                {
                    return new List<string> { "down", "up", "left", "right" };
                }
                if (command.Equals("setAirplaneMode", StringComparison.OrdinalIgnoreCase))
                {
                    return new List<string> { "true", "false" };
                }
                if (command.Equals("delay", StringComparison.OrdinalIgnoreCase))
                {
                    return new List<string> { "500", "1000", "2000" };
                }
            }
            else
            {
                // Typing property values indented under a mapping (e.g. selector: , direction: )
                if (keyPart.Equals("selector", StringComparison.OrdinalIgnoreCase) ||
                    keyPart.Equals("targetSelector", StringComparison.OrdinalIgnoreCase))
                {
                    return GetLiveSelectors(mainVm);
                }
                if (keyPart.Equals("direction", StringComparison.OrdinalIgnoreCase))
                {
                    return new List<string> { "down", "up", "left", "right" };
                }
                if (keyPart.Equals("while", StringComparison.OrdinalIgnoreCase))
                {
                    return new List<string> { "assertTrue:", "assertVisible:", "assertNotVisible:" };
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
        var cmd = command.Trim().ToLowerInvariant();
        return cmd == "tapon"
            || cmd == "doubletapon"
            || cmd == "longpresson"
            || cmd == "cleartext"
            || cmd == "copytextfrom"
            || cmd == "assertvisible"
            || cmd == "assertnotvisible";
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
        return new List<string>
        {
            "launchApp",
            "tapOn:",
            "doubleTapOn:",
            "longPressOn:",
            "inputText:",
            "clearText:",
            "pasteText:",
            "eraseText:",
            "swipe:",
            "dragAndDrop:",
            "stopApp:",
            "killApp:",
            "clearState:",
            "setOrientation:",
            "setLocation:",
            "takeScreenshot:",
            "assertVisible:",
            "assertNotVisible:",
            "assertTrue:",
            "assertFalse:",
            "setAirplaneMode:",
            "delay:",
            "scroll:",
            "scrollUntilVisible:",
            "back",
            "pressKey:",
            "repeat:",
            "retry:",
            "runFlow:",
            "evalScript:",
            "runScript:",
            "openLink:",
            "copyTextFrom:"
        };
    }

    private static List<string> GetParametersForCommand(string command)
    {
        return command.ToLowerInvariant() switch
        {
            "inputtext" => new List<string> { "selector:", "text:" },
            "scroll" => new List<string> { "selector:", "direction:", "amount:" },
            "scrolluntilvisible" => new List<string> { "selector:", "direction:", "maxScrolls:" },
            "swipe" => new List<string> { "start:", "end:", "direction:" },
            "draganddrop" => new List<string> { "selector:", "targetSelector:", "offsetX:", "offsetY:", "targetOffsetX:", "targetOffsetY:" },
            "setlocation" => new List<string> { "latitude:", "longitude:" },
            "repeat" => new List<string> { "times:", "while:", "commands:" },
            "retry" => new List<string> { "maxRetries:", "commands:" },
            _ => new List<string>()
        };
    }

    private static List<string> GetLiveSelectors(MainWindowViewModel? mainVm)
    {
        var selectors = new HashSet<string>();

        // Fallback defaults
        selectors.Add("\"#btnTarget\"");
        selectors.Add("\"#txtTarget\"");
        selectors.Add("\"#btnClickMe\"");

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
                }
            }
            if (attr.Name.Equals("AccessibilityId", System.StringComparison.OrdinalIgnoreCase))
            {
                if (!string.IsNullOrEmpty(attr.Value))
                {
                    selectors.Add($"\"[AccessibilityId=\\\"{attr.Value}\\\"]\"");
                }
            }
        }
        foreach (var child in node.Children)
        {
            CollectSelectors(child, selectors);
        }
    }
}
