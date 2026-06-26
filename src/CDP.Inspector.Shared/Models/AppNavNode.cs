using System.Collections.ObjectModel;
using System.ComponentModel;

namespace CdpInspectorApp.Models;

public class AppNavNode : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    private bool _isSelected;
    public bool IsSelected
    {
        get => _isSelected;
        set { _isSelected = value; OnPropertyChanged(); }
    }

    private bool _isExpanded;
    public bool IsExpanded
    {
        get => _isExpanded;
        set { _isExpanded = value; OnPropertyChanged(); }
    }

    public string Name { get; set; } = "";
    public string? NodeType { get; set; }
    public string? DatabasePath { get; set; }
    public ObservableCollection<AppNavNode> Children { get; } = new();

    public AppNavNode(string name)
    {
        Name = name;
    }
}
