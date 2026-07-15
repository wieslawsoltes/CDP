using System.Collections.Concurrent;
using Microsoft.UI.Xaml;

namespace WinUI.Diagnostics.Cdp;

public class NodeMap : Chrome.DevTools.Protocol.NodeMap<DependencyObject>
{
    public DependencyObject? GetVisual(int id)
    {
        return GetNode(id);
    }

    public int? GetId(DependencyObject visual)
    {
        return TryGetId(visual, out int id) ? id : null;
    }
}
