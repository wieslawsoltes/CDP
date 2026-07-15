using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
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
            var img = this.Find<Image>("imgDesignerScreenshot");
            var sim = (DataContext as MainWindowViewModel)?.Simulation;
            double targetX = point.X;
            double targetY = point.Y;

            if (img != null && sim != null && img.Bounds.Width > 0 && img.Bounds.Height > 0)
            {
                targetX = point.X * (sim.DeviceWidth / img.Bounds.Width);
                targetY = point.Y * (sim.DeviceHeight / img.Bounds.Height);
            }

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
                Designer.CanvasLeft = Snap(_initialCanvasLeft + dx);
                Designer.CanvasTop = Snap(_initialCanvasTop + dy);
            }
            else
            {
                Designer.SelectedMarginLeft = Snap(_initialMarginLeft + dx);
                Designer.SelectedMarginTop = Snap(_initialMarginTop + dy);
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
