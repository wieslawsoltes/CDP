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
}
