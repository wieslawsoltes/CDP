using System;
using System.Collections.Generic;
using CdpInspectorApp.Models;

namespace CdpInspectorApp.Services.AssertionRules;

public class NumericUpDownAssertionRule : AssertionInferenceRuleBase
{
    public override string Name => "Numeric Value";
    public override string Description => "Asserts the Value property of NumericUpDown controls.";

    public override bool CanInfer(string controlTypeName, string selector, Dictionary<string, string> properties)
    {
        return properties.ContainsKey("Value") && controlTypeName.Contains("NumericUpDown", StringComparison.OrdinalIgnoreCase);
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
