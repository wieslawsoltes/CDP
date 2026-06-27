using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Chrome.DevTools.Protocol;

public enum FlowCommandValueKind
{
    None,
    String,
    Selector,
    Map,
    List
}

public sealed record FlowCommandDefinition(
    string Name,
    string DisplayName,
    string Category,
    FlowCommandValueKind ValueKind,
    string Description,
    IReadOnlyList<string>? Parameters = null,
    IReadOnlyList<string>? ValueSuggestions = null,
    bool AcceptsSelector = false);

public static class FlowCommandCatalog
{
    public static readonly IReadOnlyList<string> SelectorKeys = new[]
    {
        "text", "id", "index", "point", "css",
        "above", "below", "leftOf", "rightOf",
        "containsChild", "childOf", "containsDescendants",
        "traits", "enabled", "checked", "focused", "selected",
        "width", "height", "tolerance"
    };

    public static readonly IReadOnlyList<string> DirectionSuggestions = new[] { "DOWN", "UP", "LEFT", "RIGHT", "down", "up", "left", "right" };
    public static readonly IReadOnlyList<string> BooleanSuggestions = new[] { "true", "false" };
    public static readonly IReadOnlyList<string> TraitSuggestions = new[] { "text", "long-text", "square" };
    public static readonly IReadOnlyList<string> KeySuggestions = new[] { "home", "lock", "enter", "backspace", "volume up", "volume down", "back", "escape", "tab", "space", "delete", "home", "end" };

    private static readonly IReadOnlyList<string> SelectorParameters = SelectorKeys.Select(key => $"{key}:").ToArray();
    private static readonly IReadOnlyList<string> TapParameters = SelectorKeys.Concat(new[] { "repeat", "delay", "retryTapIfNoChange", "waitToSettleTimeoutMs" }).Select(key => $"{key}:").ToArray();

    private static readonly FlowCommandDefinition[] s_commands =
    {
        new("addMedia", "Add Media", "Media", FlowCommandValueKind.List, "Add one or more media files to the target gallery.", new[] { "items" }),
        new("assertNoDefectsWithAI", "Assert No Defects With AI", "AI", FlowCommandValueKind.Map, "Run an AI visual-defect assertion.", new[] { "optional" }, new[] { "true", "false" }),
        new("assertNotVisible", "Assert Not Visible", "Assertions", FlowCommandValueKind.Selector, "Assert that an element is not visible.", SelectorParameters, AcceptsSelector: true),
        new("assertScreenshot", "Assert Screenshot", "Assertions", FlowCommandValueKind.Map, "Compare the current screenshot with a reference image.", new[] { "path", "cropOn", "thresholdPercentage", "label" }),
        new("assertTrue", "Assert True", "Assertions", FlowCommandValueKind.String, "Assert that an expression evaluates to true."),
        new("assertVisible", "Assert Visible", "Assertions", FlowCommandValueKind.Selector, "Assert that an element is visible.", SelectorParameters, AcceptsSelector: true),
        new("assertWithAI", "Assert With AI", "AI", FlowCommandValueKind.Map, "Run an AI visual assertion.", new[] { "assertion", "optional" }, new[] { "true", "false" }),
        new("back", "Back", "Navigation", FlowCommandValueKind.None, "Navigate back."),
        new("clearKeychain", "Clear Keychain", "App & Device", FlowCommandValueKind.None, "Clear iOS keychain data."),
        new("clearState", "Clear State", "App & Device", FlowCommandValueKind.Map, "Clear app or web origin state.", new[] { "appId", "label" }),
        new("copyTextFrom", "Copy Text From", "Input", FlowCommandValueKind.Selector, "Copy text from an element.", SelectorParameters, AcceptsSelector: true),
        new("doubleTapOn", "Double Tap On", "Interactions", FlowCommandValueKind.Selector, "Double tap an element or point.", TapParameters, new[] { "100", "200", "500" }, AcceptsSelector: true),
        new("eraseText", "Erase Text", "Input", FlowCommandValueKind.String, "Erase text from the focused field.", new[] { "characters" }, new[] { "10", "50", "100" }),
        new("evalScript", "Eval Script", "Scripting", FlowCommandValueKind.String, "Evaluate an inline script expression."),
        new("extendedWaitUntil", "Extended Wait Until", "Assertions", FlowCommandValueKind.Map, "Wait for visible or not-visible state with a custom timeout.", new[] { "visible", "notVisible", "timeout" }),
        new("extractTextWithAI", "Extract Text With AI", "AI", FlowCommandValueKind.Map, "Extract text from the screen with AI.", new[] { "query", "outputVariable", "optional" }, new[] { "true", "false" }),
        new("hideKeyboard", "Hide Keyboard", "Input", FlowCommandValueKind.None, "Dismiss the software keyboard."),
        new("inputText", "Input Text", "Input", FlowCommandValueKind.String, "Input text into the focused element.", new[] { "text", "selector", "value" }),
        new("killApp", "Kill App", "App & Device", FlowCommandValueKind.String, "Kill the app under test or another app id."),
        new("launchApp", "Launch App", "App & Device", FlowCommandValueKind.Map, "Launch an app with optional state and permissions.", new[] { "appId", "clearState", "clearKeychain", "permissions", "stopApp", "arguments" }, new[] { "true", "false" }),
        new("longPressOn", "Long Press On", "Interactions", FlowCommandValueKind.Selector, "Long press an element or point.", TapParameters, AcceptsSelector: true),
        new("openLink", "Open Link", "Navigation", FlowCommandValueKind.Map, "Open a URL or deep link.", new[] { "link", "autoVerify" }, new[] { "true", "false" }),
        new("pasteText", "Paste Text", "Input", FlowCommandValueKind.None, "Paste clipboard text into the focused element."),
        new("pressKey", "Press Key", "Input", FlowCommandValueKind.String, "Press a hardware or keyboard key.", new[] { "key", "selector", "value" }, KeySuggestions),
        new("repeat", "Repeat", "Logic", FlowCommandValueKind.Map, "Repeat nested commands.", new[] { "times", "while", "commands" }),
        new("retry", "Retry", "Logic", FlowCommandValueKind.Map, "Retry nested commands on failure.", new[] { "maxRetries", "commands", "file" }),
        new("runFlow", "Run Flow", "Logic", FlowCommandValueKind.Map, "Run a subflow file or inline command list.", new[] { "file", "label", "env", "commands", "when" }),
        new("runScript", "Run Script", "Scripting", FlowCommandValueKind.Map, "Run an external script file.", new[] { "file", "env" }),
        new("scroll", "Scroll", "Interactions", FlowCommandValueKind.None, "Scroll the current view.", new[] { "direction", "amount", "selector" }, DirectionSuggestions),
        new("scrollUntilVisible", "Scroll Until Visible", "Interactions", FlowCommandValueKind.Map, "Scroll until a target element becomes visible.", new[] { "element", "direction", "timeout", "speed", "visibilityPercentage", "centerElement" }, DirectionSuggestions, AcceptsSelector: true),
        new("setAirplaneMode", "Set Airplane Mode", "App & Device", FlowCommandValueKind.String, "Enable or disable airplane mode.", new[] { "enabled" }, BooleanSuggestions),
        new("setClipboard", "Set Clipboard", "Input", FlowCommandValueKind.String, "Set clipboard text."),
        new("setLocation", "Set Location", "App & Device", FlowCommandValueKind.Map, "Set mock geolocation.", new[] { "latitude", "longitude" }),
        new("setOrientation", "Set Orientation", "App & Device", FlowCommandValueKind.String, "Set device orientation.", new[] { "PORTRAIT", "LANDSCAPE_LEFT", "LANDSCAPE_RIGHT", "UPSIDE_DOWN" }, new[] { "PORTRAIT", "LANDSCAPE_LEFT", "LANDSCAPE_RIGHT", "UPSIDE_DOWN" }),
        new("setPermissions", "Set Permissions", "App & Device", FlowCommandValueKind.Map, "Grant or deny app permissions.", new[] { "permissions", "appId", "all" }, new[] { "allow", "deny" }),
        new("startRecording", "Start Recording", "Media", FlowCommandValueKind.Map, "Start device-screen recording.", new[] { "path", "label", "optional" }),
        new("stopApp", "Stop App", "App & Device", FlowCommandValueKind.String, "Stop an app."),
        new("stopRecording", "Stop Recording", "Media", FlowCommandValueKind.None, "Stop screen recording."),
        new("swipe", "Swipe", "Interactions", FlowCommandValueKind.Map, "Swipe by direction, coordinates, or element.", new[] { "start", "end", "direction", "from", "duration", "waitToSettleTimeoutMs" }, DirectionSuggestions),
        new("takeScreenshot", "Take Screenshot", "Media", FlowCommandValueKind.Map, "Capture a screenshot.", new[] { "path", "cropOn", "label" }),
        new("tapOn", "Tap On", "Interactions", FlowCommandValueKind.Selector, "Tap an element or point.", TapParameters, AcceptsSelector: true),
        new("toggleAirplaneMode", "Toggle Airplane Mode", "App & Device", FlowCommandValueKind.None, "Toggle airplane mode."),
        new("travel", "Travel", "App & Device", FlowCommandValueKind.Map, "Simulate a route between locations.", new[] { "points", "speedMps" }),
        new("waitForAnimationToEnd", "Wait For Animation To End", "Assertions", FlowCommandValueKind.Map, "Wait for the UI to settle.", new[] { "timeout" }, new[] { "15000", "30000" }),
        new("inputRandomEmail", "Input Random Email", "Input", FlowCommandValueKind.None, "Input a random email address."),
        new("inputRandomPersonName", "Input Random Person Name", "Input", FlowCommandValueKind.None, "Input a random person name."),
        new("inputRandomNumber", "Input Random Number", "Input", FlowCommandValueKind.Map, "Input a random number.", new[] { "length" }, new[] { "8", "9", "11" }),
        new("inputRandomText", "Input Random Text", "Input", FlowCommandValueKind.Map, "Input random text.", new[] { "length" }, new[] { "8", "11", "20" }),
        new("inputRandomCityName", "Input Random City Name", "Input", FlowCommandValueKind.None, "Input a random city name."),
        new("inputRandomCountryName", "Input Random Country Name", "Input", FlowCommandValueKind.None, "Input a random country name."),
        new("inputRandomColorName", "Input Random Color Name", "Input", FlowCommandValueKind.None, "Input a random color name."),
        new("clearText", "Clear Text", "Input", FlowCommandValueKind.Selector, "Legacy alias for clearing text in this inspector.", SelectorParameters, AcceptsSelector: true),
        new("delay", "Delay", "Timing", FlowCommandValueKind.String, "Legacy delay in milliseconds.", new[] { "ms" }, new[] { "500", "1000", "2000" }),
        new("assertFalse", "Assert False", "Assertions", FlowCommandValueKind.String, "Legacy assertion that an expression evaluates to false.")
    };

    private static readonly Dictionary<string, FlowCommandDefinition> s_byName = s_commands.ToDictionary(c => c.Name, StringComparer.OrdinalIgnoreCase);

    private static readonly Dictionary<string, string> s_aliases = new(StringComparer.OrdinalIgnoreCase)
    {
        { "doubleTap", "doubleTapOn" },
        { "longPress", "longPressOn" },
        { "input", "inputText" },
        { "clear", "clearText" },
        { "paste", "pasteText" },
        { "erase", "eraseText" },
        { "copy", "copyTextFrom" },
        { "tap", "tapOn" }
    };

    public static IReadOnlyList<FlowCommandDefinition> Commands => s_commands;
    public static IReadOnlyList<FlowCommandDefinition> PublicCommands => s_commands.Where(c => c.Name is not "clearText" and not "delay" and not "assertFalse").ToArray();

    public static bool IsKnownCommand(string action) => s_byName.ContainsKey(action);

    public static string CanonicalizeAction(string action)
    {
        if (string.IsNullOrWhiteSpace(action))
        {
            return "";
        }

        var trimmed = action.Trim();
        if (s_aliases.TryGetValue(trimmed, out var canonical))
        {
            trimmed = canonical;
        }

        return s_byName.TryGetValue(trimmed, out var command) ? command.Name : trimmed;
    }

    public static FlowCommandDefinition? Find(string action)
    {
        return s_byName.TryGetValue(action, out var command) ? command : null;
    }

    public static string GetDisplayName(string action)
    {
        if (s_byName.TryGetValue(action, out var command))
        {
            return command.DisplayName;
        }

        if (string.IsNullOrEmpty(action))
        {
            return "Step";
        }

        var sb = new StringBuilder();
        sb.Append(char.ToUpperInvariant(action[0]));
        for (var i = 1; i < action.Length; i++)
        {
            var ch = action[i];
            if (char.IsUpper(ch) && action[i - 1] != ' ')
            {
                sb.Append(' ');
            }
            sb.Append(ch);
        }
        return sb.ToString();
    }

    public static IReadOnlyList<string> GetCommandCompletions()
    {
        return PublicCommands.Select(c => c.ValueKind == FlowCommandValueKind.None ? c.Name : $"{c.Name}:").ToArray();
    }

    public static IReadOnlyList<string> GetParameterCompletions(string action)
    {
        var command = Find(action);
        return command?.Parameters?.ToArray() ?? Array.Empty<string>();
    }

    public static IReadOnlyList<string> GetValueCompletions(string action, string parameterName)
    {
        parameterName = parameterName.TrimEnd(':');
        if (parameterName.Equals("direction", StringComparison.OrdinalIgnoreCase))
        {
            return DirectionSuggestions;
        }
        if (parameterName.Equals("enabled", StringComparison.OrdinalIgnoreCase) ||
            parameterName.Equals("checked", StringComparison.OrdinalIgnoreCase) ||
            parameterName.Equals("focused", StringComparison.OrdinalIgnoreCase) ||
            parameterName.Equals("selected", StringComparison.OrdinalIgnoreCase) ||
            parameterName.Equals("optional", StringComparison.OrdinalIgnoreCase) ||
            parameterName.Equals("centerElement", StringComparison.OrdinalIgnoreCase) ||
            parameterName.Equals("clearState", StringComparison.OrdinalIgnoreCase) ||
            parameterName.Equals("clearKeychain", StringComparison.OrdinalIgnoreCase) ||
            parameterName.Equals("stopApp", StringComparison.OrdinalIgnoreCase) ||
            parameterName.Equals("autoVerify", StringComparison.OrdinalIgnoreCase))
        {
            return BooleanSuggestions;
        }
        if (parameterName.Equals("traits", StringComparison.OrdinalIgnoreCase))
        {
            return TraitSuggestions;
        }
        if (parameterName.Equals("orientation", StringComparison.OrdinalIgnoreCase) ||
            action.Equals("setOrientation", StringComparison.OrdinalIgnoreCase))
        {
            return new[] { "PORTRAIT", "LANDSCAPE_LEFT", "LANDSCAPE_RIGHT", "UPSIDE_DOWN" };
        }
        if (parameterName.Equals("key", StringComparison.OrdinalIgnoreCase) ||
            action.Equals("pressKey", StringComparison.OrdinalIgnoreCase))
        {
            return KeySuggestions;
        }
        if (parameterName.Equals("all", StringComparison.OrdinalIgnoreCase) ||
            parameterName.Equals("permissions", StringComparison.OrdinalIgnoreCase))
        {
            return new[] { "allow", "deny" };
        }

        return Find(action)?.ValueSuggestions?.ToArray() ?? Array.Empty<string>();
    }

    public static bool IsSelectorCommand(string action)
    {
        var command = Find(action);
        return command?.AcceptsSelector == true;
    }

    public static bool IsSelectorKey(string key)
    {
        return SelectorKeys.Contains(key.TrimEnd(':'), StringComparer.OrdinalIgnoreCase) ||
               key.Equals("selector", StringComparison.OrdinalIgnoreCase) ||
               key.Equals("element", StringComparison.OrdinalIgnoreCase) ||
               key.Equals("cropOn", StringComparison.OrdinalIgnoreCase) ||
               key.Equals("from", StringComparison.OrdinalIgnoreCase);
    }

    public static string BuildSelectorDisplay(IReadOnlyDictionary<string, object?> parameters)
    {
        if (parameters.TryGetValue("selector", out var selector) && selector != null)
        {
            return ScalarToString(selector);
        }

        if (parameters.TryGetValue("element", out var element) && element != null)
        {
            return element is IReadOnlyDictionary<string, object?> nested
                ? BuildSelectorDisplay(nested)
                : ScalarToString(element);
        }

        var parts = new List<string>();
        foreach (var key in SelectorKeys)
        {
            if (!parameters.TryGetValue(key, out var value) || value == null)
            {
                continue;
            }

            if (value is IReadOnlyDictionary<string, object?> nested)
            {
                parts.Add($"{key}: {{{BuildSelectorDisplay(nested)}}}");
            }
            else if (value is IReadOnlyList<object?> list)
            {
                parts.Add($"{key}: [{list.Count}]");
            }
            else
            {
                parts.Add($"{key}: {ScalarToString(value)}");
            }
        }

        return string.Join(", ", parts);
    }

    public static string BuildValueDisplay(IReadOnlyDictionary<string, object?> parameters)
    {
        var parts = new List<string>();
        foreach (var kv in parameters)
        {
            if (IsSelectorKey(kv.Key) || kv.Key.Equals("commands", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            parts.Add($"{kv.Key}: {DisplayValue(kv.Value)}");
        }

        return string.Join(", ", parts);
    }

    public static string BuildRuntimeSelector(IReadOnlyDictionary<string, object?> parameters, string? fallbackSelector)
    {
        if (parameters.TryGetValue("selector", out var explicitSelector) && explicitSelector != null)
        {
            return ScalarToString(explicitSelector);
        }

        if (parameters.TryGetValue("element", out var element) && element != null)
        {
            if (element is IReadOnlyDictionary<string, object?> nested)
            {
                return BuildRuntimeSelector(nested, fallbackSelector);
            }
            return ScalarToString(element);
        }

        if (parameters.TryGetValue("css", out var css) && css != null)
        {
            return ScalarToString(css);
        }

        var selector = "";
        if (parameters.TryGetValue("id", out var id) && id != null)
        {
            var escaped = EscapeSelectorValue(ScalarToString(id));
            selector = $"[AccessibilityId=\"{escaped}\"]";
        }
        else if (parameters.TryGetValue("text", out var text) && text != null)
        {
            var escaped = EscapeSelectorValue(ScalarToString(text));
            selector = $":contains(\"{escaped}\")";
        }
        else if (!string.IsNullOrWhiteSpace(fallbackSelector))
        {
            selector = fallbackSelector!;
        }

        var attributeParts = new List<string>();
        AddBooleanAttribute(parameters, attributeParts, "enabled", "IsEnabled");
        AddBooleanAttribute(parameters, attributeParts, "focused", "IsFocused");
        AddBooleanAttribute(parameters, attributeParts, "checked", "IsChecked");
        AddBooleanAttribute(parameters, attributeParts, "selected", "IsSelected");
        AddNumericAttribute(parameters, attributeParts, "width", "Width");
        AddNumericAttribute(parameters, attributeParts, "height", "Height");
        if (parameters.TryGetValue("traits", out var traits) && traits != null)
        {
            attributeParts.Add($"[Traits~=\"{EscapeSelectorValue(ScalarToString(traits))}\"]");
        }

        if (attributeParts.Count > 0)
        {
            selector = string.IsNullOrWhiteSpace(selector) ? "*" : selector;
            selector += string.Concat(attributeParts);
        }

        return selector;
    }

    public static string ScalarToString(object? value)
    {
        return value switch
        {
            null => "",
            string str => str,
            bool b => b.ToString().ToLowerInvariant(),
            IReadOnlyDictionary<string, object?> map => BuildValueDisplay(map),
            IReadOnlyList<object?> list => string.Join(", ", list.Select(ScalarToString)),
            _ => Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? ""
        };
    }

    private static string DisplayValue(object? value)
    {
        return value switch
        {
            null => "",
            IReadOnlyDictionary<string, object?> map => "{" + string.Join(", ", map.Select(kv => $"{kv.Key}: {DisplayValue(kv.Value)}")) + "}",
            IReadOnlyList<object?> list => "[" + string.Join(", ", list.Select(DisplayValue)) + "]",
            _ => ScalarToString(value)
        };
    }

    private static void AddBooleanAttribute(IReadOnlyDictionary<string, object?> parameters, List<string> attributeParts, string parameterName, string attributeName)
    {
        if (parameters.TryGetValue(parameterName, out var value) && value != null)
        {
            attributeParts.Add($"[{attributeName}=\"{ScalarToString(value).ToLowerInvariant()}\"]");
        }
    }

    private static void AddNumericAttribute(IReadOnlyDictionary<string, object?> parameters, List<string> attributeParts, string parameterName, string attributeName)
    {
        if (parameters.TryGetValue(parameterName, out var value) && value != null)
        {
            attributeParts.Add($"[{attributeName}=\"{ScalarToString(value)}\"]");
        }
    }

    private static string EscapeSelectorValue(string value)
    {
        return value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal);
    }
}
