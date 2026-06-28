using System;
using Xunit;
using Avalonia;
using Avalonia.Controls;
using CDP.Editor.Nodes.ViewModels;
using CDP.Editor.Nodes.Views;

namespace Avalonia.Diagnostics.Cdp.Tests;

public class LogicalScrollingTests
{
    [Fact]
    public void CanvasPanel_ReportsIsLogicalScrollEnabledTrue()
    {
        var panel = new NodeEditorCanvasPanel();
        Assert.True(panel.IsLogicalScrollEnabled);
    }

    [Fact]
    public void CanvasPanel_OffsetAndExtentStayInSyncWithViewModel()
    {
        var vm = new NodeEditorViewModel();
        var panel = new NodeEditorCanvasPanel
        {
            DataContext = vm
        };

        // Default extent is 5000 x 5000 at zoom = 1.0
        Assert.Equal(5000.0, panel.Extent.Width);
        Assert.Equal(5000.0, panel.Extent.Height);

        // Zoom change updates extent
        vm.Zoom = 2.0;
        Assert.Equal(10000.0, panel.Extent.Width);
        Assert.Equal(10000.0, panel.Extent.Height);

        // Offset sets View Model Pan coordinates (negative values)
        panel.Offset = new Vector(300, 400);
        Assert.Equal(-300.0, vm.PanX);
        Assert.Equal(-400.0, vm.PanY);

        // View Model Pan changes update Panel Offset (positive values)
        vm.PanX = -600.0;
        vm.PanY = -800.0;
        Assert.Equal(600.0, panel.Offset.X);
        Assert.Equal(800.0, panel.Offset.Y);
    }

    [Fact]
    public void CanvasPanel_RaisesScrollInvalidatedEventOnViewModelChanges()
    {
        var vm = new NodeEditorViewModel();
        var panel = new NodeEditorCanvasPanel
        {
            DataContext = vm
        };

        int eventCount = 0;
        panel.ScrollInvalidated += (s, e) => eventCount++;

        // PanX change triggers ScrollInvalidated
        vm.PanX = -100.0;
        Assert.Equal(1, eventCount);

        // PanY change triggers ScrollInvalidated
        vm.PanY = -200.0;
        Assert.Equal(2, eventCount);

        // Zoom change triggers ScrollInvalidated
        vm.Zoom = 1.5;
        Assert.Equal(3, eventCount);
    }
}
