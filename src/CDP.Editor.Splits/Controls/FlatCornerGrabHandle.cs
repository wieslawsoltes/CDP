using System;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using CDP.Editor.Splits.Models;

namespace CDP.Editor.Splits.Controls;

public class FlatCornerGrabHandle : Control
{
    public SplitContainerNode ParentContainer { get; }
    public SplitContainerNode ChildContainer { get; }

    private bool _isPressed;
    public bool IsPressed => _isPressed;
    private Point _dragStartPoint;
    private double _startParentRatio;
    private double _startChildRatio;

    public FlatCornerGrabHandle(SplitContainerNode parentContainer, SplitContainerNode childContainer)
    {
        ParentContainer = parentContainer;
        ChildContainer = childContainer;
        ZIndex = 2;

        // Choose TopLeftCorner cursor for diagonal NWSE resizing
        Cursor = new Cursor(StandardCursorType.TopLeftCorner);
        
        // Expose as an automation element with a resize/splitter-like behavior
        AutomationProperties.SetHelpText(this, "Corner Splitter Handle");
        AutomationProperties.SetName(this, "Corner Splitter Handle");
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            var panel = Parent as FlatSplitPanel;
            if (panel != null)
            {
                _isPressed = true;
                e.Pointer.Capture(this);
                _dragStartPoint = e.GetPosition(panel);
                _startParentRatio = ParentContainer.SplitterRatio;
                _startChildRatio = ChildContainer.SplitterRatio;
                e.Handled = true;
            }
        }
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (_isPressed)
        {
            var panel = Parent as FlatSplitPanel;
            if (panel != null)
            {
                var currentPos = e.GetPosition(panel);
                double deltaX = currentPos.X - _dragStartPoint.X;
                double deltaY = currentPos.Y - _dragStartPoint.Y;

                // Adjust Parent Ratio
                if (panel.ContainerBounds.TryGetValue(ParentContainer, out var pRect))
                {
                    double totalSize = ParentContainer.Orientation == Orientation.Horizontal 
                        ? pRect.Width - 8.0 
                        : pRect.Height - 8.0;

                    if (totalSize > 0)
                    {
                        double delta = ParentContainer.Orientation == Orientation.Horizontal ? deltaX : deltaY;
                        double ratioDelta = delta / totalSize;
                        ParentContainer.SplitterRatio = Math.Clamp(_startParentRatio + ratioDelta, 0.05, 0.95);
                    }
                }

                // Adjust Child Ratio
                if (panel.ContainerBounds.TryGetValue(ChildContainer, out var cRect))
                {
                    double totalSize = ChildContainer.Orientation == Orientation.Horizontal 
                        ? cRect.Width - 8.0 
                        : cRect.Height - 8.0;

                    if (totalSize > 0)
                    {
                        double delta = ChildContainer.Orientation == Orientation.Horizontal ? deltaX : deltaY;
                        double ratioDelta = delta / totalSize;
                        ChildContainer.SplitterRatio = Math.Clamp(_startChildRatio + ratioDelta, 0.05, 0.95);
                    }
                }

                e.Handled = true;
            }
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (_isPressed)
        {
            _isPressed = false;
            e.Pointer.Capture(null);
            e.Handled = true;
        }
    }

    public override void Render(DrawingContext context)
    {
        context.DrawRectangle(Brushes.Transparent, null, new Rect(Bounds.Size));
    }

    protected override AutomationPeer OnCreateAutomationPeer()
    {
        return new ControlAutomationPeer(this);
    }
}
