using System.Net;
using System.Net.WebSockets;
using System.Threading.Channels;

namespace Chrome.DevTools.Protocol.Inspector;

/// <summary>Metadata exposed through the Chrome DevTools discovery endpoints.</summary>
public sealed record CdpInspectorTargetInfo(
    string Id,
    string Title,
    string Url,
    string Type = "node",
    string Description = "");

/// <summary>Runtime metadata exposed by <c>/json/version</c>.</summary>
public sealed record CdpInspectorVersionInfo(
    string Browser,
    string ProtocolVersion,
    string UserAgent,
    string V8Version = "",
    string WebKitVersion = "");

/// <summary>Information about an accepted DevTools connection.</summary>
public sealed record CdpInspectorConnectionContext(
    CdpInspectorTargetInfo Target,
    EndPoint? RemoteEndPoint,
    string? Origin);

/// <summary>
/// A raw, duplex Inspector transport. Messages are complete UTF-8 JSON payloads and are never
/// interpreted by the CDP host.
/// </summary>
public interface IRawCdpTransport : IAsyncDisposable
{
    ValueTask SendAsync(ReadOnlyMemory<byte> message, CancellationToken cancellationToken);

    IAsyncEnumerable<ReadOnlyMemory<byte>> ReadAllAsync(CancellationToken cancellationToken);
}

/// <summary>
/// Backpressure-aware base class for callback-driven Inspector implementations. Native V8
/// notifications can be published from any thread without creating an unbounded queue.
/// </summary>
public abstract class RawCdpTransportBase : IRawCdpTransport
{
    private readonly Channel<ReadOnlyMemory<byte>> _outgoing;

    protected RawCdpTransportBase(int outgoingQueueCapacity = 256)
    {
        if (outgoingQueueCapacity < 1) throw new ArgumentOutOfRangeException(nameof(outgoingQueueCapacity));
        _outgoing = Channel.CreateBounded<ReadOnlyMemory<byte>>(new BoundedChannelOptions(outgoingQueueCapacity)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = false
        });
    }

    public abstract ValueTask SendAsync(ReadOnlyMemory<byte> message, CancellationToken cancellationToken);

    public IAsyncEnumerable<ReadOnlyMemory<byte>> ReadAllAsync(CancellationToken cancellationToken) =>
        _outgoing.Reader.ReadAllAsync(cancellationToken);

    protected bool TryPublish(ReadOnlySpan<byte> message) =>
        _outgoing.Writer.TryWrite(message.ToArray());

    protected ValueTask PublishAsync(ReadOnlyMemory<byte> message, CancellationToken cancellationToken = default) =>
        _outgoing.Writer.WriteAsync(message.ToArray(), cancellationToken);

    protected void Complete(Exception? error = null) => _outgoing.Writer.TryComplete(error);

    public virtual ValueTask DisposeAsync()
    {
        Complete();
        return ValueTask.CompletedTask;
    }
}

/// <summary>A discoverable runtime that can create raw Inspector transports.</summary>
public interface IRawCdpTarget
{
    CdpInspectorTargetInfo Info { get; }

    ValueTask<IRawCdpTransport> OpenTransportAsync(
        CdpInspectorConnectionContext context,
        CancellationToken cancellationToken);
}

/// <summary>Supplies the current runtime targets to the discovery host.</summary>
public interface IRawCdpTargetProvider
{
    IReadOnlyCollection<IRawCdpTarget> GetTargets();

    bool TryGetTarget(string targetId, out IRawCdpTarget? target);
}

/// <summary>Controls which WebSocket origins may connect to the Inspector host.</summary>
public interface ICdpInspectorOriginPolicy
{
    bool IsAllowed(string? origin);
}

/// <summary>Receives host and session lifecycle notifications without coupling the host to a runtime.</summary>
public interface ICdpInspectorLifecycleObserver
{
    ValueTask ServerStartedAsync(Uri discoveryUri, CancellationToken cancellationToken) => ValueTask.CompletedTask;

    ValueTask ServerStoppedAsync(CancellationToken cancellationToken) => ValueTask.CompletedTask;

    ValueTask SessionStartedAsync(CdpInspectorConnectionContext context, CancellationToken cancellationToken) => ValueTask.CompletedTask;

    ValueTask SessionStoppedAsync(CdpInspectorConnectionContext context, Exception? error, CancellationToken cancellationToken) => ValueTask.CompletedTask;
}

/// <summary>Default Chrome/DevTools-compatible origin policy.</summary>
public sealed class DevToolsOriginPolicy : ICdpInspectorOriginPolicy
{
    public bool AllowMissingOrigin { get; init; } = true;

    public IReadOnlyCollection<string> AdditionalOrigins { get; init; } = Array.Empty<string>();

    public bool IsAllowed(string? origin)
    {
        if (string.IsNullOrWhiteSpace(origin))
        {
            return AllowMissingOrigin;
        }

        if (origin.StartsWith("devtools://", StringComparison.OrdinalIgnoreCase) ||
            origin.StartsWith("chrome-devtools://", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return AdditionalOrigins.Contains(origin, StringComparer.OrdinalIgnoreCase);
    }
}

/// <summary>Thread-safe registry suitable for runtimes that add and remove Inspector targets.</summary>
public sealed class RawCdpTargetRegistry : IRawCdpTargetProvider
{
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, IRawCdpTarget> _targets =
        new(StringComparer.Ordinal);

    public IReadOnlyCollection<IRawCdpTarget> GetTargets() => _targets.Values.ToArray();

    public bool TryGetTarget(string targetId, out IRawCdpTarget? target) =>
        _targets.TryGetValue(targetId, out target);

    public void Register(IRawCdpTarget target)
    {
        ArgumentNullException.ThrowIfNull(target);
        if (string.IsNullOrWhiteSpace(target.Info.Id))
        {
            throw new ArgumentException("A target must have a non-empty id.", nameof(target));
        }

        _targets[target.Info.Id] = target;
    }

    public bool Unregister(string targetId) => _targets.TryRemove(targetId, out _);
}
