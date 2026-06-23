using System.Collections.Generic;
using System.Collections.ObjectModel;
using CdpInspectorApp.ViewModels;

namespace CdpInspectorApp.Models;

public enum StepStatus
{
    Pending,
    Running,
    Passed,
    Failed
}

public class TestStudioStepModel : ViewModelBase
{
    private string _action = "";
    private string? _selector;
    private string? _value;
    private StepStatus _status = StepStatus.Pending;
    private string? _errorMessage;
    private bool _isCurrent;
    private ObservableCollection<TestStudioStepModel>? _nestedSteps;
    private string? _whileConditionType;
    private string? _whileConditionValue;
    private int _startLine;
    private int _endLine;

    public int StartLine
    {
        get => _startLine;
        set => RaiseAndSetIfChanged(ref _startLine, value);
    }

    public int EndLine
    {
        get => _endLine;
        set => RaiseAndSetIfChanged(ref _endLine, value);
    }

    public string Action
    {
        get => _action;
        set
        {
            if (RaiseAndSetIfChanged(ref _action, value))
            {
                OnPropertyChanged(nameof(ActionDisplay));
                OnPropertyChanged(nameof(DetailDisplay));
            }
        }
    }

    public string? Selector
    {
        get => _selector;
        set
        {
            if (RaiseAndSetIfChanged(ref _selector, value))
            {
                OnPropertyChanged(nameof(DetailDisplay));
            }
        }
    }

    public string? Value
    {
        get => _value;
        set
        {
            if (RaiseAndSetIfChanged(ref _value, value))
            {
                OnPropertyChanged(nameof(DetailDisplay));
            }
        }
    }

    public ObservableCollection<TestStudioStepModel>? NestedSteps
    {
        get => _nestedSteps;
        set
        {
            if (RaiseAndSetIfChanged(ref _nestedSteps, value))
            {
                OnPropertyChanged(nameof(DetailDisplay));
            }
        }
    }

    public string? WhileConditionType
    {
        get => _whileConditionType;
        set
        {
            if (RaiseAndSetIfChanged(ref _whileConditionType, value))
            {
                OnPropertyChanged(nameof(DetailDisplay));
            }
        }
    }

    public string? WhileConditionValue
    {
        get => _whileConditionValue;
        set
        {
            if (RaiseAndSetIfChanged(ref _whileConditionValue, value))
            {
                OnPropertyChanged(nameof(DetailDisplay));
            }
        }
    }

    public StepStatus Status
    {
        get => _status;
        set
        {
            if (RaiseAndSetIfChanged(ref _status, value))
            {
                OnPropertyChanged(nameof(IsPending));
                OnPropertyChanged(nameof(IsRunning));
                OnPropertyChanged(nameof(IsPassed));
                OnPropertyChanged(nameof(IsFailed));
            }
        }
    }

    public bool IsPending => Status == StepStatus.Pending;
    public bool IsRunning => Status == StepStatus.Running;
    public bool IsPassed => Status == StepStatus.Passed;
    public bool IsFailed => Status == StepStatus.Failed;

    public string? ErrorMessage
    {
        get => _errorMessage;
        set => RaiseAndSetIfChanged(ref _errorMessage, value);
    }

    public bool IsCurrent
    {
        get => _isCurrent;
        set => RaiseAndSetIfChanged(ref _isCurrent, value);
    }

    public string ActionDisplay
    {
        get
        {
            if (string.IsNullOrEmpty(Action)) return "Step";
            return Action switch
            {
                "launchApp" => "Launch App",
                "tapOn" => "Tap On",
                "inputText" => "Input Text",
                "clearText" => "Clear Text",
                "assertVisible" => "Assert Visible",
                "assertNotVisible" => "Assert Not Visible",
                "assertFalse" => "Assert False",
                "setAirplaneMode" => "Set Airplane Mode",
                "delay" => "Delay",
                "back" => "Go Back",
                "scroll" => "Scroll",
                "scrollUntilVisible" => "Scroll Until Visible",
                "pressKey" => "Press Key",
                "dragAndDrop" => "Drag And Drop",
                _ => char.ToUpper(Action[0]) + Action.Substring(1)
            };
        }
    }

    public string DetailDisplay
    {
        get
        {
            if (Action == "launchApp" || Action == "back")
            {
                return "";
            }
            if (Action == "delay")
            {
                return $"{Value} ms";
            }
            if (Action == "repeat" && !string.IsNullOrEmpty(WhileConditionType))
            {
                var cond = $"while {WhileConditionType}: \"{WhileConditionValue}\"";
                if (NestedSteps != null && NestedSteps.Count > 0)
                {
                    return $"{cond} | {NestedSteps.Count} nested commands";
                }
                return cond;
            }
            var parts = new List<string>();
            if (!string.IsNullOrEmpty(Selector))
            {
                parts.Add($"Selector: \"{Selector}\"");
            }
            if (!string.IsNullOrEmpty(Value))
            {
                parts.Add($"Value: \"{Value}\"");
            }
            if (NestedSteps != null && NestedSteps.Count > 0)
            {
                parts.Add($"{NestedSteps.Count} nested commands");
            }
            return string.Join(" | ", parts);
        }
    }
}
