using System;
using System.Collections.Generic;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.VisualTree;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using CdpInspectorApp.ViewModels;
using CDP.Editor.Splits.Controls;
using CDP.Editor.Splits.Models;

namespace CdpInspectorApp.Views;

public partial class MainView : UserControl
{
    private readonly Dictionary<string, Control> _viewsCache = new();
    private readonly HashSet<SplitNode> _subscribedNodes = new();

    public MainView()
    {
        InitializeComponent();

        var vm = new MainWindowViewModel();
        DataContext = vm;

        SplitControl.BoxMenuClicked += OnBoxMenuClicked;

        // Scan targets on load
        Dispatcher.UIThread.Post(() => vm.Connection.RefreshTargetsCommand.Execute(null));
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (DataContext is MainWindowViewModel vm)
        {
            vm.PropertyChanged += OnViewModelPropertyChanged;

            // Re-bind the DataContext of all cached views to keep them in sync
            foreach (var view in _viewsCache.Values)
            {
                view.DataContext = vm;
            }

            UnsubscribeFromTree(vm.LayoutRoot);
            SubscribeToTree(vm.LayoutRoot);
            PopulateTreeViews(vm.LayoutRoot);
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainWindowViewModel.LayoutRoot))
        {
            if (DataContext is MainWindowViewModel vm)
            {
                UnsubscribeFromTree(vm.LayoutRoot);
                SubscribeToTree(vm.LayoutRoot);
                PopulateTreeViews(vm.LayoutRoot);
            }
        }
    }

    private void SubscribeToTree(SplitNode? node)
    {
        if (node == null) return;
        if (!_subscribedNodes.Contains(node))
        {
            node.PropertyChanged += OnNodePropertyChanged;
            _subscribedNodes.Add(node);
        }

        if (node is SplitContainerNode container)
        {
            SubscribeToTree(container.Child1);
            SubscribeToTree(container.Child2);
        }
    }

    private void UnsubscribeFromTree(SplitNode? node)
    {
        if (node == null) return;
        if (_subscribedNodes.Contains(node))
        {
            node.PropertyChanged -= OnNodePropertyChanged;
            _subscribedNodes.Remove(node);
        }

        if (node is SplitContainerNode container)
        {
            UnsubscribeFromTree(container.Child1);
            UnsubscribeFromTree(container.Child2);
        }
    }

    private void OnNodePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SplitContainerNode.Child1) ||
            e.PropertyName == nameof(SplitContainerNode.Child2) ||
            e.PropertyName == nameof(BoxNode.SelectedViewName))
        {
            if (DataContext is MainWindowViewModel vm)
            {
                // Resubscribe to new nodes and populate them
                UnsubscribeFromTree(vm.LayoutRoot);
                SubscribeToTree(vm.LayoutRoot);
                PopulateTreeViews(vm.LayoutRoot);
            }
        }
    }

    private void ClearViewDuplicates(SplitNode? node, string viewName, BoxNode? currentBox)
    {
        if (node == null) return;
        if (node is BoxNode box)
        {
            if (box != currentBox && box.SelectedViewName == viewName)
            {
                box.Content = null;
                box.SelectedViewName = string.Empty;
                box.Title = "Empty Pane";
                box.IconKey = "DocumentIcon";
            }
        }
        else if (node is SplitContainerNode container)
        {
            ClearViewDuplicates(container.Child1, viewName, currentBox);
            ClearViewDuplicates(container.Child2, viewName, currentBox);
        }
    }

    private void PopulateTreeViews(SplitNode? node)
    {
        if (node == null) return;
        if (node is BoxNode box)
        {
            if (!string.IsNullOrEmpty(box.SelectedViewName))
            {
                if (DataContext is MainWindowViewModel vm)
                {
                    ClearViewDuplicates(vm.LayoutRoot, box.SelectedViewName, box);
                }

                var view = GetOrCreateViewInstance(box.SelectedViewName);
                if (box.Content != view)
                {
                    box.Content = view;
                }
            }
        }
        else if (node is SplitContainerNode container)
        {
            PopulateTreeViews(container.Child1);
            PopulateTreeViews(container.Child2);
        }
    }

    private void DetachControl(Control control)
    {
        if (control.Parent is SuperSplitBox splitBox)
        {
            splitBox.InnerContent = null;
        }
        else if (control.Parent is Panel panel)
        {
            panel.Children.Remove(control);
        }
        else if (control.Parent is ContentControl contentControl)
        {
            contentControl.Content = null;
        }

        var visualParent = control.GetVisualParent();
        if (visualParent is ContentPresenter presenter)
        {
            if (control.Parent == null)
            {
                presenter.Content = null;
            }
        }
        else if (visualParent is Panel visualPanel)
        {
            visualPanel.Children.Remove(control);
        }
    }

    private Control GetOrCreateViewInstance(string viewName)
    {
        if (_viewsCache.TryGetValue(viewName, out var cached))
        {
            DetachControl(cached);
            return cached;
        }

        Control view = viewName switch
        {
            "Simulation" => new SimulationView(),
            "Elements" => new ElementsView(),
            "Console" => new ConsoleView(),
            "Sources" => new SourcesView(),
            "Network" => new NetworkView(),
            "Performance" => new PerformanceView(),
            "Memory" => new MemoryView(),
            "Application" => new ApplicationView(),
            "Audits" => new AuditsView(),
            "Recorder" => new RecorderView(),
            "Window" => new WindowControlView(),
            _ => new TextBlock { Text = $"View {viewName} not implemented", Margin = new Thickness(10) }
        };

        view.DataContext = DataContext;
        _viewsCache[viewName] = view;
        return view;
    }

    private void OnBoxMenuClicked(object? sender, BoxMenuEventArgs e)
    {
        var boxNode = e.Node;
        var anchor = e.AnchorControl;

        var contextMenu = new ContextMenu();
        string[] views = { "Simulation", "Elements", "Console", "Sources", "Network", "Performance", "Memory", "Application", "Audits", "Recorder", "Window" };

        foreach (var viewName in views)
        {
            var item = new MenuItem
            {
                Header = viewName,
                Icon = new PathIcon
                {
                    Data = Application.Current?.FindResource(MainWindowViewModel.GetIconKeyForView(viewName)) as Geometry,
                    Width = 12,
                    Height = 12
                }
            };
            item.Click += (s, ev) =>
            {
                boxNode.SelectedViewName = viewName;
                boxNode.Title = viewName;
                boxNode.IconKey = MainWindowViewModel.GetIconKeyForView(viewName);

                if (DataContext is MainWindowViewModel vm)
                {
                    ClearViewDuplicates(vm.LayoutRoot, viewName, boxNode);
                }

                boxNode.Content = GetOrCreateViewInstance(viewName);
            };
            contextMenu.Items.Add(item);
        }

        contextMenu.Open(anchor);
    }
}
