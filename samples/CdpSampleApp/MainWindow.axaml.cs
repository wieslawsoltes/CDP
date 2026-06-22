using System;
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

        var btnOpenSecond = this.FindControl<Button>("btnOpenSecond");
        if (btnOpenSecond != null)
        {
            btnOpenSecond.Click += ButtonOpenSecond_Click;
        }

        var btnBack = this.FindControl<Button>("btnGoBack");
        if (btnBack != null)
        {
            btnBack.Click += (s, e) => Navigate("/");
        }
    }

    public void Navigate(string url)
    {
        var tabs = this.FindControl<TabControl>("tabContainer");
        if (tabs == null) return;

        if (url.EndsWith("/about", StringComparison.OrdinalIgnoreCase))
        {
            tabs.SelectedIndex = 2; // About tab
        }
        else if (url.EndsWith("/scroll", StringComparison.OrdinalIgnoreCase))
        {
            tabs.SelectedIndex = 1; // Scroll tab
        }
        else
        {
            tabs.SelectedIndex = 0; // Home tab
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

    private void ButtonOpenSecond_Click(object? sender, RoutedEventArgs e)
    {
        var secondWin = new Window
        {
            Title = "Sample Second Window",
            Width = 400,
            Height = 300,
            Content = new StackPanel
            {
                Spacing = 15,
                Margin = new Avalonia.Thickness(20),
                Children =
                {
                    new TextBlock { Text = "This is the second window!", FontSize = 16, FontWeight = Avalonia.Media.FontWeight.Bold },
                    new Button { Name = "btnSecondClick", Content = "Click Me Second" }
                }
            }
        };
        secondWin.Show();
    }
}