using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Reactive;
using System.Threading.Tasks;
using Avalonia.Controls;
using ReactiveUI;
using ReactiveUI.SourceGenerators;

namespace CdpSampleApp.ViewModels;

public partial class MainWindowViewModel : ReactiveObject
{
    private readonly HttpClient _httpClient = new();

    [Reactive]
    private int _selectedTabIndex = 0;

    [Reactive]
    private int _clickCount = 0;

    [Reactive]
    private string _statusText = "Not Clicked";

    [Reactive]
    private string _inputText = "";

    [Reactive]
    private bool _isOptionEnabled = false;

    [Reactive]
    private double _sliderValue = 50.0;

    [Reactive]
    private string _selectedOption = "Option 1";

    [ReactiveCommand]
    private void Click()
    {
        ClickCount++;
        StatusText = $"Clicked {ClickCount} times!";
    }

    [ReactiveCommand]
    private async Task SendHttp()
    {
        StatusText = "Sending HTTP request...";
        try
        {
            await _httpClient.GetStringAsync("https://jsonplaceholder.typicode.com/todos/1");
            StatusText = "HTTP Request successful!";
        }
        catch (Exception ex)
        {
            StatusText = $"HTTP Failed: {ex.Message}";
        }
    }

    [ReactiveCommand]
    private void OpenSecondWindow()
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


    [ReactiveCommand]
    private void GoBack()
    {
        SelectedTabIndex = 0;
    }
}
