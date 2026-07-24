namespace CDP.Rdp.Session;

using System;
using System.Threading;
using System.Threading.Tasks;
using CDP.Rdp.Frames;
using CDP.Rdp.Input;

public interface IRdpSession : IAsyncDisposable, IDisposable
{
    RdpConnectionState State { get; }
    RdpSessionOptions Options { get; }

    event EventHandler<RdpFrameUpdateEventArgs>? FrameUpdated;
    event EventHandler<RdpConnectionStateChangedEventArgs>? StateChanged;

    Task ConnectAsync(CancellationToken cancellationToken = default);
    Task DisconnectAsync(CancellationToken cancellationToken = default);

    Task SendInputEventAsync(RdpInputEvent inputEvent, CancellationToken cancellationToken = default);
    Task SendFastPathInputEventAsync(RdpFastPathInputEvent inputEvent, CancellationToken cancellationToken = default);
}
