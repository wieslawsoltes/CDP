using System;
using System.Collections.Generic;
using CdpInspectorApp.Models;

namespace CdpInspectorApp.Services.AssertionRules;

public class PlaceholderAssertionRule : AssertionInferenceRuleBase
{
    public override string Name => "Placeholder Text";
    public override string Description => "Asserts the PlaceholderText property of TextBoxes and ComboBoxes.";

    public override bool CanInfer(string controlTypeName, string selector, Dictionary<string, string> properties)
    {
        return properties.ContainsKey("PlaceholderText") && 
               (controlTypeName.Contains("TextBox", StringComparison.OrdinalIgnoreCase) || 
                controlTypeName.Contains("ComboBox", StringComparison.OrdinalIgnoreCase));
    }

    public override List<TestStudioStepModel> Infer(string controlTypeName, string selector, Dictionary<string, string> properties)
    {
        var steps = new List<TestStudioStepModel>();
        if (properties.TryGetValue("PlaceholderText", out var phVal) && !string.IsNullOrEmpty(phVal))
        {
            var escapedPH = phVal.Replace("'", "\\'");
            steps.Add(new TestStudioStepModel
            {
                Action = "assertVisible",
                Selector = $"{selector}[PlaceholderText='{escapedPH}']",
                Value = ""
            });
        }
        return steps;
    }
}
