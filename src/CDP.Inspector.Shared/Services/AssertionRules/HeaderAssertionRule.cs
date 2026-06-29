using System;
using System.Collections.Generic;
using CdpInspectorApp.Models;

namespace CdpInspectorApp.Services.AssertionRules;

public class HeaderAssertionRule : AssertionInferenceRuleBase
{
    public override string Name => "Header Text";
    public override string Description => "Asserts the Header property of tab items and group boxes if it is a string.";

    public override bool CanInfer(string controlTypeName, string selector, Dictionary<string, string> properties)
    {
        return properties.ContainsKey("Header") && 
               (controlTypeName.Contains("TabItem", StringComparison.OrdinalIgnoreCase) || 
                controlTypeName.Contains("GroupBox", StringComparison.OrdinalIgnoreCase) || 
                controlTypeName.Contains("Expander", StringComparison.OrdinalIgnoreCase));
    }

    public override List<TestStudioStepModel> Infer(string controlTypeName, string selector, Dictionary<string, string> properties)
    {
        var steps = new List<TestStudioStepModel>();
        if (properties.TryGetValue("Header", out var headerVal) && !string.IsNullOrEmpty(headerVal))
        {
            var escapedHeader = headerVal.Replace("'", "\\'");
            steps.Add(new TestStudioStepModel
            {
                Action = "assertVisible",
                Selector = $"{selector}[Header='{escapedHeader}']",
                Value = ""
            });
        }
        return steps;
    }
}
