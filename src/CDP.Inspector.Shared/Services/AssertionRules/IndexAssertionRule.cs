using System;
using System.Collections.Generic;
using CdpInspectorApp.Models;

namespace CdpInspectorApp.Services.AssertionRules;

public class IndexAssertionRule : AssertionInferenceRuleBase
{
    public override string Name => "Selection Index";
    public override string Description => "Asserts the SelectedIndex property of dropdowns and comboboxes.";

    public override bool CanInfer(string controlTypeName, string selector, Dictionary<string, string> properties)
    {
        return properties.ContainsKey("SelectedIndex") || 
               controlTypeName.Contains("ComboBox", StringComparison.OrdinalIgnoreCase) || 
               controlTypeName.Contains("TabControl", StringComparison.OrdinalIgnoreCase);
    }

    public override List<TestStudioStepModel> Infer(string controlTypeName, string selector, Dictionary<string, string> properties)
    {
        var steps = new List<TestStudioStepModel>();
        if (properties.TryGetValue("SelectedIndex", out var idxVal) && int.TryParse(idxVal, out var idx))
        {
            steps.Add(new TestStudioStepModel
            {
                Action = "assertVisible",
                Selector = $"{selector}[SelectedIndex='{idx}']",
                Value = ""
            });
        }
        return steps;
    }
}
