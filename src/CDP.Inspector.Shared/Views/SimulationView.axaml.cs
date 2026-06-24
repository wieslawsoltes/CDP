using System;
using System.IO;
using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using CdpInspectorApp.ViewModels;
using CdpInspectorApp.Models;

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
            img.PointerExited += Image_PointerExited;
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

        var img = this.Find<Image>("imgScreenshot");
        if (img != null)
        {
            var pointerPoint = e.GetCurrentPoint(img);
            if (pointerPoint.Properties.IsRightButtonPressed)
            {
                e.Handled = true;
                return;
            }
        }

        SendMouseEvent("mousePressed", e);
        e.Handled = true;
    }

    private void Image_PointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        var img = this.Find<Image>("imgScreenshot");
        if (img != null)
        {
            var pointerPoint = e.GetCurrentPoint(img);
            if (pointerPoint.Properties.PointerUpdateKind == PointerUpdateKind.RightButtonReleased)
            {
                ShowRecommendedCommandsMenu(pointerPoint.Position, e);
                e.Handled = true;
                return;
            }
        }

        SendMouseEvent("mouseReleased", e);
        e.Handled = true;
    }

    private async void ShowRecommendedCommandsMenu(Avalonia.Point pos, PointerReleasedEventArgs e)
    {
        var img = this.Find<Image>("imgScreenshot");
        if (img == null || img.Source is not Bitmap bitmap) return;

        double imageWidth = img.Bounds.Width;
        double imageHeight = img.Bounds.Height;
        if (imageWidth <= 0 || imageHeight <= 0) return;

        double targetX = pos.X;
        double targetY = pos.Y;

        if (DataContext is MainWindowViewModel mainVm && mainVm.Simulation != null && mainVm.Recorder != null)
        {
            var simVm = mainVm.Simulation;
            if (simVm.DeviceWidth > 0 && simVm.DeviceHeight > 0)
            {
                targetX = pos.X * (simVm.DeviceWidth / imageWidth);
                targetY = pos.Y * (simVm.DeviceHeight / imageHeight);
            }

            var selector = await simVm.GetSelectorAtCoordinatesAsync(targetX, targetY, true);
            if (string.IsNullOrEmpty(selector))
            {
                selector = await simVm.GetSelectorAtCoordinatesAsync(targetX, targetY, false);
            }

            if (string.IsNullOrEmpty(selector))
            {
                var pointValue = $"{targetX:F0},{targetY:F0}";
                var coordsMenu = new ContextMenu();
                var tapPointItem = new MenuItem { Header = $"Tap Point '{pointValue}'" };
                tapPointItem.Click += async (s, ev) =>
                {
                    var step = new TestStudioStepModel { Action = "tapOn" };
                    step.Parameters["point"] = pointValue;
                    await mainVm.Recorder.TestStudio.AddInteractiveStepAsync(step);
                };
                coordsMenu.Items.Add(tapPointItem);
                coordsMenu.Open(img);
                return;
            }

            var contextMenu = new ContextMenu();

            var tapItem = new MenuItem { Header = $"Tap '{selector}'" };
            tapItem.Click += async (s, ev) =>
            {
                var step = new TestStudioStepModel { Action = "tapOn", Selector = selector };
                await mainVm.Recorder.TestStudio.AddInteractiveStepAsync(step);
            };

            var assertItem = new MenuItem { Header = $"Assert Visible '{selector}'" };
            assertItem.Click += async (s, ev) =>
            {
                var step = new TestStudioStepModel { Action = "assertVisible", Selector = selector };
                await mainVm.Recorder.TestStudio.AddInteractiveStepAsync(step);
            };

            var inputItem = new MenuItem { Header = $"Input Text into '{selector}'" };
            inputItem.Click += (s, ev) =>
            {
                mainVm.Recorder.TestStudio.NamePromptTitle = "Input Text";
                mainVm.Recorder.TestStudio.NamePromptValue = "";
                mainVm.Recorder.TestStudio.NamePromptCallback = async text =>
                {
                    if (!string.IsNullOrEmpty(text))
                    {
                        var step = new TestStudioStepModel { Action = "inputText", Selector = selector, Value = text };
                        await mainVm.Recorder.TestStudio.AddInteractiveStepAsync(step);
                    }
                };
                mainVm.Recorder.TestStudio.IsNamePromptVisible = true;
            };

            var scrollItem = new MenuItem { Header = $"Scroll until Visible '{selector}'" };
            scrollItem.Click += async (s, ev) =>
            {
                var step = new TestStudioStepModel { Action = "scrollUntilVisible", Selector = selector };
                await mainVm.Recorder.TestStudio.AddInteractiveStepAsync(step);
            };

            contextMenu.Items.Add(tapItem);
            contextMenu.Items.Add(assertItem);
            contextMenu.Items.Add(inputItem);
            contextMenu.Items.Add(scrollItem);

            contextMenu.Open(img);
        }
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
        if (imageWidth <= 0 || imageHeight <= 0) return;

        double targetX = pos.X;
        double targetY = pos.Y;

        if (targetX < 0 || targetX > imageWidth || targetY < 0 || targetY > imageHeight) return;

        if (DataContext is MainWindowViewModel mainVm && mainVm.Simulation != null)
        {
            var simVm = mainVm.Simulation;
            if (simVm.DeviceWidth > 0 && simVm.DeviceHeight > 0)
            {
                targetX = pos.X * (simVm.DeviceWidth / imageWidth);
                targetY = pos.Y * (simVm.DeviceHeight / imageHeight);
            }
            _ = mainVm.Simulation.SendWheelEventAsync(targetX, targetY, e.Delta.Y);
        }
        e.Handled = true;
    }

    private void Image_PointerExited(object? sender, PointerEventArgs e)
    {
        if (DataContext is MainWindowViewModel mainVm && mainVm.Simulation != null)
        {
            mainVm.Simulation.ClearInspectHover();
            _ = mainVm.Simulation.TriggerHighlightRefreshAsync();
        }
    }

    private void SendMouseEvent(string type, PointerEventArgs e)
    {
        var img = this.Find<Image>("imgScreenshot");
        if (img == null || img.Source is not Bitmap bitmap) return;

        var pointerPoint = e.GetCurrentPoint(img);
        var pos = pointerPoint.Position;

        double imageWidth = img.Bounds.Width;
        double imageHeight = img.Bounds.Height;
        if (imageWidth <= 0 || imageHeight <= 0) return;

        double targetX = pos.X;
        double targetY = pos.Y;

        // Clamp to image boundaries
        if (targetX < 0 || targetX > imageWidth || targetY < 0 || targetY > imageHeight)
        {
            return;
        }

        if (DataContext is MainWindowViewModel mainVm && mainVm.Simulation != null)
        {
            var simVm = mainVm.Simulation;
            if (simVm.DeviceWidth > 0 && simVm.DeviceHeight > 0)
            {
                targetX = pos.X * (simVm.DeviceWidth / imageWidth);
                targetY = pos.Y * (simVm.DeviceHeight / imageHeight);
            }

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
