using System.Collections.ObjectModel;
using Avalonia.Media;

namespace CdpInspectorApp.Models;

public class DomNodeModel : System.ComponentModel.INotifyPropertyChanged
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

    public int NodeId { get; }
    public string NodeName { get; }
    public string DisplayName { get; set; } = "";
    public ObservableCollection<DomNodeModel> Children { get; } = new();
    public ObservableCollection<AttributeModel> AttributesList { get; } = new();

    public IBrush ForegroundBrush => NodeName.StartsWith("#") ? Brushes.Gray : Brushes.CornflowerBlue;

    public DomNodeModel(int nodeId, string nodeName)
    {
        NodeId = nodeId;
        NodeName = nodeName;
    }
}
