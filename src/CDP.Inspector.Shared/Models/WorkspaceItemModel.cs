using System.Collections.ObjectModel;
namespace CdpInspectorApp.Models;
public class WorkspaceItemModel
{
    public string Name { get; set; } = "";
    public string Path { get; set; } = "";
    public bool IsFolder { get; set; }
    public ObservableCollection<WorkspaceItemModel> Children { get; set; } = new();
}
