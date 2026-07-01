using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using CDP.Editor.Splits.Controls;
using CDP.Editor.Splits.Models;

namespace CDP.Inspector.Shared.Controls;

public class FloatingSplitWindow : Window
{
    private readonly SuperSplit _superSplit;
    private readonly SuperSplit _mainSplit;

    public FloatingSplitWindow(SuperSplit mainSplit, BoxNode rootNode)
    {
        _mainSplit = mainSplit;

        Title = "Inspector Panel - Floating";
        Width = 800;
        Height = 600;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Background = Brush.Parse("#202124");
        RequestedThemeVariant = Avalonia.Styling.ThemeVariant.Dark;

        _superSplit = new SuperSplit
        {
            ViewResolver = mainSplit.ViewResolver,
            Root = rootNode
        };

        _superSplit.LayoutRebuilt += OnLayoutRebuilt;

        Content = _superSplit;
    }

    private void OnLayoutRebuilt(object? sender, EventArgs e)
    {
        if (_superSplit.Root == null)
        {
            _superSplit.LayoutRebuilt -= OnLayoutRebuilt;
            Close();
        }
    }

    protected override void OnClosing(WindowClosingEventArgs e)
    {
        base.OnClosing(e);

        if (_superSplit.Root != null)
        {
            var nodeToMove = _superSplit.Root;
            _superSplit.Root = null; // Detach from floating split

            if (_mainSplit.Root == null)
            {
                _mainSplit.Root = nodeToMove;
            }
            else
            {
                var newRoot = new SplitContainerNode(Orientation.Horizontal, _mainSplit.Root, nodeToMove)
                {
                    SplitterRatio = 0.7
                };
                _mainSplit.Root = newRoot;
            }
            _mainSplit.Rebuild();
        }
    }
}
