namespace CDP.Rdp.Security;

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using CDP.Rdp.Protocol;

/// <summary>
/// Plain TCP transport for legacy Standard RDP Security.
/// </summary>
public sealed class PlainRdpSecurityTransport : IRdpSecurityTransport
{
    private readonly Stream _baseStream;

    public PlainRdpSecurityTransport(Stream baseStream)
    {
        _baseStream = baseStream ?? throw new ArgumentNullException(nameof(baseStream));
    }

    public RdpSecurityProtocol Protocol => RdpSecurityProtocol.Rdp;
    public Stream TransportStream => _baseStream;
    public bool IsEncrypted => false;

    public Task HandshakeAsync(string targetHost, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _baseStream.Dispose();
    }

    public ValueTask DisposeAsync()
    {
        return _baseStream.DisposeAsync();
    }
}
