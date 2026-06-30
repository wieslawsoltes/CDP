using System;
using System.Collections.Generic;
using SkiaSharp;

namespace CDP.Automation.OS;

public sealed class OSNode
{
    public required string Id { get; set; }
    public required string Name { get; set; }
    public string Text { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public SKRectI Bounds { get; set; }
    public List<OSNode> Children { get; } = new();
    public Dictionary<string, string> Attributes { get; } = new(StringComparer.OrdinalIgnoreCase);
}
