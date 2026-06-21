using System;
using System.Collections;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace CdpInspectorApp.Controls;

public class ReorderableListBox : ListBox
{
    private Point _dragStartPoint;
    private bool _isMouseDown;
    private object? _draggedItem;
    private PointerPressedEventArgs? _pressedEventArgs;

    private static object? s_draggedItem;

    private ListBoxItem? _lastDropTarget;
    private DispatcherTimer? _autoScrollTimer;
    private Point _lastDragPosition;
    private bool _shouldAutoScroll;

    protected override Type StyleKeyOverride => typeof(ListBox);

    public ReorderableListBox()
    {
        DragDrop.SetAllowDrop(this, true);
        AddHandler(DragDrop.DragOverEvent, LstSteps_DragOver);
        AddHandler(DragDrop.DropEvent, LstSteps_Drop);
        AddHandler(DragDrop.DragLeaveEvent, LstSteps_DragLeave);

        _autoScrollTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(50)
        };
        _autoScrollTimer.Tick += AutoScrollTimer_Tick;

        // Register handlers to process events even if they were handled by child controls (like ListBoxItems)
        AddHandler(PointerPressedEvent, (s, e) => { if (e.Handled) ProcessPointerPressed(e); }, RoutingStrategies.Bubble, true);
        AddHandler(PointerMovedEvent, (s, e) => { if (e.Handled) ProcessPointerMoved(e); }, RoutingStrategies.Bubble, true);
        AddHandler(PointerReleasedEvent, (s, e) => { if (e.Handled) ProcessPointerReleased(e); }, RoutingStrategies.Bubble, true);
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        ProcessPointerPressed(e);
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        ProcessPointerMoved(e);
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        ProcessPointerReleased(e);
    }

    private void ProcessPointerPressed(PointerPressedEventArgs e)
    {
        _isMouseDown = false;
        _draggedItem = null;
        _pressedEventArgs = null;

        var properties = e.GetCurrentPoint(this);
        if (!properties.Properties.IsLeftButtonPressed) return;

        ListBoxItem? listBoxItem = null;
        var current = e.Source as Avalonia.Visual;
        Console.WriteLine($"[DEBUG ReorderableListBox] OnPointerPressed: Source={e.Source?.GetType().Name}");
        while (current != null)
        {
            if (current is TextBox || current is Button || current is AutoCompleteBox)
            {
                Console.WriteLine($"[DEBUG ReorderableListBox] OnPointerPressed: Bypassing input/action control {current.GetType().Name}");
                return;
            }
            if (current is ListBoxItem item)
            {
                listBoxItem = item;
            }
            if (current == this)
            {
                break;
            }
            current = current.GetVisualParent();
        }

        if (listBoxItem != null && listBoxItem.DataContext != null)
        {
            _isMouseDown = true;
            _dragStartPoint = e.GetPosition(this);
            _draggedItem = listBoxItem.DataContext;
            s_draggedItem = _draggedItem;
            _pressedEventArgs = e;
            Console.WriteLine($"[DEBUG ReorderableListBox] OnPointerPressed: Drag target candidate set: {_draggedItem.GetType().Name}");
        }
    }

    private async void ProcessPointerMoved(PointerEventArgs e)
    {
        if (!_isMouseDown || _draggedItem == null || _pressedEventArgs == null) return;

        var currentPoint = e.GetPosition(this);
        var diff = _dragStartPoint - currentPoint;
        if (Math.Abs(diff.X) > 10 || Math.Abs(diff.Y) > 10)
        {
            Console.WriteLine($"[DEBUG ReorderableListBox] OnPointerMoved: Drag threshold exceeded. Initiating DoDragDropAsync for {_draggedItem.GetType().Name}");
            _isMouseDown = false;

            var dragData = new DataTransfer();
            dragData.Add(DataTransferItem.CreateText("reorder"));

            var triggerEvent = _pressedEventArgs;
            _pressedEventArgs = null;

            var result = await DragDrop.DoDragDropAsync(triggerEvent, dragData, DragDropEffects.Move);
            Console.WriteLine($"[DEBUG ReorderableListBox] OnPointerMoved: DoDragDropAsync completed with result={result}");
            _draggedItem = null;
            s_draggedItem = null;
            _shouldAutoScroll = false;
            _autoScrollTimer?.Stop();
            if (_lastDropTarget != null)
            {
                _lastDropTarget.Classes.Remove("drag-over-above");
                _lastDropTarget.Classes.Remove("drag-over-below");
                _lastDropTarget = null;
            }
        }
    }

    private void ProcessPointerReleased(PointerReleasedEventArgs e)
    {
        Console.WriteLine($"[DEBUG ReorderableListBox] OnPointerReleased: _isMouseDown={_isMouseDown}, _draggedItem={_draggedItem != null}, _pressedEventArgs={_pressedEventArgs != null}");

        _isMouseDown = false;
        // Only clear dragged item if we haven't started a drag-and-drop session yet
        if (_pressedEventArgs != null)
        {
            _draggedItem = null;
            s_draggedItem = null;
            _pressedEventArgs = null;
        }
    }

    private void LstSteps_DragOver(object? sender, DragEventArgs e)
    {
        var draggedItem = s_draggedItem ?? _draggedItem;
        Console.WriteLine($"[DEBUG ReorderableListBox] LstSteps_DragOver: draggedItem={draggedItem != null}");
        if (draggedItem != null)
        {
            e.DragEffects = DragDropEffects.Move;

            ListBoxItem? destListBoxItem = null;
            var current = e.Source as Avalonia.Visual;
            while (current != null)
            {
                if (current is ListBoxItem item)
                {
                    destListBoxItem = item;
                    break;
                }
                if (current == this)
                {
                    break;
                }
                current = current.GetVisualParent();
            }

            if (_lastDropTarget != null && _lastDropTarget != destListBoxItem)
            {
                _lastDropTarget.Classes.Remove("drag-over-above");
                _lastDropTarget.Classes.Remove("drag-over-below");
            }

            if (destListBoxItem != null)
            {
                var position = e.GetPosition(destListBoxItem);
                bool isAbove = position.Y < destListBoxItem.Bounds.Height / 2.0;

                destListBoxItem.Classes.Set("drag-over-above", isAbove);
                destListBoxItem.Classes.Set("drag-over-below", !isAbove);

                _lastDropTarget = destListBoxItem;
            }
            else
            {
                _lastDropTarget = null;
            }

            // Update drag positions for auto-scrolling
            _lastDragPosition = e.GetPosition(this);
            _shouldAutoScroll = true;
            if (_autoScrollTimer != null && !_autoScrollTimer.IsEnabled)
            {
                _autoScrollTimer.Start();
            }
        }
        else
        {
            e.DragEffects = DragDropEffects.None;
            if (_lastDropTarget != null)
            {
                _lastDropTarget.Classes.Remove("drag-over-above");
                _lastDropTarget.Classes.Remove("drag-over-below");
                _lastDropTarget = null;
            }
            _shouldAutoScroll = false;
            _autoScrollTimer?.Stop();
        }
        e.Handled = true;
    }

    private void LstSteps_DragLeave(object? sender, DragEventArgs e)
    {
        // Check if the pointer is actually outside the bounds of the ListBox
        var position = e.GetPosition(this);
        var bounds = Bounds;
        bool isOutside = position.X < 0 || position.X > bounds.Width ||
                         position.Y < 0 || position.Y > bounds.Height;

        if (isOutside)
        {
            if (_lastDropTarget != null)
            {
                _lastDropTarget.Classes.Remove("drag-over-above");
                _lastDropTarget.Classes.Remove("drag-over-below");
                _lastDropTarget = null;
            }
            _shouldAutoScroll = false;
            _autoScrollTimer?.Stop();
        }
    }

    private void LstSteps_Drop(object? sender, DragEventArgs e)
    {
        if (_lastDropTarget != null)
        {
            _lastDropTarget.Classes.Remove("drag-over-above");
            _lastDropTarget.Classes.Remove("drag-over-below");
            _lastDropTarget = null;
        }
        _shouldAutoScroll = false;
        _autoScrollTimer?.Stop();

        var draggedItem = s_draggedItem ?? _draggedItem;
        Console.WriteLine($"[DEBUG ReorderableListBox] LstSteps_Drop: draggedItem={draggedItem != null}, Source={e.Source?.GetType().Name}");
        if (draggedItem == null) return;

        ListBoxItem? destListBoxItem = null;
        var current = e.Source as Avalonia.Visual;
        while (current != null)
        {
            if (current is ListBoxItem item)
            {
                destListBoxItem = item;
                break;
            }
            if (current == this)
            {
                break;
            }
            current = current.GetVisualParent();
        }

        if (ItemsSource is IList list && !list.IsReadOnly)
        {
            int sourceIdx = list.IndexOf(draggedItem);
            int destIdx = -1;

            if (destListBoxItem != null && destListBoxItem.DataContext != null)
            {
                var destItem = destListBoxItem.DataContext;
                if (draggedItem == destItem)
                {
                    Console.WriteLine("[DEBUG ReorderableListBox] LstSteps_Drop: Source and destination are the same.");
                    return;
                }

                int targetIdx = list.IndexOf(destItem);
                var position = e.GetPosition(destListBoxItem);
                bool isAbove = position.Y < destListBoxItem.Bounds.Height / 2.0;

                if (sourceIdx < targetIdx)
                {
                    destIdx = isAbove ? targetIdx - 1 : targetIdx;
                }
                else if (sourceIdx > targetIdx)
                {
                    destIdx = isAbove ? targetIdx : targetIdx + 1;
                }
                else
                {
                    destIdx = sourceIdx;
                }
            }
            else
            {
                destIdx = list.Count - 1;
                Console.WriteLine($"[DEBUG ReorderableListBox] LstSteps_Drop: Empty area drop, defaulting to last index: {destIdx}");
            }

            Console.WriteLine($"[DEBUG ReorderableListBox] LstSteps_Drop: Moving from idx={sourceIdx} to idx={destIdx}");

            if (sourceIdx >= 0 && destIdx >= 0 && sourceIdx != destIdx)
            {
                bool moved = false;
                try
                {
                    var moveMethod = list.GetType().GetMethod("Move", new[] { typeof(int), typeof(int) });
                    if (moveMethod != null)
                    {
                        moveMethod.Invoke(list, new object[] { sourceIdx, destIdx });
                        moved = true;
                        Console.WriteLine("[DEBUG ReorderableListBox] LstSteps_Drop: Moved via collection.Move()");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[DEBUG ReorderableListBox] LstSteps_Drop: Move() failed: {ex.Message}");
                }

                if (!moved)
                {
                    try
                    {
                        list.RemoveAt(sourceIdx);
                        list.Insert(destIdx, draggedItem);
                        Console.WriteLine("[DEBUG ReorderableListBox] LstSteps_Drop: Moved via RemoveAt/Insert");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[DEBUG ReorderableListBox] LstSteps_Drop: RemoveAt/Insert failed: {ex.Message}");
                    }
                }
            }
        }
        e.Handled = true;
    }

    private void AutoScrollTimer_Tick(object? sender, EventArgs e)
    {
        if (!_shouldAutoScroll)
        {
            _autoScrollTimer?.Stop();
            return;
        }

        var scrollViewer = this.GetVisualDescendants().OfType<ScrollViewer>().FirstOrDefault();
        if (scrollViewer == null) return;

        double threshold = 30.0;
        double positionY = _lastDragPosition.Y;
        double scrollSpeed = 0.0;

        if (positionY < threshold)
        {
            scrollSpeed = -5.0 * (1.0 - (Math.Max(0.0, positionY) / threshold));
        }
        else if (positionY > Bounds.Height - threshold)
        {
            double distFromBottom = Bounds.Height - positionY;
            scrollSpeed = 5.0 * (1.0 - (Math.Max(0.0, distFromBottom) / threshold));
        }

        if (Math.Abs(scrollSpeed) > 0.1)
        {
            double newOffset = scrollViewer.Offset.Y + scrollSpeed;
            double maxOffset = scrollViewer.Extent.Height - scrollViewer.Viewport.Height;
            newOffset = Math.Max(0.0, Math.Min(maxOffset, newOffset));
            scrollViewer.Offset = new Vector(scrollViewer.Offset.X, newOffset);
        }
    }
}
