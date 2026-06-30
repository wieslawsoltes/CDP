using System;
using SkiaSharp;

namespace CDP.Automation.OS;

public sealed class OSWindow
{
    public required string Id { get; init; }
    public required string Title { get; init; }
    public required string ProcessName { get; init; }
    public required int ProcessId { get; init; }
    public required SKRectI Bounds { get; init; }
}
