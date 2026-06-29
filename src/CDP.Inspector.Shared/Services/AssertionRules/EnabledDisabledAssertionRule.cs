using System;
using System.Collections.Generic;
using CdpInspectorApp.Models;

namespace CdpInspectorApp.Services.AssertionRules;

public class EnabledDisabledAssertionRule : IAssertionInferenceRule
{
    public bool CanInfer(string controlTypeName, string selector, Dictionary<string, string> properties)
    {
        return properties.TryGetValue("IsEnabled", out var isEnabledVal) && 
               isEnabledVal.Equals("false", StringComparison.OrdinalIgnoreCase);
    }

    public List<TestStudioStepModel> Infer(string controlTypeName, string selector, Dictionary<string, string> properties)
    {
        var steps = new List<TestStudioStepModel>();
        var escapedSelector = selector.Replace("\"", "\\\"");
        steps.Add(new TestStudioStepModel
        {
            Action = "assertFalse",
            Selector = "",
            Value = $"document.querySelector(\"{escapedSelector}\").visual.IsEnabled"
        });
        return steps;
    }
}
