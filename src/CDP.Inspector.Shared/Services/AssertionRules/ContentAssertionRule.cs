using System;
using System.Collections.Generic;
using CdpInspectorApp.Models;

namespace CdpInspectorApp.Services.AssertionRules;

public class ContentAssertionRule : AssertionInferenceRuleBase
{
    public override string Name => "Content Text";
    public override string Description => "Asserts the Content property of buttons and labels if it is a string.";

    public override bool CanInfer(string controlTypeName, string selector, Dictionary<string, string> properties)
    {
        return properties.ContainsKey("Content") && 
               (controlTypeName.Contains("Button", StringComparison.OrdinalIgnoreCase) || 
                controlTypeName.Contains("Label", StringComparison.OrdinalIgnoreCase));
    }

    public override List<TestStudioStepModel> Infer(string controlTypeName, string selector, Dictionary<string, string> properties)
    {
        var steps = new List<TestStudioStepModel>();
        if (properties.TryGetValue("Content", out var contentVal) && !string.IsNullOrEmpty(contentVal))
        {
            var escapedSelector = selector.Replace("\"", "\\\"");
            var escapedContent = contentVal.Replace("\"", "\\\"");
            steps.Add(new TestStudioStepModel
            {
                Action = "assertVisible",
                Selector = $"{selector}[Content=\"{escapedContent}\"]",
                Value = ""
            });
        }
        return steps;
    }
}
