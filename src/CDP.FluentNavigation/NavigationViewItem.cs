using System;
using Avalonia;
using Avalonia.Controls;

namespace CDP.FluentNavigation;

public class NavigationViewItem : ListBoxItem
{
    public static readonly StyledProperty<object?> IconProperty =
        AvaloniaProperty.Register<NavigationViewItem, object?>(nameof(Icon));

    public static readonly StyledProperty<object?> InfoBadgeProperty =
        AvaloniaProperty.Register<NavigationViewItem, object?>(nameof(InfoBadge));

    public static readonly StyledProperty<bool> SelectsOnTriggerProperty =
        AvaloniaProperty.Register<NavigationViewItem, bool>(nameof(SelectsOnTrigger), true);

    public object? Icon
    {
        get => GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    public object? InfoBadge
    {
        get => GetValue(InfoBadgeProperty);
        set => SetValue(InfoBadgeProperty, value);
    }

    public bool SelectsOnTrigger
    {
        get => GetValue(SelectsOnTriggerProperty);
        set => SetValue(SelectsOnTriggerProperty, value);
    }

    protected override Type StyleKeyOverride => typeof(NavigationViewItem);
}
