using System;
using System.Diagnostics;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;

namespace CdpSampleApp;

public partial class MainWindow : Window
{
    public const int LargeListItemCount = 2000;

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

    public void MenuItem_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is MenuItem menuItem)
        {
            var txtStatus = this.FindControl<TextBlock>("txtPopupStatus");
            if (txtStatus != null)
            {
                txtStatus.Text = $"Selected Menu: {menuItem.Header}";
            }
        }
    }

    public void BtnToggleLargeList_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs? e)
    {
        var border = this.FindControl<Border>("borderLargeList");
        if (border == null)
        {
            return;
        }

        if (border.Child != null)
        {
            border.Child = null;
            return;
        }

        // The panel is filled before it is attached, so the visual tree grows in one step.
        var panel = new StackPanel { Name = "panelLargeList" };
        for (var i = 1; i <= LargeListItemCount; i++)
        {
            var item = new TextBlock { Text = $"Large List Item {i}" };
            item.Classes.Add("largeListItem");
            panel.Children.Add(item);
        }
        border.Child = panel;
    }

    public void BtnAddLargeListIncrementally_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs? e)
    {
        var border = this.FindControl<Border>("borderLargeList");
        if (border == null)
        {
            return;
        }

        // The empty panel is attached first, so every item is a separate insertion into the live tree
        // and produces its own DOM.childNodeInserted event for connected CDP clients.
        var panel = new StackPanel { Name = "panelLargeList" };
        border.Child = panel;
        for (var i = 1; i <= LargeListItemCount; i++)
        {
            var item = new TextBlock { Text = $"Large List Item {i}" };
            item.Classes.Add("largeListItem");
            panel.Children.Add(item);
        }
    }

    public async void BtnOpenOwnedDialog_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs? e)
    {
        var clickCount = 0;
        var countText = new TextBlock { Name = "txtOwnedDialogCount", Text = "Dialog Clicks: 0", FontSize = 14 };
        var incrementButton = new Button { Name = "btnOwnedDialogIncrement", Content = "Increment", Width = 160 };
        var closeButton = new Button { Name = "btnOwnedDialogClose", Content = "Close", Width = 160 };
        AutomationProperties.SetAutomationId(countText, "txtOwnedDialogCount");
        AutomationProperties.SetAutomationId(incrementButton, "btnOwnedDialogIncrement");
        AutomationProperties.SetAutomationId(closeButton, "btnOwnedDialogClose");

        var dialog = new Window
        {
            Title = "Sample Owned Dialog",
            Width = 320,
            Height = 220,
            CanResize = false,
            ShowInTaskbar = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Content = new StackPanel
            {
                Spacing = 15,
                Margin = new Avalonia.Thickness(20),
                Children =
                {
                    new TextBlock { Text = "Owned modal dialog", FontSize = 16, FontWeight = Avalonia.Media.FontWeight.Bold },
                    countText,
                    incrementButton,
                    closeButton
                }
            }
        };

        incrementButton.Click += (_, _) =>
        {
            clickCount++;
            countText.Text = $"Dialog Clicks: {clickCount}";
        };
        closeButton.Click += (_, _) => dialog.Close();

        await dialog.ShowDialog(this);
    }

    public void BtnInsideFlyout_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var txtStatus = this.FindControl<TextBlock>("txtPopupStatus");
        if (txtStatus != null)
        {
            txtStatus.Text = "Clicked Inside Flyout!";
        }
        var flyout = this.FindControl<Button>("btnFlyout")?.Flyout;
        if (flyout != null)
        {
            flyout.Hide();
        }
    }
}