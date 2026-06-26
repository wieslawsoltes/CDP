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
    bool IsConnected { get; }
    string ConnectionStatus { get; }
    string ConnectedHost { get; }
    string ConnectedTargetId { get; }
    bool IsPreviewScreencastActive { get; set; }
    bool RecordFullFrames { get => false; set { } }
    byte[]? LastReconstructedFrameBytes => null;

    event EventHandler<CdpEventEventArgs>? EventReceived;

    Task<List<TargetItem>> GetTargetsAsync(string host);
    Task ConnectAsync(string host, TargetItem target);
    Task DisconnectAsync();
    Task<JsonObject> SendCommandAsync(string method, JsonObject? parameters = null);
}
