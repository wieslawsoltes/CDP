using System;

namespace CdpInspectorApp.Models;

public class TargetItem
{
    public string Title { get; }
    public string WebSocketUrl { get; }
    public string Id { get; }

    public TargetItem(string title, string wsUrl, string id)
    {
        Title = title;
        WebSocketUrl = wsUrl;
        Id = id;
    }

    public override string ToString() => $"{Title} ({Id.Substring(0, Math.Min(8, Id.Length))})";
}
