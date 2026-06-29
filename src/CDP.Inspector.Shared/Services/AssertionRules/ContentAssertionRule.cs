using System;
using System.Collections.Generic;
using CdpInspectorApp.Models;

namespace CdpInspectorApp.Services.AssertionRules;

public class ContentAssertionRule : IAssertionInferenceRule
{
    public bool CanInfer(string controlTypeName, string selector, Dictionary<string, string> properties)
    {
        return properties.TryGetValue("Content", out var contentVal) && 
               !string.IsNullOrEmpty(contentVal) && 
               !contentVal.Contains('{') && 
               !contentVal.Contains(':') &&
               (controlTypeName.Contains("Button", StringComparison.OrdinalIgnoreCase) || 
                controlTypeName.Contains("Label", StringComparison.OrdinalIgnoreCase) ||
                controlTypeName.Contains("ToolTip", StringComparison.OrdinalIgnoreCase));
    }

    public List<TestStudioStepModel> Infer(string controlTypeName, string selector, Dictionary<string, string> properties)
    {
        var steps = new List<TestStudioStepModel>();
        if (properties.TryGetValue("Content", out var contentVal) && contentVal != null)
        {
            var escapedSelector = selector.Replace("\"", "\\\"");
            var escapedContent = contentVal.Replace("\"", "\\\"");
            steps.Add(new TestStudioStepModel
            {
                Action = "assertTrue",
                Selector = "",
                Value = $"document.querySelector(\"{escapedSelector}\").visual.Content.ToString() == \"{escapedContent}\""
            });
        }
        return steps;
    }
}
