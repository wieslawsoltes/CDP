namespace Chrome.DevTools.Protocol;

public interface INodeMap
{
    bool TryGetId(object node, out int id);
    void UpdateNodeMapping(int id, object newNode);
}
