using System;
using System.ComponentModel;

namespace CdpInspectorApp.Models;

public class BlockedUrlModel : INotifyPropertyChanged
{
    private string _pattern = "";

    public string Pattern
    {
        get => _pattern;
        set { _pattern = value; OnPropertyChanged(nameof(Pattern)); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
