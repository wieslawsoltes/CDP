using System;
using System.Collections.Generic;
using CdpInspectorApp.Models;

namespace CdpInspectorApp.Services.AssertionRules;

public class EnabledDisabledAssertionRule : AssertionInferenceRuleBase
{
    public override string Name => "Enabled/Disabled";
    public override string Description => "Asserts the IsEnabled property of interactive controls (primarily when disabled).";

    public override bool CanInfer(string controlTypeName, string selector, Dictionary<string, string> properties)
    {
        return properties.ContainsKey("IsEnabled");
    }

    public override List<TestStudioStepModel> Infer(string controlTypeName, string selector, Dictionary<string, string> properties)
    {
        var steps = new List<TestStudioStepModel>();
        if (properties.TryGetValue("IsEnabled", out var isEnabledVal) && !string.IsNullOrEmpty(isEnabledVal))
        {
            var escapedSelector = selector.Replace("\"", "\\\"");
            bool isTrue = isEnabledVal.Equals("true", StringComparison.OrdinalIgnoreCase);
            if (!isTrue)
            {
                steps.Add(new TestStudioStepModel
                {
                    Action = "assertVisible",
                    Selector = $"{selector}[IsEnabled=\"false\"]",
                    Value = ""
                });
            }
        }
        return steps;
    }
}
