using System;
using System.Collections.Generic;
using CdpInspectorApp.Models;

namespace CdpInspectorApp.Services.AssertionRules;

public class DateTimeAssertionRule : AssertionInferenceRuleBase
{
    private bool _assertDate = true;
    private bool _assertTime = true;

    public override string Name => "Date & Time";
    public override string Description => "Asserts SelectedDate and SelectedTime properties of DatePicker and TimePicker controls.";

    public bool AssertDate
    {
        get => _assertDate;
        set => RaiseAndSetIfChanged(ref _assertDate, value);
    }

    public bool AssertTime
    {
        get => _assertTime;
        set => RaiseAndSetIfChanged(ref _assertTime, value);
    }

    public override bool CanInfer(string controlTypeName, string selector, Dictionary<string, string> properties)
    {
        return properties.ContainsKey("SelectedDate") || 
               properties.ContainsKey("SelectedTime") || 
               controlTypeName.Contains("DatePicker", StringComparison.OrdinalIgnoreCase) || 
               controlTypeName.Contains("TimePicker", StringComparison.OrdinalIgnoreCase) || 
               controlTypeName.Contains("CalendarDatePicker", StringComparison.OrdinalIgnoreCase);
    }

    public override List<TestStudioStepModel> Infer(string controlTypeName, string selector, Dictionary<string, string> properties)
    {
        var steps = new List<TestStudioStepModel>();
        var escapedSelector = selector.Replace("\"", "\\\"");

        if (AssertDate && properties.TryGetValue("SelectedDate", out var dateVal) && !string.IsNullOrEmpty(dateVal))
        {
            var escapedDate = dateVal.Replace("\"", "\\\"");
            steps.Add(new TestStudioStepModel
            {
                Action = "assertVisible",
                Selector = $"{selector}[SelectedDate=\"{escapedDate}\"]",
                Value = ""
            });
        }

        if (AssertTime && properties.TryGetValue("SelectedTime", out var timeVal) && !string.IsNullOrEmpty(timeVal))
        {
            var escapedTime = timeVal.Replace("\"", "\\\"");
            steps.Add(new TestStudioStepModel
            {
                Action = "assertVisible",
                Selector = $"{selector}[SelectedTime=\"{escapedTime}\"]",
                Value = ""
            });
        }

        return steps;
    }
}
