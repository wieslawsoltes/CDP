using System;
using System.ComponentModel;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace UnoSampleApp.ViewModels;

public class MainWindowViewModel : INotifyPropertyChanged
{
    private readonly HttpClient _httpClient = new();

    public event PropertyChangedEventHandler? PropertyChanged;

    protected bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string? propertyName = null)
    {
        if (Equals(storage, value)) return false;
        storage = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }

    private int _selectedTabIndex = 0;
    public int SelectedTabIndex
    {
        get => _selectedTabIndex;
        set => SetProperty(ref _selectedTabIndex, value);
    }

    private int _clickCount = 0;
    public int ClickCount
    {
        get => _clickCount;
        set => SetProperty(ref _clickCount, value);
    }

    private string _statusText = "Not Clicked";
    public string StatusText
    {
        get => _statusText;
        set => SetProperty(ref _statusText, value);
    }

    private string _inputText = "";
    public string InputText
    {
        get => _inputText;
        set => SetProperty(ref _inputText, value);
    }

    private bool _isOptionEnabled = false;
    public bool IsOptionEnabled
    {
        get => _isOptionEnabled;
        set => SetProperty(ref _isOptionEnabled, value);
    }

    private double _sliderValue = 50.0;
    public double SliderValue
    {
        get => _sliderValue;
        set
        {
            if (SetProperty(ref _sliderValue, value))
            {
                OnPropertyChanged(nameof(SliderValueText));
            }
        }
    }

    public string SliderValueText => $"Slider Value: {SliderValue:0}";

    private int _doubleClickedCount = 0;
    public int DoubleClickedCount
    {
        get => _doubleClickedCount;
        set => SetProperty(ref _doubleClickedCount, value);
    }

    private string _doubleClickStatus = "Not Double Clicked";
    public string DoubleClickStatus
    {
        get => _doubleClickStatus;
        set => SetProperty(ref _doubleClickStatus, value);
    }

    private int _longPressedCount = 0;
    public int LongPressedCount
    {
        get => _longPressedCount;
        set => SetProperty(ref _longPressedCount, value);
    }

    private string _longPressStatus = "Not Long Pressed";
    public string LongPressStatus
    {
        get => _longPressStatus;
        set => SetProperty(ref _longPressStatus, value);
    }

    private string _clearTargetText = "Clear me";
    public string ClearTargetText
    {
        get => _clearTargetText;
        set => SetProperty(ref _clearTargetText, value);
    }

    private string _dragDropStatus = "No Drag Drop";
    public string DragDropStatus
    {
        get => _dragDropStatus;
        set => SetProperty(ref _dragDropStatus, value);
    }

    private bool _isVisibleTarget = true;
    public bool IsVisibleTarget
    {
        get => _isVisibleTarget;
        set => SetProperty(ref _isVisibleTarget, value);
    }

    private string _lastPressedKey = "";
    public string LastPressedKey
    {
        get => _lastPressedKey;
        set => SetProperty(ref _lastPressedKey, value);
    }

    private string _popupStatus = "No interaction yet";
    public string PopupStatus
    {
        get => _popupStatus;
        set => SetProperty(ref _popupStatus, value);
    }

    public void Click()
    {
        ClickCount++;
        StatusText = $"Clicked {ClickCount} times!";
    }

    public async Task SendHttpAsync()
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

    public void OpenSecondWindow()
    {
        try
        {
            var secondWin = new Window
            {
                Title = "Sample Second Window"
            };

            var stack = new StackPanel
            {
                Spacing = 15,
                Margin = new Thickness(20)
            };
            stack.Children.Add(new TextBlock
            {
                Text = "This is the second window!",
                FontSize = 16,
                FontWeight = Microsoft.UI.Text.FontWeights.Bold
            });
            stack.Children.Add(new Button
            {
                Name = "btnSecondClick",
                Content = "Click Me Second"
            });

            secondWin.Content = stack;
            secondWin.Activate();
            WinUI.Diagnostics.Cdp.CdpServer.GetOrCreateTarget(secondWin);
        }
        catch (Exception ex)
        {
            StatusText = $"Second window creation note: {ex.Message}";
        }
    }

    public void ToggleVisibility()
    {
        IsVisibleTarget = !IsVisibleTarget;
    }

    public void GoBack()
    {
        SelectedTabIndex = 0;
    }

    protected void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
