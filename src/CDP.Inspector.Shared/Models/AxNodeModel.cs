using System.Collections.ObjectModel;
using Avalonia.Media;

namespace CdpInspectorApp.Models;

public class AxNodeModel : System.ComponentModel.INotifyPropertyChanged
{
    public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(name));

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

    public string NodeId { get; }
    public string Role { get; }
    public string Name { get; }
    public bool Ignored { get; }
    public int? BackendDOMNodeId { get; }

    public string DisplayName => Ignored ? $"[Ignored] {Role}" + (string.IsNullOrEmpty(Name) ? "" : $" \"{Name}\"")
                                          : $"{Role}" + (string.IsNullOrEmpty(Name) ? "" : $" \"{Name}\"");

    public ObservableCollection<AxNodeModel> Children { get; } = new();

    public IBrush ForegroundBrush => Ignored ? Brushes.Gray : Brushes.LightGreen;

    public AxNodeModel(string nodeId, string role, string name, bool ignored, int? backendDOMNodeId)
    {
        NodeId = nodeId;
        Role = role;
        Name = name;
        Ignored = ignored;
        BackendDOMNodeId = backendDOMNodeId;
    }
}
