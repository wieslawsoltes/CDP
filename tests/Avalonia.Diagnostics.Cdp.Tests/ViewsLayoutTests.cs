using Avalonia.Headless.XUnit;
using Xunit;
using CdpInspectorApp.Views;
using CdpInspectorApp.ViewModels;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.VisualTree;
using System;
using Avalonia.Input;
using Avalonia.Interactivity;
using CDP.Editor.Splits.Models;

namespace Avalonia.Diagnostics.Cdp.Tests;

public class ViewsLayoutTests
{
    [AvaloniaFact]
    public void Test_Views_Instantiate_And_Load_Successfully()
    {
        var app = Avalonia.Application.Current;
        Assert.NotNull(app);

        // Load the shared stylesheet to ensure Resource references (like FolderIcon, PlayIcon) resolve correctly
        var sharedStyles = new Avalonia.Markup.Xaml.Styling.StyleInclude(new Uri("avares://Avalonia.Diagnostics.Cdp.Tests/"))
        {
            Source = new Uri("avares://CDP.Inspector.Shared/Styles.axaml")
        };
        app.Styles.Add(sharedStyles);

        try
        {
            // Instantiate views to trigger XAML loading, parsing, and resource resolution
            var testStudioView = new TestStudioView();
            Assert.NotNull(testStudioView);

            var testStudioNodeEditorView = new TestStudioNodeEditorView();
            Assert.NotNull(testStudioNodeEditorView);

            var recorderView = new RecorderView();
            Assert.NotNull(recorderView);

            var simulationView = new SimulationView();
            Assert.NotNull(simulationView);

            var videoPlaybackWindow = new VideoPlaybackWindow();
            Assert.NotNull(videoPlaybackWindow);

            var consoleView = new ConsoleView();
            Assert.NotNull(consoleView);

            var networkView = new NetworkView();
            Assert.NotNull(networkView);

            var memoryView = new MemoryView();
            Assert.NotNull(memoryView);

            var applicationView = new ApplicationView();
            Assert.NotNull(applicationView);
        }
        finally
        {
            app.Styles.Remove(sharedStyles);
        }
    }

    [AvaloniaFact]
    public void Test_TestStudioNodeEditorView_Layout_Rendering()
    {
        var app = Avalonia.Application.Current;
        Assert.NotNull(app);

        // Load the shared stylesheet to ensure Resource references resolve correctly
        var sharedStyles = new Avalonia.Markup.Xaml.Styling.StyleInclude(new Uri("avares://Avalonia.Diagnostics.Cdp.Tests/"))
        {
            Source = new Uri("avares://CDP.Inspector.Shared/Styles.axaml")
        };
        app.Styles.Add(sharedStyles);

        try
        {
            var view = new TestStudioNodeEditorView();
            var vm = new TestStudioNodeEditorViewModel();
            view.DataContext = vm;

            // Add a node at a specific coordinate
            var node = vm.CreateNode("Test Node", "tapOn", "#testBtn", "testVal", 120.0, 200.0);

            // Wrap in a window and show to attach it to visual root and run template/layout compilation
            var window = new Window { Width = 1000, Height = 800, Content = view };
            window.Show();

            // Force layout update
            view.UpdateLayout();

            // Find the NodeEditor control first, then search within it
            var nodeEditorControl = view.FindControl<CDP.Editor.Nodes.Views.NodeEditorView>("NodeEditor");
            Assert.NotNull(nodeEditorControl);
            var itemsControl = nodeEditorControl.FindControl<ItemsControl>("NodesItemsControl");
            Assert.NotNull(itemsControl);

            // Find the generated containers
            var containers = Avalonia.VisualTree.VisualExtensions.GetVisualDescendants(itemsControl)
                .OfType<ContentPresenter>()
                .ToList();

            // Verify a container is generated and positioned at X, Y on the Canvas
            var container = containers.FirstOrDefault();
            Assert.NotNull(container);
            
            Assert.Equal(120.0, Canvas.GetLeft(container));
            Assert.Equal(200.0, Canvas.GetTop(container));
        }
        finally
        {
            app.Styles.Remove(sharedStyles);
        }
    }

    [AvaloniaFact]
    public void Test_SuperSplit_Dynamic_Splitting()
    {
        var app = Avalonia.Application.Current;
        Assert.NotNull(app);

        // Instantiate view model and test default layout structure
        var vm = new MainWindowViewModel();
        Assert.NotNull(vm.LayoutRoot);
        Assert.NotNull(vm.SelectedPane);

        // Current layout root should be a horizontal split container (Simulation on left, right split on right)
        var rootSplit = vm.LayoutRoot as CDP.Editor.Splits.Models.SplitContainerNode;
        Assert.NotNull(rootSplit);
        Assert.Equal(Avalonia.Layout.Orientation.Horizontal, rootSplit.Orientation);

        // Simulate click to change selection and split horizontally
        vm.SelectedPane = rootSplit.Child1 as CDP.Editor.Splits.Models.BoxNode;
        Assert.NotNull(vm.SelectedPane);

        // Execute SplitRight Command
        vm.SplitRightCommand.Execute(null);

        // The selected pane should now be a split container since it was replaced
        var updatedRoot = vm.LayoutRoot as CDP.Editor.Splits.Models.SplitContainerNode;
        Assert.NotNull(updatedRoot);
        var leftChild = updatedRoot.Child1 as CDP.Editor.Splits.Models.SplitContainerNode;
        Assert.NotNull(leftChild); // Splitting the left pane replaced it with a split container
        Assert.Equal(Avalonia.Layout.Orientation.Horizontal, leftChild.Orientation);

        // Execute CloseSelected Command
        vm.ClosePaneCommand.Execute(null);

        // Closing the new pane should restore the original single node on the left
        Assert.True(updatedRoot.Child1 is CDP.Editor.Splits.Models.BoxNode);

        // Verify SuperSplit control can instantiate with the root layout successfully
        var superSplit = new CDP.Editor.Splits.Controls.SuperSplit
        {
            Root = vm.LayoutRoot,
            SelectedNode = vm.SelectedPane
        };
        Assert.NotNull(superSplit);
        superSplit.Rebuild();
        Assert.NotNull(superSplit.Content);
    }

    [AvaloniaFact]
    public void Test_SuperSplit_Drag_And_Drop_DataContext()
    {
        var vm = new MainWindowViewModel();
        var superSplit = new CDP.Editor.Splits.Controls.SuperSplit
        {
            Root = vm.LayoutRoot,
            SelectedNode = vm.SelectedPane
        };

        var window = new Window { Width = 1000, Height = 800, Content = superSplit };
        window.Show();

        superSplit.Rebuild();
        superSplit.UpdateLayout();

        // Find all SuperSplitBox elements inside the SuperSplit Content
        var boxes = Avalonia.VisualTree.VisualExtensions.GetVisualDescendants(superSplit)
            .OfType<CDP.Editor.Splits.Controls.SuperSplitBox>()
            .ToList();

        Assert.NotEmpty(boxes);

        foreach (var boxControl in boxes)
        {
            // Verify that each SuperSplitBox has its DataContext correctly set to a BoxNode
            Assert.NotNull(boxControl.DataContext);
            Assert.IsType<CDP.Editor.Splits.Models.BoxNode>(boxControl.DataContext);
            
            // Check that the node properties match
            var nodeModel = (CDP.Editor.Splits.Models.BoxNode)boxControl.DataContext;
            Assert.Equal(nodeModel.Title, boxControl.HeaderTitle);
        }
    }

    [AvaloniaFact]
    public void Test_SuperSplit_Center_Dock_Join_And_Prune()
    {
        var vm = new MainWindowViewModel();
        var superSplit = new CDP.Editor.Splits.Controls.SuperSplit
        {
            Root = vm.LayoutRoot,
            SelectedNode = vm.SelectedPane
        };

        superSplit.Rebuild();

        var boxNodes = new System.Collections.Generic.List<CDP.Editor.Splits.Models.BoxNode>();
        void Collect(CDP.Editor.Splits.Models.SplitNode? node)
        {
            if (node is CDP.Editor.Splits.Models.BoxNode box) boxNodes.Add(box);
            else if (node is CDP.Editor.Splits.Models.SplitContainerNode container)
            {
                Collect(container.Child1);
                Collect(container.Child2);
            }
        }
        Collect(vm.LayoutRoot);

        Assert.True(boxNodes.Count >= 2);
        var source = boxNodes[0];
        var target = boxNodes[1];

        // Capture initial tab counts
        int sourceTabCount = source.Tabs.Count;
        int targetTabCount = target.Tabs.Count;

        // Perform the Center drop operation (index 5 of RelativeDropLocation)
        var method = typeof(CDP.Editor.Splits.Controls.SuperSplit).GetMethod("MoveNode", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        Assert.NotNull(method);

        method.Invoke(superSplit, new object[] { source, target, 5 });

        // Verify that tabs were transferred
        Assert.Equal(0, source.Tabs.Count);
        Assert.Equal(sourceTabCount + targetTabCount, target.Tabs.Count);

        // Verify that empty source node was pruned from the tree
        var newBoxNodes = new System.Collections.Generic.List<CDP.Editor.Splits.Models.BoxNode>();
        void CollectNew(CDP.Editor.Splits.Models.SplitNode? node)
        {
            if (node is CDP.Editor.Splits.Models.BoxNode box) newBoxNodes.Add(box);
            else if (node is CDP.Editor.Splits.Models.SplitContainerNode container)
            {
                CollectNew(container.Child1);
                CollectNew(container.Child2);
            }
        }
        CollectNew(superSplit.Root);

        Assert.DoesNotContain(source, newBoxNodes);
    }

    [Fact]
    public void Test_BoxNode_Tab_Property_Delegation()
    {
        var box = new CDP.Editor.Splits.Models.BoxNode();
        var tab1 = box.AddTab("Title 1", "Icon 1", "View 1");
        var tab2 = box.AddTab("Title 2", "Icon 2", "View 2");

        Assert.Same(tab1, box.ActiveTab);
        Assert.Equal("Title 1", box.Title);
        Assert.Equal("Icon 1", box.IconKey);
        Assert.Equal("View 1", box.SelectedViewName);

        box.ActiveTab = tab2;
        Assert.Equal("Title 2", box.Title);
        Assert.Equal("Icon 2", box.IconKey);
        Assert.Equal("View 2", box.SelectedViewName);

        box.Title = "Updated Title 2";
        Assert.Equal("Updated Title 2", tab2.Title);
    }

    [AvaloniaFact]
    public void Test_SuperSplit_Split_Active_Tab()
    {
        var vm = new MainWindowViewModel();

        var rightPane = vm.SelectedPane;
        Assert.NotNull(rightPane);
        Assert.True(rightPane.Tabs.Count > 1);

        var activeTab = rightPane.ActiveTab;
        Assert.NotNull(activeTab);
        string activeTabTitle = activeTab.Title;
        int originalTabCount = rightPane.Tabs.Count;

        vm.SplitRightCommand.Execute(null);

        var newSelected = vm.SelectedPane;
        Assert.NotNull(newSelected);
        Assert.NotSame(rightPane, newSelected);

        Assert.Equal(1, newSelected.Tabs.Count);
        Assert.Same(activeTab, newSelected.ActiveTab);
        Assert.Equal(activeTabTitle, newSelected.Title);

        Assert.Equal(originalTabCount - 1, rightPane.Tabs.Count);
    }

    [AvaloniaFact]
    public void Test_SuperSplit_Float_And_Merge()
    {
        var mainSplit = new CDP.Editor.Splits.Controls.SuperSplit();
        var box1 = new CDP.Editor.Splits.Models.BoxNode();
        box1.AddTab("Tab1", "Icon1", "View1");
        var box2 = new CDP.Editor.Splits.Models.BoxNode();
        box2.AddTab("Tab2", "Icon2", "View2");

        var rootContainer = new CDP.Editor.Splits.Models.SplitContainerNode(Avalonia.Layout.Orientation.Horizontal, box1, box2);
        mainSplit.Root = rootContainer;
        mainSplit.Rebuild();

        CDP.Editor.Splits.Models.BoxNode? floatedNode = null;
        CDP.Editor.Splits.Controls.SuperSplit? floatSource = null;
        CDP.Editor.Splits.Models.SuperSplitDragManager.FloatNodeCallback = (source, node) =>
        {
            floatSource = source;
            floatedNode = node;
        };

        // Simulate dragging box2 out (float)
        // 1. Manually prune it from mainSplit
        // PruneEmptyNode is private, but we can simulate the root changes
        mainSplit.Root = box1;
        mainSplit.Rebuild();

        // 2. Invoke FloatNodeCallback
        CDP.Editor.Splits.Models.SuperSplitDragManager.FloatNodeCallback(mainSplit, box2);

        Assert.Same(mainSplit, floatSource);
        Assert.Same(box2, floatedNode);
        Assert.Same(box1, mainSplit.Root);

        // Simulate merging back (e.g. closing floating window manually)
        if (mainSplit.Root == null)
        {
            mainSplit.Root = box2;
        }
        else
        {
            var newRoot = new CDP.Editor.Splits.Models.SplitContainerNode(Avalonia.Layout.Orientation.Horizontal, mainSplit.Root, box2);
            mainSplit.Root = newRoot;
        }
        mainSplit.Rebuild();

        Assert.IsType<CDP.Editor.Splits.Models.SplitContainerNode>(mainSplit.Root);
        var finalContainer = (CDP.Editor.Splits.Models.SplitContainerNode)mainSplit.Root;
        Assert.Same(box1, finalContainer.Child1);
        Assert.Same(box2, finalContainer.Child2);
    }

    [AvaloniaFact]
    public void Test_FloatingSplitWindow_Instantiation_And_Close()
    {
        var mainSplit = new CDP.Editor.Splits.Controls.SuperSplit();
        var box1 = new CDP.Editor.Splits.Models.BoxNode();
        box1.AddTab("Tab1", "Icon1", "View1");
        mainSplit.Root = box1;
        mainSplit.Rebuild();

        var window = new CDP.Inspector.Shared.Controls.FloatingSplitWindow(mainSplit, box1);
        Assert.NotNull(window);
        window.Close();
    }

    [AvaloniaFact]
    public void Test_SuperSplit_Drag_Tab_Split_Target()
    {
        var vm = new MainWindowViewModel();
        var superSplit = new CDP.Editor.Splits.Controls.SuperSplit
        {
            Root = vm.LayoutRoot,
            SelectedNode = vm.SelectedPane
        };

        superSplit.Rebuild();

        var source = new CDP.Editor.Splits.Models.BoxNode();
        source.AddTab("Detached Tab", "GlobeIcon", "Network");

        var rootContainer = vm.LayoutRoot as CDP.Editor.Splits.Models.SplitContainerNode;
        Assert.NotNull(rootContainer);
        var target = rootContainer.Child1 as CDP.Editor.Splits.Models.BoxNode;
        Assert.NotNull(target);

        var method = typeof(CDP.Editor.Splits.Controls.SuperSplit).GetMethod("MoveNode", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        Assert.NotNull(method);

        method.Invoke(superSplit, new object[] { source, target, 1 });

        var boxNodes = new System.Collections.Generic.List<CDP.Editor.Splits.Models.BoxNode>();
        void Collect(CDP.Editor.Splits.Models.SplitNode? node)
        {
            if (node is CDP.Editor.Splits.Models.BoxNode box) boxNodes.Add(box);
            else if (node is CDP.Editor.Splits.Models.SplitContainerNode container)
            {
                Collect(container.Child1);
                Collect(container.Child2);
            }
        }
        Collect(superSplit.Root);

        Assert.Contains(source, boxNodes);
        Assert.Same(source, superSplit.SelectedNode);
    }

    [AvaloniaFact]
    public void Test_VideoPlaybackWindow_Telemetry_Loading()
    {
        var app = Avalonia.Application.Current;
        Assert.NotNull(app);

        var sharedStyles = new Avalonia.Markup.Xaml.Styling.StyleInclude(new Uri("avares://Avalonia.Diagnostics.Cdp.Tests/"))
        {
            Source = new Uri("avares://CDP.Inspector.Shared/Styles.axaml")
        };
        app.Styles.Add(sharedStyles);

        try
        {
            var win = new VideoPlaybackWindow();

            var steps = new System.Collections.Generic.List<StepReportItem>
            {
                new StepReportItem
                {
                    Index = 1,
                    Action = "tap",
                    ActionDisplay = "Tap element",
                    Status = "Passed",
                    DurationMs = 150,
                    RelativeStartMs = 10,
                    CpuUsage = 5.2,
                    MemoryJsHeapUsed = 33.4,
                    MemoryJsHeapTotal = 64.0,
                    Fps = 60,
                    NetworkRequestCount = 1,
                    NetworkResponseBytes = 1024
                }
            };

            var metrics = new System.Collections.Generic.List<RunMetricSample>
            {
                new RunMetricSample { RelativeTimeMs = 0, CpuUsage = 5.0, MemoryJsHeapUsed = 33.0, Fps = 60 },
                new RunMetricSample { RelativeTimeMs = 20, CpuUsage = 5.2, MemoryJsHeapUsed = 33.4, Fps = 60 }
            };

            var network = new System.Collections.Generic.List<NetworkReportItem>
            {
                new NetworkReportItem
                {
                    RequestId = "req-1",
                    Url = "http://example.com/data",
                    Method = "GET",
                    Status = "200",
                    RelativeStartMs = 15,
                    DurationMs = 50
                }
            };

            var frames = new System.Collections.Generic.List<PlaybackFrame>
            {
                new PlaybackFrame { Data = new byte[10], TimestampMs = 0 },
                new PlaybackFrame { Data = new byte[10], TimestampMs = 100 }
            };

            // Call telemetry overload
            win.SetFramesAndSteps(frames, steps, metrics, network, null);

            // Access TextBlocks via the telemetry view control and verify values are rendered
            var telemetryView = win.FindControl<StepTelemetryView>("stepTelemetry");
            Assert.NotNull(telemetryView);

            var tabControl = telemetryView.FindControl<TabControl>("tabTelemetry");
            Assert.NotNull(tabControl);
            Assert.Equal(2, tabControl.Items.Count);

            var perfTab = tabControl.Items[0] as TabItem;
            var perfGrid = perfTab?.Content as Grid;
            Assert.NotNull(perfGrid);

            var netTab = tabControl.Items[1] as TabItem;
            var netGrid = netTab?.Content as Grid;
            Assert.NotNull(netGrid);

            var lblCpu = FindVisualDescendantByName<TextBlock>(perfGrid, "lblStepCpu");
            var lblMemory = FindVisualDescendantByName<TextBlock>(perfGrid, "lblStepMemory");
            var lblFpsDom = FindVisualDescendantByName<TextBlock>(perfGrid, "lblStepFpsDom");
            var lblNetwork = FindVisualDescendantByName<TextBlock>(netGrid, "lblStepNetwork");

            Assert.NotNull(lblCpu);
            Assert.NotNull(lblMemory);
            Assert.NotNull(lblFpsDom);
            Assert.NotNull(lblNetwork);

            Assert.Equal($"{5.2:F1} %", lblCpu.Text);
            Assert.Equal($"{33.40:F2} / {64.00:F2} MB", lblMemory.Text);
            Assert.Equal($"{60.0:F1} FPS / 0 nodes", lblFpsDom.Text);
            Assert.Equal($"1 requests / {1.00:F2} KB", lblNetwork.Text);
        }
        finally
        {
            app.Styles.Remove(sharedStyles);
        }
    }

    [AvaloniaFact]
    public void Test_SuperSplit_Focus_Overlay_Updates()
    {
        var root = new BoxNode();
        root.AddTab("Sim", "GlobeIcon", "Simulation");
        var superSplit = new CDP.Editor.Splits.Controls.SuperSplit
        {
            Root = root,
            SelectedNode = root
        };

        var window = new Window { Width = 800, Height = 600, Content = superSplit };
        window.Show();
        superSplit.Rebuild();
        superSplit.UpdateLayout();

        // Trigger focus overlay update manually or via selection change
        superSplit.SelectedNode = root;
        superSplit.UpdateLayout();

        var overlay = typeof(CDP.Editor.Splits.Controls.SuperSplit)
            .GetField("_focusOverlay", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            ?.GetValue(superSplit) as Border;

        Assert.NotNull(overlay);
        Assert.True(overlay.IsVisible);
    }

    [AvaloniaFact]
    public void Test_SuperSplit_Corner_Grab_Handle_Collection()
    {
        var rootNode = new BoxNode();
        rootNode.AddTab("Tab1", "GlobeIcon", "Sim");

        var superSplit = new CDP.Editor.Splits.Controls.SuperSplit
        {
            Root = rootNode
        };

        var window = new Window { Width = 800, Height = 600, Content = superSplit };
        window.Show();
        superSplit.Rebuild();
        superSplit.UpdateLayout();

        // Perform split to create an intersection structure
        var targetBox = superSplit.Root as BoxNode;
        Assert.NotNull(targetBox);

        // Perform split operation via reflection or by updating root manually
        var hChild1 = new BoxNode();
        hChild1.AddTab("SubTab1", "GlobeIcon", "Sim");
        var hChild2 = new BoxNode();
        hChild2.AddTab("SubTab2", "GlobeIcon", "Sim");
        
        var vChild1 = new BoxNode();
        vChild1.AddTab("SubSubTab1", "GlobeIcon", "Sim");
        var vChild2 = new BoxNode();
        vChild2.AddTab("SubSubTab2", "GlobeIcon", "Sim");

        var vContainer = new SplitContainerNode(Avalonia.Layout.Orientation.Vertical, vChild1, vChild2);
        var hContainer = new SplitContainerNode(Avalonia.Layout.Orientation.Horizontal, hChild1, hChild2);

        var topContainer = new SplitContainerNode(Avalonia.Layout.Orientation.Horizontal, hContainer, vContainer);
        superSplit.Root = topContainer;

        superSplit.Rebuild();
        superSplit.UpdateLayout();

        // Verify that FlatCornerGrabHandle is populated in the panel
        var handles = Avalonia.VisualTree.VisualExtensions.GetVisualDescendants(superSplit)
            .OfType<CDP.Editor.Splits.Controls.FlatCornerGrabHandle>()
            .ToList();

        Assert.NotEmpty(handles);
        var handle = handles.First();
        Assert.Same(topContainer, handle.ParentContainer);
        Assert.True(handle.ChildContainer == hContainer || handle.ChildContainer == vContainer);
    }

    [AvaloniaFact]
    public void Test_SuperSplit_Cross_Window_Drag_Drop_MoveNode()
    {
        var vm1 = new MainWindowViewModel();
        var superSplit1 = new CDP.Editor.Splits.Controls.SuperSplit
        {
            Root = vm1.LayoutRoot,
            SelectedNode = vm1.SelectedPane
        };
        superSplit1.Rebuild();

        var vm2 = new MainWindowViewModel();
        var superSplit2 = new CDP.Editor.Splits.Controls.SuperSplit
        {
            Root = vm2.LayoutRoot,
            SelectedNode = vm2.SelectedPane
        };
        superSplit2.Rebuild();

        // Grab boxes
        var boxNodes1 = new System.Collections.Generic.List<BoxNode>();
        void Collect(SplitNode? node, System.Collections.Generic.List<BoxNode> list)
        {
            if (node is BoxNode box) list.Add(box);
            else if (node is SplitContainerNode container)
            {
                Collect(container.Child1, list);
                Collect(container.Child2, list);
            }
        }
        Collect(superSplit1.Root, boxNodes1);

        var boxNodes2 = new System.Collections.Generic.List<BoxNode>();
        Collect(superSplit2.Root, boxNodes2);

        var sourceNode = boxNodes1[0];
        var targetNode = boxNodes2[0];

        int initialTabs1 = sourceNode.Tabs.Count;
        int initialTabs2 = targetNode.Tabs.Count;

        // Perform cross-window move node drop to Center
        var method = typeof(CDP.Editor.Splits.Controls.SuperSplit).GetMethod("MoveNodeCrossWindow", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        Assert.NotNull(method);

        method.Invoke(superSplit2, new object[] { sourceNode, targetNode, 5, superSplit1 });

        // Verify tabs moved
        Assert.Empty(sourceNode.Tabs);
        Assert.Equal(initialTabs1 + initialTabs2, targetNode.Tabs.Count);
    }

    [AvaloniaFact]
    public void Test_SuperSplit_Drag_Drop_Sibling_Split_Robustness()
    {
        var paneA = new BoxNode();
        paneA.AddTab("Pane A", "FolderIcon", "A");
        var paneB = new BoxNode();
        paneB.AddTab("Pane B", "PlayIcon", "B");
        var paneC = new BoxNode();
        paneC.AddTab("Pane C", "VideoIcon", "C");

        var subContainer = new SplitContainerNode(Avalonia.Layout.Orientation.Vertical, paneA, paneB);
        var root = new SplitContainerNode(Avalonia.Layout.Orientation.Horizontal, subContainer, paneC);

        var superSplit = new CDP.Editor.Splits.Controls.SuperSplit
        {
            Root = root,
            SelectedNode = paneA
        };
        superSplit.Rebuild();

        // Drag paneA (sibling of paneB) and drop it to split paneB (on its Left / insertBefore=true)
        var method = typeof(CDP.Editor.Splits.Controls.SuperSplit).GetMethod("MoveNodeCrossWindow", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        Assert.NotNull(method);

        // RelativeDropLocation.Left is represented as 1
        method.Invoke(superSplit, new object[] { paneA, paneB, 1, superSplit });

        // Verify Rebuild succeeds without duplicate key crashes, and layout maintains all 3 panels
        superSplit.Rebuild();

        var boxNodes = new System.Collections.Generic.List<BoxNode>();
        void Collect(SplitNode? node, System.Collections.Generic.List<BoxNode> list)
        {
            if (node is BoxNode box) list.Add(box);
            else if (node is SplitContainerNode container)
            {
                Collect(container.Child1, list);
                Collect(container.Child2, list);
            }
        }
        Collect(superSplit.Root, boxNodes);

        Assert.Equal(3, boxNodes.Count);
        Assert.Contains(paneA, boxNodes);
        Assert.Contains(paneB, boxNodes);
        Assert.Contains(paneC, boxNodes);
    }

    [AvaloniaFact]
    public void Test_SuperSplit_Corner_Grab_Handles_Creation_And_Layout()
    {
        // Setup a horizontal-vertical split layout to produce corner intersections
        var paneA = new BoxNode();
        paneA.AddTab("Pane A", "FolderIcon", "A");
        var paneB = new BoxNode();
        paneB.AddTab("Pane B", "PlayIcon", "B");
        var paneC = new BoxNode();
        paneC.AddTab("Pane C", "VideoIcon", "C");

        var rightSubContainer = new SplitContainerNode(Avalonia.Layout.Orientation.Vertical, paneB, paneC);
        var root = new SplitContainerNode(Avalonia.Layout.Orientation.Horizontal, paneA, rightSubContainer);

        var superSplit = new CDP.Editor.Splits.Controls.SuperSplit
        {
            Root = root
        };

        // Rebuild is called, which creates a new FlatSplitPanel with 0x0 size and runs SyncChildren
        superSplit.Rebuild();

        // Get the panel
        var panelField = typeof(CDP.Editor.Splits.Controls.SuperSplit).GetField("_flatPanel", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        Assert.NotNull(panelField);
        var panel = panelField.GetValue(superSplit) as Avalonia.Controls.Panel;
        Assert.NotNull(panel);

        // Verify that even with 0x0 size, corner grab handles are successfully created and added to the panel
        var handles = new System.Collections.Generic.List<CDP.Editor.Splits.Controls.FlatCornerGrabHandle>();
        foreach (var child in panel.Children)
        {
            if (child is CDP.Editor.Splits.Controls.FlatCornerGrabHandle handle)
            {
                handles.Add(handle);
            }
        }

        Assert.Single(handles);
        var cornerHandle = handles[0];
        Assert.Equal(root, cornerHandle.ParentContainer);
        Assert.Equal(rightSubContainer, cornerHandle.ChildContainer);
    }

    [AvaloniaFact]
    public void Test_SuperSplit_Zoom_And_Unzoom_Layout_Transitions()
    {
        var paneA = new BoxNode();
        paneA.AddTab("Pane A", "FolderIcon", "A");
        var paneB = new BoxNode();
        paneB.AddTab("Pane B", "PlayIcon", "B");

        var root = new SplitContainerNode(Avalonia.Layout.Orientation.Horizontal, paneA, paneB);

        var superSplit = new CDP.Editor.Splits.Controls.SuperSplit
        {
            Root = root,
            Width = 800,
            Height = 600
        };

        superSplit.Rebuild();

        var panelField = typeof(CDP.Editor.Splits.Controls.SuperSplit).GetField("_flatPanel", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        Assert.NotNull(panelField);
        var panel = panelField.GetValue(superSplit) as Avalonia.Controls.Panel;
        Assert.NotNull(panel);

        // First layout pass with size 800x600
        panel.Measure(new Avalonia.Size(800, 600));
        panel.Arrange(new Avalonia.Rect(0, 0, 800, 600));

        // Get the internal layout dictionaries of FlatSplitPanel
        var nodeBoundsField = panel.GetType().GetField("_nodeBounds", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        Assert.NotNull(nodeBoundsField);
        var nodeBounds = nodeBoundsField.GetValue(panel) as System.Collections.Generic.Dictionary<SplitNode, Avalonia.Rect>;
        Assert.NotNull(nodeBounds);

        // Verify normal layout split sizes: each gets roughly 400 width
        Assert.True(nodeBounds.TryGetValue(paneA, out var rectA));
        Assert.True(nodeBounds.TryGetValue(paneB, out var rectB));
        Assert.Equal(0, rectA.X);
        Assert.Equal(396, rectA.Width); // 800 - 8 (splitter) = 792 / 2 = 396
        Assert.Equal(404, rectB.X);
        Assert.Equal(396, rectB.Width);

        // Zoom paneA
        superSplit.ToggleZoomNode(paneA);
        Assert.Equal(paneA, superSplit.ZoomedNode);
        Assert.True(superSplit.IsZoomTransitionPending);

        // Re-arrange layout with zoom active
        panel.Measure(new Avalonia.Size(800, 600));
        panel.Arrange(new Avalonia.Rect(0, 0, 800, 600));

        // Verify zoomed bounds: paneA fills entire screen, paneB is translated out-of-bounds to the right
        Assert.True(nodeBounds.TryGetValue(paneA, out rectA));
        Assert.True(nodeBounds.TryGetValue(paneB, out rectB));

        Assert.Equal(0, rectA.X);
        Assert.Equal(0, rectA.Y);
        Assert.Equal(800, rectA.Width);
        Assert.Equal(600, rectA.Height);

        // paneB touches the right edge, so it should translate to the right
        Assert.True(rectB.X >= 800);

        // Unzoom
        superSplit.ToggleZoomNode(paneA);
        Assert.Null(superSplit.ZoomedNode);

        // Re-arrange layout to unzoomed state
        panel.Measure(new Avalonia.Size(800, 600));
        panel.Arrange(new Avalonia.Rect(0, 0, 800, 600));

        // Verify bounds restored
        Assert.True(nodeBounds.TryGetValue(paneA, out rectA));
        Assert.True(nodeBounds.TryGetValue(paneB, out rectB));
        Assert.Equal(0, rectA.X);
        Assert.Equal(396, rectA.Width);
        Assert.Equal(404, rectB.X);
        Assert.Equal(396, rectB.Width);
    }

    [AvaloniaFact]
    public void Test_SuperSplit_Focus_Overlay_Resizing_Toggles_Animations()
    {
        var root = new BoxNode();
        root.AddTab("Pane1", "GlobeIcon", "Simulation");
        var superSplit = new CDP.Editor.Splits.Controls.SuperSplit
        {
            Root = root,
            SelectedNode = root
        };

        var window = new Window { Width = 800, Height = 600, Content = superSplit };
        window.Show();
        superSplit.Rebuild();
        superSplit.UpdateLayout();

        // Initially not interactive resizing
        Assert.False(superSplit.IsInteractiveResizing());

        var overlay = typeof(CDP.Editor.Splits.Controls.SuperSplit)
            .GetField("_focusOverlay", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            ?.GetValue(superSplit) as Border;

        Assert.NotNull(overlay);
        var visual = Avalonia.Rendering.Composition.ElementComposition.GetElementVisual(overlay);
        Assert.NotNull(visual);

        // Under normal circumstances, implicit animations are assigned
        superSplit.UpdateFocusOverlay();
        Assert.NotNull(visual.ImplicitAnimations);

        // Simulate interactive resize by adding a pressed corner handle or active splitter
        var panel = typeof(CDP.Editor.Splits.Controls.SuperSplit)
            .GetField("_flatPanel", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            ?.GetValue(superSplit) as CDP.Editor.Splits.Controls.FlatSplitPanel;
        Assert.NotNull(panel);

        var child1 = new BoxNode();
        child1.AddTab("Sub1", "GlobeIcon", "Sim");
        var child2 = new BoxNode();
        child2.AddTab("Sub2", "GlobeIcon", "Sim");
        var container = new SplitContainerNode(Avalonia.Layout.Orientation.Horizontal, child1, child2);
        superSplit.Root = container;
        superSplit.SelectedNode = child1;
        superSplit.Rebuild();
        superSplit.UpdateLayout();

        var cornerHandle = new CDP.Editor.Splits.Controls.FlatCornerGrabHandle(container, container);
        panel.Children.Add(cornerHandle);

        // Simulating the corner handle press
        var pressedField = typeof(CDP.Editor.Splits.Controls.FlatCornerGrabHandle)
            .GetField("_isPressed", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        Assert.NotNull(pressedField);
        pressedField.SetValue(cornerHandle, true);

        // Now interactive resizing is active
        Assert.True(superSplit.IsInteractiveResizing());

        // Focus overlay updates during resize should have null implicit animations to stay in lock-step
        superSplit.UpdateFocusOverlay();
        Assert.Null(visual.ImplicitAnimations);

        // Releasing drag restores animations
        pressedField.SetValue(cornerHandle, false);
        Assert.False(superSplit.IsInteractiveResizing());
        superSplit.UpdateFocusOverlay();
        Assert.NotNull(visual.ImplicitAnimations);
    }

    [AvaloniaFact]
    public void Test_SuperSplit_Focus_Template_Rebuilds_On_Selection_Change()
    {
        var root = new BoxNode();
        var tab1 = root.AddTab("Pane1", "GlobeIcon", "Simulation");
        var child2 = new BoxNode();
        var tab2 = child2.AddTab("Pane2", "GlobeIcon", "Simulation");

        var container = new SplitContainerNode(Avalonia.Layout.Orientation.Horizontal, root, child2);

        var customTemplate = new Avalonia.Controls.Templates.FuncDataTemplate<BoxNode>((node, namescope) => {
            return new TextBlock { Text = node?.Title };
        });

        var superSplit = new CDP.Editor.Splits.Controls.SuperSplit
        {
            Root = container,
            FocusTemplate = customTemplate,
            SelectedNode = root
        };

        var window = new Window { Width = 800, Height = 600, Content = superSplit };
        window.Show();
        superSplit.Rebuild();
        superSplit.UpdateLayout();

        var overlay = typeof(CDP.Editor.Splits.Controls.SuperSplit)
            .GetField("_focusOverlay", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            ?.GetValue(superSplit) as Border;

        Assert.NotNull(overlay);
        Assert.NotNull(overlay.Child);
        var txtBlock = overlay.Child as TextBlock;
        Assert.NotNull(txtBlock);
        Assert.Equal("Pane1", txtBlock.Text);

        // Change selection
        superSplit.SelectedNode = child2;
        superSplit.UpdateFocusOverlay();

        // Verify focus template is rebuilt with correct new selected node Title
        Assert.NotNull(overlay.Child);
        var txtBlock2 = overlay.Child as TextBlock;
        Assert.NotNull(txtBlock2);
        Assert.Equal("Pane2", txtBlock2.Text);
    }

    private static T? FindVisualDescendantByName<T>(Avalonia.Visual? visual, string name) where T : Control
    {
        if (visual == null) return null;
        if (visual is T t && t.Name == name) return t;
        foreach (var child in visual.GetVisualChildren())
        {
            var result = FindVisualDescendantByName<T>(child, name);
            if (result != null) return result;
        }
        return null;
    }

    [AvaloniaFact]
    public void Test_SuperSplitBox_Shows_Header_But_Hides_Tab_List_When_Single_Tab()
    {
        var boxNode = new CDP.Editor.Splits.Models.BoxNode();
        boxNode.AddTab("Tab1", "GlobeIcon", "Network");

        var boxControl = new CDP.Editor.Splits.Controls.SuperSplitBox
        {
            DataContext = boxNode
        };

        var window = new Window { Content = boxControl };

        var fieldHeader = typeof(CDP.Editor.Splits.Controls.SuperSplitBox).GetField("_headerPanel", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        Assert.NotNull(fieldHeader);
        var headerPanel = fieldHeader.GetValue(boxControl) as Border;
        Assert.NotNull(headerPanel);

        var fieldScroll = typeof(CDP.Editor.Splits.Controls.SuperSplitBox).GetField("_tabsScrollViewer", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        Assert.NotNull(fieldScroll);
        var tabsScrollViewer = fieldScroll.GetValue(boxControl) as ScrollViewer;
        Assert.NotNull(tabsScrollViewer);

        var fieldSingleHeader = typeof(CDP.Editor.Splits.Controls.SuperSplitBox).GetField("_singleTabHeaderPanel", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        Assert.NotNull(fieldSingleHeader);
        var singleHeader = fieldSingleHeader.GetValue(boxControl) as StackPanel;
        Assert.NotNull(singleHeader);

        // Force layout pass
        window.Show();

        // Single tab should show header but hide tab list and show single tab panel
        Assert.True(headerPanel.IsVisible);
        Assert.False(tabsScrollViewer.IsVisible);
        Assert.True(singleHeader.IsVisible);

        // Adding a second tab should show tab list and hide single tab panel
        boxNode.AddTab("Tab2", "GlobeIcon", "Console");
        Assert.True(headerPanel.IsVisible);
        Assert.True(tabsScrollViewer.IsVisible);
        Assert.False(singleHeader.IsVisible);

        // Removing a tab should restore single tab layout
        boxNode.Tabs.RemoveAt(1);
        Assert.True(headerPanel.IsVisible);
        Assert.False(tabsScrollViewer.IsVisible);
        Assert.True(singleHeader.IsVisible);

        window.Close();
    }

    [Fact]
    public void Test_SuperSplitBox_Tab_Reordering()
    {
        var boxNode = new CDP.Editor.Splits.Models.BoxNode();
        var tab1 = boxNode.AddTab("Tab1", "Icon1", "View1");
        var tab2 = boxNode.AddTab("Tab2", "Icon2", "View2");

        Assert.Equal(0, boxNode.Tabs.IndexOf(tab1));
        Assert.Equal(1, boxNode.Tabs.IndexOf(tab2));

        // Move tab1 to index 1
        boxNode.Tabs.Move(0, 1);

        Assert.Equal(1, boxNode.Tabs.IndexOf(tab1));
        Assert.Equal(0, boxNode.Tabs.IndexOf(tab2));
    }

    [AvaloniaFact]
    public void Test_SuperSplitBox_TabDragStarted_Fires_When_Dragged_Outside()
    {
        var boxNode = new CDP.Editor.Splits.Models.BoxNode();
        var tab1 = boxNode.AddTab("Tab1", "Icon1", "View1");
        var tab2 = boxNode.AddTab("Tab2", "Icon2", "View2");

        var boxControl = new CDP.Editor.Splits.Controls.SuperSplitBox
        {
            DataContext = boxNode
        };

        var window = new Window { Content = boxControl };
        window.Show();

        bool eventFired = false;
        BoxTabNode? eventTab = null;

        boxControl.TabDragStarted += (sender, args) =>
        {
            eventFired = true;
            eventTab = args.Tab;
        };

        // Simulate the drag state via reflection or internal helper
        var isTabDraggingField = typeof(CDP.Editor.Splits.Controls.SuperSplitBox).GetField("_isTabDragging", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var draggingTabField = typeof(CDP.Editor.Splits.Controls.SuperSplitBox).GetField("_draggingTab", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var pressedArgsField = typeof(CDP.Editor.Splits.Controls.SuperSplitBox).GetField("_tabPressedEventArgs", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        Assert.NotNull(isTabDraggingField);
        Assert.NotNull(draggingTabField);
        Assert.NotNull(pressedArgsField);

        isTabDraggingField.SetValue(boxControl, true);
        draggingTabField.SetValue(boxControl, tab1);
        
        var dummyPressed = new PointerPressedEventArgs(
            boxControl,
            new Pointer(0, PointerType.Mouse, true),
            boxControl,
            new Point(0, 0),
            0,
            new PointerPointProperties(RawInputModifiers.None, PointerUpdateKind.LeftButtonPressed),
            KeyModifiers.None
        );
        pressedArgsField.SetValue(boxControl, dummyPressed);

        // Retrieve tabBorder of the first tab to raise PointerMoved
        var fieldPanel = typeof(CDP.Editor.Splits.Controls.SuperSplitBox).GetField("_tabsPanel", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        Assert.NotNull(fieldPanel);
        var tabsPanel = fieldPanel.GetValue(boxControl) as StackPanel;
        Assert.NotNull(tabsPanel);
        Assert.NotEmpty(tabsPanel.Children);
        var tabBorder = tabsPanel.Children[0] as Border;
        Assert.NotNull(tabBorder);

        // Raise a PointerMoved event at position outside the header (e.g. Y = -100)
        var dummyMoved = new PointerEventArgs(
            InputElement.PointerMovedEvent,
            tabBorder,
            new Pointer(0, PointerType.Mouse, true),
            tabBorder,
            new Point(0, -100),
            0UL,
            new PointerPointProperties(RawInputModifiers.None, PointerUpdateKind.Other),
            KeyModifiers.None
        );
        
        tabBorder.RaiseEvent(dummyMoved);

        Assert.True(eventFired);
        Assert.Equal(tab1, eventTab);

        window.Close();
    }

    [AvaloniaFact]
    public void Test_SuperSplitBox_Tab_Reordering_Preserves_Capture()
    {
        var boxNode = new CDP.Editor.Splits.Models.BoxNode();
        var tab1 = boxNode.AddTab("Tab1", "Icon1", "View1");
        var tab2 = boxNode.AddTab("Tab2", "Icon2", "View2");

        var boxControl = new CDP.Editor.Splits.Controls.SuperSplitBox
        {
            DataContext = boxNode
        };

        var window = new Window { Content = boxControl };
        window.Show();

        // Simulate the drag state via reflection
        var isTabDraggingField = typeof(CDP.Editor.Splits.Controls.SuperSplitBox).GetField("_isTabDragging", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var draggingTabField = typeof(CDP.Editor.Splits.Controls.SuperSplitBox).GetField("_draggingTab", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var pressedArgsField = typeof(CDP.Editor.Splits.Controls.SuperSplitBox).GetField("_tabPressedEventArgs", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        isTabDraggingField.SetValue(boxControl, true);
        draggingTabField.SetValue(boxControl, tab1);

        // Retrieve tabBorder of the first tab to raise PointerMoved
        var fieldPanel = typeof(CDP.Editor.Splits.Controls.SuperSplitBox).GetField("_tabsPanel", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        Assert.NotNull(fieldPanel);
        var tabsPanel = fieldPanel.GetValue(boxControl) as StackPanel;
        Assert.NotNull(tabsPanel);
        var tabBorder = tabsPanel.Children[0] as Border;
        Assert.NotNull(tabBorder);

        // Capture pointer to tabBorder
        var pointer = new Pointer(0, PointerType.Mouse, true);
        pointer.Capture(tabBorder);
        Assert.Equal(tabBorder, pointer.Captured);

        // Raise a PointerMoved event inside bounds but to the right of tab2 (e.g. X = 200, Y = 0)
        var dummyMoved = new PointerEventArgs(
            InputElement.PointerMovedEvent,
            tabBorder,
            pointer,
            tabBorder,
            new Point(200, 0),
            0UL,
            new PointerPointProperties(RawInputModifiers.None, PointerUpdateKind.Other),
            KeyModifiers.None
        );

        tabBorder.RaiseEvent(dummyMoved);

        // Dragging state should still be true (capture should be preserved)
        Assert.True((bool)isTabDraggingField.GetValue(boxControl));
        Assert.Equal(tab1, draggingTabField.GetValue(boxControl));

        window.Close();
    }
}
