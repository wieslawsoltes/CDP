using System;
using System.Collections.Generic;
using CdpInspectorApp.Models;

namespace CdpInspectorApp.Services.AssertionRules;

public class SliderAssertionRule : IAssertionInferenceRule
{
    public bool CanInfer(string controlTypeName, string selector, Dictionary<string, string> properties)
    {
        return properties.ContainsKey("Value") && 
               (controlTypeName.Contains("Slider", StringComparison.OrdinalIgnoreCase) || 
                controlTypeName.Contains("ProgressBar", StringComparison.OrdinalIgnoreCase));
    }

    public List<TestStudioStepModel> Infer(string controlTypeName, string selector, Dictionary<string, string> properties)
    {
        var steps = new List<TestStudioStepModel>();
        if (properties.TryGetValue("Value", out var val) && !string.IsNullOrEmpty(val))
        {
            var escapedSelector = selector.Replace("\"", "\\\"");
            steps.Add(new TestStudioStepModel
            {
                Action = "assertTrue",
                Selector = "",
                Value = $"document.querySelector(\"{escapedSelector}\").visual.Value == {val}"
            });
        }
        return steps;
    }
}
