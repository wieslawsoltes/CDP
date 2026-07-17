using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using CDP.Inspector.Wysiwyg.Controls;
using CdpInspectorApp.ViewModels;

namespace CdpInspectorApp.Views;

public partial class DesignerView : UserControl
{
    private bool _isDragging;
    private Point _dragStartPoint;
    private Rect _dragStartBounds;
    private double _initialMarginLeft;
    private double _initialMarginTop;
    private double _initialCanvasLeft;
    private double _initialCanvasTop;
    private ResizeHandle _activeHandle = ResizeHandle.None;

    private DesignerViewModel? Designer => (DataContext as MainWindowViewModel)?.Designer;

    public DesignerView()
    {
        InitializeComponent();

        var overlay = this.Find<DesignerOverlay>("designerOverlay");
        if (overlay != null)
        {
            overlay.PointerPressed += Overlay_PointerPressed;
            overlay.PointerMoved += Overlay_PointerMoved;
            overlay.PointerReleased += Overlay_PointerReleased;

            DragDrop.SetAllowDrop(overlay, true);
            overlay.AddHandler(DragDrop.DragOverEvent, Overlay_DragOver);
            overlay.AddHandler(DragDrop.DropEvent, Overlay_Drop);
        }

        var toolbox = this.Find<ItemsControl>("lstToolboxItems");
        if (toolbox != null)
        {
            toolbox.AddHandler(PointerPressedEvent, Toolbox_PointerPressed, RoutingStrategies.Tunnel | RoutingStrategies.Bubble, handledEventsToo: true);
        }
    }

    private async void Toolbox_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        var source = e.Source as Visual;
        while (source != null)
        {
            if (source is Button btn && btn.DataContext is ToolboxItemViewModel itemVM)
            {
                var xaml = itemVM.DefaultXaml;
                if (!string.IsNullOrEmpty(xaml))
                {
                    var dataObject = new DataTransfer();
                    var item = new DataTransferItem();
                    item.Set(DataFormat.Text, xaml);
                    dataObject.Add(item);
                    await DragDrop.DoDragDropAsync(e, dataObject, DragDropEffects.Copy);
                }
                break;
            }
            source = source.GetVisualParent();
        }
    }

    private void Overlay_DragOver(object? sender, DragEventArgs e)
    {
        if (e.DataTransfer.Formats.Contains(DataFormat.Text))
        {
            e.DragEffects = DragDropEffects.Copy;
            e.Handled = true;
        }
        else
        {
            e.DragEffects = DragDropEffects.None;
        }
    }

    private async void Overlay_Drop(object? sender, DragEventArgs e)
    {
        var overlay = sender as DesignerOverlay;
        if (overlay == null || Designer == null) return;

        var text = e.DataTransfer.TryGetText();
        if (!string.IsNullOrEmpty(text))
        {
            var point = e.GetPosition(overlay);
            await Designer.DropElementAtLocationAsync(text, point.X, point.Y);
            e.Handled = true;
        }
    }

    private void Overlay_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        var overlay = sender as DesignerOverlay;
        if (overlay == null || Designer == null) return;

        var point = e.GetCurrentPoint(overlay).Position;
        var handle = overlay.HitTestHandle(point);

        if (handle != ResizeHandle.None)
        {
            _isDragging = true;
            _activeHandle = handle;
            _dragStartPoint = point;
            _dragStartBounds = Designer.SelectedBounds;
            _initialMarginLeft = Designer.SelectedMarginLeft;
            _initialMarginTop = Designer.SelectedMarginTop;
            _initialCanvasLeft = Designer.CanvasLeft;
            _initialCanvasTop = Designer.CanvasTop;
            e.Pointer.Capture(overlay);
            e.Handled = true;
        }
        else if (Designer.SelectedBounds.Contains(point))
        {
            _isDragging = true;
            _activeHandle = ResizeHandle.None;
            _dragStartPoint = point;
            _dragStartBounds = Designer.SelectedBounds;
            _initialMarginLeft = Designer.SelectedMarginLeft;
            _initialMarginTop = Designer.SelectedMarginTop;
            _initialCanvasLeft = Designer.CanvasLeft;
            _initialCanvasTop = Designer.CanvasTop;
            e.Pointer.Capture(overlay);
            e.Handled = true;
        }
        else
        {
            bool ctrlPressed = e.KeyModifiers.HasFlag(KeyModifiers.Control);
            double targetX = point.X;
            double targetY = point.Y;

            _ = Designer.SelectElementAtLocationAsync(targetX, targetY, ctrlPressed);
            e.Handled = true;
        }
    }

    private void Overlay_PointerMoved(object? sender, PointerEventArgs e)
    {
        var overlay = sender as DesignerOverlay;
        if (overlay == null || Designer == null || !_isDragging) return;

        var point = e.GetPosition(overlay);
        var dx = point.X - _dragStartPoint.X;
        var dy = point.Y - _dragStartPoint.Y;

        double Snap(double val)
        {
            return Math.Round(val / 8.0) * 8.0;
        }

        var newBounds = _dragStartBounds;

        if (_activeHandle == ResizeHandle.None)
        {
            var newX = Snap(_dragStartBounds.X + dx);
            var newY = Snap(_dragStartBounds.Y + dy);
            newBounds = new Rect(newX, newY, _dragStartBounds.Width, _dragStartBounds.Height);

            if (Designer.ShowCanvasAttachmentEditor)
            {
                Designer.CanvasLeft = Snap(_initialCanvasLeft + (newX - _dragStartBounds.X));
                Designer.CanvasTop = Snap(_initialCanvasTop + (newY - _dragStartBounds.Y));
            }
            else
            {
                Designer.SelectedMarginLeft = Snap(_initialMarginLeft + (newX - _dragStartBounds.X));
                Designer.SelectedMarginTop = Snap(_initialMarginTop + (newY - _dragStartBounds.Y));
            }
        }
        else
        {
            var newX = _dragStartBounds.X;
            var newY = _dragStartBounds.Y;
            var newW = _dragStartBounds.Width;
            var newH = _dragStartBounds.Height;

            switch (_activeHandle)
            {
                case ResizeHandle.Left:
                    newX = Snap(_dragStartBounds.X + dx);
                    newW = Snap(_dragStartBounds.Width - dx);
                    break;
                case ResizeHandle.Right:
                    newW = Snap(_dragStartBounds.Width + dx);
                    break;
                case ResizeHandle.Top:
                    newY = Snap(_dragStartBounds.Y + dy);
                    newH = Snap(_dragStartBounds.Height - dy);
                    break;
                case ResizeHandle.Bottom:
                    newH = Snap(_dragStartBounds.Height + dy);
                    break;
                case ResizeHandle.TopLeft:
                    newX = Snap(_dragStartBounds.X + dx);
                    newW = Snap(_dragStartBounds.Width - dx);
                    newY = Snap(_dragStartBounds.Y + dy);
                    newH = Snap(_dragStartBounds.Height - dy);
                    break;
                case ResizeHandle.TopRight:
                    newW = Snap(_dragStartBounds.Width + dx);
                    newY = Snap(_dragStartBounds.Y + dy);
                    newH = Snap(_dragStartBounds.Height - dy);
                    break;
                case ResizeHandle.BottomLeft:
                    newX = Snap(_dragStartBounds.X + dx);
                    newW = Snap(_dragStartBounds.Width - dx);
                    newH = Snap(_dragStartBounds.Height + dy);
                    break;
                case ResizeHandle.BottomRight:
                    newW = Snap(_dragStartBounds.Width + dx);
                    newH = Snap(_dragStartBounds.Height + dy);
                    break;
            }

            if (newW > 0 && newH > 0)
            {
                newBounds = new Rect(newX, newY, newW, newH);

                if (Designer.ShowCanvasAttachmentEditor)
                {
                    Designer.CanvasLeft = Snap(_initialCanvasLeft + (newX - _dragStartBounds.X));
                    Designer.CanvasTop = Snap(_initialCanvasTop + (newY - _dragStartBounds.Y));
                }
                else
                {
                    Designer.SelectedMarginLeft = Snap(_initialMarginLeft + (newX - _dragStartBounds.X));
                    Designer.SelectedMarginTop = Snap(_initialMarginTop + (newY - _dragStartBounds.Y));
                }
            }
        }

        Designer.UpdateSelectedBoundsRealTime(newBounds.X, newBounds.Y, newBounds.Width, newBounds.Height);
        e.Handled = true;
    }

    private void Overlay_PointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        var overlay = sender as DesignerOverlay;
        if (overlay == null || !_isDragging || Designer == null) return;

        _isDragging = false;
        _activeHandle = ResizeHandle.None;
        e.Pointer.Capture(null);

        _ = Designer.ApplyDragFinishedAsync();
        e.Handled = true;
    }
}
