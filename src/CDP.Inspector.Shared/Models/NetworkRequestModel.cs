using System;
using System.ComponentModel;

namespace CdpInspectorApp.Models;

public class NetworkRequestModel : INotifyPropertyChanged
{
    private string _status = "Pending";
    private string _time = "--";
    private string _responseHeaders = "";
    private string _responseBody = "";

    public string RequestId { get; set; } = "";
    public string Url { get; set; } = "";
    public string Method { get; set; } = "";
    public string Type { get; set; } = "XHR";
    public string RequestHeaders { get; set; } = "";

    public string Status
    {
        get => _status;
        set { _status = value; OnPropertyChanged(nameof(Status)); }
    }

    public string Time
    {
        get => _time;
        set { _time = value; OnPropertyChanged(nameof(Time)); }
    }

    public string ResponseHeaders
    {
        get => _responseHeaders;
        set { _responseHeaders = value; OnPropertyChanged(nameof(ResponseHeaders)); }
    }

    public string ResponseBody
    {
        get => _responseBody;
        set { _responseBody = value; OnPropertyChanged(nameof(ResponseBody)); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
