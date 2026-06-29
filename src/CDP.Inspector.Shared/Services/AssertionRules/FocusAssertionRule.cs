using System;
using System.Collections.Generic;
using CdpInspectorApp.Models;

namespace CdpInspectorApp.Services.AssertionRules;

public class FocusAssertionRule : AssertionInferenceRuleBase
{
    public override string Name => "Keyboard Focus";
    public override string Description => "Asserts the IsFocused property of interactive elements.";

    public override bool CanInfer(string controlTypeName, string selector, Dictionary<string, string> properties)
    {
        return properties.ContainsKey("IsFocused") && 
               (properties["IsFocused"].Equals("true", StringComparison.OrdinalIgnoreCase) || 
                controlTypeName.Contains("TextBox", StringComparison.OrdinalIgnoreCase) || 
                controlTypeName.Contains("Button", StringComparison.OrdinalIgnoreCase));
    }

    public override List<TestStudioStepModel> Infer(string controlTypeName, string selector, Dictionary<string, string> properties)
    {
        var steps = new List<TestStudioStepModel>();
        if (properties.TryGetValue("IsFocused", out var isFocusedVal) && !string.IsNullOrEmpty(isFocusedVal))
        {
            var escapedSelector = selector.Replace("\"", "\\\"");
            bool isTrue = isFocusedVal.Equals("true", StringComparison.OrdinalIgnoreCase);
            if (isTrue)
            {
                steps.Add(new TestStudioStepModel
                {
                    Action = "assertVisible",
                    Selector = $"{selector}[IsFocused=\"true\"]",
                    Value = ""
                });
            }
        }
        return steps;
    }
}
