#nullable enable

using System;
using System.Text.Json.Nodes;

namespace Chrome.DevTools.Protocol;

/// <summary>
/// Represents a single recorded Chrome DevTools Protocol event or command response.
/// </summary>
public class TimeMachineFrame
{
    public int Index { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string Domain { get; set; } = "";
    public string Type { get; set; } = ""; // "Event" or "Response"
    public string Method { get; set; } = "";
    public JsonObject? Params { get; set; }
    public JsonObject? Payload { get; set; }
}
