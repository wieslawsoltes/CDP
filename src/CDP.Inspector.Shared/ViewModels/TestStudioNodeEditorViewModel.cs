#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows.Input;
using CdpInspectorApp.Models;
using CDP.Editor.Nodes.ViewModels;

namespace CdpInspectorApp.ViewModels;

public class TestStudioNodeEditorViewModel : NodeEditorViewModel
{
    private ObservableCollection<TestStudioStepModel> _compiledSteps = new();

    public ObservableCollection<TestStudioStepModel> CompiledSteps
    {
        get => _compiledSteps;
        set => RaiseAndSetIfChanged(ref _compiledSteps, value);
    }

    public Action? SyncToTestStudioAction { get; set; }
    public Action? SyncFromTestStudioAction { get; set; }
    public Action<TestStudioNodeViewModel>? NodeSelectedActionCustom { get; set; }

    public ICommand SyncStepsCommand { get; }
    public ICommand SyncToTestStudioCommand { get; }
    public ICommand SyncFromTestStudioCommand { get; }

    public TestStudioNodeEditorViewModel()
    {
        SyncStepsCommand = new RelayCommand(SyncSteps);
        SyncToTestStudioCommand = new RelayCommand(() => SyncToTestStudioAction?.Invoke());
        SyncFromTestStudioCommand = new RelayCommand(() => SyncFromTestStudioAction?.Invoke());

        CreateNodeHandler = () => new TestStudioNodeViewModel
        {
            Action = "tapOn"
        };

        AutoLayoutHandler = AutoLayout;

        NodeSelectedAction = node =>
        {
            if (node is TestStudioNodeViewModel tNode)
            {
                NodeSelectedActionCustom?.Invoke(tNode);
            }
        };

        CollectionChangedAction = SyncSteps;
    }

    protected override void OnNodePropertyChanged(NodeViewModel node, string? propertyName)
    {
        if (propertyName == nameof(TestStudioNodeViewModel.Action) ||
            propertyName == nameof(TestStudioNodeViewModel.Selector) ||
            propertyName == nameof(TestStudioNodeViewModel.Value))
        {
            SyncSteps();
        }
    }

    public TestStudioNodeViewModel CreateNode(string name, string action, string selector, string value, double x, double y)
    {
        var node = new TestStudioNodeViewModel
        {
            Name = name,
            Action = action,
            Selector = selector,
            Value = value,
            X = x,
            Y = y
        };
        Nodes.Add(node);
        return node;
    }

    public void ConnectNodes(TestStudioNodeViewModel fromNode, TestStudioNodeViewModel toNode)
    {
        base.ConnectNodes(fromNode, toNode);
    }

    public void SelectNode(TestStudioNodeViewModel node, bool clearOthers = true)
    {
        base.SelectNode(node, clearOthers);
    }

    public void DragNode(TestStudioNodeViewModel node, double deltaX, double deltaY)
    {
        base.DragNode(node, deltaX, deltaY);
    }

    public void DeleteNode(TestStudioNodeViewModel node)
    {
        base.DeleteNode(node);
    }

    public void AutoLayout()
    {
        if (Nodes.Count == 0) return;

        var orderedNodes = new List<TestStudioNodeViewModel>();
        var visited = new HashSet<string>();

        // Find starting node (node with no incoming connections)
        var current = Nodes.OfType<TestStudioNodeViewModel>()
                      .FirstOrDefault(n => !Connections.Any(c => c.ToNode == n))
                      ?? Nodes.OfType<TestStudioNodeViewModel>().FirstOrDefault();

        while (current != null)
        {
            if (visited.Contains(current.Id)) break;
            visited.Add(current.Id);
            orderedNodes.Add(current);

            var connection = Connections.FirstOrDefault(c => c.FromNode == current);
            current = connection?.ToNode as TestStudioNodeViewModel;
        }

        // Add remaining nodes that weren't in the sequential chain
        foreach (var node in Nodes.OfType<TestStudioNodeViewModel>())
        {
            if (!visited.Contains(node.Id))
            {
                orderedNodes.Add(node);
            }
        }

        // Arrange them
        for (int i = 0; i < orderedNodes.Count; i++)
        {
            orderedNodes[i].X = 200.0 * i + 10.0;
            orderedNodes[i].Y = 20.0;
        }
    }

    public ObservableCollection<TestStudioStepModel> CompileSteps()
    {
        var compiled = new ObservableCollection<TestStudioStepModel>();
        if (Nodes.Count == 0) return compiled;

        // Find starting node (node with no incoming connections)
        var current = Nodes.OfType<TestStudioNodeViewModel>()
                      .FirstOrDefault(n => !Connections.Any(c => c.ToNode == n))
                      ?? Nodes.OfType<TestStudioNodeViewModel>().FirstOrDefault();

        if (current == null) return compiled;

        var visited = new HashSet<string>();

        while (current != null)
        {
            if (visited.Contains(current.Id))
            {
                break;
            }
            visited.Add(current.Id);

            // Create step if action is valid
            if (!string.IsNullOrEmpty(current.Action))
            {
                TestStudioStepModel step;
                if (current.Step != null)
                {
                    step = current.Step;
                    step.Action = current.Action;
                    step.Selector = current.Selector;
                    step.Value = current.Value;
                }
                else
                {
                    step = new TestStudioStepModel
                    {
                        Action = current.Action,
                        Selector = current.Selector,
                        Value = current.Value
                    };
                    current.Step = step;
                }
                compiled.Add(step);
            }

            var connection = Connections.FirstOrDefault(c => c.FromNode == current);
            current = connection?.ToNode as TestStudioNodeViewModel;
        }

        return compiled;
    }

    public void SyncSteps()
    {
        CompiledSteps = CompileSteps();
        SyncToTestStudioAction?.Invoke();
    }
}
