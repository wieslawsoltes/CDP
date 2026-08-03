using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace Chrome.DevTools.Protocol;

public class CdpEventEventArgs : EventArgs
{
    public string Method { get; }
    public JsonObject Params { get; }

    public CdpEventEventArgs(string method, JsonObject @params)
    {
        Method = method;
        Params = @params;
    }
}

public interface ICdpService : INotifyPropertyChanged
{
    ITimeMachineService TimeMachine => new TimeMachineService();
    bool IsConnected { get; }
    string ConnectionStatus { get; }
    string ConnectedHost { get; }
    string ConnectedTargetId { get; }
    string ConnectedTargetType => "";
    string ConnectedTargetUrl => "";
    IReadOnlySet<string> SupportedDomains => new HashSet<string>();
    bool SupportsDomain(string domain) => SupportedDomains.Count == 0 || SupportedDomains.Contains(domain);
    bool IsPreviewScreencastActive { get; set; }
    bool RecordFullFrames { get => false; set { } }
    byte[]? LastReconstructedFrameBytes => null;
    ScreencastReconstructor ScreencastReconstructor => null!;

    event EventHandler<CdpEventEventArgs>? EventReceived;

    Task<List<TargetItem>> GetTargetsAsync(string host);
    Task ConnectAsync(string host, TargetItem target);
    Task ConnectAsync(string host, TargetItem target, bool autoResume) => ConnectAsync(host, target);
    Task DisconnectAsync();
    Task<JsonObject> SendCommandAsync(string method, JsonObject? parameters = null);
}
