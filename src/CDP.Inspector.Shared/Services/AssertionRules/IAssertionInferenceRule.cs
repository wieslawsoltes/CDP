using System;
using System.Collections.Generic;
using CdpInspectorApp.Models;

namespace CdpInspectorApp.Services.AssertionRules;

public interface IAssertionInferenceRule
{
    bool CanInfer(string controlTypeName, string selector, Dictionary<string, string> properties);
    List<TestStudioStepModel> Infer(string controlTypeName, string selector, Dictionary<string, string> properties);
}
