using System.Collections.Concurrent;
using System.Threading;
using Avalonia;

namespace Avalonia.Diagnostics.Cdp;

public class NodeMap
{
    private readonly ConcurrentDictionary<Visual, int> _visualToId = new();
    private readonly ConcurrentDictionary<int, Visual> _idToVisual = new();
    private int _nextId = 1; // 1 is reserved for the #document node

    public int GetOrAdd(Visual visual)
    {
        return _visualToId.GetOrAdd(visual, v =>
        {
            int id = Interlocked.Increment(ref _nextId);
            _idToVisual[id] = v;
            return id;
        });
    }

    public Visual? GetVisual(int id)
    {
        return _idToVisual.TryGetValue(id, out var visual) ? visual : null;
    }

    public bool TryGetId(Visual visual, out int id)
    {
        return _visualToId.TryGetValue(visual, out id);
    }

    public void Remove(Visual visual)
    {
        if (_visualToId.TryRemove(visual, out int id))
        {
            _idToVisual.TryRemove(id, out _);
        }
    }

    public void Clear()
    {
        _visualToId.Clear();
        _idToVisual.Clear();
        _nextId = 1;
    }
}
