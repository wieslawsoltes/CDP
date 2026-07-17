using System;
using System.Collections;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;

namespace CDP.FluentNavigation;

public class NavigationViewListBox : ListBox
{
    protected override bool NeedsContainerOverride(object? item, int index, out object? recycleKey)
    {
        recycleKey = null;
        return !(item is NavigationViewItem);
    }

    protected override Control CreateContainerForItemOverride(object? item, int index, object? recycleKey)
    {
        return new NavigationViewItem();
    }

    protected override Type StyleKeyOverride => typeof(ListBox);
}

[TemplatePart(Name = "PART_MenuItemsListBox", Type = typeof(NavigationViewListBox))]
[TemplatePart(Name = "PART_FooterMenuItemsListBox", Type = typeof(NavigationViewListBox))]
[TemplatePart(Name = "PART_SettingsItem", Type = typeof(NavigationViewItem))]
[TemplatePart(Name = "PART_PaneToggleButton", Type = typeof(ToggleButton))]
[TemplatePart(Name = "PART_BackButton", Type = typeof(Button))]
public class NavigationView : ContentControl
{
    public static readonly StyledProperty<IList> MenuItemsProperty =
        AvaloniaProperty.Register<NavigationView, IList>(nameof(MenuItems));

    public static readonly StyledProperty<IEnumerable?> MenuItemsSourceProperty =
        AvaloniaProperty.Register<NavigationView, IEnumerable?>(nameof(MenuItemsSource));

    public static readonly StyledProperty<IList> FooterMenuItemsProperty =
        AvaloniaProperty.Register<NavigationView, IList>(nameof(FooterMenuItems));

    public static readonly StyledProperty<IEnumerable?> FooterMenuItemsSourceProperty =
        AvaloniaProperty.Register<NavigationView, IEnumerable?>(nameof(FooterMenuItemsSource));

    public static readonly StyledProperty<object?> SelectedItemProperty =
        AvaloniaProperty.Register<NavigationView, object?>(nameof(SelectedItem), defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    public static readonly StyledProperty<bool> IsPaneOpenProperty =
        AvaloniaProperty.Register<NavigationView, bool>(nameof(IsPaneOpen), true, defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    public static readonly StyledProperty<NavigationViewPaneDisplayMode> PaneDisplayModeProperty =
        AvaloniaProperty.Register<NavigationView, NavigationViewPaneDisplayMode>(nameof(PaneDisplayMode), NavigationViewPaneDisplayMode.Expanded);

    public static readonly StyledProperty<double> CompactPaneLengthProperty =
        AvaloniaProperty.Register<NavigationView, double>(nameof(CompactPaneLength), 48.0);

    public static readonly StyledProperty<double> OpenPaneLengthProperty =
        AvaloniaProperty.Register<NavigationView, double>(nameof(OpenPaneLength), 240.0);

    public static readonly StyledProperty<object?> PaneHeaderProperty =
        AvaloniaProperty.Register<NavigationView, object?>(nameof(PaneHeader));

    public static readonly StyledProperty<object?> PaneFooterProperty =
        AvaloniaProperty.Register<NavigationView, object?>(nameof(PaneFooter));

    public static readonly StyledProperty<object?> HeaderProperty =
        AvaloniaProperty.Register<NavigationView, object?>(nameof(Header));

    public static readonly StyledProperty<bool> ShowBackButtonProperty =
        AvaloniaProperty.Register<NavigationView, bool>(nameof(ShowBackButton), false);

    public static readonly StyledProperty<bool> IsSettingsVisibleProperty =
        AvaloniaProperty.Register<NavigationView, bool>(nameof(IsSettingsVisible), true);

    public IList MenuItems
    {
        get => GetValue(MenuItemsProperty);
        set => SetValue(MenuItemsProperty, value);
    }

    public IEnumerable? MenuItemsSource
    {
        get => GetValue(MenuItemsSourceProperty);
        set => SetValue(MenuItemsSourceProperty, value);
    }

    public IList FooterMenuItems
    {
        get => GetValue(FooterMenuItemsProperty);
        set => SetValue(FooterMenuItemsProperty, value);
    }

    public IEnumerable? FooterMenuItemsSource
    {
        get => GetValue(FooterMenuItemsSourceProperty);
        set => SetValue(FooterMenuItemsSourceProperty, value);
    }

    public object? SelectedItem
    {
        get => GetValue(SelectedItemProperty);
        set => SetValue(SelectedItemProperty, value);
    }

    public bool IsPaneOpen
    {
        get => GetValue(IsPaneOpenProperty);
        set => SetValue(IsPaneOpenProperty, value);
    }

    public NavigationViewPaneDisplayMode PaneDisplayMode
    {
        get => GetValue(PaneDisplayModeProperty);
        set => SetValue(PaneDisplayModeProperty, value);
    }

    public double CompactPaneLength
    {
        get => GetValue(CompactPaneLengthProperty);
        set => SetValue(CompactPaneLengthProperty, value);
    }

    public double OpenPaneLength
    {
        get => GetValue(OpenPaneLengthProperty);
        set => SetValue(OpenPaneLengthProperty, value);
    }

    public object? PaneHeader
    {
        get => GetValue(PaneHeaderProperty);
        set => SetValue(PaneHeaderProperty, value);
    }

    public object? PaneFooter
    {
        get => GetValue(PaneFooterProperty);
        set => SetValue(PaneFooterProperty, value);
    }

    public object? Header
    {
        get => GetValue(HeaderProperty);
        set => SetValue(HeaderProperty, value);
    }

    public bool ShowBackButton
    {
        get => GetValue(ShowBackButtonProperty);
        set => SetValue(ShowBackButtonProperty, value);
    }

    public bool IsSettingsVisible
    {
        get => GetValue(IsSettingsVisibleProperty);
        set => SetValue(IsSettingsVisibleProperty, value);
    }

    public event EventHandler<NavigationViewSelectionChangedEventArgs>? SelectionChanged;

    static NavigationView()
    {
        SelectedItemProperty.Changed.AddClassHandler<NavigationView>((x, e) => x.OnSelectedItemChanged(e));
        MenuItemsSourceProperty.Changed.AddClassHandler<NavigationView>((x, e) => x.OnItemsSourcesChanged(e));
        FooterMenuItemsSourceProperty.Changed.AddClassHandler<NavigationView>((x, e) => x.OnItemsSourcesChanged(e));
    }

    public NavigationView()
    {
        MenuItems = new AvaloniaList<object>();
        FooterMenuItems = new AvaloniaList<object>();
    }

    protected override Type StyleKeyOverride => typeof(NavigationView);

    private NavigationViewListBox? _menuItemsListBox;
    private NavigationViewListBox? _footerItemsListBox;
    private NavigationViewItem? _settingsItem;
    private Button? _backButton;
    private ToggleButton? _paneToggleButton;
    private bool _isUpdatingSelection;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        if (_menuItemsListBox != null)
            _menuItemsListBox.SelectionChanged -= OnMenuItemsListBoxSelectionChanged;
        if (_footerItemsListBox != null)
            _footerItemsListBox.SelectionChanged -= OnFooterItemsListBoxSelectionChanged;
        if (_settingsItem != null)
            _settingsItem.PointerPressed -= OnSettingsItemPointerPressed;

        _menuItemsListBox = e.NameScope.Find<NavigationViewListBox>("PART_MenuItemsListBox");
        _footerItemsListBox = e.NameScope.Find<NavigationViewListBox>("PART_FooterMenuItemsListBox");
        _settingsItem = e.NameScope.Find<NavigationViewItem>("PART_SettingsItem");
        _backButton = e.NameScope.Find<Button>("PART_BackButton");
        _paneToggleButton = e.NameScope.Find<ToggleButton>("PART_PaneToggleButton");

        if (_menuItemsListBox != null)
            _menuItemsListBox.SelectionChanged += OnMenuItemsListBoxSelectionChanged;
        if (_footerItemsListBox != null)
            _footerItemsListBox.SelectionChanged += OnFooterItemsListBoxSelectionChanged;
        if (_settingsItem != null)
            _settingsItem.PointerPressed += OnSettingsItemPointerPressed;

        UpdateListBoxItemsSources();
        UpdateSelection();
    }

    private void OnItemsSourcesChanged(AvaloniaPropertyChangedEventArgs e)
    {
        UpdateListBoxItemsSources();
    }

    private void UpdateListBoxItemsSources()
    {
        if (_menuItemsListBox != null)
        {
            if (MenuItemsSource != null)
                _menuItemsListBox.ItemsSource = MenuItemsSource;
            else
                _menuItemsListBox.ItemsSource = MenuItems;
        }

        if (_footerItemsListBox != null)
        {
            if (FooterMenuItemsSource != null)
                _footerItemsListBox.ItemsSource = FooterMenuItemsSource;
            else
                _footerItemsListBox.ItemsSource = FooterMenuItems;
        }
    }

    private void OnMenuItemsListBoxSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_isUpdatingSelection) return;

        if (_menuItemsListBox != null && _menuItemsListBox.SelectedItem != null)
        {
            _isUpdatingSelection = true;
            try
            {
                if (_footerItemsListBox != null)
                    _footerItemsListBox.SelectedItem = null;
                if (_settingsItem != null)
                    _settingsItem.IsSelected = false;

                SelectedItem = _menuItemsListBox.SelectedItem;
                RaiseSelectionChanged(SelectedItem, false);
            }
            finally
            {
                _isUpdatingSelection = false;
            }
        }
    }

    private void OnFooterItemsListBoxSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_isUpdatingSelection) return;

        if (_footerItemsListBox != null && _footerItemsListBox.SelectedItem != null)
        {
            _isUpdatingSelection = true;
            try
            {
                if (_menuItemsListBox != null)
                    _menuItemsListBox.SelectedItem = null;
                if (_settingsItem != null)
                    _settingsItem.IsSelected = false;

                SelectedItem = _footerItemsListBox.SelectedItem;
                RaiseSelectionChanged(SelectedItem, false);
            }
            finally
            {
                _isUpdatingSelection = false;
            }
        }
    }

    private void OnSettingsItemPointerPressed(object? sender, Avalonia.Input.PointerPressedEventArgs e)
    {
        if (_isUpdatingSelection) return;

        _isUpdatingSelection = true;
        try
        {
            if (_menuItemsListBox != null)
                _menuItemsListBox.SelectedItem = null;
            if (_footerItemsListBox != null)
                _footerItemsListBox.SelectedItem = null;
            if (_settingsItem != null)
                _settingsItem.IsSelected = true;

            SelectedItem = _settingsItem;
            RaiseSelectionChanged(SelectedItem, true);
        }
        finally
        {
            _isUpdatingSelection = false;
        }
        e.Handled = true;
    }

    private void OnSelectedItemChanged(AvaloniaPropertyChangedEventArgs e)
    {
        if (_isUpdatingSelection) return;

        UpdateSelection();
        RaiseSelectionChanged(SelectedItem, SelectedItem == _settingsItem);
    }

    private void UpdateSelection()
    {
        _isUpdatingSelection = true;
        try
        {
            var selected = SelectedItem;

            if (selected == null)
            {
                if (_menuItemsListBox != null) _menuItemsListBox.SelectedItem = null;
                if (_footerItemsListBox != null) _footerItemsListBox.SelectedItem = null;
                if (_settingsItem != null) _settingsItem.IsSelected = false;
                return;
            }

            if (selected == _settingsItem)
            {
                if (_menuItemsListBox != null) _menuItemsListBox.SelectedItem = null;
                if (_footerItemsListBox != null) _footerItemsListBox.SelectedItem = null;
                if (_settingsItem != null) _settingsItem.IsSelected = true;
                return;
            }

            bool foundInMenu = false;
            if (_menuItemsListBox != null)
            {
                _menuItemsListBox.SelectedItem = selected;
                if (_menuItemsListBox.SelectedItem == selected)
                {
                    foundInMenu = true;
                    if (_footerItemsListBox != null) _footerItemsListBox.SelectedItem = null;
                    if (_settingsItem != null) _settingsItem.IsSelected = false;
                }
            }

            if (!foundInMenu && _footerItemsListBox != null)
            {
                _footerItemsListBox.SelectedItem = selected;
                if (_footerItemsListBox.SelectedItem == selected)
                {
                    if (_menuItemsListBox != null) _menuItemsListBox.SelectedItem = null;
                    if (_settingsItem != null) _settingsItem.IsSelected = false;
                }
            }
        }
        finally
        {
            _isUpdatingSelection = false;
        }
    }

    protected virtual void RaiseSelectionChanged(object? selectedItem, bool isSettingsSelected)
    {
        SelectionChanged?.Invoke(this, new NavigationViewSelectionChangedEventArgs(selectedItem, isSettingsSelected));
    }
}
