using System;

namespace CDP.FluentNavigation;

public class NavigationViewSelectionChangedEventArgs : EventArgs
{
    public object? SelectedItem { get; }
    public bool IsSettingsSelected { get; }

    public NavigationViewSelectionChangedEventArgs(object? selectedItem, bool isSettingsSelected)
    {
        SelectedItem = selectedItem;
        IsSettingsSelected = isSettingsSelected;
    }
}
