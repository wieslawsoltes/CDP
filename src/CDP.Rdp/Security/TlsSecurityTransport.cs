namespace CDP.Rdp.Security;

using System;
using System.IO;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using CDP.Rdp.Protocol;

/// <summary>
/// TLS security transport wrapper for SSL/TLS protocol.
/// </summary>
public sealed class TlsSecurityTransport : IRdpSecurityTransport
{
    private readonly Stream _baseStream;
    private SslStream? _sslStream;
    private readonly RemoteCertificateValidationCallback? _userCertValidation;

    public TlsSecurityTransport(Stream baseStream, RemoteCertificateValidationCallback? certValidation = null)
    {
        _baseStream = baseStream ?? throw new ArgumentNullException(nameof(baseStream));
        _userCertValidation = certValidation;
    }

    public RdpSecurityProtocol Protocol => RdpSecurityProtocol.Ssl;
    public Stream TransportStream => _sslStream ?? _baseStream;
    public bool IsEncrypted => _sslStream?.IsEncrypted ?? false;
    public X509Certificate2? RemoteCertificate =>
        _sslStream?.RemoteCertificate is { } certificate ? new X509Certificate2(certificate) : null;

    public async Task HandshakeAsync(string targetHost, CancellationToken cancellationToken = default)
    {
        _sslStream = new SslStream(_baseStream, false, _userCertValidation);

        SslClientAuthenticationOptions options = new SslClientAuthenticationOptions
        {
            TargetHost = targetHost,
            EnabledSslProtocols = System.Security.Authentication.SslProtocols.Tls12 | System.Security.Authentication.SslProtocols.Tls13
        };

        await _sslStream.AuthenticateAsClientAsync(options, cancellationToken).ConfigureAwait(false);
    }

    public void Dispose()
    {
        _sslStream?.Dispose();
        _baseStream.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        if (_sslStream != null)
        {
            await _sslStream.DisposeAsync().ConfigureAwait(false);
        }
        else
        {
            await _baseStream.DisposeAsync().ConfigureAwait(false);
        }
    }
}
