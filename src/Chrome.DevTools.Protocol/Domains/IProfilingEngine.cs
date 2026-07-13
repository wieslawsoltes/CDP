using System.Text.Json.Nodes;

namespace Chrome.DevTools.Protocol.Domains;

public interface IProfilingEngine
{
    string Name { get; }
    void Start();
    JsonObject Stop();
    void AddSpan(ProfileSpan span);
    bool IsRunning { get; }
}
