using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using CDP.FluentNavigation;
using CdpGalleryApp.ViewModels;

namespace CdpGalleryApp;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainWindowViewModel();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void OnSelectionChanged(object? sender, NavigationViewSelectionChangedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
        {
            if (e.IsSettingsSelected)
            {
                vm.NavigateTo("Settings");
            }
            else if (e.SelectedItem is NavigationViewItem item && item.Tag is string tag)
            {
                vm.NavigateTo(tag);
            }
        }
    }
}
