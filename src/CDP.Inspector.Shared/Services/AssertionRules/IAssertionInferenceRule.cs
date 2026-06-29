using System;
using System.Collections.Generic;
using CdpInspectorApp.Models;

namespace CdpInspectorApp.Services.AssertionRules;

public interface IAssertionInferenceRule
{
    string Name { get; }
    string Description { get; }
    bool IsEnabled { get; set; }
    Dictionary<string, object> Options { get; }

    bool CanInfer(string controlTypeName, string selector, Dictionary<string, string> properties);
    List<TestStudioStepModel> Infer(string controlTypeName, string selector, Dictionary<string, string> properties);
}
