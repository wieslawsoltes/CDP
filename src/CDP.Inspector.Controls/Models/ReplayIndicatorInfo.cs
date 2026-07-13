using System.Text.Json.Nodes;

namespace CdpInspectorApp.Models;

public enum ReplayIndicatorStatus
{
    Running,
    Passed,
    Failed
}

public class ReplayIndicatorInfo
{
    public string Action { get; set; } = "";
    public string Selector { get; set; } = "";
    public string Value { get; set; } = "";
    public ReplayIndicatorStatus Status { get; set; } = ReplayIndicatorStatus.Running;
    public string? ErrorMessage { get; set; }
    
    // Coordinates (for gestures: tapOn, doubleTapOn, longPressOn, swipe, scroll, dragAndDrop)
    public double? X { get; set; }
    public double? Y { get; set; }
    public double? EndX { get; set; }
    public double? EndY { get; set; }
    
    // Target element bounds (box model from DOM.getBoxModel)
    public JsonObject? BoxModel { get; set; }
}
