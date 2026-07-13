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

    public static List<RecordedStep> ConvertYamlToRecordedSteps(string yaml, string filePath, Dictionary<string, string>? activeEnv)
    {
        var testStudioSteps = TestStudioYamlParser.Parse(yaml, out _, out _, out _, out var flowEnv);
        
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (!string.IsNullOrEmpty(filePath))
        {
            visited.Add(System.IO.Path.GetFullPath(filePath));
        }

        var combinedEnv = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (activeEnv != null)
        {
            foreach (var kv in activeEnv) combinedEnv[kv.Key] = kv.Value;
        }
        if (flowEnv != null)
        {
            foreach (var kv in flowEnv) combinedEnv[kv.Key] = kv.Value;
        }

        var sysEnv = Environment.GetEnvironmentVariables();
        foreach (System.Collections.DictionaryEntry entry in sysEnv)
        {
            string key = entry.Key?.ToString() ?? "";
            if (!string.IsNullOrEmpty(key) && !combinedEnv.ContainsKey(key))
            {
                combinedEnv[key] = entry.Value?.ToString() ?? "";
            }
        }

        var expandedSteps = ExpandSteps(testStudioSteps, filePath, visited, combinedEnv);

        return expandedSteps.Select(step => ToRecordedStep(step, combinedEnv)).ToList();
    }

    private static TestStudioStep InterpolateStep(TestStudioStep step, Dictionary<string, string> env)
    {
        var cloned = new TestStudioStep
        {
            Action = step.Action,
            Selector = Interpolate(step.Selector, env),
            Value = Interpolate(step.Value, env),
            StartLine = step.StartLine,
            EndLine = step.EndLine,
            WhileConditionType = step.WhileConditionType,
            WhileConditionValue = Interpolate(step.WhileConditionValue, env)
        };

        if (step.Parameters != null)
        {
            cloned.Parameters = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            foreach (var kv in step.Parameters)
            {
                if (kv.Value is string strVal)
                {
                    cloned.Parameters[kv.Key] = Interpolate(strVal, env);
                }
                else
                {
                    cloned.Parameters[kv.Key] = kv.Value;
                }
            }
        }

        if (step.NestedSteps != null)
        {
            cloned.NestedSteps = step.NestedSteps.Select(s => InterpolateStep(s, env)).ToList();
        }

        return cloned;
    }

    private static List<TestStudioStep> ExpandSteps(List<TestStudioStep> steps, string currentFilePath, HashSet<string> visitedFiles, Dictionary<string, string> currentEnv)
    {
        var expanded = new List<TestStudioStep>();
        foreach (var step in steps)
        {
            if (step.Action == "runFlow")
            {
                if (step.NestedSteps != null && step.NestedSteps.Count > 0)
                {
                    expanded.AddRange(ExpandSteps(step.NestedSteps, currentFilePath, visitedFiles, currentEnv));
                    continue;
                }

                string? flowPath = null;
                if (step.Parameters != null && step.Parameters.TryGetValue("file", out var fObj) && fObj is string fStr)
                {
                    flowPath = fStr;
                }
                if (string.IsNullOrEmpty(flowPath))
                {
                    flowPath = step.Value?.Trim();
                }

                if (string.IsNullOrEmpty(flowPath)) continue;

                string resolvedPath = ResolveFlowPath(flowPath, currentFilePath);
                if (string.IsNullOrEmpty(resolvedPath) || !System.IO.File.Exists(resolvedPath))
                {
                    expanded.Add(InterpolateStep(step, currentEnv));
                    continue;
                }

                if (visitedFiles.Contains(resolvedPath))
                {
                    throw new InvalidOperationException($"Circular dependency detected: {resolvedPath}");
                }

                visitedFiles.Add(resolvedPath);
                try
                {
                    string subYaml = System.IO.File.ReadAllText(resolvedPath);
                    var subSteps = TestStudioYamlParser.Parse(subYaml, out _, out _, out _, out var subFlowEnv);

                    var mergedEnv = new Dictionary<string, string>(currentEnv, StringComparer.OrdinalIgnoreCase);
                    if (step.Parameters != null)
                    {
                        foreach (var kv in step.Parameters)
                        {
                            if (kv.Value != null)
                            {
                                mergedEnv[kv.Key] = Interpolate(kv.Value.ToString() ?? "", currentEnv);
                            }
                        }
                    }
                    if (subFlowEnv != null)
                    {
                        foreach (var kv in subFlowEnv)
                        {
                            mergedEnv[kv.Key] = kv.Value;
                        }
                    }

                    expanded.AddRange(ExpandSteps(subSteps, resolvedPath, visitedFiles, mergedEnv));
                }
                finally
                {
                    visitedFiles.Remove(resolvedPath);
                }
            }
            else if ((step.Action == "repeat" || step.Action == "retry") && step.NestedSteps != null && step.NestedSteps.Count > 0)
            {
                var clonedStep = new TestStudioStep
                {
                    Action = step.Action,
                    Selector = Interpolate(step.Selector, currentEnv),
                    Value = Interpolate(step.Value, currentEnv),
                    StartLine = step.StartLine,
                    EndLine = step.EndLine,
                    WhileConditionType = step.WhileConditionType,
                    WhileConditionValue = Interpolate(step.WhileConditionValue, currentEnv),
                    Parameters = step.Parameters != null ? new Dictionary<string, object>(step.Parameters, StringComparer.OrdinalIgnoreCase) : null,
                    NestedSteps = ExpandSteps(step.NestedSteps, currentFilePath, visitedFiles, currentEnv)
                };
                expanded.Add(clonedStep);
            }
            else
            {
                expanded.Add(InterpolateStep(step, currentEnv));
            }
        }
        return expanded;
    }

    private static string ResolveFlowPath(string flowPath, string currentFilePath)
    {
        if (string.IsNullOrEmpty(flowPath)) return "";
        string normalizedFlowPath = flowPath.Replace('\\', System.IO.Path.DirectorySeparatorChar).Replace('/', System.IO.Path.DirectorySeparatorChar);

        // 1. Relative to currentFilePath
        if (!string.IsNullOrEmpty(currentFilePath))
        {
            var dir = System.IO.Path.GetDirectoryName(currentFilePath);
            if (!string.IsNullOrEmpty(dir))
            {
                var relativePath = System.IO.Path.Combine(dir, normalizedFlowPath);
                if (System.IO.File.Exists(relativePath)) return System.IO.Path.GetFullPath(relativePath);
            }
        }

        // 2. Climb up parent directories of currentFilePath
        if (!string.IsNullOrEmpty(currentFilePath))
        {
            var dir = System.IO.Path.GetDirectoryName(currentFilePath);
            while (!string.IsNullOrEmpty(dir))
            {
                var checkPath = System.IO.Path.Combine(dir, normalizedFlowPath);
                if (System.IO.File.Exists(checkPath)) return System.IO.Path.GetFullPath(checkPath);
                var parent = System.IO.Path.GetDirectoryName(dir);
                if (parent == dir || string.IsNullOrEmpty(parent)) break;
                dir = parent;
            }
        }

        // 3. Relative to CWD
        var cwd = System.IO.Directory.GetCurrentDirectory();
        var cwdPath = System.IO.Path.Combine(cwd, normalizedFlowPath);
        if (System.IO.File.Exists(cwdPath)) return System.IO.Path.GetFullPath(cwdPath);

        // 4. Climb up parent directories of CWD
        var checkCwd = cwd;
        while (!string.IsNullOrEmpty(checkCwd))
        {
            var checkPath = System.IO.Path.Combine(checkCwd, normalizedFlowPath);
            if (System.IO.File.Exists(checkPath)) return System.IO.Path.GetFullPath(checkPath);
            var parent = System.IO.Path.GetDirectoryName(checkCwd);
            if (parent == checkCwd || string.IsNullOrEmpty(parent)) break;
            checkCwd = parent;
        }

        return "";
    }
}
