using System;
using System.Collections.Generic;
using CdpInspectorApp.Models;

namespace CdpInspectorApp.Services.AssertionRules;

public class DateTimeAssertionRule : IAssertionInferenceRule
{
    public bool CanInfer(string controlTypeName, string selector, Dictionary<string, string> properties)
    {
        return properties.ContainsKey("SelectedDate") || 
               properties.ContainsKey("SelectedTime") || 
               controlTypeName.Contains("DatePicker", StringComparison.OrdinalIgnoreCase) || 
               controlTypeName.Contains("TimePicker", StringComparison.OrdinalIgnoreCase) || 
               controlTypeName.Contains("Calendar", StringComparison.OrdinalIgnoreCase);
    }

    public List<TestStudioStepModel> Infer(string controlTypeName, string selector, Dictionary<string, string> properties)
    {
        var steps = new List<TestStudioStepModel>();
        var escapedSelector = selector.Replace("\"", "\\\"");

        if (properties.TryGetValue("SelectedDate", out var dateVal) && !string.IsNullOrEmpty(dateVal))
        {
            var escapedDate = dateVal.Replace("\"", "\\\"");
            steps.Add(new TestStudioStepModel
            {
                Action = "assertTrue",
                Selector = "",
                Value = $"document.querySelector(\"{escapedSelector}\").visual.SelectedDate.ToString() == \"{escapedDate}\""
            });
        }

        if (properties.TryGetValue("SelectedTime", out var timeVal) && !string.IsNullOrEmpty(timeVal))
        {
            var escapedTime = timeVal.Replace("\"", "\\\"");
            steps.Add(new TestStudioStepModel
            {
                Action = "assertTrue",
                Selector = "",
                Value = $"document.querySelector(\"{escapedSelector}\").visual.SelectedTime.ToString() == \"{escapedTime}\""
            });
        }
        return steps;
    }
}
