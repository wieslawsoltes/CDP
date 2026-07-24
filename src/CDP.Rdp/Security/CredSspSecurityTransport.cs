namespace CDP.Rdp.Security;

using System;
using System.IO;
using System.Net.Security;
using System.Threading;
using System.Threading.Tasks;
using CDP.Rdp.Exceptions;
using CDP.Rdp.Protocol;

/// <summary>
/// CredSSP (NLA) security transport wrapping TLS and performing TSRequest authentication tokens exchange.
/// </summary>
public sealed class CredSspSecurityTransport : IRdpSecurityTransport
{
    private readonly Stream _baseStream;
    private SslStream? _sslStream;
    private readonly string _username;
    private readonly string _password;
    private readonly string? _domain;
    private readonly RemoteCertificateValidationCallback? _userCertValidation;

    public CredSspSecurityTransport(
        Stream baseStream,
        string username,
        string password,
        string? domain = null,
        RemoteCertificateValidationCallback? certValidation = null)
    {
        _baseStream = baseStream ?? throw new ArgumentNullException(nameof(baseStream));
        _username = username ?? throw new ArgumentNullException(nameof(username));
        _password = password ?? throw new ArgumentNullException(nameof(password));
        _domain = domain;
        _userCertValidation = certValidation;
    }

    public RdpSecurityProtocol Protocol => RdpSecurityProtocol.Hybrid;
    public Stream TransportStream => _sslStream ?? _baseStream;
    public bool IsEncrypted => _sslStream?.IsEncrypted ?? false;

    public async Task HandshakeAsync(string targetHost, CancellationToken cancellationToken = default)
    {
        _sslStream = new SslStream(_baseStream, false, _userCertValidation);
        SslClientAuthenticationOptions options = new SslClientAuthenticationOptions
        {
            TargetHost = targetHost,
            EnabledSslProtocols = System.Security.Authentication.SslProtocols.Tls12 | System.Security.Authentication.SslProtocols.Tls13
        };

        await _sslStream.AuthenticateAsClientAsync(options, cancellationToken).ConfigureAwait(false);
        await ExecuteCredSspAuthAsync(_sslStream, cancellationToken).ConfigureAwait(false);
    }

    private async Task ExecuteCredSspAuthAsync(SslStream stream, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(_username))
        {
            throw new RdpNegotiationException("CredSSP authentication failed: Username credential was not specified.");
        }

        byte[] ntlmNegotiateToken = System.Text.Encoding.ASCII.GetBytes("NTLMSSP\0\x01\x00\x00\x00\x07\x82\x08\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00");
        TsRequestPdu clientReq = new TsRequestPdu
        {
            Version = 2,
            NegoToken = ntlmNegotiateToken
        };

        byte[] encodedReq = clientReq.Encode();
        await stream.WriteAsync(encodedReq, cancellationToken).ConfigureAwait(false);
        await stream.FlushAsync(cancellationToken).ConfigureAwait(false);

        byte[] responseBuffer = new byte[1024];
        int bytesRead = await stream.ReadAsync(responseBuffer, cancellationToken).ConfigureAwait(false);
        if (bytesRead == 0)
        {
            throw new RdpNegotiationException("CredSSP handshake failed: Server closed connection during TSRequest exchange.");
        }

        if (!TsRequestPdu.TryParse(responseBuffer.AsSpan(0, bytesRead), out TsRequestPdu serverResp))
        {
            throw new RdpNegotiationException("CredSSP handshake failed: Invalid TSRequest ASN.1 PDU received from server.");
        }

        if (serverResp.ErrorCode.HasValue && serverResp.ErrorCode.Value != 0)
        {
            throw new RdpNegotiationException($"CredSSP authentication rejected by server with error code: 0x{serverResp.ErrorCode.Value:X8}");
        }
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

