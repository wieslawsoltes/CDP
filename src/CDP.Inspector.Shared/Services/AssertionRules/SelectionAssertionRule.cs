using System;
using System.Collections.Generic;
using CdpInspectorApp.Models;

namespace CdpInspectorApp.Services.AssertionRules;

public class SelectionAssertionRule : AssertionInferenceRuleBase
{
    public override string Name => "Selection State";
    public override string Description => "Asserts the IsSelected property of tab items, list boxes, and tree views.";

    public override bool CanInfer(string controlTypeName, string selector, Dictionary<string, string> properties)
    {
        return properties.ContainsKey("IsSelected") || 
               controlTypeName.Contains("TabItem", StringComparison.OrdinalIgnoreCase) || 
               controlTypeName.Contains("ListBoxItem", StringComparison.OrdinalIgnoreCase) || 
               controlTypeName.Contains("TreeViewItem", StringComparison.OrdinalIgnoreCase);
    }

    public override List<TestStudioStepModel> Infer(string controlTypeName, string selector, Dictionary<string, string> properties)
    {
        var steps = new List<TestStudioStepModel>();
        if (properties.TryGetValue("IsSelected", out var isSelectedVal) && !string.IsNullOrEmpty(isSelectedVal))
        {
            var escapedSelector = selector.Replace("\"", "\\\"");
            bool isTrue = isSelectedVal.Equals("true", StringComparison.OrdinalIgnoreCase);
            steps.Add(new TestStudioStepModel
            {
                Action = isTrue ? "assertTrue" : "assertFalse",
                Selector = "",
                Value = $"document.querySelector(\"{escapedSelector}\").visual.IsSelected"
            });
        }
        return steps;
    }
}
