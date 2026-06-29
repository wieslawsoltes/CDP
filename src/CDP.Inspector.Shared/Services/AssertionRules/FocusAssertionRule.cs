using System;
using System.Collections.Generic;
using CdpInspectorApp.Models;

namespace CdpInspectorApp.Services.AssertionRules;

public class FocusAssertionRule : IAssertionInferenceRule
{
    public bool CanInfer(string controlTypeName, string selector, Dictionary<string, string> properties)
    {
        return properties.TryGetValue("IsFocused", out var isFocusedVal) && 
               isFocusedVal.Equals("true", StringComparison.OrdinalIgnoreCase);
    }

    public List<TestStudioStepModel> Infer(string controlTypeName, string selector, Dictionary<string, string> properties)
    {
        var steps = new List<TestStudioStepModel>();
        var escapedSelector = selector.Replace("\"", "\\\"");
        steps.Add(new TestStudioStepModel
        {
            Action = "assertTrue",
            Selector = "",
            Value = $"document.querySelector(\"{escapedSelector}\").visual.IsFocused"
        });
        return steps;
    }
}
