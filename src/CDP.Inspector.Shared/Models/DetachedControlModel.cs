namespace CdpInspectorApp.Models;

public class DetachedControlModel
{
    public string Id { get; set; } = "";
    public string Type { get; set; } = "";
    public string Name { get; set; } = "";
    public int HashCode { get; set; }
    public long DetachedDurationMs { get; set; }
    public bool HasDataContext { get; set; }
    public string DataContextType { get; set; } = "";
}
