using System;
using System.Collections.Generic;
using CdpInspectorApp.Models;

namespace CdpInspectorApp.Services.AssertionRules;

public class TextBoxAssertionRule : AssertionInferenceRuleBase
{
    private bool _assertEmptyText = true;

    public override string Name => "TextBox Value";
    public override string Description => "Asserts the Text property of TextBoxes.";

    public bool AssertEmptyText
    {
        get => _assertEmptyText;
        set => RaiseAndSetIfChanged(ref _assertEmptyText, value);
    }

    public override bool CanInfer(string controlTypeName, string selector, Dictionary<string, string> properties)
    {
        return properties.ContainsKey("Text") || controlTypeName.Contains("TextBox", StringComparison.OrdinalIgnoreCase);
    }

    public override List<TestStudioStepModel> Infer(string controlTypeName, string selector, Dictionary<string, string> properties)
    {
        var steps = new List<TestStudioStepModel>();
        if (properties.TryGetValue("Text", out var textVal))
        {
            if (string.IsNullOrEmpty(textVal) && !AssertEmptyText)
            {
                return steps;
            }

            var escapedText = (textVal ?? "").Replace("'", "\\'");
            steps.Add(new TestStudioStepModel
            {
                Action = "assertVisible",
                Selector = $"{selector}[Text='{escapedText}']",
                Value = ""
            });
        }
        return steps;
    }
}
