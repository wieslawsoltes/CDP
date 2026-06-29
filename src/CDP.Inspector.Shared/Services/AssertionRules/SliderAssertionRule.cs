using System;
using System.Collections.Generic;
using CdpInspectorApp.Models;

namespace CdpInspectorApp.Services.AssertionRules;

public class SliderAssertionRule : AssertionInferenceRuleBase
{
    public override string Name => "Slider Value";
    public override string Description => "Asserts the Value property of Sliders.";

    public override bool CanInfer(string controlTypeName, string selector, Dictionary<string, string> properties)
    {
        return properties.ContainsKey("Value") && controlTypeName.Contains("Slider", StringComparison.OrdinalIgnoreCase);
    }

    public override List<TestStudioStepModel> Infer(string controlTypeName, string selector, Dictionary<string, string> properties)
    {
        var steps = new List<TestStudioStepModel>();
        if (properties.TryGetValue("Value", out var val) && double.TryParse(val, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var dVal))
        {
            var escapedSelector = selector.Replace("\"", "\\\"");
            var valStr = dVal.ToString(System.Globalization.CultureInfo.InvariantCulture);
            steps.Add(new TestStudioStepModel
            {
                Action = "assertVisible",
                Selector = $"{selector}[Value=\"{valStr}\"]",
                Value = ""
            });
        }
        return steps;
    }
}
