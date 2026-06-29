using System;
using System.Collections.Generic;
using CdpInspectorApp.Models;

namespace CdpInspectorApp.Services.AssertionRules;

public class TextBoxAssertionRule : IAssertionInferenceRule
{
    public bool CanInfer(string controlTypeName, string selector, Dictionary<string, string> properties)
    {
        return properties.ContainsKey("Text") || controlTypeName.Contains("TextBox", StringComparison.OrdinalIgnoreCase);
    }

    public List<TestStudioStepModel> Infer(string controlTypeName, string selector, Dictionary<string, string> properties)
    {
        var steps = new List<TestStudioStepModel>();
        if (properties.TryGetValue("Text", out var textVal))
        {
            var escapedSelector = selector.Replace("\"", "\\\"");
            var escapedText = (textVal ?? "").Replace("\"", "\\\"");
            steps.Add(new TestStudioStepModel
            {
                Action = "assertTrue",
                Selector = "",
                Value = $"document.querySelector(\"{escapedSelector}\").visual.Text == \"{escapedText}\""
            });
        }
        return steps;
    }
}
