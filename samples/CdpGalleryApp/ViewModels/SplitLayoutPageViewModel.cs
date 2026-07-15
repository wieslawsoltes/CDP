using System;
using System.Collections.Generic;
using System.Windows.Input;
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
    private BoxNode? _selectedPane;

    public SplitNode? SplitRoot
    {
        get => _splitRoot;
        set => RaiseAndSetIfChanged(ref _splitRoot, value);
    }

    public BoxNode? SelectedPane
    {
        get => _selectedPane;
        set => RaiseAndSetIfChanged(ref _selectedPane, value);
    }

    public Func<string, SuperSplitBox?, Control> ViewResolver { get; }

    public ICommand SplitLeftCommand { get; }
    public ICommand SplitRightCommand { get; }
    public ICommand SplitUpCommand { get; }
    public ICommand SplitDownCommand { get; }
    public ICommand ClosePaneCommand { get; }
    public ICommand ResetLayoutCommand { get; }
    public ICommand FloatPaneCommand { get; }

    public SplitLayoutPageViewModel()
    {
        // Initialize Commands
        SplitLeftCommand = new RelayCommand(() => SplitSelected(Orientation.Horizontal, true));
        SplitRightCommand = new RelayCommand(() => SplitSelected(Orientation.Horizontal, false));
        SplitUpCommand = new RelayCommand(() => SplitSelected(Orientation.Vertical, true));
        SplitDownCommand = new RelayCommand(() => SplitSelected(Orientation.Vertical, false));
        ClosePaneCommand = new RelayCommand(CloseSelected);
        ResetLayoutCommand = new RelayCommand(ResetLayout);
        FloatPaneCommand = new RelayCommand(FloatSelected);

        // Reset to default layout
        ResetLayout();

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

    private void SplitSelected(Orientation orientation, bool insertBefore)
    {
        var selected = SelectedPane;
        if (selected == null) return;

        BoxNode newBox;

        if (selected.Tabs.Count > 1 && selected.ActiveTab != null)
        {
            var activeTab = selected.ActiveTab;
            selected.Tabs.Remove(activeTab);
            if (selected.Tabs.Count > 0)
            {
                selected.ActiveTab = selected.Tabs[0];
            }

            newBox = new BoxNode
            {
                BackgroundTint = "#292a2d"
            };
            newBox.Tabs.Add(activeTab);
            newBox.ActiveTab = activeTab;
        }
        else
        {
            string[] views = { "Terminal", "Outline", "Output", "Properties", "Explorer", "Timeline", "Issues" };
            string viewName = "Terminal";
            foreach (var v in views)
            {
                if (FindBoxNodeByViewName(SplitRoot, v) == null)
                {
                    viewName = v;
                    break;
                }
            }

            newBox = new BoxNode
            {
                BackgroundTint = "#292a2d"
            };
            newBox.AddTab(viewName, "DocumentIcon", viewName);
        }

        if (selected == SplitRoot)
        {
            var newContainer = insertBefore
                ? new SplitContainerNode(orientation, newBox, selected)
                : new SplitContainerNode(orientation, selected, newBox);
            SplitRoot = newContainer;
        }
        else if (selected.Parent is SplitContainerNode parent)
        {
            var newContainer = insertBefore
                ? new SplitContainerNode(orientation, newBox, selected)
                : new SplitContainerNode(orientation, selected, newBox);

            if (parent.Child1 == selected)
            {
                parent.Child1 = newContainer;
            }
            else
            {
                parent.Child2 = newContainer;
            }
        }

        SelectedPane = newBox;
    }

    private void CloseSelected()
    {
        var selected = SelectedPane;
        if (selected == null || selected == SplitRoot) return;

        if (selected.Parent is SplitContainerNode parent)
        {
            var sibling = parent.Child1 == selected ? parent.Child2 : parent.Child1;
            var grandparent = parent.Parent;

            if (parent == SplitRoot)
            {
                sibling.Parent = null;
                SplitRoot = sibling;
                if (sibling is BoxNode boxSibling) SelectedPane = boxSibling;
            }
            else if (grandparent is SplitContainerNode gp)
            {
                if (gp.Child1 == parent)
                {
                    gp.Child1 = sibling;
                }
                else
                {
                    gp.Child2 = sibling;
                }
                if (sibling is BoxNode boxSibling) SelectedPane = boxSibling;
            }
        }
    }

    private void ResetLayout()
    {
        var panel1 = new BoxNode("Terminal", "Terminal Output", "TerminalIcon");
        var panel2 = new BoxNode("Outline", "Outline View", "CodeIcon");
        var panel3 = new BoxNode("Output", "Compilation Output", "FileIcon");

        var rightSplit = new SplitContainerNode(Orientation.Vertical, panel2, panel3)
        {
            SplitterRatio = 0.5
        };

        SplitRoot = new SplitContainerNode(Orientation.Horizontal, panel1, rightSplit)
        {
            SplitterRatio = 0.4
        };
        SelectedPane = panel1;
    }

    private void FloatSelected()
    {
        var selected = SelectedPane;
        if (selected == null) return;

        BoxNode nodeToFloat;
        if (selected.Tabs.Count > 1 && selected.ActiveTab != null)
        {
            var activeTab = selected.ActiveTab;
            selected.Tabs.Remove(activeTab);
            if (selected.Tabs.Count > 0)
            {
                selected.ActiveTab = selected.Tabs[0];
            }

            nodeToFloat = new BoxNode
            {
                BackgroundTint = "#292a2d"
            };
            nodeToFloat.Tabs.Add(activeTab);
            nodeToFloat.ActiveTab = activeTab;
        }
        else
        {
            nodeToFloat = selected;
            if (selected == SplitRoot)
            {
                SplitRoot = null;
                SelectedPane = null;
            }
            else if (selected.Parent is SplitContainerNode parent)
            {
                var sibling = parent.Child1 == selected ? parent.Child2 : parent.Child1;
                var grandparent = parent.Parent;

                if (parent == SplitRoot)
                {
                    sibling.Parent = null;
                    SplitRoot = sibling;
                    if (sibling is BoxNode boxSibling) SelectedPane = boxSibling;
                    else if (sibling is SplitContainerNode sc) SelectedPane = FindFirstBoxNode(sc);
                }
                else if (grandparent is SplitContainerNode gp)
                {
                    if (gp.Child1 == parent)
                    {
                        gp.Child1 = sibling;
                    }
                    else
                    {
                        gp.Child2 = sibling;
                    }
                    if (sibling is BoxNode boxSibling) SelectedPane = boxSibling;
                    else if (sibling is SplitContainerNode sc) SelectedPane = FindFirstBoxNode(sc);
                }
            }
        }

        SuperSplitDragManager.FloatNodeCallback?.Invoke(null!, nodeToFloat);
    }

    private BoxNode? FindBoxNodeByViewName(SplitNode? node, string viewName)
    {
        if (node is BoxNode box)
        {
            foreach (var tab in box.Tabs)
            {
                if (tab.SelectedViewName == viewName) return box;
            }
            return null;
        }
        else if (node is SplitContainerNode container)
        {
            var found = FindBoxNodeByViewName(container.Child1, viewName);
            if (found != null) return found;
            return FindBoxNodeByViewName(container.Child2, viewName);
        }
        return null;
    }

    private BoxNode? FindFirstBoxNode(SplitNode? node)
    {
        if (node is BoxNode box) return box;
        if (node is SplitContainerNode container)
        {
            var found = FindFirstBoxNode(container.Child1);
            if (found != null) return found;
            return FindFirstBoxNode(container.Child2);
        }
        return null;
    }

    private class RelayCommand : ICommand
    {
        private readonly Action _execute;
        public RelayCommand(Action execute) => _execute = execute;
        public bool CanExecute(object? parameter) => true;
        public void Execute(object? parameter) => _execute();
        public event EventHandler? CanExecuteChanged { add { } remove { } }
    }
}
