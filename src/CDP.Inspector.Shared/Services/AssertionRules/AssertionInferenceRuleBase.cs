using System;
using System.Collections.Generic;
using CdpInspectorApp.Models;
using CdpInspectorApp.ViewModels;

namespace CdpInspectorApp.Services.AssertionRules;

public abstract class AssertionInferenceRuleBase : ViewModelBase, IAssertionInferenceRule
{
    private bool _isEnabled = true;

    public abstract string Name { get; }
    public abstract string Description { get; }

    public bool IsEnabled
    {
        get => _isEnabled;
        set => RaiseAndSetIfChanged(ref _isEnabled, value);
    }

    public Dictionary<string, object> Options { get; } = new();

    public abstract bool CanInfer(string controlTypeName, string selector, Dictionary<string, string> properties);
    public abstract List<TestStudioStepModel> Infer(string controlTypeName, string selector, Dictionary<string, string> properties);
}
