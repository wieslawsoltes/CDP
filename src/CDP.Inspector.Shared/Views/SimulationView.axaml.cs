using System;
using System.IO;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using CdpInspectorApp.ViewModels;

namespace CdpInspectorApp.Views;

public partial class SimulationView : UserControl
{
    private ConnectionViewModel? _connectionVm;

    public SimulationView()
    {
        InitializeComponent();
        
        DataContextChanged += SimulationView_DataContextChanged;

        var img = this.Find<Image>("imgScreenshot");
        if (img != null)
        {
            img.PointerPressed += Image_PointerPressed;
            img.PointerReleased += Image_PointerReleased;
            img.PointerMoved += Image_PointerMoved;
            img.PointerWheelChanged += Image_PointerWheelChanged;
        }

        var border = this.Find<Border>("borderScreenshot");
        if (border != null)
        {
            border.KeyDown += Border_KeyDown;
            border.KeyUp += Border_KeyUp;
            border.TextInput += Border_TextInput;
        }
    }

    private void SimulationView_DataContextChanged(object? sender, EventArgs e)
    {
        if (_connectionVm != null)
        {
            _connectionVm.PropertyChanged -= ConnectionVm_PropertyChanged;
            _connectionVm = null;
        }

        if (DataContext is MainWindowViewModel mainVm)
        {
            _connectionVm = mainVm.Connection;
            _connectionVm.PropertyChanged += ConnectionVm_PropertyChanged;
            UpdateCursor(_connectionVm.IsInspectModeActive);
        }
        else
        {
            UpdateCursor(false);
        }
    }

    private void ConnectionVm_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ConnectionViewModel.IsInspectModeActive) && _connectionVm != null)
        {
            UpdateCursor(_connectionVm.IsInspectModeActive);
        }
    }

    private void UpdateCursor(bool isInspectActive)
    {
        var img = this.Find<Image>("imgScreenshot");
        if (img != null)
        {
            img.Cursor = isInspectActive ? new Cursor(StandardCursorType.Cross) : null;
        }
    }

    private void Image_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        var border = this.Find<Border>("borderScreenshot");
        border?.Focus();
        SendMouseEvent("mousePressed", e);
        e.Handled = true;
    }

    private void Image_PointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        SendMouseEvent("mouseReleased", e);
        e.Handled = true;
    }

    private void Image_PointerMoved(object? sender, PointerEventArgs e)
    {
        SendMouseEvent("mouseMoved", e);
        e.Handled = true;
    }

    private void Image_PointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        var img = this.Find<Image>("imgScreenshot");
        if (img == null || img.Source is not Bitmap bitmap) return;

        var pointerPoint = e.GetCurrentPoint(img);
        var pos = pointerPoint.Position;

        double imageWidth = img.Bounds.Width;
        double imageHeight = img.Bounds.Height;

        double targetX = pos.X;
        double targetY = pos.Y;

        if (targetX < 0 || targetX > imageWidth || targetY < 0 || targetY > imageHeight) return;

        if (DataContext is MainWindowViewModel mainVm && mainVm.Simulation != null)
        {
            _ = mainVm.Simulation.SendWheelEventAsync(targetX, targetY, e.Delta.Y);
        }
        e.Handled = true;
    }

    private void SendMouseEvent(string type, PointerEventArgs e)
    {
        var img = this.Find<Image>("imgScreenshot");
        if (img == null || img.Source is not Bitmap bitmap) return;

        var pointerPoint = e.GetCurrentPoint(img);
        var pos = pointerPoint.Position;

        double imageWidth = img.Bounds.Width;
        double imageHeight = img.Bounds.Height;

        double targetX = pos.X;
        double targetY = pos.Y;

        // Clamp to image boundaries
        if (targetX < 0 || targetX > imageWidth || targetY < 0 || targetY > imageHeight)
        {
            return;
        }

        if (DataContext is MainWindowViewModel mainVm && mainVm.Simulation != null)
        {
            var button = "none";
            if (type == "mouseReleased")
            {
                button = pointerPoint.Properties.PointerUpdateKind switch
                {
                    PointerUpdateKind.LeftButtonReleased => "left",
                    PointerUpdateKind.RightButtonReleased => "right",
                    PointerUpdateKind.MiddleButtonReleased => "middle",
                    _ => "left"
                };
            }
            else
            {
                button = pointerPoint.Properties.IsLeftButtonPressed ? "left" :
                         pointerPoint.Properties.IsRightButtonPressed ? "right" :
                         pointerPoint.Properties.IsMiddleButtonPressed ? "middle" : "none";
            }

            int modifiers = 0;
            if (e.KeyModifiers.HasFlag(KeyModifiers.Alt)) modifiers |= 1;
            if (e.KeyModifiers.HasFlag(KeyModifiers.Control)) modifiers |= 2;
            if (e.KeyModifiers.HasFlag(KeyModifiers.Meta)) modifiers |= 4;
            if (e.KeyModifiers.HasFlag(KeyModifiers.Shift)) modifiers |= 8;

            int buttons = 0;
            if (pointerPoint.Properties.IsLeftButtonPressed) buttons |= 1;
            if (pointerPoint.Properties.IsRightButtonPressed) buttons |= 2;
            if (pointerPoint.Properties.IsMiddleButtonPressed) buttons |= 4;

            _ = mainVm.Simulation.SendMouseEventAsync(type, targetX, targetY, button, modifiers, buttons);
        }
    }

    private void Border_KeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is MainWindowViewModel mainVm && mainVm.Simulation != null)
        {
            int modifiers = 0;
            if (e.KeyModifiers.HasFlag(KeyModifiers.Alt)) modifiers |= 1;
            if (e.KeyModifiers.HasFlag(KeyModifiers.Control)) modifiers |= 2;
            if (e.KeyModifiers.HasFlag(KeyModifiers.Meta)) modifiers |= 4;
            if (e.KeyModifiers.HasFlag(KeyModifiers.Shift)) modifiers |= 8;

            string keyStr = MapKey(e.Key);
            _ = mainVm.Simulation.SendKeyboardEventAsync("rawKeyDown", keyStr, modifiers);
        }
        
        if (e.Key is Key.Tab or Key.Left or Key.Right or Key.Up or Key.Down or Key.Back or Key.Delete or Key.Escape or Key.Enter)
        {
            e.Handled = true;
        }
    }

    private void Border_KeyUp(object? sender, KeyEventArgs e)
    {
        if (DataContext is MainWindowViewModel mainVm && mainVm.Simulation != null)
        {
            int modifiers = 0;
            if (e.KeyModifiers.HasFlag(KeyModifiers.Alt)) modifiers |= 1;
            if (e.KeyModifiers.HasFlag(KeyModifiers.Control)) modifiers |= 2;
            if (e.KeyModifiers.HasFlag(KeyModifiers.Meta)) modifiers |= 4;
            if (e.KeyModifiers.HasFlag(KeyModifiers.Shift)) modifiers |= 8;

            string keyStr = MapKey(e.Key);
            _ = mainVm.Simulation.SendKeyboardEventAsync("keyUp", keyStr, modifiers);
        }
        
        if (e.Key is Key.Tab or Key.Left or Key.Right or Key.Up or Key.Down or Key.Back or Key.Delete or Key.Escape or Key.Enter)
        {
            e.Handled = true;
        }
    }

    private void Border_TextInput(object? sender, TextInputEventArgs e)
    {
        if (string.IsNullOrEmpty(e.Text)) return;
        if (DataContext is MainWindowViewModel mainVm && mainVm.Simulation != null)
        {
            _ = mainVm.Simulation.SendTextInputAsync(e.Text);
        }
        e.Handled = true;
    }

    private string MapKey(Key key)
    {
        return key switch
        {
            Key.Back => "Backspace",
            Key.Tab => "Tab",
            Key.Enter => "Enter",
            Key.Escape => "Escape",
            Key.Space => "Space",
            Key.Delete => "Delete",
            Key.Left => "ArrowLeft",
            Key.Right => "ArrowRight",
            Key.Up => "ArrowUp",
            Key.Down => "ArrowDown",
            Key.PageUp => "PageUp",
            Key.PageDown => "PageDown",
            Key.Home => "Home",
            Key.End => "End",
            _ => key.ToString()
        };
    }
}
