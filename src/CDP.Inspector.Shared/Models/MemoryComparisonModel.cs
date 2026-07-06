namespace CdpInspectorApp.Models;

public class MemoryComparisonModel
{
    public string Type { get; set; } = "";
    public int BaselineCount { get; set; }
    public int SnapshotCount { get; set; }
    public int Delta => SnapshotCount - BaselineCount;
    public string DeltaText => Delta >= 0 ? $"+{Delta}" : Delta.ToString();
    public string DeltaBg => Delta > 0 ? "#450a0a" : (Delta < 0 ? "#143d21" : "Transparent");
    public string DeltaFg => Delta > 0 ? "#f28b82" : (Delta < 0 ? "#81c995" : "#9aa0a6");
}
