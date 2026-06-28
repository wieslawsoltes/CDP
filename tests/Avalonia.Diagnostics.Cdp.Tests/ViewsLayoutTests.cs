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
}
