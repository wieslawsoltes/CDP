using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using CdpInspectorApp.ViewModels;
using CdpInspectorApp.Models;
using Chrome.DevTools.Protocol;

namespace CdpInspectorApp.Views;

public partial class SimulationView : UserControl
{
    private ConnectionViewModel? _connectionVm;
    private SimulationViewModel? _simulationVm;

    private bool _isSpaceKeyDown;

    private bool _isTitleBarPanning;
    private Avalonia.Point _panStartPoint;
    private double _panStartPanX;
    private double _panStartPanY;

    private bool _isResizing;
    private bool _resizeLeft;
    private bool _resizeRight;
    private bool _resizeTop;
    private bool _resizeBottom;
    private Avalonia.Point _resizeStartPoint;
    private double _resizeStartWidth;
    private double _resizeStartHeight;

    public SimulationView()
    {
        InitializeComponent();
        
        DataContextChanged += SimulationView_DataContextChanged;
        SizeChanged += (s, e) => UpdateViewportTransform();

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

        var titleBar = this.Find<Border>("borderVirtualWindowFrame");
        if (titleBar != null)
        {
            titleBar.PointerPressed += TitleBar_PointerPressed;
            titleBar.PointerMoved += TitleBar_PointerMoved;
            titleBar.PointerReleased += TitleBar_PointerReleased;
        }

        WireResizeHandle("handleResizeTop", false, false, true, false);
        WireResizeHandle("handleResizeBottom", false, false, false, true);
        WireResizeHandle("handleResizeLeft", true, false, false, false);
        WireResizeHandle("handleResizeRight", false, true, false, false);
        WireResizeHandle("handleResizeTopLeft", true, false, true, false);
        WireResizeHandle("handleResizeTopRight", false, true, true, false);
        WireResizeHandle("handleResizeBottomLeft", true, false, false, true);
        WireResizeHandle("handleResizeBottomRight", false, true, false, true);
    }

    private void WireResizeHandle(string name, bool left, bool right, bool top, bool bottom)
    {
        var handle = this.Find<Border>(name);
        if (handle != null)
        {
            handle.PointerPressed += (s, e) => StartResize(e, left, right, top, bottom);
            handle.PointerMoved += Resize_PointerMoved;
            handle.PointerReleased += Resize_PointerReleased;
        }
    }

    private void SimulationView_DataContextChanged(object? sender, EventArgs e)
    {
        if (_connectionVm != null)
        {
            _connectionVm.PropertyChanged -= ConnectionVm_PropertyChanged;
            _connectionVm = null;
        }

        if (_simulationVm != null)
        {
            _simulationVm.PropertyChanged -= SimulationVm_PropertyChanged;
            _simulationVm = null;
        }

        if (DataContext is MainWindowViewModel mainVm)
        {
            _connectionVm = mainVm.Connection;
            _connectionVm.PropertyChanged += ConnectionVm_PropertyChanged;

            _simulationVm = mainVm.Simulation;
            if (_simulationVm != null)
            {
                _simulationVm.PropertyChanged += SimulationVm_PropertyChanged;
            }

            UpdateCursor(_connectionVm.IsInspectModeActive);
            UpdateViewportTransform();
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

    private void SimulationVm_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(SimulationViewModel.ZoomLevel) or
            nameof(SimulationViewModel.IsFitZoomActive) or
            nameof(SimulationViewModel.PanX) or
            nameof(SimulationViewModel.PanY) or
            nameof(SimulationViewModel.IsPanModeActive) or
            nameof(SimulationViewModel.DeviceWidth) or
            nameof(SimulationViewModel.DeviceHeight))
        {
            UpdateViewportTransform();
            UpdateCursor(_connectionVm?.IsInspectModeActive ?? false);
        }
    }

    private void UpdateCursor(bool isInspectActive)
    {
        var img = this.Find<Image>("imgScreenshot");
        if (img != null)
        {
            if (isInspectActive)
            {
                img.Cursor = new Cursor(StandardCursorType.Cross);
            }
            else if (DataContext is MainWindowViewModel mainVm && mainVm.Simulation != null && mainVm.Simulation.IsPanModeActive)
            {
                img.Cursor = new Cursor(StandardCursorType.Hand);
            }
            else
            {
                img.Cursor = null;
            }
        }
    }

    private void UpdateViewportTransform()
    {
        var transformGroup = this.Find<Grid>("gridCanvasTransformGroup");
        if (transformGroup == null) return;

        if (DataContext is MainWindowViewModel mainVm && mainVm.Simulation != null)
        {
            var simVm = mainVm.Simulation;
            double zoom = simVm.ZoomLevel;
            if (simVm.IsFitZoomActive)
            {
                var container = this.Find<Control>("borderPreviewViewport") ?? this.Parent as Control;
                if (container != null && container.Bounds.Width > 0 && container.Bounds.Height > 0 && simVm.DeviceWidth > 0 && simVm.DeviceHeight > 0)
                {
                    double availW = Math.Max(100, container.Bounds.Width - 40);
                    double availH = Math.Max(100, container.Bounds.Height - 60);
                    zoom = Math.Min(availW / simVm.DeviceWidth, availH / simVm.DeviceHeight);
                    zoom = Math.Clamp(zoom, 0.1, 3.0);
                }
            }
            double panX = simVm.PanX;
            double panY = simVm.PanY;

            var scaleTransform = new Avalonia.Media.ScaleTransform(zoom, zoom);
            var translateTransform = new Avalonia.Media.TranslateTransform(panX, panY);
            var group = new Avalonia.Media.TransformGroup();
            group.Children.Add(scaleTransform);
            group.Children.Add(translateTransform);
            transformGroup.RenderTransform = group;
        }
    }

    private void TitleBar_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is MainWindowViewModel mainVm && mainVm.Simulation != null)
        {
            _isTitleBarPanning = true;
            _panStartPoint = e.GetPosition(this);
            _panStartPanX = mainVm.Simulation.PanX;
            _panStartPanY = mainVm.Simulation.PanY;
            e.Pointer.Capture(sender as Control);
            e.Handled = true;
        }
    }

    private void TitleBar_PointerMoved(object? sender, PointerEventArgs e)
    {
        if (_isTitleBarPanning && DataContext is MainWindowViewModel mainVm && mainVm.Simulation != null)
        {
            var current = e.GetPosition(this);
            var delta = current - _panStartPoint;
            mainVm.Simulation.PanX = _panStartPanX + delta.X;
            mainVm.Simulation.PanY = _panStartPanY + delta.Y;
            e.Handled = true;
        }
    }

    private void TitleBar_PointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_isTitleBarPanning)
        {
            _isTitleBarPanning = false;
            e.Pointer.Capture(null);
            e.Handled = true;
        }
    }

    private void StartResize(PointerPressedEventArgs e, bool left, bool right, bool top, bool bottom)
    {
        if (DataContext is MainWindowViewModel mainVm && mainVm.Simulation != null)
        {
            _isResizing = true;
            _resizeLeft = left;
            _resizeRight = right;
            _resizeTop = top;
            _resizeBottom = bottom;
            _resizeStartPoint = e.GetPosition(this);
            _resizeStartWidth = mainVm.Simulation.DeviceWidth;
            _resizeStartHeight = mainVm.Simulation.DeviceHeight;
            e.Pointer.Capture(e.Source as Control);
            e.Handled = true;
        }
    }

    private void Resize_PointerMoved(object? sender, PointerEventArgs e)
    {
        if (_isResizing && DataContext is MainWindowViewModel mainVm && mainVm.Simulation != null)
        {
            var current = e.GetPosition(this);
            var delta = current - _resizeStartPoint;

            if (_resizeRight)
            {
                double newW = Math.Max(200, _resizeStartWidth + delta.X);
                mainVm.Simulation.WidthText = Math.Round(newW).ToString();
            }
            else if (_resizeLeft)
            {
                double newW = Math.Max(200, _resizeStartWidth - delta.X);
                mainVm.Simulation.WidthText = Math.Round(newW).ToString();
            }

            if (_resizeBottom)
            {
                double newH = Math.Max(200, _resizeStartHeight + delta.Y);
                mainVm.Simulation.HeightText = Math.Round(newH).ToString();
            }
            else if (_resizeTop)
            {
                double newH = Math.Max(200, _resizeStartHeight - delta.Y);
                mainVm.Simulation.HeightText = Math.Round(newH).ToString();
            }

            e.Handled = true;
        }
    }

    private void Resize_PointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_isResizing && DataContext is MainWindowViewModel mainVm && mainVm.Simulation != null)
        {
            _isResizing = false;
            e.Pointer.Capture(null);
            mainVm.Simulation.ResizeCommand.Execute(null);
            e.Handled = true;
        }
    }

    private void Image_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        var border = this.Find<Border>("borderScreenshot");
        border?.Focus();

        if (DataContext is MainWindowViewModel mainVm && mainVm.Simulation != null)
        {
            var simVm = mainVm.Simulation;
            var pointerPoint = e.GetCurrentPoint(sender as Control);
            bool isMiddle = pointerPoint.Properties.IsMiddleButtonPressed;

            if (isMiddle || simVm.IsPanModeActive || _isSpaceKeyDown)
            {
                _isTitleBarPanning = true;
                _panStartPoint = e.GetPosition(this);
                _panStartPanX = simVm.PanX;
                _panStartPanY = simVm.PanY;
                e.Pointer.Capture(sender as Control);
                e.Handled = true;
                return;
            }
        }

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
        if (_isTitleBarPanning)
        {
            _isTitleBarPanning = false;
            e.Pointer.Capture(null);
            e.Handled = true;
            return;
        }

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

            bool CommandRequiresValuePrompt(FlowCommandDefinition cmd)
            {
                if (cmd.Name is "inputText" or "delay" or "clearState" or "setOrientation" or "launchApp" or "openLink")
                {
                    return true;
                }
                return !cmd.AcceptsSelector && 
                       (cmd.ValueKind == FlowCommandValueKind.String || 
                        cmd.ValueKind == FlowCommandValueKind.Map || 
                        cmd.ValueKind == FlowCommandValueKind.List);
            }

            MenuItem CreateCommandMenuItem(FlowCommandDefinition cmd, string? sel, string? headerOverride = null)
            {
                var header = headerOverride ?? (string.IsNullOrEmpty(sel) ? cmd.DisplayName : $"{cmd.DisplayName} '{sel}'");
                var menuItem = new MenuItem { Header = header };
                menuItem.Click += async (s, ev) =>
                {
                    if (CommandRequiresValuePrompt(cmd))
                    {
                        mainVm.Recorder.TestStudio.NamePromptTitle = cmd.DisplayName;
                        mainVm.Recorder.TestStudio.NamePromptValue = "";
                        mainVm.Recorder.TestStudio.NamePromptCallback = async value =>
                        {
                            if (!string.IsNullOrEmpty(value))
                            {
                                var step = new TestStudioStepModel { Action = cmd.Name, Selector = sel, Value = value };
                                await mainVm.Recorder.TestStudio.AddInteractiveStepAsync(step);
                            }
                        };
                        mainVm.Recorder.TestStudio.IsNamePromptVisible = true;
                    }
                    else
                    {
                        var step = new TestStudioStepModel { Action = cmd.Name, Selector = sel };
                        await mainVm.Recorder.TestStudio.AddInteractiveStepAsync(step);
                    }
                };
                return menuItem;
            }

            var contextMenu = new ContextMenu();

            var tapOnCmd = FlowCommandCatalog.Find("tapOn") ?? new FlowCommandDefinition("tapOn", "Tap On", "Interactions", FlowCommandValueKind.Selector, "", AcceptsSelector: true);
            var assertVisibleCmd = FlowCommandCatalog.Find("assertVisible") ?? new FlowCommandDefinition("assertVisible", "Assert Visible", "Assertions", FlowCommandValueKind.Selector, "", AcceptsSelector: true);
            var assertNotVisibleCmd = FlowCommandCatalog.Find("assertNotVisible") ?? new FlowCommandDefinition("assertNotVisible", "Assert Not Visible", "Assertions", FlowCommandValueKind.Selector, "", AcceptsSelector: true);
            var inputTextCmd = FlowCommandCatalog.Find("inputText") ?? new FlowCommandDefinition("inputText", "Input Text", "Input", FlowCommandValueKind.String, "", AcceptsSelector: false);
            var doubleTapOnCmd = FlowCommandCatalog.Find("doubleTapOn") ?? new FlowCommandDefinition("doubleTapOn", "Double Tap On", "Interactions", FlowCommandValueKind.Selector, "", AcceptsSelector: true);
            var longPressOnCmd = FlowCommandCatalog.Find("longPressOn") ?? new FlowCommandDefinition("longPressOn", "Long Press On", "Interactions", FlowCommandValueKind.Selector, "", AcceptsSelector: true);
            var scrollUntilVisibleCmd = FlowCommandCatalog.Find("scrollUntilVisible") ?? new FlowCommandDefinition("scrollUntilVisible", "Scroll Until Visible", "Interactions", FlowCommandValueKind.Map, "", AcceptsSelector: true);
            var copyTextFromCmd = FlowCommandCatalog.Find("copyTextFrom") ?? new FlowCommandDefinition("copyTextFrom", "Copy Text From", "Input", FlowCommandValueKind.Selector, "", AcceptsSelector: true);

            if (!string.IsNullOrEmpty(selector))
            {
                contextMenu.Items.Add(CreateCommandMenuItem(tapOnCmd, selector, $"Tap '{selector}'"));
                contextMenu.Items.Add(CreateCommandMenuItem(assertVisibleCmd, selector, $"Assert Visible '{selector}'"));
                contextMenu.Items.Add(CreateCommandMenuItem(assertNotVisibleCmd, selector, $"Assert Not Visible '{selector}'"));
                contextMenu.Items.Add(CreateCommandMenuItem(inputTextCmd, selector, $"Input Text into '{selector}'"));
                contextMenu.Items.Add(CreateCommandMenuItem(doubleTapOnCmd, selector, $"Double Tap '{selector}'"));
                contextMenu.Items.Add(CreateCommandMenuItem(longPressOnCmd, selector, $"Long Press '{selector}'"));
                contextMenu.Items.Add(CreateCommandMenuItem(scrollUntilVisibleCmd, selector, $"Scroll until Visible '{selector}'"));
                contextMenu.Items.Add(CreateCommandMenuItem(copyTextFromCmd, selector, $"Copy Text From '{selector}'"));

                contextMenu.Items.Add(new Separator());

                var propertyAssertionsSubMenu = new MenuItem { Header = "Assert Element Property (True/False)" };
                var escapedSelector = selector.Replace("\"", "\\\"");

                // 1. IsEnabled
                var assertEnabledTrue = new MenuItem { Header = "Assert .isEnabled is True" };
                assertEnabledTrue.Click += async (s, ev) =>
                {
                    var step = new TestStudioStepModel { Action = "assertTrue", Selector = "", Value = $"document.querySelector(\"{escapedSelector}\").isEnabled" };
                    await mainVm.Recorder.TestStudio.AddInteractiveStepAsync(step);
                };
                propertyAssertionsSubMenu.Items.Add(assertEnabledTrue);

                var assertEnabledFalse = new MenuItem { Header = "Assert .isEnabled is False" };
                assertEnabledFalse.Click += async (s, ev) =>
                {
                    var step = new TestStudioStepModel { Action = "assertFalse", Selector = "", Value = $"document.querySelector(\"{escapedSelector}\").isEnabled" };
                    await mainVm.Recorder.TestStudio.AddInteractiveStepAsync(step);
                };
                propertyAssertionsSubMenu.Items.Add(assertEnabledFalse);

                // 2. IsVisible
                var assertVisibleTrue = new MenuItem { Header = "Assert .isVisible is True" };
                assertVisibleTrue.Click += async (s, ev) =>
                {
                    var step = new TestStudioStepModel { Action = "assertTrue", Selector = "", Value = $"document.querySelector(\"{escapedSelector}\").isVisible" };
                    await mainVm.Recorder.TestStudio.AddInteractiveStepAsync(step);
                };
                propertyAssertionsSubMenu.Items.Add(assertVisibleTrue);

                var assertVisibleFalse = new MenuItem { Header = "Assert .isVisible is False" };
                assertVisibleFalse.Click += async (s, ev) =>
                {
                    var step = new TestStudioStepModel { Action = "assertFalse", Selector = "", Value = $"document.querySelector(\"{escapedSelector}\").isVisible" };
                    await mainVm.Recorder.TestStudio.AddInteractiveStepAsync(step);
                };
                propertyAssertionsSubMenu.Items.Add(assertVisibleFalse);

                // 3. TextContent
                var assertTextContent = new MenuItem { Header = "Assert .textContent is..." };
                assertTextContent.Click += (s, ev) =>
                {
                    mainVm.Recorder.TestStudio.NamePromptTitle = "Expected TextContent";
                    mainVm.Recorder.TestStudio.NamePromptValue = "";
                    mainVm.Recorder.TestStudio.NamePromptCallback = async val =>
                    {
                        if (val != null)
                        {
                            var step = new TestStudioStepModel { Action = "assertTrue", Selector = "", Value = $"document.querySelector(\"{escapedSelector}\").textContent == \"{val.Replace("\"", "\\\"")}\"" };
                            await mainVm.Recorder.TestStudio.AddInteractiveStepAsync(step);
                        }
                    };
                    mainVm.Recorder.TestStudio.IsNamePromptVisible = true;
                };
                propertyAssertionsSubMenu.Items.Add(assertTextContent);

                // 4. Value
                var assertValue = new MenuItem { Header = "Assert .value is..." };
                assertValue.Click += (s, ev) =>
                {
                    mainVm.Recorder.TestStudio.NamePromptTitle = "Expected Value";
                    mainVm.Recorder.TestStudio.NamePromptValue = "";
                    mainVm.Recorder.TestStudio.NamePromptCallback = async val =>
                    {
                        if (val != null)
                        {
                            var step = new TestStudioStepModel { Action = "assertTrue", Selector = "", Value = $"document.querySelector(\"{escapedSelector}\").value == \"{val.Replace("\"", "\\\"")}\"" };
                            await mainVm.Recorder.TestStudio.AddInteractiveStepAsync(step);
                        }
                    };
                    mainVm.Recorder.TestStudio.IsNamePromptVisible = true;
                };
                propertyAssertionsSubMenu.Items.Add(assertValue);

                // 5. Custom Property Picker
                var assertCustomProperty = new MenuItem { Header = "Assert Custom Property..." };
                assertCustomProperty.Click += (s, ev) =>
                {
                    mainVm.Recorder.TestStudio.ShowAssertPropertyPickerCommand.Execute(selector);
                };
                propertyAssertionsSubMenu.Items.Add(assertCustomProperty);

                contextMenu.Items.Add(propertyAssertionsSubMenu);

                var assertionsSubMenu = new MenuItem { Header = "Add Assertion (Selector)" };
                var selectorAssertions = FlowCommandCatalog.PublicCommands
                    .Where(c => c.Category.Equals("Assertions", StringComparison.OrdinalIgnoreCase) && c.AcceptsSelector)
                    .OrderBy(c => c.DisplayName);
                foreach (var cmd in selectorAssertions)
                {
                    assertionsSubMenu.Items.Add(CreateCommandMenuItem(cmd, selector));
                }
                contextMenu.Items.Add(assertionsSubMenu);

                var interactionsSubMenu = new MenuItem { Header = "Add Interaction (Selector)" };
                var selectorInteractions = FlowCommandCatalog.PublicCommands
                    .Where(c => (c.Category.Equals("Interactions", StringComparison.OrdinalIgnoreCase) ||
                                 c.Category.Equals("Input", StringComparison.OrdinalIgnoreCase)) &&
                                c.AcceptsSelector)
                    .OrderBy(c => c.DisplayName);
                foreach (var cmd in selectorInteractions)
                {
                    interactionsSubMenu.Items.Add(CreateCommandMenuItem(cmd, selector));
                }
                contextMenu.Items.Add(interactionsSubMenu);
            }
            else
            {
                var pointValue = $"{targetX:F0},{targetY:F0}";
                var tapPointItem = new MenuItem { Header = $"Tap Point '{pointValue}'" };
                tapPointItem.Click += async (s, ev) =>
                {
                    var step = new TestStudioStepModel { Action = "tapOn" };
                    step.Parameters["point"] = pointValue;
                    await mainVm.Recorder.TestStudio.AddInteractiveStepAsync(step);
                };
                contextMenu.Items.Add(tapPointItem);
                contextMenu.Items.Add(new Separator());

                var customAssertTrue = new MenuItem { Header = "Add Assert True (Custom Expression)" };
                customAssertTrue.Click += (s, ev) =>
                {
                    mainVm.Recorder.TestStudio.NamePromptTitle = "Assert True Expression";
                    mainVm.Recorder.TestStudio.NamePromptValue = "";
                    mainVm.Recorder.TestStudio.NamePromptCallback = async val =>
                    {
                        if (!string.IsNullOrEmpty(val))
                        {
                            var step = new TestStudioStepModel { Action = "assertTrue", Selector = "", Value = val };
                            await mainVm.Recorder.TestStudio.AddInteractiveStepAsync(step);
                        }
                    };
                    mainVm.Recorder.TestStudio.IsNamePromptVisible = true;
                };
                contextMenu.Items.Add(customAssertTrue);

                var customAssertFalse = new MenuItem { Header = "Add Assert False (Custom Expression)" };
                customAssertFalse.Click += (s, ev) =>
                {
                    mainVm.Recorder.TestStudio.NamePromptTitle = "Assert False Expression";
                    mainVm.Recorder.TestStudio.NamePromptValue = "";
                    mainVm.Recorder.TestStudio.NamePromptCallback = async val =>
                    {
                        if (!string.IsNullOrEmpty(val))
                        {
                            var step = new TestStudioStepModel { Action = "assertFalse", Selector = "", Value = val };
                            await mainVm.Recorder.TestStudio.AddInteractiveStepAsync(step);
                        }
                    };
                    mainVm.Recorder.TestStudio.IsNamePromptVisible = true;
                };
                contextMenu.Items.Add(customAssertFalse);
                contextMenu.Items.Add(new Separator());
            }

            var globalCommandMenu = new MenuItem { Header = "Add Global Command" };
            var appDeviceMenu = new MenuItem { Header = "App & Device" };
            var assertionsMenu = new MenuItem { Header = "Assertions" };
            var inputMenu = new MenuItem { Header = "Input" };
            var logicMenu = new MenuItem { Header = "Logic" };
            var timingNavMenu = new MenuItem { Header = "Timing & Navigation" };

            var otherCommands = FlowCommandCatalog.PublicCommands
                .Where(c => !c.AcceptsSelector)
                .OrderBy(c => c.DisplayName);

            foreach (var cmd in otherCommands)
            {
                MenuItem? categoryMenu = cmd.Category switch
                {
                    "App & Device" or "Media" => appDeviceMenu,
                    "Assertions" or "AI" => assertionsMenu,
                    "Input" => inputMenu,
                    "Logic" or "Scripting" => logicMenu,
                    "Timing" or "Navigation" => timingNavMenu,
                    _ => null
                };

                if (categoryMenu != null)
                {
                    categoryMenu.Items.Add(CreateCommandMenuItem(cmd, null));
                }
            }

            if (appDeviceMenu.Items.Count > 0) globalCommandMenu.Items.Add(appDeviceMenu);
            if (assertionsMenu.Items.Count > 0) globalCommandMenu.Items.Add(assertionsMenu);
            if (inputMenu.Items.Count > 0) globalCommandMenu.Items.Add(inputMenu);
            if (logicMenu.Items.Count > 0) globalCommandMenu.Items.Add(logicMenu);
            if (timingNavMenu.Items.Count > 0) globalCommandMenu.Items.Add(timingNavMenu);

            contextMenu.Items.Add(globalCommandMenu);

            contextMenu.Open(img);
        }
    }

    private void Image_PointerMoved(object? sender, PointerEventArgs e)
    {
        if (_isTitleBarPanning && DataContext is MainWindowViewModel mainVm && mainVm.Simulation != null)
        {
            var current = e.GetPosition(this);
            var delta = current - _panStartPoint;
            mainVm.Simulation.PanX = _panStartPanX + delta.X;
            mainVm.Simulation.PanY = _panStartPanY + delta.Y;
            e.Handled = true;
            return;
        }

        SendMouseEvent("mouseMoved", e);
        e.Handled = true;
    }

    private void Image_PointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        if (e.KeyModifiers.HasFlag(KeyModifiers.Control) || e.KeyModifiers.HasFlag(KeyModifiers.Meta))
        {
            if (DataContext is MainWindowViewModel mainVm && mainVm.Simulation != null)
            {
                if (e.Delta.Y > 0)
                {
                    mainVm.Simulation.ZoomIn();
                }
                else if (e.Delta.Y < 0)
                {
                    mainVm.Simulation.ZoomOut();
                }
            }
            e.Handled = true;
            return;
        }

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

        if (DataContext is MainWindowViewModel mainVm2 && mainVm2.Simulation != null)
        {
            var simVm = mainVm2.Simulation;
            if (simVm.DeviceWidth > 0 && simVm.DeviceHeight > 0)
            {
                targetX = pos.X * (simVm.DeviceWidth / imageWidth);
                targetY = pos.Y * (simVm.DeviceHeight / imageHeight);
            }
            _ = mainVm2.Simulation.SendWheelEventAsync(targetX, targetY, e.Delta.Y);
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
        if (e.Key == Key.Space)
        {
            _isSpaceKeyDown = true;
        }

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
        
        if (e.Key is Key.Tab or Key.Left or Key.Right or Key.Up or Key.Down or Key.Back or Key.Delete or Key.Escape or Key.Enter or Key.Space)
        {
            e.Handled = true;
        }
    }

    private void Border_KeyUp(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Space)
        {
            _isSpaceKeyDown = false;
        }

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
        
        if (e.Key is Key.Tab or Key.Left or Key.Right or Key.Up or Key.Down or Key.Back or Key.Delete or Key.Escape or Key.Enter or Key.Space)
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
