using System;
using System.Collections.Generic;
using CdpInspectorApp.Models;

namespace CdpInspectorApp.Services.AssertionRules;

public class HeaderAssertionRule : IAssertionInferenceRule
{
    public bool CanInfer(string controlTypeName, string selector, Dictionary<string, string> properties)
    {
        return properties.TryGetValue("Header", out var headerVal) && 
               !string.IsNullOrEmpty(headerVal) && 
               !headerVal.Contains('{') && 
               !headerVal.Contains(':') &&
               (controlTypeName.Contains("TabItem", StringComparison.OrdinalIgnoreCase) || 
                controlTypeName.Contains("Expander", StringComparison.OrdinalIgnoreCase) ||
                controlTypeName.Contains("MenuItem", StringComparison.OrdinalIgnoreCase) ||
                controlTypeName.Contains("GroupBox", StringComparison.OrdinalIgnoreCase));
    }

    public List<TestStudioStepModel> Infer(string controlTypeName, string selector, Dictionary<string, string> properties)
    {
        var steps = new List<TestStudioStepModel>();
        if (properties.TryGetValue("Header", out var headerVal) && headerVal != null)
        {
            var escapedSelector = selector.Replace("\"", "\\\"");
            var escapedHeader = headerVal.Replace("\"", "\\\"");
            steps.Add(new TestStudioStepModel
            {
                Action = "assertTrue",
                Selector = "",
                Value = $"document.querySelector(\"{escapedSelector}\").visual.Header.ToString() == \"{escapedHeader}\""
            });
        }
        return steps;
    }
}
