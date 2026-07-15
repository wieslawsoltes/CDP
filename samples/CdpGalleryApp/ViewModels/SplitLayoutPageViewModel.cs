using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using CDP.Editor.Splits.Controls;
using CDP.Editor.Splits.Models;

namespace CdpGalleryApp.ViewModels;

public class SplitLayoutPageViewModel : ViewModelBase
{
    private SplitNode? _splitRoot;

    public SplitNode? SplitRoot
    {
        get => _splitRoot;
        set => RaiseAndSetIfChanged(ref _splitRoot, value);
    }

    public Func<string, SuperSplitBox?, Control> ViewResolver { get; }

    public SplitLayoutPageViewModel()
    {
        // Define default layout root tree
        var panel1 = new BoxNode("Terminal", "Terminal Output", "TerminalIcon");
        var panel2 = new BoxNode("Outline", "Outline View", "CodeIcon");
        var panel3 = new BoxNode("Output", "Compilation Output", "FileIcon");

        var rightSplit = new SplitContainerNode(Orientation.Vertical, panel2, panel3)
        {
            SplitterRatio = 0.5
        };

        _splitRoot = new SplitContainerNode(Orientation.Horizontal, panel1, rightSplit)
        {
            SplitterRatio = 0.4
        };

        // Custom ViewResolver that renders simple panels so users can drag, split, dock, or float panels.
        ViewResolver = (viewName, splitBox) =>
        {
            var border = new Border
            {
                Background = Brush.Parse("#1e1e1e"),
                BorderBrush = Brush.Parse("#3c4043"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Margin = new Thickness(4),
                Padding = new Thickness(12)
            };

            var stack = new StackPanel
            {
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                Spacing = 8
            };

            stack.Children.Add(new TextBlock
            {
                Text = $"Dock Content: {viewName}",
                FontWeight = FontWeight.Bold,
                FontSize = 14,
                HorizontalAlignment = HorizontalAlignment.Center,
                Foreground = Brush.Parse("#8ab4f8")
            });

            stack.Children.Add(new TextBlock
            {
                Text = "Drag tab headers to dock/undock, split, or float panels.",
                FontSize = 10,
                HorizontalAlignment = HorizontalAlignment.Center,
                Foreground = Brush.Parse("#9aa0a6")
            });

            border.Child = stack;
            return border;
        };
    }
}
