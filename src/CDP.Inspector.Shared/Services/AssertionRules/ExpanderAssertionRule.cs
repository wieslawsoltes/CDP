using System;
using System.Collections.Generic;
using CdpInspectorApp.Models;

namespace CdpInspectorApp.Services.AssertionRules;

public class ExpanderAssertionRule : IAssertionInferenceRule
{
    public bool CanInfer(string controlTypeName, string selector, Dictionary<string, string> properties)
    {
        return properties.ContainsKey("IsExpanded") || 
               controlTypeName.Contains("Expander", StringComparison.OrdinalIgnoreCase) ||
               controlTypeName.Contains("TreeViewItem", StringComparison.OrdinalIgnoreCase);
    }

    public List<TestStudioStepModel> Infer(string controlTypeName, string selector, Dictionary<string, string> properties)
    {
        var steps = new List<TestStudioStepModel>();
        if (properties.TryGetValue("IsExpanded", out var isExpandedVal) && !string.IsNullOrEmpty(isExpandedVal))
        {
            var escapedSelector = selector.Replace("\"", "\\\"");
            bool isTrue = isExpandedVal.Equals("true", StringComparison.OrdinalIgnoreCase);
            steps.Add(new TestStudioStepModel
            {
                Action = isTrue ? "assertTrue" : "assertFalse",
                Selector = "",
                Value = $"document.querySelector(\"{escapedSelector}\").visual.IsExpanded"
            });
        }
        return steps;
    }
}
