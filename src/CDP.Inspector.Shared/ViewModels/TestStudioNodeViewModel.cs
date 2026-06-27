#nullable enable

using System;
using System.Collections.ObjectModel;
using System.Linq;
using CdpInspectorApp.Models;
using CdpInspectorApp.Services;
using Chrome.DevTools.Protocol;
using CDP.Editor.Nodes.ViewModels;

namespace CdpInspectorApp.ViewModels;

public class TestStudioNodeViewModel : NodeViewModel
{
    private string _action = "";
    private string _selector = "";
    private string _value = "";
    private TestStudioStepModel? _step;

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
    }

    public string Action
    {
        get => _action;
        set => RaiseAndSetIfChanged(ref _action, value);
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
        UpdateVisualBrushes();
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
