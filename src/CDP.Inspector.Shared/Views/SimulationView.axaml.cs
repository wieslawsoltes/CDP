using System;
using System.IO;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using CdpInspectorApp.ViewModels;

namespace CdpInspectorApp.Views;

public partial class SimulationView : UserControl
{
    public SimulationView()
    {
        InitializeComponent();
        
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
        double bitmapWidth = bitmap.Size.Width;
        double bitmapHeight = bitmap.Size.Height;

        double dx = 0;
        double dy = 0;

        if (imageWidth > bitmapWidth) dx = (imageWidth - bitmapWidth) / 2;
        if (imageHeight > bitmapHeight) dy = (imageHeight - bitmapHeight) / 2;

        double targetX = pos.X - dx;
        double targetY = pos.Y - dy;

        if (targetX < 0 || targetX > bitmapWidth || targetY < 0 || targetY > bitmapHeight) return;

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
        double bitmapWidth = bitmap.Size.Width;
        double bitmapHeight = bitmap.Size.Height;

        double dx = 0;
        double dy = 0;

        if (imageWidth > bitmapWidth)
        {
            dx = (imageWidth - bitmapWidth) / 2;
        }
        if (imageHeight > bitmapHeight)
        {
            dy = (imageHeight - bitmapHeight) / 2;
        }

        double targetX = pos.X - dx;
        double targetY = pos.Y - dy;

        // Clamp to bitmap boundaries
        if (targetX < 0 || targetX > bitmapWidth || targetY < 0 || targetY > bitmapHeight)
        {
            return;
        }

        if (DataContext is MainWindowViewModel mainVm && mainVm.Simulation != null)
        {
            var button = pointerPoint.Properties.IsLeftButtonPressed ? "left" :
                         pointerPoint.Properties.IsRightButtonPressed ? "right" :
                         pointerPoint.Properties.IsMiddleButtonPressed ? "middle" : "none";

            int modifiers = 0;
            if (e.KeyModifiers.HasFlag(KeyModifiers.Alt)) modifiers |= 1;
            if (e.KeyModifiers.HasFlag(KeyModifiers.Control)) modifiers |= 2;
            if (e.KeyModifiers.HasFlag(KeyModifiers.Shift)) modifiers |= 4;
            if (e.KeyModifiers.HasFlag(KeyModifiers.Meta)) modifiers |= 8;

            _ = mainVm.Simulation.SendMouseEventAsync(type, targetX, targetY, button, modifiers);
        }
    }

    private void Border_KeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is MainWindowViewModel mainVm && mainVm.Simulation != null)
        {
            int modifiers = 0;
            if (e.KeyModifiers.HasFlag(KeyModifiers.Alt)) modifiers |= 1;
            if (e.KeyModifiers.HasFlag(KeyModifiers.Control)) modifiers |= 2;
            if (e.KeyModifiers.HasFlag(KeyModifiers.Shift)) modifiers |= 4;
            if (e.KeyModifiers.HasFlag(KeyModifiers.Meta)) modifiers |= 8;

            string keyStr = MapKey(e.Key);
            _ = mainVm.Simulation.SendKeyboardEventAsync("rawKeyDown", keyStr, modifiers);
        }
        e.Handled = true;
    }

    private void Border_KeyUp(object? sender, KeyEventArgs e)
    {
        if (DataContext is MainWindowViewModel mainVm && mainVm.Simulation != null)
        {
            int modifiers = 0;
            if (e.KeyModifiers.HasFlag(KeyModifiers.Alt)) modifiers |= 1;
            if (e.KeyModifiers.HasFlag(KeyModifiers.Control)) modifiers |= 2;
            if (e.KeyModifiers.HasFlag(KeyModifiers.Shift)) modifiers |= 4;
            if (e.KeyModifiers.HasFlag(KeyModifiers.Meta)) modifiers |= 8;

            string keyStr = MapKey(e.Key);
            _ = mainVm.Simulation.SendKeyboardEventAsync("keyUp", keyStr, modifiers);
        }
        e.Handled = true;
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
