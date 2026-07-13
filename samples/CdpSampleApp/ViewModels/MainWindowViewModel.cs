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

    [Reactive]
    private int _doubleClickedCount = 0;

    [Reactive]
    private string _doubleClickStatus = "Not Double Clicked";

    [Reactive]
    private int _longPressedCount = 0;

    [Reactive]
    private string _longPressStatus = "Not Long Pressed";

    [Reactive]
    private string _clearTargetText = "Clear me";

    [Reactive]
    private string _dragDropStatus = "No Drag Drop";

    [Reactive]
    private bool _isVisibleTarget = true;

    [Reactive]
    private string _lastPressedKey = "";

    [ReactiveCommand]
    private void ToggleVisibility()
    {
        IsVisibleTarget = !IsVisibleTarget;
    }


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
