using System;
using System.Collections.Generic;
using CdpInspectorApp.Models;

namespace CdpInspectorApp.Services.AssertionRules;

public class ToggleAssertionRule : IAssertionInferenceRule
{
    public bool CanInfer(string controlTypeName, string selector, Dictionary<string, string> properties)
    {
        return properties.ContainsKey("IsChecked") || 
               controlTypeName.Contains("CheckBox", StringComparison.OrdinalIgnoreCase) || 
               controlTypeName.Contains("ToggleButton", StringComparison.OrdinalIgnoreCase) || 
               controlTypeName.Contains("RadioButton", StringComparison.OrdinalIgnoreCase) || 
               controlTypeName.Contains("ToggleSwitch", StringComparison.OrdinalIgnoreCase);
    }

    public List<TestStudioStepModel> Infer(string controlTypeName, string selector, Dictionary<string, string> properties)
    {
        var steps = new List<TestStudioStepModel>();
        if (properties.TryGetValue("IsChecked", out var isCheckedVal) && !string.IsNullOrEmpty(isCheckedVal))
        {
            var escapedSelector = selector.Replace("\"", "\\\"");
            bool isTrue = isCheckedVal.Equals("true", StringComparison.OrdinalIgnoreCase);
            steps.Add(new TestStudioStepModel
            {
                Action = isTrue ? "assertTrue" : "assertFalse",
                Selector = "",
                Value = $"document.querySelector(\"{escapedSelector}\").visual.IsChecked"
            });
        }
        return steps;
    }
}
