using System;
using System.Collections.Generic;

namespace Chrome.DevTools.Protocol;

public class TestStudioStep
{
    public string Action { get; set; } = "";
    public string? Selector { get; set; }
    public string? Value { get; set; }
    public List<TestStudioStep>? NestedSteps { get; set; }
    public Dictionary<string, object?> Parameters { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public string? WhileConditionType { get; set; }
    public string? WhileConditionValue { get; set; }
    public int StartLine { get; set; }
    public int EndLine { get; set; }
}
