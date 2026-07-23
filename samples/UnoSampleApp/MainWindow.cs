using System;
using System.Diagnostics;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using UnoSampleApp.ViewModels;

namespace UnoSampleApp;

public class BoolToVisConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        return value is true ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        return value is Visibility.Visible;
    }
}

public class MainWindow : Window
{
    private readonly Stopwatch _stopwatch = new();
    private bool _dragSourcePressed;

    public MainWindowViewModel ViewModel { get; }

    public TabView TabContainer { get; }
    public TextBlock TxtStatus { get; }
    public TextBlock TxtSliderVal { get; }
    public TextBlock DoubleClickStatus { get; }
    public TextBlock LongPressStatus { get; }
    public TextBlock DragDropStatus { get; }
    public TextBlock TxtVisibilityTarget { get; }
    public TextBlock TxtPopupStatus { get; }
    public Button BtnFlyout { get; }

    public MainWindow()
    {
        Title = "Uno CDP Inspector Sample";
        ViewModel = new MainWindowViewModel();

        try
        {
            AppWindow?.Resize(new Windows.Graphics.SizeInt32 { Width = 550, Height = 520 });
        }
        catch
        {
            // Ignore platform specific window size exception
        }

        var boolToVis = new BoolToVisConverter();

        var rootGrid = new Grid
        {
            Width = 550,
            Height = 520,
            MinWidth = 550,
            MinHeight = 520,
            Margin = new Thickness(10),
            DataContext = ViewModel
        };
        rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        rootGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        var header = new TextBlock
        {
            Name = "txtHeader",
            Text = "Uno CDP Inspector Sample",
            FontSize = 20,
            FontWeight = Microsoft.UI.Text.FontWeights.Bold,
            Margin = new Thickness(10, 0, 10, 10)
        };
        Grid.SetRow(header, 0);
        rootGrid.Children.Add(header);

        TabContainer = new TabView
        {
            Name = "tabContainer",
            IsAddTabButtonVisible = false
        };
        Grid.SetRow(TabContainer, 1);

        TxtSliderVal = new TextBlock
        {
            Name = "txtSliderVal"
        };
        TxtSliderVal.SetBinding(TextBlock.TextProperty, new Binding { Path = new PropertyPath("SliderValueText") });

        DoubleClickStatus = new TextBlock
        {
            Name = "DoubleClickStatus",
            VerticalAlignment = VerticalAlignment.Center
        };
        DoubleClickStatus.SetBinding(TextBlock.TextProperty, new Binding { Path = new PropertyPath("DoubleClickStatus") });

        LongPressStatus = new TextBlock
        {
            Name = "LongPressStatus",
            VerticalAlignment = VerticalAlignment.Center
        };
        LongPressStatus.SetBinding(TextBlock.TextProperty, new Binding { Path = new PropertyPath("LongPressStatus") });

        DragDropStatus = new TextBlock
        {
            Name = "DragDropStatus"
        };
        DragDropStatus.SetBinding(TextBlock.TextProperty, new Binding { Path = new PropertyPath("DragDropStatus") });

        // 1. Home Tab
        var homeItem = new TabViewItem
        {
            Name = "tabHome",
            Header = "Home",
            IsClosable = false
        };
        var homeScroll = new ScrollViewer { Margin = new Thickness(10) };
        var homeStack = new StackPanel { Spacing = 15 };

        var homeButtons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
        
        var btnClickMe = new Button
        {
            Name = "btnClickMe",
            Content = "Click Me",
            Width = 100
        };
        AutomationProperties.SetAutomationId(btnClickMe, "btnClickMe");
        btnClickMe.Click += (s, e) => ViewModel.Click();
        homeButtons.Children.Add(btnClickMe);

        var btnSendHttp = new Button
        {
            Name = "btnSendHttp",
            Content = "Send HTTP Request",
            Width = 150
        };
        AutomationProperties.SetAutomationId(btnSendHttp, "btnSendHttp");
        btnSendHttp.Click += async (s, e) => await ViewModel.SendHttpAsync();
        homeButtons.Children.Add(btnSendHttp);

        var btnOpenSecond = new Button
        {
            Name = "btnOpenSecond",
            Content = "Open Second Window",
            Width = 150
        };
        AutomationProperties.SetAutomationId(btnOpenSecond, "btnOpenSecond");
        btnOpenSecond.Click += (s, e) => ViewModel.OpenSecondWindow();
        homeButtons.Children.Add(btnOpenSecond);

        TxtStatus = new TextBlock
        {
            Name = "txtStatus",
            VerticalAlignment = VerticalAlignment.Center
        };
        TxtStatus.SetBinding(TextBlock.TextProperty, new Binding { Path = new PropertyPath("StatusText") });
        homeButtons.Children.Add(TxtStatus);
        homeStack.Children.Add(homeButtons);

        var txtInput = new TextBox
        {
            Name = "txtInput",
            PlaceholderText = "Type text here...",
            Width = 300,
            HorizontalAlignment = HorizontalAlignment.Left
        };
        AutomationProperties.SetAutomationId(txtInput, "txtTarget");
        txtInput.SetBinding(TextBox.TextProperty, new Binding { Path = new PropertyPath("InputText"), Mode = BindingMode.TwoWay });
        homeStack.Children.Add(txtInput);

        var chkToggle = new CheckBox
        {
            Name = "chkToggle",
            Content = "Enable Option"
        };
        AutomationProperties.SetAutomationId(chkToggle, "chkToggle");
        chkToggle.SetBinding(ToggleButton.IsCheckedProperty, new Binding { Path = new PropertyPath("IsOptionEnabled"), Mode = BindingMode.TwoWay });
        homeStack.Children.Add(chkToggle);

        var sliderValue = new Slider
        {
            Name = "sliderValue",
            Minimum = 0,
            Maximum = 100,
            Width = 300,
            HorizontalAlignment = HorizontalAlignment.Left
        };
        AutomationProperties.SetAutomationId(sliderValue, "sliderValue");
        sliderValue.SetBinding(Slider.ValueProperty, new Binding { Path = new PropertyPath("SliderValue"), Mode = BindingMode.TwoWay });
        homeStack.Children.Add(sliderValue);
        homeStack.Children.Add(TxtSliderVal);

        homeStack.Children.Add(new TextBlock
        {
            Text = "Additional Controls:",
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Margin = new Thickness(0, 10, 0, 0)
        });

        var rbStack = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
        rbStack.Children.Add(new RadioButton { Name = "rbOption1", Content = "Option 1", IsChecked = true });
        rbStack.Children.Add(new RadioButton { Name = "rbOption2", Content = "Option 2" });
        homeStack.Children.Add(rbStack);

        var progressVal = new ProgressBar
        {
            Name = "progressVal",
            Minimum = 0,
            Maximum = 100,
            Height = 15,
            Width = 300,
            HorizontalAlignment = HorizontalAlignment.Left
        };
        progressVal.SetBinding(RangeBase.ValueProperty, new Binding { Path = new PropertyPath("SliderValue") });
        homeStack.Children.Add(progressVal);

        homeScroll.Content = homeStack;
        homeItem.Content = homeScroll;
        TabContainer.TabItems.Add(homeItem);

        // 2. Scroll Test Tab
        var scrollItem = new TabViewItem
        {
            Name = "tabScroll",
            Header = "Scroll Test",
            IsClosable = false
        };
        var scrollGrid = new Grid();
        scrollGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        scrollGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        var scrollDesc = new TextBlock
        {
            Text = "Use mouse wheel or simulation to scroll the container below:",
            Margin = new Thickness(10, 5, 10, 5),
            FontSize = 11
        };
        Grid.SetRow(scrollDesc, 0);
        scrollGrid.Children.Add(scrollDesc);

        var scrollViewer = new ScrollViewer
        {
            Name = "scrollContainer",
            Margin = new Thickness(10),
            Height = 250
        };
        Grid.SetRow(scrollViewer, 1);

        var scrollStack = new StackPanel { Spacing = 10, Margin = new Thickness(15) };
        scrollStack.Children.Add(new TextBlock { Text = "Scroll Start", FontSize = 14, FontWeight = Microsoft.UI.Text.FontWeights.Bold });
        scrollStack.Children.Add(new Button { Name = "scrollBtn1", Content = "Scroll Button 1" });
        scrollStack.Children.Add(new Button { Name = "scrollBtn2", Content = "Scroll Button 2" });
        scrollStack.Children.Add(new TextBox { Name = "scrollTxt1", PlaceholderText = "Scroll Textbox 1", Width = 200, HorizontalAlignment = HorizontalAlignment.Left });
        scrollStack.Children.Add(new TextBlock { Text = "Middle of Scroll Area", FontSize = 14, FontWeight = Microsoft.UI.Text.FontWeights.Bold, Margin = new Thickness(0, 30, 0, 30) });
        scrollStack.Children.Add(new Button { Name = "scrollBtn3", Content = "Scroll Button 3" });
        scrollStack.Children.Add(new CheckBox { Name = "scrollChk1", Content = "Scroll Checkbox 1" });
        scrollStack.Children.Add(new Slider { Name = "scrollSlider1", Width = 200, HorizontalAlignment = HorizontalAlignment.Left });
        scrollStack.Children.Add(new TextBlock { Text = "Scroll End", FontSize = 14, FontWeight = Microsoft.UI.Text.FontWeights.Bold, Margin = new Thickness(0, 30, 0, 0) });
        scrollStack.Children.Add(new Button { Name = "scrollBtn4", Content = "Scroll Button 4" });

        scrollViewer.Content = scrollStack;
        scrollGrid.Children.Add(scrollViewer);
        scrollItem.Content = scrollGrid;
        TabContainer.TabItems.Add(scrollItem);

        // 3. About Tab
        var aboutItem = new TabViewItem
        {
            Name = "tabAbout",
            Header = "About",
            IsClosable = false
        };
        var aboutStack = new StackPanel { Spacing = 15, Margin = new Thickness(20) };
        aboutStack.Children.Add(new TextBlock { Text = "About This App", FontSize = 18, FontWeight = Microsoft.UI.Text.FontWeights.Bold });
        aboutStack.Children.Add(new TextBlock { Text = "This is a CDP sample target application built for Uno Platform end-to-end testing, visual self-inspection, and layout verifications.", TextWrapping = TextWrapping.Wrap });
        aboutStack.Children.Add(new TextBlock { Text = "Use the URL navigation 'http://localhost:9225/about' to reach this page programmatically.", TextWrapping = TextWrapping.Wrap, FontStyle = Windows.UI.Text.FontStyle.Italic });

        var btnGoBack = new Button
        {
            Name = "btnGoBack",
            Content = "Back to Home",
            Width = 120,
            Margin = new Thickness(0, 20, 0, 0)
        };
        btnGoBack.Click += (s, e) => ViewModel.GoBack();
        aboutStack.Children.Add(btnGoBack);
        aboutItem.Content = aboutStack;
        TabContainer.TabItems.Add(aboutItem);

        // 4. Gestures Tab
        var gesturesItem = new TabViewItem
        {
            Name = "tabGestures",
            Header = "Gestures",
            IsClosable = false
        };
        AutomationProperties.SetAutomationId(gesturesItem, "tabContainerTabItem");

        var gesturesScroll = new ScrollViewer { Margin = new Thickness(10) };
        var gesturesStack = new StackPanel { Spacing = 15 };
        gesturesStack.Children.Add(new TextBlock { Text = "Gesture Testing", FontSize = 18, FontWeight = Microsoft.UI.Text.FontWeights.Bold });

        var doubleClickStack = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
        var btnDoubleClick = new Button
        {
            Name = "btnDoubleClick",
            Content = "Double Click Me",
            Width = 150
        };
        btnDoubleClick.DoubleTapped += (s, e) =>
        {
            ViewModel.DoubleClickedCount++;
            ViewModel.DoubleClickStatus = $"Double Clicked {ViewModel.DoubleClickedCount} times!";
        };
        doubleClickStack.Children.Add(btnDoubleClick);
        doubleClickStack.Children.Add(DoubleClickStatus);
        gesturesStack.Children.Add(doubleClickStack);

        var longPressStack = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
        var btnLongPress = new Button
        {
            Name = "btnLongPress",
            Content = "Long Press Me",
            Width = 150
        };
        btnLongPress.PointerPressed += (s, e) => _stopwatch.Restart();
        btnLongPress.PointerReleased += (s, e) =>
        {
            _stopwatch.Stop();
            if (_stopwatch.ElapsedMilliseconds > 800)
            {
                ViewModel.LongPressedCount++;
                ViewModel.LongPressStatus = $"Long Pressed {ViewModel.LongPressedCount} times!";
            }
        };
        longPressStack.Children.Add(btnLongPress);
        longPressStack.Children.Add(LongPressStatus);
        gesturesStack.Children.Add(longPressStack);

        var txtClearTarget = new TextBox
        {
            Name = "txtClearTarget",
            PlaceholderText = "Clear target text...",
            Width = 300,
            HorizontalAlignment = HorizontalAlignment.Left
        };
        txtClearTarget.SetBinding(TextBox.TextProperty, new Binding { Path = new PropertyPath("ClearTargetText"), Mode = BindingMode.TwoWay });
        gesturesStack.Children.Add(txtClearTarget);

        var dragDropStack = new StackPanel { Spacing = 10 };
        var borderStack = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };

        var borderDragSource = new Border
        {
            Name = "borderDragSource",
            Background = new SolidColorBrush(Microsoft.UI.Colors.Gray),
            Width = 120,
            Height = 60,
            CornerRadius = new CornerRadius(4),
            Child = new TextBlock { Text = "Drag Source", HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center }
        };
        borderDragSource.PointerPressed += (s, e) => _dragSourcePressed = true;
        borderStack.Children.Add(borderDragSource);

        var borderDropTarget = new Border
        {
            Name = "borderDropTarget",
            Background = new SolidColorBrush(Microsoft.UI.Colors.DarkGray),
            Width = 120,
            Height = 60,
            CornerRadius = new CornerRadius(4),
            Child = new TextBlock { Text = "Drop Target", HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center }
        };
        borderDropTarget.PointerReleased += (s, e) =>
        {
            if (_dragSourcePressed)
            {
                ViewModel.DragDropStatus = "Dropped Successfully!";
                _dragSourcePressed = false;
            }
        };
        borderStack.Children.Add(borderDropTarget);
        dragDropStack.Children.Add(borderStack);

        dragDropStack.Children.Add(DragDropStatus);
        gesturesStack.Children.Add(dragDropStack);

        gesturesScroll.Content = gesturesStack;
        gesturesItem.Content = gesturesScroll;
        TabContainer.TabItems.Add(gesturesItem);

        // 5. Asserts & Keys Tab
        var assertsItem = new TabViewItem
        {
            Name = "tabAssertsAndKeys",
            Header = "Asserts & Keys",
            IsClosable = false
        };
        AutomationProperties.SetAutomationId(assertsItem, "tabContainerTabItem");

        var assertsScroll = new ScrollViewer { Margin = new Thickness(10) };
        var assertsStack = new StackPanel { Spacing = 15 };
        assertsStack.Children.Add(new TextBlock { Text = "Asserts & Keys Testing", FontSize = 18, FontWeight = Microsoft.UI.Text.FontWeights.Bold });

        var txtKeyInput = new TextBox
        {
            Name = "txtKeyInput",
            PlaceholderText = "Press keys here...",
            Width = 300,
            HorizontalAlignment = HorizontalAlignment.Left
        };
        txtKeyInput.KeyDown += (s, e) => ViewModel.LastPressedKey = e.Key.ToString();
        assertsStack.Children.Add(txtKeyInput);

        TxtVisibilityTarget = new TextBlock
        {
            Name = "txtVisibilityTarget",
            Text = "Visibility Target Text",
            FontSize = 16,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
        };
        TxtVisibilityTarget.SetBinding(UIElement.VisibilityProperty, new Binding { Path = new PropertyPath("IsVisibleTarget"), Converter = boolToVis });

        var btnToggleVisibility = new Button
        {
            Name = "btnToggleVisibility",
            Content = "Toggle Visibility",
            Width = 150
        };
        btnToggleVisibility.Click += (s, e) => ViewModel.ToggleVisibility();
        assertsStack.Children.Add(btnToggleVisibility);
        assertsStack.Children.Add(TxtVisibilityTarget);

        assertsScroll.Content = assertsStack;
        assertsItem.Content = assertsScroll;
        TabContainer.TabItems.Add(assertsItem);

        // 6. Popups & Windows Tab
        var popupsItem = new TabViewItem
        {
            Name = "tabPopups",
            Header = "Popups & Windows",
            IsClosable = false
        };
        AutomationProperties.SetAutomationId(popupsItem, "tabPopups");

        var popupsScroll = new ScrollViewer { Margin = new Thickness(10) };
        var popupsStack = new StackPanel { Spacing = 20 };
        popupsStack.Children.Add(new TextBlock { Text = "Popups, Dropdowns & Context Menus", FontSize = 18, FontWeight = Microsoft.UI.Text.FontWeights.Bold });

        var comboStack = new StackPanel { Spacing = 5 };
        comboStack.Children.Add(new TextBlock { Text = "ComboBox Dropdown:", FontSize = 12, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });

        var comboPopup = new ComboBox
        {
            Name = "comboPopup",
            Width = 220,
            PlaceholderText = "Select an option"
        };
        comboPopup.Items.Add(new ComboBoxItem { Name = "comboItem1", Content = "Popup Option 1" });
        comboPopup.Items.Add(new ComboBoxItem { Name = "comboItem2", Content = "Popup Option 2" });
        comboPopup.Items.Add(new ComboBoxItem { Name = "comboItem3", Content = "Popup Option 3" });
        comboStack.Children.Add(comboPopup);
        popupsStack.Children.Add(comboStack);

        var contextStack = new StackPanel { Spacing = 5 };
        contextStack.Children.Add(new TextBlock { Text = "ContextMenu (Right Click):", FontSize = 12, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });

        var btnContextMenu = new Button
        {
            Name = "btnContextMenu",
            Content = "Right Click Me for Menu",
            Width = 220
        };
        var menuFlyout = new MenuFlyout();
        AutomationProperties.SetAutomationId(menuFlyout, "contextMenu");

        var item1 = new MenuFlyoutItem { Name = "menuItem1", Text = "Menu Item 1" };
        item1.Click += (s, e) => ViewModel.PopupStatus = $"Selected Menu: {item1.Text}";
        menuFlyout.Items.Add(item1);

        var item2 = new MenuFlyoutItem { Name = "menuItem2", Text = "Menu Item 2" };
        item2.Click += (s, e) => ViewModel.PopupStatus = $"Selected Menu: {item2.Text}";
        menuFlyout.Items.Add(item2);

        btnContextMenu.ContextFlyout = menuFlyout;
        contextStack.Children.Add(btnContextMenu);
        popupsStack.Children.Add(contextStack);

        var flyoutStack = new StackPanel { Spacing = 5 };
        flyoutStack.Children.Add(new TextBlock { Text = "Button Flyout:", FontSize = 12, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });

        BtnFlyout = new Button
        {
            Name = "btnFlyout",
            Content = "Click Me for Flyout",
            Width = 220
        };
        var flyout = new Flyout();
        AutomationProperties.SetAutomationId(flyout, "buttonFlyout");

        var flyoutContentStack = new StackPanel { Spacing = 10, Margin = new Thickness(10) };
        flyoutContentStack.Children.Add(new TextBlock { Text = "Flyout content!", FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });

        var btnInsideFlyout = new Button
        {
            Name = "btnInsideFlyout",
            Content = "Click Inside Flyout"
        };
        btnInsideFlyout.Click += (s, e) =>
        {
            ViewModel.PopupStatus = "Clicked Inside Flyout!";
            flyout.Hide();
        };
        flyoutContentStack.Children.Add(btnInsideFlyout);
        flyout.Content = flyoutContentStack;
        BtnFlyout.Flyout = flyout;
        flyoutStack.Children.Add(BtnFlyout);
        popupsStack.Children.Add(flyoutStack);

        var popupStatusStack = new StackPanel { Spacing = 5 };
        popupStatusStack.Children.Add(new TextBlock { Text = "Popup Status:", FontSize = 12, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });

        TxtPopupStatus = new TextBlock
        {
            Name = "txtPopupStatus",
            FontSize = 14,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
        };
        TxtPopupStatus.SetBinding(TextBlock.TextProperty, new Binding { Path = new PropertyPath("PopupStatus") });
        popupStatusStack.Children.Add(TxtPopupStatus);
        popupsStack.Children.Add(popupStatusStack);

        popupsScroll.Content = popupsStack;
        popupsItem.Content = popupsScroll;
        TabContainer.TabItems.Add(popupsItem);

        TabContainer.SetBinding(TabView.SelectedIndexProperty, new Binding { Path = new PropertyPath("SelectedTabIndex"), Mode = BindingMode.TwoWay });

        rootGrid.Children.Add(TabContainer);
        Content = rootGrid;
    }

    public void Navigate(string url)
    {
        if (url.EndsWith("/about", StringComparison.OrdinalIgnoreCase))
        {
            TabContainer.SelectedIndex = 2; // About tab
        }
        else if (url.EndsWith("/scroll", StringComparison.OrdinalIgnoreCase))
        {
            TabContainer.SelectedIndex = 1; // Scroll tab
        }
        else if (url.EndsWith("/gestures", StringComparison.OrdinalIgnoreCase))
        {
            TabContainer.SelectedIndex = 3; // Gestures tab
        }
        else
        {
            TabContainer.SelectedIndex = 0; // Home tab
        }
    }
}
