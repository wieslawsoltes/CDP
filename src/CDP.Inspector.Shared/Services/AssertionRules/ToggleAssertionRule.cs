using System;
using System.Collections.Generic;
using CdpInspectorApp.Models;

namespace CdpInspectorApp.Services.AssertionRules;

public class ToggleAssertionRule : AssertionInferenceRuleBase
{
    public override string Name => "Toggle State";
    public override string Description => "Asserts IsChecked property of checkboxes and toggle controls.";

    public override bool CanInfer(string controlTypeName, string selector, Dictionary<string, string> properties)
    {
        return properties.ContainsKey("IsChecked") || 
               controlTypeName.Contains("CheckBox", StringComparison.OrdinalIgnoreCase) || 
               controlTypeName.Contains("ToggleButton", StringComparison.OrdinalIgnoreCase) || 
               controlTypeName.Contains("RadioButton", StringComparison.OrdinalIgnoreCase) || 
               controlTypeName.Contains("ToggleSwitch", StringComparison.OrdinalIgnoreCase);
    }

    public override List<TestStudioStepModel> Infer(string controlTypeName, string selector, Dictionary<string, string> properties)
    {
        var steps = new List<TestStudioStepModel>();
        if (properties.TryGetValue("IsChecked", out var isCheckedVal) && !string.IsNullOrEmpty(isCheckedVal))
        {
            bool isTrue = isCheckedVal.Equals("true", StringComparison.OrdinalIgnoreCase);
            steps.Add(new TestStudioStepModel
            {
                Action = "assertVisible",
                Selector = $"{selector}[IsChecked='{(isTrue ? "true" : "false")}']",
                Value = ""
            });
        }
        return steps;
    }
}
