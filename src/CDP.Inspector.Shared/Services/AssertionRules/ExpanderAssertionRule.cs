using System;
using System.Collections.Generic;
using CdpInspectorApp.Models;

namespace CdpInspectorApp.Services.AssertionRules;

public class ExpanderAssertionRule : AssertionInferenceRuleBase
{
    public override string Name => "Expanded State";
    public override string Description => "Asserts the IsExpanded property of Expanders and TreeView items.";

    public override bool CanInfer(string controlTypeName, string selector, Dictionary<string, string> properties)
    {
        return properties.ContainsKey("IsExpanded") || 
               controlTypeName.Contains("Expander", StringComparison.OrdinalIgnoreCase) || 
               controlTypeName.Contains("TreeViewItem", StringComparison.OrdinalIgnoreCase);
    }

    public override List<TestStudioStepModel> Infer(string controlTypeName, string selector, Dictionary<string, string> properties)
    {
        var steps = new List<TestStudioStepModel>();
        if (properties.TryGetValue("IsExpanded", out var isExpandedVal) && !string.IsNullOrEmpty(isExpandedVal))
        {
            bool isTrue = isExpandedVal.Equals("true", StringComparison.OrdinalIgnoreCase);
            steps.Add(new TestStudioStepModel
            {
                Action = "assertVisible",
                Selector = $"{selector}[IsExpanded='{(isTrue ? "true" : "false")}']",
                Value = ""
            });
        }
        return steps;
    }
}
