using Avalonia.Headless.XUnit;
using Xunit;
using CdpInspectorApp.Views;
using CdpInspectorApp.ViewModels;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using System;

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
}
