namespace CDP.Rdp.Security;

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using CDP.Rdp.Protocol;

/// <summary>
/// Abstract security transport wrapping stream IO after protocol negotiation.
/// </summary>
public interface IRdpSecurityTransport : IAsyncDisposable, IDisposable
{
    RdpSecurityProtocol Protocol { get; }
    Stream TransportStream { get; }
    bool IsEncrypted { get; }

    Task HandshakeAsync(string targetHost, CancellationToken cancellationToken = default);
}
