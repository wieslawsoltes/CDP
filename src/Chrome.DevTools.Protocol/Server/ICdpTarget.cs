using System;

namespace Chrome.DevTools.Protocol;

public interface ICdpTarget
{
    string Id { get; }
    string Title { get; }
    string Type { get; }
    string Url { get; }
    void Activate();
    void Close();
}
