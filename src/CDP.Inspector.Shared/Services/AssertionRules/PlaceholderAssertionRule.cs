using System;
using System.Collections.Generic;
using CdpInspectorApp.Models;

namespace CdpInspectorApp.Services.AssertionRules;

public class PlaceholderAssertionRule : IAssertionInferenceRule
{
    public bool CanInfer(string controlTypeName, string selector, Dictionary<string, string> properties)
    {
        return properties.TryGetValue("PlaceholderText", out var pVal) && !string.IsNullOrEmpty(pVal) &&
               (controlTypeName.Contains("TextBox", StringComparison.OrdinalIgnoreCase) || 
                controlTypeName.Contains("ComboBox", StringComparison.OrdinalIgnoreCase));
    }

    public List<TestStudioStepModel> Infer(string controlTypeName, string selector, Dictionary<string, string> properties)
    {
        var steps = new List<TestStudioStepModel>();
        if (properties.TryGetValue("PlaceholderText", out var pVal) && pVal != null)
        {
            var escapedSelector = selector.Replace("\"", "\\\"");
            var escapedP = pVal.Replace("\"", "\\\"");
            steps.Add(new TestStudioStepModel
            {
                Action = "assertTrue",
                Selector = "",
                Value = $"document.querySelector(\"{escapedSelector}\").visual.PlaceholderText.ToString() == \"{escapedP}\""
            });
        }
        return steps;
    }
}
