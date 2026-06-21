using System;
using System.Collections;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;

namespace CdpInspectorApp.Controls;

public class ReorderableListBox : ListBox
{
    private Point _dragStartPoint;
    private bool _isMouseDown;
    private object? _draggedItem;
    private PointerPressedEventArgs? _pressedEventArgs;

    private static object? s_draggedItem;

    protected override Type StyleKeyOverride => typeof(ListBox);

    public ReorderableListBox()
    {
        DragDrop.SetAllowDrop(this, true);
        AddHandler(DragDrop.DragOverEvent, LstSteps_DragOver);
        AddHandler(DragDrop.DropEvent, LstSteps_Drop);
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);

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

    protected override async void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);

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
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
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
        }
        else
        {
            e.DragEffects = DragDropEffects.None;
        }
        e.Handled = true;
    }

    private void LstSteps_Drop(object? sender, DragEventArgs e)
    {
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
                destIdx = list.IndexOf(destItem);
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
}
