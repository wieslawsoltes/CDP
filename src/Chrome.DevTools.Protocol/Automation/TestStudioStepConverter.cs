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

    public static RecordedStep ToRecordedStep(TestStudioStep step)
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
        recStep.Selector = step.Selector ?? "";

        // Map Value
        recStep.Value = step.Value ?? "";

        // Map parameters from step.Parameters if they exist
        if (step.Parameters != null)
        {
            if (step.Parameters.TryGetValue("selector", out var selObj) && selObj is string selStr)
            {
                recStep.Selector = selStr;
            }
            if (step.Parameters.TryGetValue("text", out var txtObj) && txtObj is string txtStr)
            {
                recStep.Value = txtStr;
            }
            else if (step.Parameters.TryGetValue("value", out var valObj) && valObj is string valStr)
            {
                recStep.Value = valStr;
            }

            // OffsetX, OffsetY
            if (step.Parameters.TryGetValue("offsetX", out var oxObj)) recStep.OffsetX = SafeToDouble(oxObj);
            if (step.Parameters.TryGetValue("offsetY", out var oyObj)) recStep.OffsetY = SafeToDouble(oyObj);

            // Width, Height
            if (step.Parameters.TryGetValue("width", out var wObj)) recStep.Width = SafeToDouble(wObj);
            if (step.Parameters.TryGetValue("height", out var hObj)) recStep.Height = SafeToDouble(hObj);

            // Url
            if (step.Parameters.TryGetValue("url", out var urlObj) && urlObj is string urlStr) recStep.Url = urlStr;

            // Key
            if (step.Parameters.TryGetValue("key", out var keyObj) && keyObj is string keyStr) recStep.Key = keyStr;

            // Button
            if (step.Parameters.TryGetValue("button", out var btnObj) && btnObj is string btnStr) recStep.Button = btnStr;

            // ClickCount
            if (step.Parameters.TryGetValue("clickCount", out var ccObj)) recStep.ClickCount = SafeToInt(ccObj);

            // Modifiers
            if (step.Parameters.TryGetValue("modifiers", out var modObj)) recStep.Modifiers = SafeToInt(modObj);

            // TargetSelector
            if (step.Parameters.TryGetValue("targetSelector", out var tsObj) && tsObj is string tsStr) recStep.TargetSelector = tsStr;

            // TargetOffsetX, TargetOffsetY
            if (step.Parameters.TryGetValue("targetOffsetX", out var toxObj)) recStep.TargetOffsetX = SafeToDouble(toxObj);
            if (step.Parameters.TryGetValue("targetOffsetY", out var toyObj)) recStep.TargetOffsetY = SafeToDouble(toyObj);
        }

        return recStep;
    }

    public static List<RecordedStep> ConvertYamlToRecordedSteps(string yaml)
    {
        var testStudioSteps = TestStudioYamlParser.Parse(yaml, out _, out _);
        return testStudioSteps.Select(ToRecordedStep).ToList();
    }
}
