using System;
using Avalonia.Controls;

namespace CdpSampleApp;

public partial class MainWindow : Window
{
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
            else
            {
                vm.SelectedTabIndex = 0; // Home tab
            }
        }
    }
}