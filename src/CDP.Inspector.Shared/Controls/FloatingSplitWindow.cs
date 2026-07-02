using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using CDP.Editor.Splits.Controls;
using CDP.Editor.Splits.Models;

namespace CDP.Inspector.Shared.Controls;

public class FloatingSplitWindow : Window
{
    private readonly SuperSplit _superSplit;
    private readonly SuperSplit _mainSplit;
    private Button? _btnMaximize;

    public FloatingSplitWindow(SuperSplit mainSplit, BoxNode rootNode)
    {
        _mainSplit = mainSplit;

        Title = "Inspector Panel - Floating";
        Width = 800;
        Height = 600;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Background = Brush.Parse("#202124");
        RequestedThemeVariant = Avalonia.Styling.ThemeVariant.Dark;
        DataContext = mainSplit.DataContext;

        // Use border-only decorations to hide default title bar but keep resize borders
        WindowDecorations = Avalonia.Controls.WindowDecorations.BorderOnly;

        _superSplit = new SuperSplit
        {
            ViewResolver = mainSplit.ViewResolver,
            Root = rootNode
        };

        _superSplit.LayoutRebuilt += OnLayoutRebuilt;

        // Custom Title Bar Grid layout
        var titleBarGrid = new Grid
        {
            Height = 32,
            Background = Brush.Parse("#1e1e1e"),
            ColumnDefinitions = new ColumnDefinitions("*, Auto"),
            VerticalAlignment = VerticalAlignment.Stretch
        };

        // Title and Icon container
        var titleContainer = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(10, 0, 0, 0)
        };

        var appIcon = new PathIcon
        {
            Width = 12,
            Height = 12,
            Foreground = Brush.Parse("#8ab4f8"),
            Margin = new Thickness(0, 0, 8, 0)
        };
        if (Application.Current != null && Application.Current.TryFindResource("WindowMultipleIcon", out var iconRes) && iconRes is Geometry iconGeom)
        {
            appIcon.Data = iconGeom;
        }
        titleContainer.Children.Add(appIcon);

        var titleText = new TextBlock
        {
            Text = "Inspector Panel - Floating",
            FontSize = 11,
            Foreground = Brush.Parse("#e8eaed"),
            VerticalAlignment = VerticalAlignment.Center
        };
        titleContainer.Children.Add(titleText);
        Grid.SetColumn(titleContainer, 0);
        titleBarGrid.Children.Add(titleContainer);

        // Window control buttons panel
        var buttonsPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Stretch
        };

        var btnMinimize = new Button
        {
            Width = 45,
            Height = 32,
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            CornerRadius = new CornerRadius(0),
            Content = CreateMinimizeIcon(),
            VerticalAlignment = VerticalAlignment.Stretch
        };

        _btnMaximize = new Button
        {
            Width = 45,
            Height = 32,
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            CornerRadius = new CornerRadius(0),
            Content = CreateMaximizeIcon(),
            VerticalAlignment = VerticalAlignment.Stretch
        };

        var btnClose = new Button
        {
            Width = 45,
            Height = 32,
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            CornerRadius = new CornerRadius(0),
            Content = CreateCloseIcon(),
            VerticalAlignment = VerticalAlignment.Stretch
        };

        // Add premium mouse hover effects
        btnMinimize.PointerEntered += (s, e) => { btnMinimize.Background = Brush.Parse("#35363a"); };
        btnMinimize.PointerExited += (s, e) => { btnMinimize.Background = Brushes.Transparent; };

        _btnMaximize.PointerEntered += (s, e) => { _btnMaximize.Background = Brush.Parse("#35363a"); };
        _btnMaximize.PointerExited += (s, e) => { _btnMaximize.Background = Brushes.Transparent; };

        btnClose.PointerEntered += (s, e) =>
        {
            btnClose.Background = Brush.Parse("#e81123");
            if (btnClose.Content is PathIcon icon) icon.Foreground = Brushes.White;
        };
        btnClose.PointerExited += (s, e) =>
        {
            btnClose.Background = Brushes.Transparent;
            if (btnClose.Content is PathIcon icon) icon.Foreground = Brush.Parse("#9aa0a6");
        };

        // Handle button clicks
        btnMinimize.Click += (s, e) => { WindowState = WindowState.Minimized; };
        _btnMaximize.Click += (s, e) => { ToggleMaximize(); };
        btnClose.Click += (s, e) => { Close(); };

        buttonsPanel.Children.Add(btnMinimize);
        buttonsPanel.Children.Add(_btnMaximize);
        buttonsPanel.Children.Add(btnClose);

        Grid.SetColumn(buttonsPanel, 1);
        titleBarGrid.Children.Add(buttonsPanel);

        // Window drag and double click to maximize handling
        titleBarGrid.PointerPressed += (s, e) =>
        {
            if (e.GetCurrentPoint(titleBarGrid).Properties.IsLeftButtonPressed)
            {
                if (e.ClickCount == 2)
                {
                    ToggleMaximize();
                }
                else
                {
                    BeginMoveDrag(e);
                }
                e.Handled = true;
            }
        };

        // Grid hosting custom title bar + main content
        var mainLayout = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto, *")
        };
        Grid.SetRow(titleBarGrid, 0);
        mainLayout.Children.Add(titleBarGrid);

        Grid.SetRow(_superSplit, 1);
        mainLayout.Children.Add(_superSplit);

        // Window border frame
        var windowBorder = new Border
        {
            BorderBrush = Brush.Parse("#3c4043"),
            BorderThickness = new Thickness(1),
            Background = Brush.Parse("#202124"),
            Child = mainLayout
        };

        Content = windowBorder;
    }

    private PathIcon CreateMinimizeIcon() => new() { Width = 10, Height = 10, Foreground = Brush.Parse("#9aa0a6"), Data = Geometry.Parse("M 1 5 H 9") };
    private PathIcon CreateMaximizeIcon() => new() { Width = 10, Height = 10, Foreground = Brush.Parse("#9aa0a6"), Data = Geometry.Parse("M 1 1 H 9 V 9 H 1 Z") };
    private PathIcon CreateRestoreIcon() => new() { Width = 10, Height = 10, Foreground = Brush.Parse("#9aa0a6"), Data = Geometry.Parse("M 3 1 H 9 V 7 H 3 Z M 1 3 H 7 V 9 H 1 Z") };
    private PathIcon CreateCloseIcon() => new() { Width = 8, Height = 8, Foreground = Brush.Parse("#9aa0a6"), Data = Geometry.Parse("M 1 1 L 9 9 M 9 1 L 1 9") };

    private void ToggleMaximize()
    {
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    }

    private void UpdateMaximizeButtonIcon()
    {
        if (_btnMaximize != null)
        {
            _btnMaximize.Content = WindowState == WindowState.Maximized ? CreateRestoreIcon() : CreateMaximizeIcon();
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == WindowStateProperty)
        {
            UpdateMaximizeButtonIcon();
        }
    }

    private void OnLayoutRebuilt(object? sender, EventArgs e)
    {
        if (_superSplit.Root == null)
        {
            _superSplit.LayoutRebuilt -= OnLayoutRebuilt;
            Close();
        }
    }

    protected override void OnClosing(WindowClosingEventArgs e)
    {
        base.OnClosing(e);

        if (_superSplit.Root != null)
        {
            var nodeToMove = _superSplit.Root;
            _superSplit.Root = null; // Detach from floating split

            if (_mainSplit.Root == null)
            {
                _mainSplit.Root = nodeToMove;
            }
            else
            {
                var newRoot = new SplitContainerNode(Orientation.Horizontal, _mainSplit.Root, nodeToMove)
                {
                    SplitterRatio = 0.7
                };
                _mainSplit.Root = newRoot;
            }
            _mainSplit.Rebuild();
        }
    }
}
