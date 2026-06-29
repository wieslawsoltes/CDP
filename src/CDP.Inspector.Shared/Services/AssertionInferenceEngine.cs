using System;
using System.Collections.Generic;
using CdpInspectorApp.Models;
using CdpInspectorApp.Services.AssertionRules;

namespace CdpInspectorApp.Services;

public class AssertionInferenceEngine
{
    private readonly List<IAssertionInferenceRule> _rules = new();

    public AssertionInferenceEngine()
    {
        _rules.Add(new ToggleAssertionRule());
        _rules.Add(new TextBoxAssertionRule());
        _rules.Add(new SliderAssertionRule());
        _rules.Add(new SelectionAssertionRule());
        _rules.Add(new IndexAssertionRule());
        _rules.Add(new NumericUpDownAssertionRule());
        _rules.Add(new ExpanderAssertionRule());
        _rules.Add(new DateTimeAssertionRule());
        _rules.Add(new FocusAssertionRule());
        _rules.Add(new EnabledDisabledAssertionRule());
        _rules.Add(new ContentAssertionRule());
        _rules.Add(new HeaderAssertionRule());
        _rules.Add(new PlaceholderAssertionRule());
    }

    public IReadOnlyList<IAssertionInferenceRule> Rules => _rules;

    public void AddRule(IAssertionInferenceRule rule)
    {
        if (rule == null) throw new ArgumentNullException(nameof(rule));
        _rules.Add(rule);
    }

    public List<TestStudioStepModel> InferAssertions(string controlTypeName, string selector, Dictionary<string, string> properties)
    {
        var inferred = new List<TestStudioStepModel>();
        if (string.IsNullOrEmpty(selector)) return inferred;

        foreach (var rule in _rules)
        {
            if (rule.IsEnabled && rule.CanInfer(controlTypeName, selector, properties))
            {
                var steps = rule.Infer(controlTypeName, selector, properties);
                if (steps != null)
                {
                    inferred.AddRange(steps);
                }
            }
        }
        return inferred;
    }
}
