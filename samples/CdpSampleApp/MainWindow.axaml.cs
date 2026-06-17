using Avalonia.Controls;
using Avalonia.Interactivity;

namespace CdpSampleApp;

public partial class MainWindow : Window
{
    private int _clickCount = 0;

    public MainWindow()
    {
        InitializeComponent();
        
        var btn = this.FindControl<Button>("btnClickMe");
        if (btn != null)
        {
            btn.Click += Button_Click;
        }
    }

    private void Button_Click(object? sender, RoutedEventArgs e)
    {
        _clickCount++;
        var status = this.FindControl<TextBlock>("txtStatus");
        if (status != null)
        {
            status.Text = $"Clicked {_clickCount} times!";
        }
    }
}