using System.ComponentModel;

namespace CdpInspectorApp.Models;

public class ResourceEntryModel : INotifyPropertyChanged
{
    private string _value = "";
    public string Key { get; set; } = "";
    public string Type { get; set; } = "";

    public string Value
    {
        get => _value;
        set { _value = value; OnPropertyChanged(nameof(Value)); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
