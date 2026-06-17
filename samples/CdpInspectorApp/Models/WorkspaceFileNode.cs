using System.Collections.ObjectModel;

namespace CdpInspectorApp.Models;

public class WorkspaceFileNode
{
    public string Name { get; set; } = "";
    public string Path { get; set; } = "";
    public bool IsDirectory { get; set; }
    public ObservableCollection<WorkspaceFileNode> Children { get; } = new();
}
