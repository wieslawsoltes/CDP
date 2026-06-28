#nullable enable

using System;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using CDP.Editor.Nodes.ViewModels;

namespace CDP.Editor.Nodes.Views;

public class NodeEditorCanvasPanel : Canvas, ILogicalScrollable
{
    private Vector _offset;
    private NodeEditorViewModel? _viewModel;

    public bool CanHorizontallyScroll { get; set; } = true;
    public bool CanVerticallyScroll { get; set; } = true;

    public bool IsLogicalScrollEnabled => true;

    public Size ScrollSize => new(16, 16);
    public Size PageScrollSize => new(80, 80);

    public event EventHandler? ScrollInvalidated;

    public Size Extent
    {
        get
        {
            if (DataContext is NodeEditorViewModel vm)
            {
                return new Size(5000 * vm.Zoom, 5000 * vm.Zoom);
            }
            return new Size(5000, 5000);
        }
    }

    public Size Viewport => (Parent as Visual)?.Bounds.Size ?? Bounds.Size;

    public Vector Offset
    {
        get
        {
            if (DataContext is NodeEditorViewModel vm)
            {
                return new Vector(-vm.PanX, -vm.PanY);
            }
            return _offset;
        }
        set
        {
            if (DataContext is NodeEditorViewModel vm)
            {
                var maxScrollX = Math.Max(0, Extent.Width - Viewport.Width);
                var maxScrollY = Math.Max(0, Extent.Height - Viewport.Height);
                double targetX = Math.Clamp(value.X, 0, maxScrollX);
                double targetY = Math.Clamp(value.Y, 0, maxScrollY);

                if (Math.Abs(vm.PanX + targetX) > 0.001 || Math.Abs(vm.PanY + targetY) > 0.001)
                {
                    vm.PanX = -targetX;
                    vm.PanY = -targetY;
                    _offset = new Vector(targetX, targetY);
                    ScrollInvalidated?.Invoke(this, EventArgs.Empty);
                }
            }
            else
            {
                _offset = value;
            }
        }
    }

    public bool BringIntoView(Control target, Rect targetRect)
    {
        return false;
    }

    public Control? GetControlInDirection(NavigationDirection direction, Control? from)
    {
        return null;
    }

    public void RaiseScrollInvalidated(EventArgs e)
    {
        ScrollInvalidated?.Invoke(this, e);
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        if (_viewModel != null)
        {
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }

        _viewModel = DataContext as NodeEditorViewModel;

        if (_viewModel != null)
        {
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(NodeEditorViewModel.PanX) ||
            e.PropertyName == nameof(NodeEditorViewModel.PanY) ||
            e.PropertyName == nameof(NodeEditorViewModel.Zoom))
        {
            ScrollInvalidated?.Invoke(this, EventArgs.Empty);
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == BoundsProperty)
        {
            ScrollInvalidated?.Invoke(this, EventArgs.Empty);
        }
    }
}
