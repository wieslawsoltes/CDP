using System.Collections.Concurrent;
using System.Threading;

namespace Chrome.DevTools.Protocol;

public class NodeMap<TNode> : INodeMap where TNode : class
{
    private readonly ConcurrentDictionary<TNode, int> _nodeToId = new();
    private readonly ConcurrentDictionary<int, TNode> _idToNode = new();
    private int _nextId = 1; // 1 is reserved for the #document node

    public int GetOrAdd(TNode node)
    {
        return _nodeToId.GetOrAdd(node, n =>
        {
            int id = Interlocked.Increment(ref _nextId);
            _idToNode[id] = n;
            return id;
        });
    }

    public TNode? GetNode(int id)
    {
        return _idToNode.TryGetValue(id, out var node) ? node : null;
    }

    public void UpdateNodeMapping(int id, TNode newNode)
    {
        if (_idToNode.TryGetValue(id, out var oldNode))
        {
            _nodeToId.TryRemove(oldNode, out _);
        }
        if (_nodeToId.TryRemove(newNode, out var existingId))
        {
            _idToNode.TryRemove(existingId, out _);
        }
        _idToNode[id] = newNode;
        _nodeToId[newNode] = id;
    }

    public bool TryGetId(TNode node, out int id)
    {
        return _nodeToId.TryGetValue(node, out id);
    }

    public void Remove(TNode node)
    {
        if (_nodeToId.TryRemove(node, out int id))
        {
            _idToNode.TryRemove(id, out _);
        }
    }

    public void Clear()
    {
        _nodeToId.Clear();
        _idToNode.Clear();
        _nextId = 1;
    }

    bool INodeMap.TryGetId(object node, out int id)
    {
        if (node is TNode tNode)
        {
            return TryGetId(tNode, out id);
        }
        id = 0;
        return false;
    }

    void INodeMap.UpdateNodeMapping(int id, object newNode)
    {
        if (newNode is TNode tNode)
        {
            UpdateNodeMapping(id, tNode);
        }
    }
}
