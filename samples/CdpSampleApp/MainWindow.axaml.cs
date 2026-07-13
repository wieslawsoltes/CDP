using System;
using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Input;

namespace CdpSampleApp;

public partial class MainWindow : Window
{
    private readonly Stopwatch _stopwatch = new();
    private bool _dragSourcePressed;

    public MainWindow()
    {
        InitializeComponent();
        DataContext = new ViewModels.MainWindowViewModel();
    }

    public void Navigate(string url)
    {
        if (DataContext is ViewModels.MainWindowViewModel vm)
        {
            if (url.EndsWith("/about", StringComparison.OrdinalIgnoreCase))
            {
                vm.SelectedTabIndex = 2; // About tab
            }
            else if (url.EndsWith("/scroll", StringComparison.OrdinalIgnoreCase))
            {
                vm.SelectedTabIndex = 1; // Scroll tab
            }
            else if (url.EndsWith("/gestures", StringComparison.OrdinalIgnoreCase))
            {
                vm.SelectedTabIndex = 3; // Gestures tab
            }
            else
            {
                vm.SelectedTabIndex = 0; // Home tab
            }
        }
    }

    public void OnDoubleClick(object? sender, Avalonia.Input.TappedEventArgs e)
    {
        if (DataContext is ViewModels.MainWindowViewModel vm)
        {
            vm.DoubleClickedCount++;
            vm.DoubleClickStatus = $"Double Clicked {vm.DoubleClickedCount} times!";
        }
    }

    public void OnPointerPressed(object? sender, Avalonia.Input.PointerPressedEventArgs e)
    {
        _stopwatch.Restart();
    }

    public void OnPointerReleased(object? sender, Avalonia.Input.PointerReleasedEventArgs e)
    {
        _stopwatch.Stop();
        if (_stopwatch.ElapsedMilliseconds > 800)
        {
            if (DataContext is ViewModels.MainWindowViewModel vm)
            {
                vm.LongPressedCount++;
                vm.LongPressStatus = $"Long Pressed {vm.LongPressedCount} times!";
            }
        }
    }

    public void OnDragSourcePointerPressed(object? sender, Avalonia.Input.PointerPressedEventArgs e)
    {
        _dragSourcePressed = true;
    }

    public void OnDropTargetPointerReleased(object? sender, Avalonia.Input.PointerReleasedEventArgs e)
    {
        if (_dragSourcePressed)
        {
            if (DataContext is ViewModels.MainWindowViewModel vm)
            {
                vm.DragDropStatus = "Dropped Successfully!";
            }
            _dragSourcePressed = false;
        }
    }

    public void OnKeyDown(object? sender, Avalonia.Input.KeyEventArgs e)
    {
        if (DataContext is ViewModels.MainWindowViewModel vm)
        {
            vm.LastPressedKey = e.Key.ToString();
        }
    }

    public void BtnDoubleClick_DoubleTapped(object? sender, Avalonia.Input.TappedEventArgs e)
    {
        OnDoubleClick(sender, e);
    }

    public void BtnLongPress_PointerPressed(object? sender, Avalonia.Input.PointerPressedEventArgs e)
    {
        OnPointerPressed(sender, e);
    }

    public void BtnLongPress_PointerReleased(object? sender, Avalonia.Input.PointerReleasedEventArgs e)
    {
        OnPointerReleased(sender, e);
    }
    public void BorderDragSource_PointerPressed(object? sender, Avalonia.Input.PointerPressedEventArgs e)
    {
        OnDragSourcePointerPressed(sender, e);
    }

    public void BorderDropTarget_PointerReleased(object? sender, Avalonia.Input.PointerReleasedEventArgs e)
    {
        OnDropTargetPointerReleased(sender, e);
    }

    public void TxtKeyInput_KeyDown(object? sender, Avalonia.Input.KeyEventArgs e)
    {
        OnKeyDown(sender, e);
    }
}