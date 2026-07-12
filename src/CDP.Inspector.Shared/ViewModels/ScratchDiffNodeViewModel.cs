#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using CDP.Editor.Nodes.ViewModels;

namespace CdpInspectorApp.ViewModels;

public class ScratchDiffNodeData
{
    public string? LeftNodeId { get; set; }
    public string? RightNodeId { get; set; }
    public string LeftTitle { get; set; } = "";
    public string RightTitle { get; set; } = "";

    public ScratchDiffNodeData Clone()
    {
        return new ScratchDiffNodeData
        {
            LeftNodeId = this.LeftNodeId,
            RightNodeId = this.RightNodeId,
            LeftTitle = this.LeftTitle,
            RightTitle = this.RightTitle
        };
    }
}

public class ScratchDiffNodeViewModel : ScratchNodeViewModelBase
{
    private string? _leftNodeId;
    private string? _rightNodeId;
    private string _leftTitle = "Left Input";
    private string _rightTitle = "Right Input";
    private ScratchNodeViewModelBase? _leftNode;
    private ScratchNodeViewModelBase? _rightNode;

    public string? LeftNodeId
    {
        get => _leftNodeId;
        set => RaiseAndSetIfChanged(ref _leftNodeId, value);
    }

    public string? RightNodeId
    {
        get => _rightNodeId;
        set => RaiseAndSetIfChanged(ref _rightNodeId, value);
    }

    public ScratchNodeViewModelBase? LeftNode
    {
        get => _leftNode;
        set
        {
            if (RaiseAndSetIfChanged(ref _leftNode, value))
            {
                if (_leftNodeId != value?.Id)
                {
                    _leftNodeId = value?.Id;
                    OnPropertyChanged(nameof(LeftNodeId));
                }
            }
        }
    }

    public ScratchNodeViewModelBase? RightNode
    {
        get => _rightNode;
        set
        {
            if (RaiseAndSetIfChanged(ref _rightNode, value))
            {
                if (_rightNodeId != value?.Id)
                {
                    _rightNodeId = value?.Id;
                    OnPropertyChanged(nameof(RightNodeId));
                }
            }
        }
    }

    public DiffViewModel Diff { get; } = new DiffViewModel();

    public string LeftTitle
    {
        get => _leftTitle;
        set => RaiseAndSetIfChanged(ref _leftTitle, value);
    }

    public string RightTitle
    {
        get => _rightTitle;
        set => RaiseAndSetIfChanged(ref _rightTitle, value);
    }

    public ScratchDiffNodeViewModel()
    {
        TitleBackground = Avalonia.Media.Brush.Parse("#137333");
        BorderBrush = Avalonia.Media.Brush.Parse("#1e8e3e");

        AddInputPin("left", "Left");
        AddInputPin("right", "Right");
        AddOutputPin("diff", "Diff Text");
    }

    public void UpdateDiff(Func<string, ScratchNodeViewModelBase?> getNodeById, IEnumerable<CDP.Editor.Nodes.ViewModels.ConnectionViewModel> connections)
    {
        // Filter incoming connections to this node
        var incoming = connections
            .Where(c => c.ToNode == this && c.FromNode is ScratchNodeViewModelBase)
            .ToList();

        string? resolvedLeftId = LeftNodeId;
        string? resolvedRightId = RightNodeId;

        // Pin-based lookup
        var leftConn = connections.FirstOrDefault(c => c.ToPin?.Owner == this && c.ToPin.Id == "left");
        var rightConn = connections.FirstOrDefault(c => c.ToPin?.Owner == this && c.ToPin.Id == "right");

        if (leftConn?.FromNode is ScratchNodeViewModelBase leftBase)
        {
            resolvedLeftId = leftBase.Id;
        }
        else if (string.IsNullOrEmpty(resolvedLeftId) && incoming.Count > 0)
        {
            resolvedLeftId = incoming[0].FromNode?.Id;
        }

        if (rightConn?.FromNode is ScratchNodeViewModelBase rightBase)
        {
            resolvedRightId = rightBase.Id;
        }
        else if (string.IsNullOrEmpty(resolvedRightId) && incoming.Count > 1)
        {
            resolvedRightId = incoming[1].FromNode?.Id;
        }

        var leftNode = !string.IsNullOrEmpty(resolvedLeftId) ? getNodeById(resolvedLeftId) : null;
        var rightNode = !string.IsNullOrEmpty(resolvedRightId) ? getNodeById(resolvedRightId) : null;

        // Conditionally set properties to avoid raising loops of property change notifications
        bool leftChanged = _leftNode != leftNode;
        bool rightChanged = _rightNode != rightNode;

        if (leftChanged)
        {
            _leftNode = leftNode;
            _leftNodeId = leftNode?.Id;
            OnPropertyChanged(nameof(LeftNode));
            OnPropertyChanged(nameof(LeftNodeId));
        }

        if (rightChanged)
        {
            _rightNode = rightNode;
            _rightNodeId = rightNode?.Id;
            OnPropertyChanged(nameof(RightNode));
            OnPropertyChanged(nameof(RightNodeId));
        }

        string GetNodeSuffix(ScratchNodeViewModelBase? node)
        {
            if (node is ScratchDomNodeViewModel) return " (DOM)";
            if (node is ScratchAccessibilityNodeViewModel) return " (Accessibility)";
            if (node is ScratchConsoleNodeViewModel) return " (Console)";
            if (node is ScratchNetworkNodeViewModel) return " (Network)";
            if (node is ScratchPerformanceNodeViewModel) return " (Performance)";
            if (node is ScratchMvvmNodeViewModel) return " (MVVM)";
            if (node is ScratchApplicationNodeViewModel) return " (Application)";
            if (node is ScratchPageNodeViewModel) return " (Page)";
            if (node is ScratchTimeMachineNodeViewModel) return " (TimeMachine)";
            return "";
        }

        string leftSuffix = GetNodeSuffix(leftNode);
        string rightSuffix = GetNodeSuffix(rightNode);

        LeftTitle = leftNode != null ? $"{leftNode.Name}{leftSuffix}" : "Left (No Input)";
        RightTitle = rightNode != null ? $"{rightNode.Name}{rightSuffix}" : "Right (No Input)";

        string leftText = leftNode?.OutputJson ?? "";
        string rightText = rightNode?.OutputJson ?? "";

        Diff.SetCompareTexts(LeftTitle, leftText, RightTitle, rightText);
        OnPropertyChanged(nameof(OutputJson));
    }

    public override string OutputJson => Diff.DiffLines != null ? System.Text.Json.JsonSerializer.Serialize(Diff.DiffLines) : "[]";
}
