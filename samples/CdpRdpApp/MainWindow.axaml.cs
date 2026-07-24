using Avalonia.Controls;
using CdpRdpApp.ViewModels;

namespace CdpRdpApp;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainWindowViewModel();
    }
}
