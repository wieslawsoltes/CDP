namespace CdpInspectorApp.Models;

public class MemoryComparisonModel
{
    public string Type { get; set; } = "";
    public int BaselineCount { get; set; }
    public int SnapshotCount { get; set; }
    public int Delta => SnapshotCount - BaselineCount;
    public string DeltaText => Delta >= 0 ? $"+{Delta}" : Delta.ToString();
}
