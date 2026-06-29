using System;
using System.Collections.Generic;
using CdpInspectorApp.Models;

namespace CdpInspectorApp.Services.AssertionRules;

public class IndexAssertionRule : IAssertionInferenceRule
{
    public bool CanInfer(string controlTypeName, string selector, Dictionary<string, string> properties)
    {
        return properties.ContainsKey("SelectedIndex") || 
               controlTypeName.Contains("ComboBox", StringComparison.OrdinalIgnoreCase) ||
               controlTypeName.Contains("ListBox", StringComparison.OrdinalIgnoreCase) ||
               controlTypeName.Contains("TabControl", StringComparison.OrdinalIgnoreCase);
    }

    public List<TestStudioStepModel> Infer(string controlTypeName, string selector, Dictionary<string, string> properties)
    {
        var steps = new List<TestStudioStepModel>();
        if (properties.TryGetValue("SelectedIndex", out var idxVal) && !string.IsNullOrEmpty(idxVal))
        {
            var escapedSelector = selector.Replace("\"", "\\\"");
            steps.Add(new TestStudioStepModel
            {
                Action = "assertTrue",
                Selector = "",
                Value = $"document.querySelector(\"{escapedSelector}\").visual.SelectedIndex == {idxVal}"
            });
        }
        return steps;
    }
}
