using System;
using System.Collections.Generic;
using System.Linq;

namespace Chrome.DevTools.Protocol;

public static class TestStudioStepConverter
{
    private static double SafeToDouble(object? value)
    {
        if (value == null) return 0;
        try 
        { 
            return Convert.ToDouble(value, System.Globalization.CultureInfo.InvariantCulture); 
        } 
        catch 
        { 
            return 0; 
        }
    }

    private static int SafeToInt(object? value)
    {
        if (value == null) return 0;
        try 
        { 
            return Convert.ToInt32(value, System.Globalization.CultureInfo.InvariantCulture); 
        } 
        catch 
        { 
            return 0; 
        }
    }

    private static readonly System.Text.RegularExpressions.Regex PlaceholderRegex = 
        new(@"\$\{([^}]+)\}", System.Text.RegularExpressions.RegexOptions.Compiled);

    private static string Interpolate(string? input, Dictionary<string, string> env)
    {
        if (string.IsNullOrEmpty(input)) return "";
        return PlaceholderRegex.Replace(input, match =>
        {
            var expression = match.Groups[1].Value.Trim();
            if (env.TryGetValue(expression, out var val))
            {
                return val ?? "";
            }
            var key = env.Keys.FirstOrDefault(k => string.Equals(k, expression, StringComparison.OrdinalIgnoreCase));
            if (key != null)
            {
                return env[key] ?? "";
            }

            var systemVal = Environment.GetEnvironmentVariable(expression);
            if (systemVal != null)
            {
                return systemVal;
            }

            try
            {
                var engine = new Jint.Engine();
                foreach (var kv in env)
                {
                    engine.SetValue(kv.Key, kv.Value);
                }
                var systemVars = Environment.GetEnvironmentVariables();
                foreach (System.Collections.DictionaryEntry entry in systemVars)
                {
                    string k = entry.Key?.ToString() ?? "";
                    if (!string.IsNullOrEmpty(k) && !env.ContainsKey(k))
                    {
                        engine.SetValue(k, entry.Value?.ToString() ?? "");
                    }
                }
                var result = engine.Evaluate(expression);
                return result.ToString();
            }
            catch
            {
                return match.Value;
            }
        });
    }

    public static RecordedStep ToRecordedStep(TestStudioStep step)
    {
        return ToRecordedStep(step, new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));
    }

    public static RecordedStep ToRecordedStep(TestStudioStep step, Dictionary<string, string> env)
    {
        var recStep = new RecordedStep();
        
        // Map Action to Type
        string action = step.Action;
        if (action.Equals("tapOn", StringComparison.OrdinalIgnoreCase)) 
            recStep.Type = "tap";
        else if (action.Equals("doubleTapOn", StringComparison.OrdinalIgnoreCase)) 
            recStep.Type = "doubleTap";
        else if (action.Equals("longPressOn", StringComparison.OrdinalIgnoreCase)) 
            recStep.Type = "longPress";
        else if (action.Equals("inputText", StringComparison.OrdinalIgnoreCase)) 
            recStep.Type = "inputText";
        else if (action.Equals("clearText", StringComparison.OrdinalIgnoreCase)) 
            recStep.Type = "clear";
        else if (action.Equals("pressKey", StringComparison.OrdinalIgnoreCase)) 
            recStep.Type = "pressKey";
        else if (action.Equals("openLink", StringComparison.OrdinalIgnoreCase))
            recStep.Type = "navigate";
        else 
            recStep.Type = action;

        // Map Selector
        recStep.Selector = Interpolate(step.Selector ?? "", env);

        // Map Value
        recStep.Value = Interpolate(step.Value ?? "", env);

        // Map parameters from step.Parameters if they exist
        if (step.Parameters != null)
        {
            if (step.Parameters.TryGetValue("selector", out var selObj) && selObj is string selStr)
            {
                recStep.Selector = Interpolate(selStr, env);
            }
            if (step.Parameters.TryGetValue("text", out var txtObj) && txtObj is string txtStr)
            {
                recStep.Value = Interpolate(txtStr, env);
            }
            else if (step.Parameters.TryGetValue("value", out var valObj) && valObj is string valStr)
            {
                recStep.Value = Interpolate(valStr, env);
            }

            // OffsetX, OffsetY
            if (step.Parameters.TryGetValue("offsetX", out var oxObj)) recStep.OffsetX = SafeToDouble(oxObj);
            if (step.Parameters.TryGetValue("offsetY", out var oyObj)) recStep.OffsetY = SafeToDouble(oyObj);

            // Width, Height
            if (step.Parameters.TryGetValue("width", out var wObj)) recStep.Width = SafeToDouble(wObj);
            if (step.Parameters.TryGetValue("height", out var hObj)) recStep.Height = SafeToDouble(hObj);

            // Url
            if (step.Parameters.TryGetValue("url", out var urlObj) && urlObj is string urlStr)
            {
                recStep.Url = Interpolate(urlStr, env);
            }
            else if (step.Parameters.TryGetValue("link", out var linkObj) && linkObj is string linkStr)
            {
                recStep.Url = Interpolate(linkStr, env);
            }

            // Key
            if (step.Parameters.TryGetValue("key", out var keyObj) && keyObj is string keyStr) recStep.Key = Interpolate(keyStr, env);

            // Button
            if (step.Parameters.TryGetValue("button", out var btnObj) && btnObj is string btnStr) recStep.Button = btnStr;

            // ClickCount
            if (step.Parameters.TryGetValue("clickCount", out var ccObj)) recStep.ClickCount = SafeToInt(ccObj);

            // Modifiers
            if (step.Parameters.TryGetValue("modifiers", out var modObj)) recStep.Modifiers = SafeToInt(modObj);

            // TargetSelector
            if (step.Parameters.TryGetValue("targetSelector", out var tsObj) && tsObj is string tsStr) recStep.TargetSelector = Interpolate(tsStr, env);

            // TargetOffsetX, TargetOffsetY
            if (step.Parameters.TryGetValue("targetOffsetX", out var toxObj)) recStep.TargetOffsetX = SafeToDouble(toxObj);
            if (step.Parameters.TryGetValue("targetOffsetY", out var toyObj)) recStep.TargetOffsetY = SafeToDouble(toyObj);
        }

        if (string.IsNullOrEmpty(recStep.Url) && action.Equals("openLink", StringComparison.OrdinalIgnoreCase))
        {
            recStep.Url = Interpolate(step.Value ?? "", env);
        }

        return recStep;
    }

    public static List<RecordedStep> ConvertYamlToRecordedSteps(string yaml)
    {
        return ConvertYamlToRecordedSteps(yaml, null);
    }

    public static List<RecordedStep> ConvertYamlToRecordedSteps(string yaml, Dictionary<string, string>? activeEnv)
    {
        var testStudioSteps = TestStudioYamlParser.Parse(yaml, out _, out _, out _, out var flowEnv);
        
        // Build combined environment: start with activeEnv, overlay flowEnv
        var combinedEnv = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (activeEnv != null)
        {
            foreach (var kv in activeEnv)
            {
                combinedEnv[kv.Key] = kv.Value;
            }
        }
        if (flowEnv != null)
        {
            foreach (var kv in flowEnv)
            {
                combinedEnv[kv.Key] = kv.Value;
            }
        }

        // Overlay system environment variables
        var sysEnv = Environment.GetEnvironmentVariables();
        foreach (System.Collections.DictionaryEntry entry in sysEnv)
        {
            string key = entry.Key?.ToString() ?? "";
            if (!string.IsNullOrEmpty(key) && !combinedEnv.ContainsKey(key))
            {
                combinedEnv[key] = entry.Value?.ToString() ?? "";
            }
        }

        return testStudioSteps.Select(step => ToRecordedStep(step, combinedEnv)).ToList();
    }
}
