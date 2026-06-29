#nullable enable

using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CdpInspectorApp.Models;
using CdpInspectorApp.Services;
using Chrome.DevTools.Protocol;
using CDP.Editor.Nodes.ViewModels;

namespace CdpInspectorApp.ViewModels;

public class CustomParamEditor : NodeEditorViewModelBase
{
    private readonly TestStudioNodeViewModel _owner;
    public string Name { get; }

    public string? Value
    {
        get => _owner.GetParameterValue(Name);
        set
        {
            _owner.SetParameterValue(Name, value);
            OnPropertyChanged(nameof(Value));
        }
    }

    public ObservableCollection<string> ValueSuggestions { get; }

    public bool IsPathParameter => string.Equals(Name, "path", StringComparison.OrdinalIgnoreCase) ||
                                   string.Equals(Name, "file", StringComparison.OrdinalIgnoreCase);

    public System.Windows.Input.ICommand BrowseCommand { get; }

    public CustomParamEditor(TestStudioNodeViewModel owner, string name)
    {
        _owner = owner;
        Name = name;
        var suggestions = FlowCommandCatalog.GetValueCompletions(owner.Action, name);
        ValueSuggestions = new ObservableCollection<string>(suggestions);
        BrowseCommand = new RelayCommand(async () =>
        {
            if (_owner.TestStudio != null && _owner.TestStudio.FilePickerHandler != null)
            {
                var absolutePath = await _owner.TestStudio.FilePickerHandler();
                if (!string.IsNullOrEmpty(absolutePath))
                {
                    Value = _owner.TestStudio.GetRelativePathForFile(absolutePath);
                }
            }
        });
    }

    public void NotifyValueChanged()
    {
        OnPropertyChanged(nameof(Value));
    }
}

public class TestStudioNodeViewModel : NodeViewModel
{
    private string _action = "";
    private string _selector = "";
    private string _value = "";
    private TestStudioStepModel? _step;
    private bool _isExpanded;

    public TestStudioStepModel? Step
    {
        get => _step;
        set
        {
            if (_step != null)
            {
                _step.PropertyChanged -= OnStepPropertyChanged;
            }
            _step = value;
            if (_step != null)
            {
                _step.PropertyChanged += OnStepPropertyChanged;
                _action = _step.Action;
                _selector = _step.Selector ?? "";
                _value = _step.Value ?? "";
                UpdateCustomParameters();
            }
            OnPropertyChanged(nameof(Step));
            OnPropertyChanged(nameof(Status));
            OnPropertyChanged(nameof(IsRunning));
            OnPropertyChanged(nameof(IsPassed));
            OnPropertyChanged(nameof(IsFailed));
            UpdateVisualBrushes();
        }
    }

    public StepStatus Status => _step?.Status ?? StepStatus.Pending;

    public bool IsRunning => Status == StepStatus.Running;
    public bool IsPassed => Status == StepStatus.Passed;
    public bool IsFailed => Status == StepStatus.Failed;

    private void OnStepPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(TestStudioStepModel.Status))
        {
            OnPropertyChanged(nameof(Status));
            OnPropertyChanged(nameof(IsRunning));
            OnPropertyChanged(nameof(IsPassed));
            OnPropertyChanged(nameof(IsFailed));
            UpdateVisualBrushes();
        }
        else if (e.PropertyName == nameof(TestStudioStepModel.Parameters))
        {
            foreach (var cp in CustomParameters)
            {
                cp.NotifyValueChanged();
            }
        }
    }

    public TestStudioViewModel? TestStudio { get; set; }

    public bool IsLaunchApp => string.Equals(Action, "launchApp", StringComparison.OrdinalIgnoreCase);

    public System.Windows.Input.ICommand BrowseValueCommand { get; }

    public string Action
    {
        get => _action;
        set
        {
            if (RaiseAndSetIfChanged(ref _action, value))
            {
                UpdateCustomParameters();
                OnPropertyChanged(nameof(IsLaunchApp));
            }
        }
    }

    public string Selector
    {
        get => _selector;
        set => RaiseAndSetIfChanged(ref _selector, value);
    }

    public string Value
    {
        get => _value;
        set => RaiseAndSetIfChanged(ref _value, value);
    }

    public bool IsExpanded
    {
        get => _isExpanded;
        set
        {
            if (RaiseAndSetIfChanged(ref _isExpanded, value))
            {
                Height = value ? 200 : 100;
            }
        }
    }

    public bool ShowSelector => FlowCommandCatalog.IsSelectorCommand(Action) || (FlowCommandCatalog.Find(Action)?.AcceptsSelector ?? false);

    public bool ShowValue => FlowCommandCatalog.Find(Action)?.ValueKind == FlowCommandValueKind.String;

    private ObservableCollection<CustomParamEditor> _customParameters = new();
    public ObservableCollection<CustomParamEditor> CustomParameters => _customParameters;

    public bool ShowCustomParams => CustomParameters.Count > 0;

    public ObservableCollection<string> CommandSuggestions { get; } = new(FlowCommandCatalog.PublicCommands.Select(c => c.Name));
    public ObservableCollection<string> SelectorSuggestions => SelectorService.Instance.AvailableSelectors;
    public ObservableCollection<string> ValueSuggestions { get; } = new(new[]
    {
        "true", "false", "DOWN", "UP", "LEFT", "RIGHT", "PORTRAIT", "LANDSCAPE_LEFT", "LANDSCAPE_RIGHT",
        "15000", "30000", "path: \"screenshot\"", "query: \"Describe the value to extract\"",
        "assertion: \"The screen has no overlapping text\"", "permissions: { all: allow }",
        "point: \"50%, 50%\"", "text: \"Visible text\"", "id: \"automation_id\""
    });

    public TestStudioNodeViewModel()
    {
        Width = 160;
        Height = 100;
        Content = this;
        BrowseValueCommand = new RelayCommand(async () => await BrowseValueAsync());
        UpdateVisualBrushes();
        UpdateCustomParameters();
    }

    private async Task BrowseValueAsync()
    {
        if (TestStudio != null && TestStudio.FilePickerHandler != null)
        {
            var absolutePath = await TestStudio.FilePickerHandler();
            if (!string.IsNullOrEmpty(absolutePath))
            {
                Value = TestStudio.GetRelativePathForFile(absolutePath);
                if (Step != null)
                {
                    Step.Value = Value;
                    if (Step.Parameters.ContainsKey("path"))
                    {
                        Step.Parameters["path"] = Value;
                        Step.Parameters = new Dictionary<string, object?>(Step.Parameters, StringComparer.OrdinalIgnoreCase);
                    }
                }
            }
        }
    }

    public void UpdateCustomParameters()
    {
        CustomParameters.Clear();
        var def = FlowCommandCatalog.Find(Action);
        if (def != null && def.Parameters != null)
        {
            bool showSelector = ShowSelector;
            foreach (var p in def.Parameters)
            {
                var cleanParam = p.EndsWith(":") ? p.Substring(0, p.Length - 1) : p;
                if (showSelector && FlowCommandCatalog.IsSelectorKey(cleanParam)) continue;
                CustomParameters.Add(new CustomParamEditor(this, cleanParam));
            }
        }
        OnPropertyChanged(nameof(ShowSelector));
        OnPropertyChanged(nameof(ShowValue));
        OnPropertyChanged(nameof(ShowCustomParams));
    }

    public string? GetParameterValue(string name)
    {
        if (Step != null && Step.Parameters.TryGetValue(name, out var val))
        {
            if (val is System.Collections.IDictionary || val is System.Collections.IList)
            {
                try
                {
                    var options = new System.Text.Json.JsonSerializerOptions { WriteIndented = false };
                    return System.Text.Json.JsonSerializer.Serialize(val, options);
                }
                catch
                {
                    // Fallback to ToString
                }
            }
            return val?.ToString();
        }
        return null;
    }

    public void SetParameterValue(string name, string? val)
    {
        if (Step != null)
        {
            if (string.IsNullOrEmpty(val))
            {
                Step.Parameters.Remove(name);
            }
            else
            {
                Step.Parameters[name] = ParseStructuredValue(val);
            }
            // Trigger parameters change to force updates/serialization
            Step.Parameters = new System.Collections.Generic.Dictionary<string, object?>(Step.Parameters, StringComparer.OrdinalIgnoreCase);
        }
    }

    private static object? ParseStructuredValue(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var trimmed = text.Trim();
        if ((trimmed.StartsWith("{") && trimmed.EndsWith("}")) ||
            (trimmed.StartsWith("[") && trimmed.EndsWith("]")))
        {
            try
            {
                var node = System.Text.Json.Nodes.JsonNode.Parse(trimmed);
                return ConvertJsonNode(node);
            }
            catch
            {
                // Fallback to string if parsing fails
            }
        }
        return text;
    }

    private static object? ConvertJsonNode(System.Text.Json.Nodes.JsonNode? node)
    {
        if (node == null) return null;
        if (node is System.Text.Json.Nodes.JsonObject obj)
        {
            var dict = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            foreach (var kv in obj)
            {
                dict[kv.Key] = ConvertJsonNode(kv.Value);
            }
            return dict;
        }
        if (node is System.Text.Json.Nodes.JsonArray arr)
        {
            var list = new List<object?>();
            foreach (var item in arr)
            {
                list.Add(ConvertJsonNode(item));
            }
            return list;
        }
        if (node is System.Text.Json.Nodes.JsonValue val)
        {
            if (val.TryGetValue<string>(out var s)) return s;
            if (val.TryGetValue<double>(out var d)) return d;
            if (val.TryGetValue<bool>(out var b)) return b;
            if (val.TryGetValue<int>(out var i)) return i;
            return val.ToString();
        }
        return node.ToString();
    }

    private void UpdateVisualBrushes()
    {
        switch (Status)
        {
            case StepStatus.Running:
                BorderBrush = Avalonia.Media.Brush.Parse("#f1c40f");
                Background = Avalonia.Media.Brush.Parse("#2d2b1e");
                TitleBackground = Avalonia.Media.Brush.Parse("#f39c12");
                break;
            case StepStatus.Passed:
                BorderBrush = Avalonia.Media.Brush.Parse("#2ecc71");
                Background = Avalonia.Media.Brush.Parse("#1b2a1e");
                TitleBackground = Avalonia.Media.Brush.Parse("#27ae60");
                break;
            case StepStatus.Failed:
                BorderBrush = Avalonia.Media.Brush.Parse("#e74c3c");
                Background = Avalonia.Media.Brush.Parse("#2d1c1c");
                TitleBackground = Avalonia.Media.Brush.Parse("#c0392b");
                break;
            default:
                BorderBrush = Avalonia.Media.Brush.Parse("#3c4043");
                Background = Avalonia.Media.Brush.Parse("#292a2d");
                TitleBackground = Avalonia.Media.Brush.Parse("#35363a");
                break;
        }
    }
}
