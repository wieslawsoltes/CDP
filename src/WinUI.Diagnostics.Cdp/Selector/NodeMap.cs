using System.Collections.Concurrent;
using Microsoft.UI.Xaml;

namespace WinUI.Diagnostics.Cdp;

public class NodeMap
{
    private readonly ConcurrentDictionary<int, DependencyObject> _idToVisual = new();
    private readonly ConcurrentDictionary<DependencyObject, int> _visualToId = new();
    private int _nextId = 1;

    public int GetOrAdd(DependencyObject visual)
    {
        return _visualToId.GetOrAdd(visual, v =>
        {
            int id = _nextId++;
            _idToVisual[id] = v;
            return id;
        });
    }

    public DependencyObject? GetVisual(int id)
    {
        return _idToVisual.TryGetValue(id, out var visual) ? visual : null;
    }

    public int? GetId(DependencyObject visual)
    {
        return _visualToId.TryGetValue(visual, out int id) ? id : null;
    }

    public void Clear()
    {
        _idToVisual.Clear();
        _visualToId.Clear();
        _nextId = 1;
    }
}
