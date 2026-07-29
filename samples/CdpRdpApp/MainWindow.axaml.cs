using System;
using Avalonia.Controls;
using CdpRdpApp.ViewModels;

namespace CdpRdpApp;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainWindowViewModel();
        Closed += async (_, _) =>
        {
            if (DataContext is IAsyncDisposable asyncDisposable)
            {
                await asyncDisposable.DisposeAsync();
            }
            else if (DataContext is IDisposable disposable)
            {
                disposable.Dispose();
            }
        };
    }
}
