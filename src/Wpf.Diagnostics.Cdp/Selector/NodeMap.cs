using System.Windows.Media;

namespace Wpf.Diagnostics.Cdp;

public class NodeMap : Chrome.DevTools.Protocol.NodeMap<Visual>
{
    public Visual? GetVisual(int id)
    {
        return GetNode(id);
    }
}
