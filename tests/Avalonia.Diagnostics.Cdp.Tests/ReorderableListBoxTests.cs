using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Raw;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Avalonia.Headless.XUnit;
using CdpInspectorApp.Controls;
using Xunit;

namespace Avalonia.Diagnostics.Cdp.Tests;

public class ReorderableListBoxTests
{
    private static ListBoxItem? FindItem(Visual parent, string dataContextVal)
    {
        if (parent is ListBoxItem lbi && lbi.DataContext as string == dataContextVal)
            return lbi;
        foreach (var child in parent.GetVisualChildren())
        {
            var found = FindItem(child, dataContextVal);
            if (found != null) return found;
        }
        return null;
    }

    [AvaloniaFact]
    public async Task PointerPressed_LeftButton_StartsDragState()
    {
        var listBox = new ReorderableListBox();
        var list = new ObservableCollection<string> { "Step A", "Step B" };
        listBox.ItemsSource = list;

        var window = new Window { Content = listBox, Width = 300, Height = 300 };
        window.Show();
        await Task.Delay(50);

        var item = FindItem(listBox, "Step A");
        Assert.NotNull(item);

        var pressedArgs = new PointerPressedEventArgs(
            item,
            new Avalonia.Input.Pointer(0, PointerType.Mouse, true),
            window,
            new Point(5, 5),
            0,
            new PointerPointProperties(RawInputModifiers.LeftMouseButton, PointerUpdateKind.LeftButtonPressed),
            KeyModifiers.None);

        item.RaiseEvent(pressedArgs);

        var t = listBox.GetType();
        var fDraggedItem = t.GetField("_draggedItem", BindingFlags.NonPublic | BindingFlags.Instance);
        var fIsMouseDown = t.GetField("_isMouseDown", BindingFlags.NonPublic | BindingFlags.Instance);

        Assert.NotNull(fDraggedItem);
        Assert.NotNull(fIsMouseDown);

        Assert.Equal("Step A", fDraggedItem.GetValue(listBox));
        Assert.True((bool)fIsMouseDown.GetValue(listBox)!);

        window.Close();
    }

    [AvaloniaFact]
    public async Task PointerPressed_RightButton_DoesNotStartDragState()
    {
        var listBox = new ReorderableListBox();
        var list = new ObservableCollection<string> { "Step A", "Step B" };
        listBox.ItemsSource = list;

        var window = new Window { Content = listBox, Width = 300, Height = 300 };
        window.Show();
        await Task.Delay(50);

        var item = FindItem(listBox, "Step A");
        Assert.NotNull(item);

        var pressedArgs = new PointerPressedEventArgs(
            item,
            new Avalonia.Input.Pointer(0, PointerType.Mouse, true),
            window,
            new Point(5, 5),
            0,
            new PointerPointProperties(RawInputModifiers.RightMouseButton, PointerUpdateKind.RightButtonPressed),
            KeyModifiers.None);

        item.RaiseEvent(pressedArgs);

        var t = listBox.GetType();
        var fDraggedItem = t.GetField("_draggedItem", BindingFlags.NonPublic | BindingFlags.Instance);
        var fIsMouseDown = t.GetField("_isMouseDown", BindingFlags.NonPublic | BindingFlags.Instance);

        Assert.Null(fDraggedItem?.GetValue(listBox));
        Assert.False((bool)fIsMouseDown?.GetValue(listBox)!);

        window.Close();
    }

    [AvaloniaFact]
    public async Task PointerPressed_BypassedControls_DoesNotStartDragState()
    {
        var listBox = new ReorderableListBox();
        var list = new ObservableCollection<string> { "Step A", "Step B" };
        listBox.ItemsSource = list;

        var window = new Window { Content = listBox, Width = 300, Height = 300 };
        window.Show();
        await Task.Delay(50);

        var item = FindItem(listBox, "Step A");
        Assert.NotNull(item);

        // TextBox inside item to simulate bypassed control
        var textBox = new TextBox { Name = "txtBypass" };
        item.Content = textBox;
        await Task.Delay(10);

        var pressedArgs = new PointerPressedEventArgs(
            textBox,
            new Avalonia.Input.Pointer(0, PointerType.Mouse, true),
            window,
            new Point(5, 5),
            0,
            new PointerPointProperties(RawInputModifiers.LeftMouseButton, PointerUpdateKind.LeftButtonPressed),
            KeyModifiers.None);

        textBox.RaiseEvent(pressedArgs);

        var t = listBox.GetType();
        var fDraggedItem = t.GetField("_draggedItem", BindingFlags.NonPublic | BindingFlags.Instance);
        var fIsMouseDown = t.GetField("_isMouseDown", BindingFlags.NonPublic | BindingFlags.Instance);

        Assert.Null(fDraggedItem?.GetValue(listBox));
        Assert.False((bool)fIsMouseDown?.GetValue(listBox)!);

        window.Close();
    }

    [AvaloniaFact]
    public async Task PointerPressed_HandledByChild_BubblesToReorderableListBox()
    {
        var listBox = new ReorderableListBox();
        var list = new ObservableCollection<string> { "Step A", "Step B" };
        listBox.ItemsSource = list;

        var window = new Window { Content = listBox, Width = 300, Height = 300 };
        window.Show();
        await Task.Delay(50);

        var item = FindItem(listBox, "Step A");
        Assert.NotNull(item);

        var pressedArgs = new PointerPressedEventArgs(
            item,
            new Avalonia.Input.Pointer(0, PointerType.Mouse, true),
            window,
            new Point(5, 5),
            0,
            new PointerPointProperties(RawInputModifiers.LeftMouseButton, PointerUpdateKind.LeftButtonPressed),
            KeyModifiers.None);

        // Mark as Handled = true to simulate standard ListBoxItem event handling
        pressedArgs.Handled = true;

        item.RaiseEvent(pressedArgs);

        var t = listBox.GetType();
        var fDraggedItem = t.GetField("_draggedItem", BindingFlags.NonPublic | BindingFlags.Instance);
        var fIsMouseDown = t.GetField("_isMouseDown", BindingFlags.NonPublic | BindingFlags.Instance);

        Assert.Equal("Step A", fDraggedItem?.GetValue(listBox));
        Assert.True((bool)fIsMouseDown?.GetValue(listBox)!);

        window.Close();
    }

    [AvaloniaFact]
    public async Task LstSteps_Drop_OnItem_Below_ReordersCollection()
    {
        var listBox = new ReorderableListBox();
        var list = new ObservableCollection<string> { "Step A", "Step B" };
        listBox.ItemsSource = list;

        var window = new Window { Content = listBox, Width = 300, Height = 300 };
        window.Show();
        await Task.Delay(50);

        var item1 = FindItem(listBox, "Step A");
        var item2 = FindItem(listBox, "Step B");
        Assert.NotNull(item1);
        Assert.NotNull(item2);

        // Set dragged item state via reflection
        var t = listBox.GetType();
        var fDraggedItem = t.GetField("_draggedItem", BindingFlags.NonPublic | BindingFlags.Instance);
        fDraggedItem?.SetValue(listBox, "Step A");

        var dropArgs = new DragEventArgs(
            DragDrop.DropEvent,
            new DataTransfer(),
            item2,
            new Point(0, 100), // Below center
            KeyModifiers.None);

        item2.RaiseEvent(dropArgs);

        Assert.Equal("Step B", list[0]);
        Assert.Equal("Step A", list[1]);

        window.Close();
    }

    [AvaloniaFact]
    public async Task LstSteps_Drop_OnItem_Above_ReordersCollection()
    {
        var listBox = new ReorderableListBox();
        var list = new ObservableCollection<string> { "Step A", "Step B" };
        listBox.ItemsSource = list;

        var window = new Window { Content = listBox, Width = 300, Height = 300 };
        window.Show();
        await Task.Delay(50);

        var item1 = FindItem(listBox, "Step A");
        var item2 = FindItem(listBox, "Step B");
        Assert.NotNull(item1);
        Assert.NotNull(item2);

        // Set dragged item state via reflection (drag Step B)
        var t = listBox.GetType();
        var fDraggedItem = t.GetField("_draggedItem", BindingFlags.NonPublic | BindingFlags.Instance);
        fDraggedItem?.SetValue(listBox, "Step B");

        var dropArgs = new DragEventArgs(
            DragDrop.DropEvent,
            new DataTransfer(),
            item1,
            new Point(0, -5), // Above center
            KeyModifiers.None);

        item1.RaiseEvent(dropArgs);

        Assert.Equal("Step B", list[0]);
        Assert.Equal("Step A", list[1]);

        window.Close();
    }

    [AvaloniaFact]
    public async Task LstSteps_Drop_OnEmptyArea_MovesToLast()
    {
        var listBox = new ReorderableListBox();
        var list = new ObservableCollection<string> { "Step A", "Step B", "Step C" };
        listBox.ItemsSource = list;

        var window = new Window { Content = listBox, Width = 300, Height = 300 };
        window.Show();
        await Task.Delay(50);

        // Set dragged item state via reflection (drag "Step A")
        var t = listBox.GetType();
        var fDraggedItem = t.GetField("_draggedItem", BindingFlags.NonPublic | BindingFlags.Instance);
        fDraggedItem?.SetValue(listBox, "Step A");

        // Simulate dropping on the ListBox itself (representing empty area)
        var dropArgs = new DragEventArgs(
            DragDrop.DropEvent,
            new DataTransfer(),
            listBox,
            new Point(0, 0),
            KeyModifiers.None);

        listBox.RaiseEvent(dropArgs);

        // Expected order: Step B, Step C, Step A (moved to the end of the collection)
        Assert.Equal("Step B", list[0]);
        Assert.Equal("Step C", list[1]);
        Assert.Equal("Step A", list[2]);

        window.Close();
    }

    [AvaloniaFact]
    public async Task DragOver_AboveCenter_SetsDragOverAboveClass()
    {
        var listBox = new ReorderableListBox();
        var list = new ObservableCollection<string> { "Step A", "Step B" };
        listBox.ItemsSource = list;

        var window = new Window { Content = listBox, Width = 300, Height = 300 };
        window.Show();
        await Task.Delay(50);

        var item1 = FindItem(listBox, "Step A");
        var item2 = FindItem(listBox, "Step B");
        Assert.NotNull(item1);
        Assert.NotNull(item2);

        // Set dragged item state via reflection
        var t = listBox.GetType();
        var fDraggedItem = t.GetField("_draggedItem", BindingFlags.NonPublic | BindingFlags.Instance);
        fDraggedItem?.SetValue(listBox, "Step A");

        var dragOverArgs = new DragEventArgs(
            DragDrop.DragOverEvent,
            new DataTransfer(),
            item2,
            new Point(0, -5), // Above center
            KeyModifiers.None);

        item2.RaiseEvent(dragOverArgs);

        Assert.Contains("drag-over-above", item2.Classes);
        Assert.DoesNotContain("drag-over-below", item2.Classes);

        window.Close();
    }

    [AvaloniaFact]
    public async Task DragOver_BelowCenter_SetsDragOverBelowClass()
    {
        var listBox = new ReorderableListBox();
        var list = new ObservableCollection<string> { "Step A", "Step B" };
        listBox.ItemsSource = list;

        var window = new Window { Content = listBox, Width = 300, Height = 300 };
        window.Show();
        await Task.Delay(50);

        var item1 = FindItem(listBox, "Step A");
        var item2 = FindItem(listBox, "Step B");
        Assert.NotNull(item1);
        Assert.NotNull(item2);

        // Set dragged item state via reflection
        var t = listBox.GetType();
        var fDraggedItem = t.GetField("_draggedItem", BindingFlags.NonPublic | BindingFlags.Instance);
        fDraggedItem?.SetValue(listBox, "Step A");

        var dragOverArgs = new DragEventArgs(
            DragDrop.DragOverEvent,
            new DataTransfer(),
            item2,
            new Point(0, 100), // Below center
            KeyModifiers.None);

        item2.RaiseEvent(dragOverArgs);

        Assert.DoesNotContain("drag-over-above", item2.Classes);
        Assert.Contains("drag-over-below", item2.Classes);

        window.Close();
    }

    [AvaloniaFact]
    public async Task DragLeave_ClearsDragOverClasses()
    {
        var listBox = new ReorderableListBox();
        var list = new ObservableCollection<string> { "Step A", "Step B" };
        listBox.ItemsSource = list;

        var window = new Window { Content = listBox, Width = 300, Height = 300 };
        window.Show();
        await Task.Delay(50);

        var item2 = FindItem(listBox, "Step B");
        Assert.NotNull(item2);

        // Set dragged item state via reflection
        var t = listBox.GetType();
        var fDraggedItem = t.GetField("_draggedItem", BindingFlags.NonPublic | BindingFlags.Instance);
        fDraggedItem?.SetValue(listBox, "Step A");

        // DragOver first to set class
        var dragOverArgs = new DragEventArgs(
            DragDrop.DragOverEvent,
            new DataTransfer(),
            item2,
            new Point(0, -5),
            KeyModifiers.None);
        item2.RaiseEvent(dragOverArgs);

        Assert.Contains("drag-over-above", item2.Classes);

        // Now trigger DragLeave
        var dragLeaveArgs = new DragEventArgs(
            DragDrop.DragLeaveEvent,
            new DataTransfer(),
            listBox,
            new Point(-10, -10),
            KeyModifiers.None);
        listBox.RaiseEvent(dragLeaveArgs);

        Assert.DoesNotContain("drag-over-above", item2.Classes);
        Assert.DoesNotContain("drag-over-below", item2.Classes);

        window.Close();
    }

    [AvaloniaFact]
    public async Task Drop_ClearsDragOverClasses()
    {
        var listBox = new ReorderableListBox();
        var list = new ObservableCollection<string> { "Step A", "Step B" };
        listBox.ItemsSource = list;

        var window = new Window { Content = listBox, Width = 300, Height = 300 };
        window.Show();
        await Task.Delay(50);

        var item2 = FindItem(listBox, "Step B");
        Assert.NotNull(item2);

        // Set dragged item state via reflection
        var t = listBox.GetType();
        var fDraggedItem = t.GetField("_draggedItem", BindingFlags.NonPublic | BindingFlags.Instance);
        fDraggedItem?.SetValue(listBox, "Step A");

        // DragOver first to set class
        var dragOverArgs = new DragEventArgs(
            DragDrop.DragOverEvent,
            new DataTransfer(),
            item2,
            new Point(0, -5),
            KeyModifiers.None);
        item2.RaiseEvent(dragOverArgs);

        Assert.Contains("drag-over-above", item2.Classes);

        // Now trigger Drop
        var dropArgs = new DragEventArgs(
            DragDrop.DropEvent,
            new DataTransfer(),
            item2,
            new Point(0, -5),
            KeyModifiers.None);
        item2.RaiseEvent(dropArgs);

        Assert.DoesNotContain("drag-over-above", item2.Classes);
        Assert.DoesNotContain("drag-over-below", item2.Classes);

        window.Close();
    }

    [AvaloniaFact]
    public async Task DragOver_NearTopEdge_ScrollsUp()
    {
        var listBox = new ReorderableListBox();
        var list = new ObservableCollection<string>();
        for (int i = 0; i < 50; i++)
        {
            list.Add($"Step {i}");
        }
        listBox.ItemsSource = list;

        var window = new Window { Content = listBox, Width = 300, Height = 200 };
        window.Show();
        await Task.Delay(100);

        var scrollViewer = listBox.GetVisualDescendants().OfType<ScrollViewer>().FirstOrDefault();
        Assert.NotNull(scrollViewer);

        // Scroll down initially
        scrollViewer.Offset = new Vector(0, 50);
        await Task.Delay(20);

        // Set dragged item state via reflection
        var t = listBox.GetType();
        var fDraggedItem = t.GetField("_draggedItem", BindingFlags.NonPublic | BindingFlags.Instance);
        fDraggedItem?.SetValue(listBox, "Step 0");

        // Drag over near top edge (Point(0, 5))
        var dragOverArgs = new DragEventArgs(
            DragDrop.DragOverEvent,
            new DataTransfer(),
            listBox,
            new Point(0, 5),
            KeyModifiers.None);
        listBox.RaiseEvent(dragOverArgs);

        // Call the tick handler using reflection to simulate timer tick
        var mTick = t.GetMethod("AutoScrollTimer_Tick", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(mTick);
        mTick.Invoke(listBox, new object[] { listBox, EventArgs.Empty });

        Assert.True(scrollViewer.Offset.Y < 50, $"Expected Offset Y to be less than 50, but was {scrollViewer.Offset.Y}");

        window.Close();
    }

    [AvaloniaFact]
    public async Task DragOver_NearBottomEdge_ScrollsDown()
    {
        var listBox = new ReorderableListBox();
        var list = new ObservableCollection<string>();
        for (int i = 0; i < 50; i++)
        {
            list.Add($"Step {i}");
        }
        listBox.ItemsSource = list;

        var window = new Window { Content = listBox, Width = 300, Height = 200 };
        window.Show();
        await Task.Delay(100);

        var scrollViewer = listBox.GetVisualDescendants().OfType<ScrollViewer>().FirstOrDefault();
        Assert.NotNull(scrollViewer);

        // Start at top
        scrollViewer.Offset = new Vector(0, 0);
        await Task.Delay(20);

        // Set dragged item state via reflection
        var t = listBox.GetType();
        var fDraggedItem = t.GetField("_draggedItem", BindingFlags.NonPublic | BindingFlags.Instance);
        fDraggedItem?.SetValue(listBox, "Step 0");

        double bottomEdgeY = listBox.Bounds.Height - 5;
        Assert.True(bottomEdgeY > 0);

        // Drag over near bottom edge
        var dragOverArgs = new DragEventArgs(
            DragDrop.DragOverEvent,
            new DataTransfer(),
            listBox,
            new Point(0, bottomEdgeY),
            KeyModifiers.None);
        listBox.RaiseEvent(dragOverArgs);

        // Call the tick handler using reflection to simulate timer tick
        var mTick = t.GetMethod("AutoScrollTimer_Tick", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(mTick);
        mTick.Invoke(listBox, new object[] { listBox, EventArgs.Empty });

        Assert.True(scrollViewer.Offset.Y > 0, $"Expected Offset Y to be greater than 0, but was {scrollViewer.Offset.Y}");

        window.Close();
    }
}
