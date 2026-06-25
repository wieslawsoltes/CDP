using Avalonia;

namespace Avalonia.Diagnostics.Cdp;

public class NodeMap : Chrome.DevTools.Protocol.NodeMap<Visual>
{
    public Visual? GetVisual(int id)
    {
        return GetNode(id);
    }
}
