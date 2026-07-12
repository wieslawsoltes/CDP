#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using System.Windows.Input;
using CDP.Editor.Nodes.ViewModels;
using Chrome.DevTools.Protocol;
using CdpInspectorApp.Services;
using Avalonia.Layout;
using CDP.Editor.Splits.Models;

namespace CdpInspectorApp.ViewModels;

public class ScratchViewModel : NodeEditorViewModel, IStateProvider
{
    private readonly ICdpService? _cdpService;
    private readonly ConsoleViewModel? _consoleViewModel;
    private readonly NetworkViewModel? _networkViewModel;

    private NodeViewModel? _selectedNode;
    private SplitNode? _layoutRoot;
    private BoxNode? _selectedPane;

    public NodeViewModel? SelectedNode
    {
        get => _selectedNode;
        set => RaiseAndSetIfChanged(ref _selectedNode, value);
    }

    public SplitNode? LayoutRoot { get => _layoutRoot; set => RaiseAndSetIfChanged(ref _layoutRoot, value); }
    public BoxNode? SelectedPane { get => _selectedPane; set => RaiseAndSetIfChanged(ref _selectedPane, value); }

    public IEnumerable<ScratchNodeViewModelBase> InputNodes => Nodes.OfType<ScratchNodeViewModelBase>().Where(n => n is not ScratchDiffNodeViewModel);

    public Func<Task<string?>>? FileSavePickerHandler { get; set; }
    public Func<Task<string?>>? FileLoadPickerHandler { get; set; }

    public ICommand SaveProjectCommand { get; }
    public ICommand LoadProjectCommand { get; }
    public ICommand ClearCanvasCommand { get; }
    public ICommand AddScratchNodeCommand { get; }
    public ICommand AddDomNodeFromTreeCommand { get; }
    public ICommand AddAxNodeFromTreeCommand { get; }
    public ICommand AddMvvmNodeFromTreeCommand { get; }

    public class AddNodeParameters
    {
        public string NodeType { get; }
        public double? X { get; }
        public double? Y { get; }

        public AddNodeParameters(string nodeType, double? x = null, double? y = null)
        {
            NodeType = nodeType;
            X = x;
            Y = y;
        }
    }

    public static ScratchViewModel? Instance { get; private set; }

    public ScratchViewModel() : this(null, null, null)
    {
    }

    public ScratchViewModel(
        ICdpService? cdpService,
        ConsoleViewModel? consoleViewModel,
        NetworkViewModel? networkViewModel)
    {
        Instance = this;
        _cdpService = cdpService;
        _consoleViewModel = consoleViewModel;
        _networkViewModel = networkViewModel;

        var toolbox = new BoxNode("ScratchToolbox", "Toolbox", "FlowchartIcon") { BackgroundTint = "#28292c" };
        var canvas = new BoxNode("ScratchCanvas", "Scratch Canvas", "DeveloperBoardIcon") { BackgroundTint = "#202124" };
        var details = new BoxNode("ScratchDetails", "Node Details", "AppsIcon") { BackgroundTint = "#28292c" };
        var rightSplit = new SplitContainerNode(Orientation.Horizontal, canvas, details) { SplitterRatio = 0.7 };
        LayoutRoot = new SplitContainerNode(Orientation.Horizontal, toolbox, rightSplit) { SplitterRatio = 0.15 };
        SelectedPane = canvas;

        // Custom handlers for copying/pasting node data
        GetNodeCustomDataHandler = node =>
        {
            if (node is ScratchDomNodeViewModel domNode)
            {
                return new ScratchDomNodeData
                {
                    RawJsonData = domNode.RawJsonData,
                    Timestamp = domNode.Timestamp
                };
            }
            if (node is ScratchAccessibilityNodeViewModel a11yNode)
            {
                return new ScratchAccessibilityNodeData
                {
                    RawJsonData = a11yNode.RawJsonData,
                    Timestamp = a11yNode.Timestamp
                };
            }
            if (node is ScratchConsoleNodeViewModel consoleNode)
            {
                return new ScratchConsoleNodeData
                {
                    RawJsonData = consoleNode.RawJsonData,
                    Timestamp = consoleNode.Timestamp,
                    ConsoleLogsJson = consoleNode.ConsoleLogsJson,
                    ErrorsCount = consoleNode.ErrorsCount,
                    WarningsCount = consoleNode.WarningsCount
                };
            }
            if (node is ScratchNetworkNodeViewModel networkNode)
            {
                return new ScratchNetworkNodeData
                {
                    RawJsonData = networkNode.RawJsonData,
                    Timestamp = networkNode.Timestamp
                };
            }
            if (node is ScratchPerformanceNodeViewModel perfNode)
            {
                return new ScratchPerformanceNodeData
                {
                    RawJsonData = perfNode.RawJsonData,
                    Timestamp = perfNode.Timestamp
                };
            }
            if (node is ScratchMvvmNodeViewModel mvvmNode)
            {
                return new ScratchMvvmNodeData
                {
                    InputNodeId = mvvmNode.InputNodeId,
                    InputTitle = mvvmNode.InputTitle,
                    MvvmTreeJson = mvvmNode.MvvmTreeJson,
                    CurrentVmType = mvvmNode.CurrentVmType,
                    PropertiesCount = mvvmNode.PropertiesCount,
                    Timestamp = mvvmNode.Timestamp
                };
            }
            if (node is ScratchApplicationNodeViewModel appNode)
            {
                return new ScratchApplicationNodeData
                {
                    RawJsonData = appNode.RawJsonData,
                    Timestamp = appNode.Timestamp
                };
            }
            if (node is ScratchPageNodeViewModel pageNode)
            {
                return new ScratchPageNodeData
                {
                    ScreenshotBase64 = pageNode.ScreenshotBase64,
                    IsSyncedWithTimeMachine = pageNode.IsSyncedWithTimeMachine,
                    PinnedFrameIndex = pageNode.PinnedFrameIndex
                };
            }
            if (node is ScratchImageDiffNodeViewModel imgDiffNode)
            {
                return new ScratchImageDiffNodeData
                {
                    LeftNodeId = imgDiffNode.LeftNodeId,
                    RightNodeId = imgDiffNode.RightNodeId,
                    LeftTitle = imgDiffNode.LeftTitle,
                    RightTitle = imgDiffNode.RightTitle,
                    DiffPercentage = imgDiffNode.DiffPercentage
                };
            }
            if (node is ScratchDiffNodeViewModel diffNode)
            {
                return new ScratchDiffNodeData
                {
                    LeftNodeId = diffNode.LeftNodeId,
                    RightNodeId = diffNode.RightNodeId,
                    LeftTitle = diffNode.LeftTitle,
                    RightTitle = diffNode.RightTitle
                };
            }
            if (node is ScratchTimeMachineNodeViewModel tmNode)
            {
                return new ScratchTimeMachineNodeData
                {
                    SelectedFramePayloadText = tmNode.SelectedFramePayloadText,
                    IsPinned = tmNode.IsPinned,
                    PinnedFrameIndex = tmNode.PinnedFrameIndex
                };
            }
            if (node is ScratchAssertionNodeViewModel assertNode)
            {
                return new ScratchAssertionNodeData
                {
                    InputNodeId = assertNode.InputNodeId,
                    InputTitle = assertNode.InputTitle,
                    Path = assertNode.Path,
                    ExpectedValue = assertNode.ExpectedValue,
                    Operator = assertNode.Operator
                };
            }
            return null;
        };

        SetNodeCustomDataHandler = (node, data) =>
        {
            if (node is ScratchDomNodeViewModel domNode && data is ScratchDomNodeData domData)
            {
                domNode.RawJsonData = domData.RawJsonData;
                domNode.Timestamp = domData.Timestamp;
            }
            else if (node is ScratchAccessibilityNodeViewModel a11yNode && data is ScratchAccessibilityNodeData a11yData)
            {
                a11yNode.RawJsonData = a11yData.RawJsonData;
                a11yNode.Timestamp = a11yData.Timestamp;
            }
            else if (node is ScratchConsoleNodeViewModel consoleNode && data is ScratchConsoleNodeData consoleData)
            {
                consoleNode.RawJsonData = consoleData.RawJsonData;
                consoleNode.Timestamp = consoleData.Timestamp;
                consoleNode.ConsoleLogsJson = consoleData.ConsoleLogsJson;
                consoleNode.ErrorsCount = consoleData.ErrorsCount;
                consoleNode.WarningsCount = consoleData.WarningsCount;
            }
            else if (node is ScratchNetworkNodeViewModel networkNode && data is ScratchNetworkNodeData networkData)
            {
                networkNode.RawJsonData = networkData.RawJsonData;
                networkNode.Timestamp = networkData.Timestamp;
            }
            else if (node is ScratchPerformanceNodeViewModel perfNode && data is ScratchPerformanceNodeData perfData)
            {
                perfNode.RawJsonData = perfData.RawJsonData;
                perfNode.Timestamp = perfData.Timestamp;
            }
            else if (node is ScratchMvvmNodeViewModel mvvmNode && data is ScratchMvvmNodeData mvvmData)
            {
                mvvmNode.InputNodeId = mvvmData.InputNodeId;
                mvvmNode.InputTitle = mvvmData.InputTitle;
                mvvmNode.MvvmTreeJson = mvvmData.MvvmTreeJson;
                mvvmNode.CurrentVmType = mvvmData.CurrentVmType;
                mvvmNode.PropertiesCount = mvvmData.PropertiesCount;
                mvvmNode.Timestamp = mvvmData.Timestamp;
            }
            else if (node is ScratchApplicationNodeViewModel appNode && data is ScratchApplicationNodeData appData)
            {
                appNode.RawJsonData = appData.RawJsonData;
                appNode.Timestamp = appData.Timestamp;
            }
            else if (node is ScratchPageNodeViewModel pageNode && data is ScratchPageNodeData pageData)
            {
                pageNode.IsSyncedWithTimeMachine = pageData.IsSyncedWithTimeMachine;
                pageNode.PinnedFrameIndex = pageData.PinnedFrameIndex;
                pageNode.ScreenshotBase64 = pageData.ScreenshotBase64;
            }
            else if (node is ScratchImageDiffNodeViewModel imgDiffNode && data is ScratchImageDiffNodeData imgDiffData)
            {
                imgDiffNode.LeftNodeId = imgDiffData.LeftNodeId;
                imgDiffNode.RightNodeId = imgDiffData.RightNodeId;
                imgDiffNode.LeftTitle = imgDiffData.LeftTitle;
                imgDiffNode.RightTitle = imgDiffData.RightTitle;
                imgDiffNode.DiffPercentage = imgDiffData.DiffPercentage;
            }
            else if (node is ScratchDiffNodeViewModel diffNode && data is ScratchDiffNodeData diffNodeData)
            {
                diffNode.LeftNodeId = diffNodeData.LeftNodeId;
                diffNode.RightNodeId = diffNodeData.RightNodeId;
                diffNode.LeftTitle = diffNodeData.LeftTitle;
                diffNode.RightTitle = diffNodeData.RightTitle;
            }
            else if (node is ScratchTimeMachineNodeViewModel tmNode && data is ScratchTimeMachineNodeData tmData)
            {
                tmNode.SelectedFramePayloadText = tmData.SelectedFramePayloadText;
                tmNode.IsPinned = tmData.IsPinned;
                tmNode.PinnedFrameIndex = tmData.PinnedFrameIndex;
            }
            else if (node is ScratchAssertionNodeViewModel assertNode && data is ScratchAssertionNodeData assertData)
            {
                assertNode.InputNodeId = assertData.InputNodeId;
                assertNode.InputTitle = assertData.InputTitle;
                assertNode.Path = assertData.Path;
                assertNode.ExpectedValue = assertData.ExpectedValue;
                assertNode.Operator = assertData.Operator;
            }
        };

        // When a node is selected, update SelectedNode
        NodeSelectedAction = node =>
        {
            SelectedNode = node;
        };

        // Default factory for creating new nodes in the diagram
        CreateNodeHandler = () => new ScratchDomNodeViewModel(_cdpService);

        // Automatically update all diff nodes when nodes/connections list changes
        Nodes.CollectionChanged += (sender, e) =>
        {
            if (e.OldItems != null)
            {
                foreach (var item in e.OldItems)
                {
                    if (item is IDisposable disposable)
                    {
                        disposable.Dispose();
                    }
                }
            }
            UpdateAllDiffNodes();
            UpdateAllImageDiffNodes();
            UpdateAllAssertionNodes();
            UpdateAllMvvmNodes();
            OnPropertyChanged(nameof(InputNodes));
        };
        Connections.CollectionChanged += (sender, e) =>
        {
            UpdateAllDiffNodes();
            UpdateAllImageDiffNodes();
            UpdateAllAssertionNodes();
            UpdateAllMvvmNodes();
        };

        SaveProjectCommand = new RelayCommand(async () =>
        {
            if (FileSavePickerHandler != null)
            {
                var path = await FileSavePickerHandler();
                if (!string.IsNullOrEmpty(path))
                {
                    await SaveProjectAsync(path);
                }
            }
        });

        LoadProjectCommand = new RelayCommand(async () =>
        {
            if (FileLoadPickerHandler != null)
            {
                var path = await FileLoadPickerHandler();
                if (!string.IsNullOrEmpty(path))
                {
                    await LoadProjectAsync(path);
                }
            }
        });

        ClearCanvasCommand = new RelayCommand(() =>
        {
            foreach (var node in Nodes)
            {
                if (node is IDisposable disposable)
                {
                    disposable.Dispose();
                }
            }
            Nodes.Clear();
            Connections.Clear();
            SelectedNode = null;
        });

        AddDomNodeFromTreeCommand = new RelayCommand<object>(param =>
        {
            var model = param as CdpInspectorApp.Models.DomNodeModel;
            if (model == null) return;
            
            var node = new ScratchDomNodeViewModel(_cdpService)
            {
                X = 150,
                Y = 150,
                LinkedElementId = model.NodeId.ToString(),
                LinkedElementName = model.DisplayName
            };
            
            Nodes.Add(node);
            SelectedNode = node;
            node.CaptureCommand.Execute(null);

            var mainVm = MainWindowViewModel.Instance;
            if (mainVm != null)
            {
                var scratchBox = mainVm.FindBoxNodeByViewName(mainVm.LayoutRoot, "Scratch");
                if (scratchBox != null)
                {
                    scratchBox.SelectedViewName = "Scratch";
                }
            }
        });

        AddAxNodeFromTreeCommand = new RelayCommand<object>(param =>
        {
            var model = param as CdpInspectorApp.Models.AxNodeModel;
            if (model == null) return;

            var node = new ScratchAccessibilityNodeViewModel(_cdpService)
            {
                X = 150,
                Y = 150,
                LinkedElementId = model.NodeId,
                LinkedElementName = model.DisplayName
            };

            Nodes.Add(node);
            SelectedNode = node;
            node.CaptureCommand.Execute(null);

            var mainVm = MainWindowViewModel.Instance;
            if (mainVm != null)
            {
                var scratchBox = mainVm.FindBoxNodeByViewName(mainVm.LayoutRoot, "Scratch");
                if (scratchBox != null)
                {
                    scratchBox.SelectedViewName = "Scratch";
                }
            }
        });

        AddMvvmNodeFromTreeCommand = new RelayCommand<object>(param =>
        {
            var model = param as CdpInspectorApp.ViewModels.InspectorViewModelNode;
            if (model == null) return;

            var node = new ScratchMvvmNodeViewModel(_cdpService)
            {
                X = 150,
                Y = 150,
                LinkedElementId = model.Id,
                LinkedElementName = model.DisplayName
            };

            Nodes.Add(node);
            SelectedNode = node;
            node.CaptureCommand.Execute(null);

            var mainVm = MainWindowViewModel.Instance;
            if (mainVm != null)
            {
                var scratchBox = mainVm.FindBoxNodeByViewName(mainVm.LayoutRoot, "Scratch");
                if (scratchBox != null)
                {
                    scratchBox.SelectedViewName = "Scratch";
                }
            }
        });

        AddScratchNodeCommand = new RelayCommand<object>(param =>
        {
            if (param == null) return;
            string nodeType = "";
            double? customX = null;
            double? customY = null;

            if (param is string str)
            {
                nodeType = str;
            }
            else if (param is AddNodeParameters p)
            {
                nodeType = p.NodeType;
                customX = p.X;
                customY = p.Y;
            }

            if (string.IsNullOrEmpty(nodeType)) return;

            if (nodeType.Equals("Diff", StringComparison.OrdinalIgnoreCase))
            {
                var node = new ScratchDiffNodeViewModel()
                {
                    Name = "Diff Node",
                    X = customX ?? (20 + Nodes.Count * 200),
                    Y = customY ?? 20
                };
                Nodes.Add(node);
                SelectNode(node, true);
            }
            else if (nodeType.Equals("ImageDiff", StringComparison.OrdinalIgnoreCase) || nodeType.Equals("Image Diff", StringComparison.OrdinalIgnoreCase))
            {
                var node = new ScratchImageDiffNodeViewModel()
                {
                    Name = "Image Diff Node",
                    X = customX ?? (20 + Nodes.Count * 200),
                    Y = customY ?? 20
                };
                Nodes.Add(node);
                SelectNode(node, true);
            }
            else if (nodeType.Equals("Page", StringComparison.OrdinalIgnoreCase) || nodeType.Equals("Screenshot", StringComparison.OrdinalIgnoreCase))
            {
                var node = new ScratchPageNodeViewModel(_cdpService)
                {
                    Name = "Page Node",
                    X = customX ?? (20 + Nodes.Count * 200),
                    Y = customY ?? 20
                };
                Nodes.Add(node);
                SelectNode(node, true);
            }
            else if (nodeType.Equals("TimeMachine", StringComparison.OrdinalIgnoreCase))
            {
                var node = new ScratchTimeMachineNodeViewModel(_cdpService)
                {
                    Name = "Time Machine Node",
                    X = customX ?? (20 + Nodes.Count * 200),
                    Y = customY ?? 20
                };
                Nodes.Add(node);
                SelectNode(node, true);
            }
            else if (nodeType.Equals("Assertion", StringComparison.OrdinalIgnoreCase))
            {
                var node = new ScratchAssertionNodeViewModel()
                {
                    Name = "Assertion Node",
                    X = customX ?? (20 + Nodes.Count * 200),
                    Y = customY ?? 20
                };
                Nodes.Add(node);
                SelectNode(node, true);
            }
            else if (nodeType.Equals("Console", StringComparison.OrdinalIgnoreCase))
            {
                var node = new ScratchConsoleNodeViewModel(_cdpService, _consoleViewModel)
                {
                    Name = "Console Node",
                    X = customX ?? (20 + Nodes.Count * 200),
                    Y = customY ?? 20
                };
                Nodes.Add(node);
                SelectNode(node, true);
            }
            else if (nodeType.Equals("DOM", StringComparison.OrdinalIgnoreCase))
            {
                var node = new ScratchDomNodeViewModel(_cdpService)
                {
                    Name = "DOM Node",
                    X = customX ?? (20 + Nodes.Count * 200),
                    Y = customY ?? 20
                };
                Nodes.Add(node);
                SelectNode(node, true);
            }
            else if (nodeType.Equals("Accessibility", StringComparison.OrdinalIgnoreCase))
            {
                var node = new ScratchAccessibilityNodeViewModel(_cdpService)
                {
                    Name = "Accessibility Node",
                    X = customX ?? (20 + Nodes.Count * 200),
                    Y = customY ?? 20
                };
                Nodes.Add(node);
                SelectNode(node, true);
            }
            else if (nodeType.Equals("Network", StringComparison.OrdinalIgnoreCase))
            {
                var node = new ScratchNetworkNodeViewModel(_cdpService, _networkViewModel)
                {
                    Name = "Network Node",
                    X = customX ?? (20 + Nodes.Count * 200),
                    Y = customY ?? 20
                };
                Nodes.Add(node);
                SelectNode(node, true);
            }
            else if (nodeType.Equals("Performance", StringComparison.OrdinalIgnoreCase))
            {
                var node = new ScratchPerformanceNodeViewModel(_cdpService)
                {
                    Name = "Performance Node",
                    X = customX ?? (20 + Nodes.Count * 200),
                    Y = customY ?? 20
                };
                Nodes.Add(node);
                SelectNode(node, true);
            }
            else if (nodeType.Equals("MVVM", StringComparison.OrdinalIgnoreCase))
            {
                var node = new ScratchMvvmNodeViewModel(_cdpService)
                {
                    Name = "MVVM Node",
                    X = customX ?? (20 + Nodes.Count * 200),
                    Y = customY ?? 20
                };
                Nodes.Add(node);
                SelectNode(node, true);
            }
            else if (nodeType.Equals("Application", StringComparison.OrdinalIgnoreCase))
            {
                var node = new ScratchApplicationNodeViewModel(_cdpService)
                {
                    Name = "Application Node",
                    X = customX ?? (20 + Nodes.Count * 200),
                    Y = customY ?? 20
                };
                Nodes.Add(node);
                SelectNode(node, true);
            }
        });
    }

    private bool _isUpdatingNodes;

    protected override void OnNodePropertyChanged(NodeViewModel node, string? propertyName)
    {
        base.OnNodePropertyChanged(node, propertyName);

        if (node is ScratchDomNodeViewModel && propertyName == nameof(ScratchDomNodeViewModel.RawJsonData))
        {
            PropagateNodeUpdate(node);
        }
        else if (node is ScratchAccessibilityNodeViewModel && propertyName == nameof(ScratchAccessibilityNodeViewModel.RawJsonData))
        {
            PropagateNodeUpdate(node);
        }
        else if (node is ScratchConsoleNodeViewModel && propertyName == nameof(ScratchConsoleNodeViewModel.RawJsonData))
        {
            PropagateNodeUpdate(node);
        }
        else if (node is ScratchNetworkNodeViewModel && propertyName == nameof(ScratchNetworkNodeViewModel.RawJsonData))
        {
            PropagateNodeUpdate(node);
        }
        else if (node is ScratchPerformanceNodeViewModel && propertyName == nameof(ScratchPerformanceNodeViewModel.RawJsonData))
        {
            PropagateNodeUpdate(node);
        }
        else if (node is ScratchMvvmNodeViewModel mvvmNode &&
                 (propertyName == nameof(ScratchMvvmNodeViewModel.InputNodeId) ||
                  propertyName == nameof(ScratchMvvmNodeViewModel.RawJsonData)))
        {
            UpdateMvvmNode(mvvmNode);
            PropagateNodeUpdate(mvvmNode);
        }
        else if (node is ScratchApplicationNodeViewModel && propertyName == nameof(ScratchApplicationNodeViewModel.RawJsonData))
        {
            PropagateNodeUpdate(node);
        }
        else if (node is ScratchPageNodeViewModel &&
                 (propertyName == nameof(ScratchPageNodeViewModel.ScreenshotBase64) ||
                  propertyName == nameof(ScratchPageNodeViewModel.OutputJson)))
        {
            PropagateNodeUpdate(node);
        }
        else if (node is ScratchImageDiffNodeViewModel && propertyName == nameof(ScratchImageDiffNodeViewModel.OutputJson))
        {
            PropagateNodeUpdate(node);
        }
        else if (node is ScratchImageDiffNodeViewModel imgDiffNode &&
                 (propertyName == nameof(ScratchImageDiffNodeViewModel.LeftNodeId) ||
                  propertyName == nameof(ScratchImageDiffNodeViewModel.RightNodeId)))
        {
            UpdateImageDiffNode(imgDiffNode);
            PropagateNodeUpdate(imgDiffNode);
        }
        else if (node is ScratchTimeMachineNodeViewModel && propertyName == nameof(ScratchTimeMachineNodeViewModel.SelectedFramePayloadText))
        {
            PropagateNodeUpdate(node);
        }
        else if (node is ScratchAssertionNodeViewModel && propertyName == nameof(ScratchAssertionNodeViewModel.OutputJson))
        {
            PropagateNodeUpdate(node);
        }
        else if (node is ScratchDiffNodeViewModel diffNode && propertyName == nameof(ScratchDiffNodeViewModel.OutputJson))
        {
            PropagateNodeUpdate(diffNode);
        }
        else if (node is ScratchDiffNodeViewModel diffNode2 &&
                 (propertyName == nameof(ScratchDiffNodeViewModel.LeftNodeId) ||
                  propertyName == nameof(ScratchDiffNodeViewModel.RightNodeId)))
        {
            UpdateDiffNode(diffNode2);
            PropagateNodeUpdate(diffNode2);
        }
        else if (node is ScratchAssertionNodeViewModel assertNode &&
                 (propertyName == nameof(ScratchAssertionNodeViewModel.InputNodeId) ||
                  propertyName == nameof(ScratchAssertionNodeViewModel.Path) ||
                  propertyName == nameof(ScratchAssertionNodeViewModel.ExpectedValue) ||
                  propertyName == nameof(ScratchAssertionNodeViewModel.Operator)))
        {
            UpdateAssertionNode(assertNode);
            PropagateNodeUpdate(assertNode);
        }
    }

    public void UpdateAllDiffNodes()
    {
        if (_isUpdatingNodes) return;
        _isUpdatingNodes = true;
        try
        {
            var diffNodes = Nodes.OfType<ScratchDiffNodeViewModel>().ToList();
            foreach (var diffNode in diffNodes)
            {
                UpdateDiffNode(diffNode);
            }
        }
        finally
        {
            _isUpdatingNodes = false;
        }
    }

    private void UpdateDiffNode(ScratchDiffNodeViewModel diffNode)
    {
        diffNode.UpdateDiff(
            id => Nodes.OfType<ScratchNodeViewModelBase>().FirstOrDefault(n => n.Id == id),
            Connections
        );
    }

    public void UpdateAllAssertionNodes()
    {
        if (_isUpdatingNodes) return;
        _isUpdatingNodes = true;
        try
        {
            var assertNodes = Nodes.OfType<ScratchAssertionNodeViewModel>().ToList();
            foreach (var assertNode in assertNodes)
            {
                UpdateAssertionNode(assertNode);
            }
        }
        finally
        {
            _isUpdatingNodes = false;
        }
    }

    private void UpdateAssertionNode(ScratchAssertionNodeViewModel assertNode)
    {
        assertNode.UpdateAssertion(
            id => Nodes.OfType<ScratchNodeViewModelBase>().FirstOrDefault(n => n.Id == id),
            Connections
        );
    }

    public void UpdateAllAccessibilityNodes()
    {
        if (_isUpdatingNodes) return;
        _isUpdatingNodes = true;
        try
        {
            var a11yNodes = Nodes.OfType<ScratchAccessibilityNodeViewModel>().ToList();
            foreach (var a11yNode in a11yNodes)
            {
                UpdateAccessibilityNode(a11yNode);
            }
        }
        finally
        {
            _isUpdatingNodes = false;
        }
    }

    private void UpdateAccessibilityNode(ScratchAccessibilityNodeViewModel a11yNode)
    {
        a11yNode.UpdateAccessibility(
            id => Nodes.OfType<ScratchNodeViewModelBase>().FirstOrDefault(n => n.Id == id),
            Connections
        );
    }

    public void UpdateAllImageDiffNodes()
    {
        if (_isUpdatingNodes) return;
        _isUpdatingNodes = true;
        try
        {
            var imgDiffNodes = Nodes.OfType<ScratchImageDiffNodeViewModel>().ToList();
            foreach (var imgDiffNode in imgDiffNodes)
            {
                UpdateImageDiffNode(imgDiffNode);
            }
        }
        finally
        {
            _isUpdatingNodes = false;
        }
    }

    private void UpdateImageDiffNode(ScratchImageDiffNodeViewModel imgDiffNode)
    {
        imgDiffNode.UpdateDiff(
            id => Nodes.OfType<ScratchNodeViewModelBase>().FirstOrDefault(n => n.Id == id),
            Connections
        );
    }

    public void UpdateAllMvvmNodes()
    {
        if (_isUpdatingNodes) return;
        _isUpdatingNodes = true;
        try
        {
            var mvvmNodes = Nodes.OfType<ScratchMvvmNodeViewModel>().ToList();
            foreach (var mvvmNode in mvvmNodes)
            {
                UpdateMvvmNode(mvvmNode);
            }
        }
        finally
        {
            _isUpdatingNodes = false;
        }
    }

    private void UpdateMvvmNode(ScratchMvvmNodeViewModel mvvmNode)
    {
        mvvmNode.UpdateMvvm(
            id => Nodes.OfType<ScratchNodeViewModelBase>().FirstOrDefault(n => n.Id == id),
            Connections
        );
    }

    private readonly HashSet<NodeViewModel> _propagatingNodes = new();

    public void PropagateNodeUpdate(NodeViewModel node)
    {
        if (node == null) return;
        if (!_propagatingNodes.Add(node)) return;

        try
        {
            var outgoing = Connections.Where(c => c.FromNode == node).ToList();
            foreach (var conn in outgoing)
            {
                var target = conn.ToNode;
                if (target is ScratchDiffNodeViewModel diffNode)
                {
                    UpdateDiffNode(diffNode);
                    PropagateNodeUpdate(diffNode);
                }
                else if (target is ScratchImageDiffNodeViewModel imgDiffNode)
                {
                    UpdateImageDiffNode(imgDiffNode);
                    PropagateNodeUpdate(imgDiffNode);
                }
                else if (target is ScratchAssertionNodeViewModel assertNode)
                {
                    UpdateAssertionNode(assertNode);
                    PropagateNodeUpdate(assertNode);
                }
                else if (target is ScratchAccessibilityNodeViewModel a11yNode)
                {
                    UpdateAccessibilityNode(a11yNode);
                    PropagateNodeUpdate(a11yNode);
                }
                else if (target is ScratchMvvmNodeViewModel mvvmNode)
                {
                    UpdateMvvmNode(mvvmNode);
                    PropagateNodeUpdate(mvvmNode);
                }
            }
        }
        finally
        {
            _propagatingNodes.Remove(node);
        }
    }

    public async Task SaveProjectAsync(string filePath)
    {
        var json = SaveProject();
        if (json != null)
        {
            var content = json.ToJsonString(new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
            await System.IO.File.WriteAllTextAsync(filePath, content);
        }
    }

    public async Task LoadProjectAsync(string filePath)
    {
        if (System.IO.File.Exists(filePath))
        {
            var content = await System.IO.File.ReadAllTextAsync(filePath);
            var json = JsonNode.Parse(content);
            LoadProject(json);
        }
    }

    public JsonNode? SaveProject()
    {
        var root = new JsonObject();
        root["zoom"] = Zoom;
        root["panX"] = PanX;
        root["panY"] = PanY;

        var nodesArray = new JsonArray();
        foreach (var node in Nodes)
        {
            var nodeJson = new JsonObject
            {
                ["id"] = node.Id,
                ["name"] = node.Name,
                ["x"] = node.X,
                ["y"] = node.Y,
                ["width"] = node.Width,
                ["height"] = node.Height
            };

            if (node is ScratchDomNodeViewModel domNode)
            {
                nodeJson["type"] = "DomNode";
                nodeJson["rawJsonData"] = domNode.RawJsonData;
                nodeJson["timestamp"] = domNode.Timestamp?.ToString("o");
                nodeJson["searchTerm"] = domNode.SearchTerm;
            }
            else if (node is ScratchAccessibilityNodeViewModel a11yNode)
            {
                nodeJson["type"] = "A11yNode";
                nodeJson["rawJsonData"] = a11yNode.RawJsonData;
                nodeJson["timestamp"] = a11yNode.Timestamp?.ToString("o");
                nodeJson["inputNodeId"] = a11yNode.InputNodeId;
                nodeJson["inputTitle"] = a11yNode.InputTitle;
                nodeJson["a11yTreeJson"] = a11yNode.A11yTreeJson;
                nodeJson["warningsCount"] = a11yNode.WarningsCount;
                nodeJson["nodeCount"] = a11yNode.NodeCount;
            }
            else if (node is ScratchConsoleNodeViewModel consoleNode)
            {
                nodeJson["type"] = "ConsoleNode";
                nodeJson["rawJsonData"] = consoleNode.RawJsonData;
                nodeJson["timestamp"] = consoleNode.Timestamp?.ToString("o");
                nodeJson["consoleLogsJson"] = consoleNode.ConsoleLogsJson;
                nodeJson["errorsCount"] = consoleNode.ErrorsCount;
                nodeJson["warningsCount"] = consoleNode.WarningsCount;
            }
            else if (node is ScratchNetworkNodeViewModel networkNode)
            {
                nodeJson["type"] = "NetworkNode";
                nodeJson["rawJsonData"] = networkNode.RawJsonData;
                nodeJson["timestamp"] = networkNode.Timestamp?.ToString("o");
            }
            else if (node is ScratchPerformanceNodeViewModel perfNode)
            {
                nodeJson["type"] = "PerfNode";
                nodeJson["rawJsonData"] = perfNode.RawJsonData;
                nodeJson["timestamp"] = perfNode.Timestamp?.ToString("o");
            }
            else if (node is ScratchMvvmNodeViewModel mvvmNode)
            {
                nodeJson["type"] = "MvvmNode";
                nodeJson["rawJsonData"] = mvvmNode.RawJsonData;
                nodeJson["timestamp"] = mvvmNode.Timestamp?.ToString("o");
            }
            else if (node is ScratchApplicationNodeViewModel appNode)
            {
                nodeJson["type"] = "AppNode";
                nodeJson["rawJsonData"] = appNode.RawJsonData;
                nodeJson["timestamp"] = appNode.Timestamp?.ToString("o");
            }
            else if (node is ScratchPageNodeViewModel pageNode)
            {
                nodeJson["type"] = "PageNode";
                nodeJson["screenshotBase64"] = pageNode.ScreenshotBase64;
                nodeJson["isSyncedWithTimeMachine"] = pageNode.IsSyncedWithTimeMachine;
                nodeJson["pinnedFrameIndex"] = pageNode.PinnedFrameIndex;
            }
            else if (node is ScratchImageDiffNodeViewModel imgDiffNode)
            {
                nodeJson["type"] = "ImageDiffNode";
                nodeJson["leftNodeId"] = imgDiffNode.LeftNodeId;
                nodeJson["rightNodeId"] = imgDiffNode.RightNodeId;
                nodeJson["leftTitle"] = imgDiffNode.LeftTitle;
                nodeJson["rightTitle"] = imgDiffNode.RightTitle;
                nodeJson["diffPercentage"] = imgDiffNode.DiffPercentage;
            }
            else if (node is ScratchDiffNodeViewModel diffNode)
            {
                nodeJson["type"] = "DiffNode";
                nodeJson["leftNodeId"] = diffNode.LeftNodeId;
                nodeJson["rightNodeId"] = diffNode.RightNodeId;
                nodeJson["leftTitle"] = diffNode.LeftTitle;
                nodeJson["rightTitle"] = diffNode.RightTitle;
            }
            else if (node is ScratchTimeMachineNodeViewModel tmNode)
            {
                nodeJson["type"] = "TimeMachineNode";
                nodeJson["selectedFramePayloadText"] = tmNode.SelectedFramePayloadText;
                nodeJson["isPinned"] = tmNode.IsPinned;
                nodeJson["pinnedFrameIndex"] = tmNode.PinnedFrameIndex;
            }
            else if (node is ScratchAssertionNodeViewModel assertNode)
            {
                nodeJson["type"] = "AssertionNode";
                nodeJson["inputNodeId"] = assertNode.InputNodeId;
                nodeJson["inputTitle"] = assertNode.InputTitle;
                nodeJson["path"] = assertNode.Path;
                nodeJson["expectedValue"] = assertNode.ExpectedValue;
                nodeJson["operator"] = assertNode.Operator.ToString();
            }
            else
            {
                nodeJson["type"] = "Unknown";
            }

            nodesArray.Add(nodeJson);
        }
        root["nodes"] = nodesArray;

        var connectionsArray = new JsonArray();
        foreach (var conn in Connections)
        {
            if (conn.FromNode != null && conn.ToNode != null)
            {
                var connJson = new JsonObject
                {
                    ["fromNodeId"] = conn.FromNode.Id,
                    ["toNodeId"] = conn.ToNode.Id,
                    ["fromPinId"] = conn.FromPin?.Id ?? "",
                    ["toPinId"] = conn.ToPin?.Id ?? ""
                };
                connectionsArray.Add(connJson);
            }
        }
        root["connections"] = connectionsArray;

        return root;
    }

    public void LoadProject(JsonNode? projectNode)
    {
        if (projectNode is not JsonObject json) return;

        foreach (var oldNode in Nodes)
        {
            if (oldNode is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }

        Nodes.Clear();
        Connections.Clear();

        if (json.TryGetPropertyValue("zoom", out var zoomVal) && zoomVal != null) Zoom = (double?)zoomVal ?? 1.0;
        if (json.TryGetPropertyValue("panX", out var panXVal) && panXVal != null) PanX = (double?)panXVal ?? 0.0;
        if (json.TryGetPropertyValue("panY", out var panYVal) && panYVal != null) PanY = (double?)panYVal ?? 0.0;

        var nodeMap = new Dictionary<string, NodeViewModel>();

        if (json.TryGetPropertyValue("nodes", out var nodesNode) && nodesNode is JsonArray nodesArray)
        {
            foreach (var nItem in nodesArray)
            {
                if (nItem is not JsonObject nObj) continue;

                var id = (string?)nObj["id"] ?? Guid.NewGuid().ToString();
                var name = (string?)nObj["name"] ?? "";
                var x = (double?)nObj["x"] ?? 0.0;
                var y = (double?)nObj["y"] ?? 0.0;
                var width = (double?)nObj["width"] ?? 160.0;
                var height = (double?)nObj["height"] ?? 100.0;
                var type = (string?)nObj["type"];

                NodeViewModel node;
                if (type == "DomNode")
                {
                    var rawJson = (string?)nObj["rawJsonData"] ?? "";
                    var searchTerm = (string?)nObj["searchTerm"] ?? "";
                    DateTime? timestamp = null;
                    if (nObj.TryGetPropertyValue("timestamp", out var tsNode) && tsNode != null && DateTime.TryParse((string?)tsNode, out var ts))
                    {
                        timestamp = ts;
                    }

                    node = new ScratchDomNodeViewModel(_cdpService)
                    {
                        Id = id,
                        Name = name,
                        X = x,
                        Y = y,
                        Width = width,
                        Height = height,
                        RawJsonData = rawJson,
                        SearchTerm = searchTerm,
                        Timestamp = timestamp
                    };
                }
                else if (type == "A11yNode")
                {
                    var rawJson = (string?)nObj["rawJsonData"] ?? "";
                    DateTime? timestamp = null;
                    if (nObj.TryGetPropertyValue("timestamp", out var tsNode) && tsNode != null && DateTime.TryParse((string?)tsNode, out var ts))
                    {
                        timestamp = ts;
                    }
                    var inputNodeId = (string?)nObj["inputNodeId"];
                    var inputTitle = (string?)nObj["inputTitle"] ?? "";
                    var a11yTreeJson = (string?)nObj["a11yTreeJson"] ?? "{}";
                    var warningsCount = (int?)(nObj["warningsCount"]) ?? 0;
                    var nodeCount = (int?)(nObj["nodeCount"]) ?? 0;

                    node = new ScratchAccessibilityNodeViewModel(_cdpService)
                    {
                        Id = id,
                        Name = name,
                        X = x,
                        Y = y,
                        Width = width,
                        Height = height,
                        RawJsonData = rawJson,
                        Timestamp = timestamp,
                        InputNodeId = inputNodeId,
                        InputTitle = inputTitle,
                        A11yTreeJson = a11yTreeJson,
                        WarningsCount = warningsCount,
                        NodeCount = nodeCount
                    };
                }
                else if (type == "ConsoleNode")
                {
                    var rawJson = (string?)nObj["rawJsonData"] ?? "";
                    var logsJson = (string?)nObj["consoleLogsJson"] ?? "[]";
                    var errs = (int?)(nObj["errorsCount"]) ?? 0;
                    var warns = (int?)(nObj["warningsCount"]) ?? 0;
                    DateTime? timestamp = null;
                    if (nObj.TryGetPropertyValue("timestamp", out var tsNode) && tsNode != null && DateTime.TryParse((string?)tsNode, out var ts))
                    {
                        timestamp = ts;
                    }

                    node = new ScratchConsoleNodeViewModel(_cdpService, _consoleViewModel)
                    {
                        Id = id,
                        Name = name,
                        X = x,
                        Y = y,
                        Width = width,
                        Height = height,
                        RawJsonData = rawJson,
                        ConsoleLogsJson = logsJson,
                        ErrorsCount = errs,
                        WarningsCount = warns,
                        Timestamp = timestamp
                    };
                }
                else if (type == "NetworkNode")
                {
                    var rawJson = (string?)nObj["rawJsonData"] ?? "";
                    DateTime? timestamp = null;
                    if (nObj.TryGetPropertyValue("timestamp", out var tsNode) && tsNode != null && DateTime.TryParse((string?)tsNode, out var ts))
                    {
                        timestamp = ts;
                    }

                    node = new ScratchNetworkNodeViewModel(_cdpService, _networkViewModel)
                    {
                        Id = id,
                        Name = name,
                        X = x,
                        Y = y,
                        Width = width,
                        Height = height,
                        RawJsonData = rawJson,
                        Timestamp = timestamp
                    };
                }
                else if (type == "PerfNode")
                {
                    var rawJson = (string?)nObj["rawJsonData"] ?? "";
                    DateTime? timestamp = null;
                    if (nObj.TryGetPropertyValue("timestamp", out var tsNode) && tsNode != null && DateTime.TryParse((string?)tsNode, out var ts))
                    {
                        timestamp = ts;
                    }

                    node = new ScratchPerformanceNodeViewModel(_cdpService)
                    {
                        Id = id,
                        Name = name,
                        X = x,
                        Y = y,
                        Width = width,
                        Height = height,
                        RawJsonData = rawJson,
                        Timestamp = timestamp
                    };
                }
                else if (type == "MvvmNode")
                {
                    var rawJson = (string?)nObj["rawJsonData"] ?? "";
                    DateTime? timestamp = null;
                    if (nObj.TryGetPropertyValue("timestamp", out var tsNode) && tsNode != null && DateTime.TryParse((string?)tsNode, out var ts))
                    {
                        timestamp = ts;
                    }

                    node = new ScratchMvvmNodeViewModel(_cdpService)
                    {
                        Id = id,
                        Name = name,
                        X = x,
                        Y = y,
                        Width = width,
                        Height = height,
                        RawJsonData = rawJson,
                        Timestamp = timestamp
                    };
                }
                else if (type == "AppNode")
                {
                    var rawJson = (string?)nObj["rawJsonData"] ?? "";
                    DateTime? timestamp = null;
                    if (nObj.TryGetPropertyValue("timestamp", out var tsNode) && tsNode != null && DateTime.TryParse((string?)tsNode, out var ts))
                    {
                        timestamp = ts;
                    }

                    node = new ScratchApplicationNodeViewModel(_cdpService)
                    {
                        Id = id,
                        Name = name,
                        X = x,
                        Y = y,
                        Width = width,
                        Height = height,
                        RawJsonData = rawJson,
                        Timestamp = timestamp
                    };
                }
                else if (type == "PageNode")
                {
                    var base64 = (string?)nObj["screenshotBase64"];
                    var isSynced = (bool?)(nObj["isSyncedWithTimeMachine"]) ?? false;
                    var pinnedFrameIndex = (int?)(nObj["pinnedFrameIndex"]) ?? -1;

                    node = new ScratchPageNodeViewModel(_cdpService)
                    {
                        Id = id,
                        Name = name,
                        X = x,
                        Y = y,
                        Width = width,
                        Height = height,
                        IsSyncedWithTimeMachine = isSynced,
                        PinnedFrameIndex = pinnedFrameIndex,
                        ScreenshotBase64 = base64
                    };
                }
                else if (type == "ImageDiffNode")
                {
                    var leftNodeId = (string?)nObj["leftNodeId"];
                    var rightNodeId = (string?)nObj["rightNodeId"];
                    var leftTitle = (string?)nObj["leftTitle"] ?? "";
                    var rightTitle = (string?)nObj["rightTitle"] ?? "";
                    var diffPercentage = (double?)(nObj["diffPercentage"]) ?? 0.0;

                    node = new ScratchImageDiffNodeViewModel
                    {
                        Id = id,
                        Name = name,
                        X = x,
                        Y = y,
                        Width = width,
                        Height = height,
                        LeftNodeId = leftNodeId,
                        RightNodeId = rightNodeId,
                        LeftTitle = leftTitle,
                        RightTitle = rightTitle,
                        DiffPercentage = diffPercentage
                    };
                }
                else if (type == "DiffNode")
                {
                    var leftNodeId = (string?)nObj["leftNodeId"];
                    var rightNodeId = (string?)nObj["rightNodeId"];
                    var leftTitle = (string?)nObj["leftTitle"] ?? "";
                    var rightTitle = (string?)nObj["rightTitle"] ?? "";

                    node = new ScratchDiffNodeViewModel
                    {
                        Id = id,
                        Name = name,
                        X = x,
                        Y = y,
                        Width = width,
                        Height = height,
                        LeftNodeId = leftNodeId,
                        RightNodeId = rightNodeId,
                        LeftTitle = leftTitle,
                        RightTitle = rightTitle
                    };
                }
                else if (type == "TimeMachineNode")
                {
                    var payload = (string?)nObj["selectedFramePayloadText"] ?? "";
                    var isPinned = (bool?)(nObj["isPinned"]) ?? false;
                    var pinnedFrameIndex = (int?)(nObj["pinnedFrameIndex"]) ?? -1;

                    node = new ScratchTimeMachineNodeViewModel(_cdpService)
                    {
                        Id = id,
                        Name = name,
                        X = x,
                        Y = y,
                        Width = width,
                        Height = height,
                        SelectedFramePayloadText = payload,
                        IsPinned = isPinned,
                        PinnedFrameIndex = pinnedFrameIndex
                    };
                }
                else if (type == "AssertionNode")
                {
                    var inputNodeId = (string?)nObj["inputNodeId"];
                    var inputTitle = (string?)nObj["inputTitle"] ?? "";
                    var path = (string?)nObj["path"] ?? "";
                    var expectedValue = (string?)nObj["expectedValue"] ?? "";
                    var opStr = (string?)nObj["operator"] ?? "Equals";
                    Enum.TryParse<AssertionOperator>(opStr, true, out var op);

                    node = new ScratchAssertionNodeViewModel
                    {
                        Id = id,
                        Name = name,
                        X = x,
                        Y = y,
                        Width = width,
                        Height = height,
                        InputNodeId = inputNodeId,
                        InputTitle = inputTitle,
                        Path = path,
                        ExpectedValue = expectedValue,
                        Operator = op
                    };
                }
                else
                {
                    node = new NodeViewModel
                    {
                        Id = id,
                        Name = name,
                        X = x,
                        Y = y,
                        Width = width,
                        Height = height
                    };
                }

                Nodes.Add(node);
                nodeMap[id] = node;
            }
        }

        if (json.TryGetPropertyValue("connections", out var connectionsNode) && connectionsNode is JsonArray connectionsArray)
        {
            foreach (var cItem in connectionsArray)
            {
                if (cItem is not JsonObject cObj) continue;

                var fromId = (string?)cObj["fromNodeId"];
                var toId = (string?)cObj["toNodeId"];
                var fromPinId = (string?)cObj["fromPinId"];
                var toPinId = (string?)cObj["toPinId"];

                if (fromId != null && toId != null && nodeMap.TryGetValue(fromId, out var fromNode) && nodeMap.TryGetValue(toId, out var toNode))
                {
                    var fromPin = fromNode.Outputs.FirstOrDefault(p => p.Id == fromPinId) ?? fromNode.Outputs.FirstOrDefault();
                    var toPin = toNode.Inputs.FirstOrDefault(p => p.Id == toPinId) ?? toNode.Inputs.FirstOrDefault();

                    CDP.Editor.Nodes.ViewModels.ConnectionViewModel conn;
                    if (fromPin != null && toPin != null)
                    {
                        conn = new CDP.Editor.Nodes.ViewModels.ConnectionViewModel(fromPin, toPin);
                    }
                    else
                    {
                        conn = new CDP.Editor.Nodes.ViewModels.ConnectionViewModel(fromNode, toNode);
                    }
                    Connections.Add(conn);
                }
            }
        }

        UpdateAllDiffNodes();
        UpdateAllImageDiffNodes();
        UpdateAllAssertionNodes();
        UpdateAllMvvmNodes();
        UpdateAllAccessibilityNodes();
    }

    #region IStateProvider Implementation

    public string StateKey => "scratch";

    public JsonNode? SaveState()
    {
        return SaveProject();
    }

    public void LoadState(JsonNode? stateNode)
    {
        LoadProject(stateNode);
    }

    #endregion
}
