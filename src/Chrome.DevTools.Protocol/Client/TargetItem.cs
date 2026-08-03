using System;

namespace Chrome.DevTools.Protocol;

public class TargetItem
{
    public string Title { get; }
    public string WebSocketUrl { get; }
    public string Id { get; }
    public string Type { get; }
    public string Url { get; }
    public string Description { get; }

    public TargetItem(
        string title,
        string wsUrl,
        string id,
        string type = "page",
        string url = "",
        string description = "")
    {
        Title = title;
        WebSocketUrl = wsUrl;
        Id = id;
        Type = type;
        Url = url;
        Description = description;
    }

    public override string ToString() => $"{Title} [{Type}] ({Id.Substring(0, Math.Min(8, Id.Length))})";
}
