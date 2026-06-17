using System.Net.Http;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace CdpSampleApp;

public partial class MainWindow : Window
{
    private int _clickCount = 0;
    private readonly HttpClient _httpClient = new();

    public MainWindow()
    {
        InitializeComponent();
        
        var btn = this.FindControl<Button>("btnClickMe");
        if (btn != null)
        {
            btn.Click += Button_Click;
        }

        var btnHttp = this.FindControl<Button>("btnSendHttp");
        if (btnHttp != null)
        {
            btnHttp.Click += ButtonHttp_Click;
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

    private async void ButtonHttp_Click(object? sender, RoutedEventArgs e)
    {
        var status = this.FindControl<TextBlock>("txtStatus");
        if (status != null) status.Text = "Sending HTTP request...";
        try
        {
            var res = await _httpClient.GetStringAsync("https://jsonplaceholder.typicode.com/todos/1");
            if (status != null) status.Text = "HTTP Request successful!";
        }
        catch (System.Exception ex)
        {
            if (status != null) status.Text = $"HTTP Failed: {ex.Message}";
        }
    }
}