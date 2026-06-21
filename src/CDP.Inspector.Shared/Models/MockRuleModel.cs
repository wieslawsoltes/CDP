using System;
using System.ComponentModel;

namespace CdpInspectorApp.Models;

public class MockRuleModel : INotifyPropertyChanged
{
    private string _urlPattern = "";
    private bool _isActive = true;
    private int _statusCode = 200;
    private string _mockBody = "";
    private string _responseHeaders = "Content-Type: application/json";

    public string UrlPattern
    {
        get => _urlPattern;
        set { _urlPattern = value; OnPropertyChanged(nameof(UrlPattern)); }
    }

    public bool IsActive
    {
        get => _isActive;
        set { _isActive = value; OnPropertyChanged(nameof(IsActive)); }
    }

    public int StatusCode
    {
        get => _statusCode;
        set { _statusCode = value; OnPropertyChanged(nameof(StatusCode)); }
    }

    public string MockBody
    {
        get => _mockBody;
        set { _mockBody = value; OnPropertyChanged(nameof(MockBody)); }
    }

    public string ResponseHeaders
    {
        get => _responseHeaders;
        set { _responseHeaders = value; OnPropertyChanged(nameof(ResponseHeaders)); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
