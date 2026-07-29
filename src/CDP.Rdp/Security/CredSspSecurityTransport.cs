namespace CDP.Rdp.Security;

using System;
using System.Buffers;
using System.IO;
using System.Net;
using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Security.Principal;
using System.Text;
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
        await ExecuteCredSspAuthAsync(_sslStream, targetHost, cancellationToken).ConfigureAwait(false);
    }

    private async Task ExecuteCredSspAuthAsync(SslStream stream, string targetHost, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(_username))
        {
            throw new RdpNegotiationException("CredSSP authentication failed: Username credential was not specified.");
        }

        if (stream.RemoteCertificate == null)
        {
            throw new RdpNegotiationException("CredSSP handshake failed: TLS did not provide a server certificate.");
        }

        byte[] publicKey = stream.RemoteCertificate.GetPublicKey();
        byte[] clientNonce = RandomNumberGenerator.GetBytes(32);
        var authOptions = new NegotiateAuthenticationClientOptions
        {
            Package = "Negotiate",
            Credential = new NetworkCredential(_username, _password, _domain),
            TargetName = $"TERMSRV/{targetHost}",
            RequiredProtectionLevel = ProtectionLevel.EncryptAndSign,
            AllowedImpersonationLevel = TokenImpersonationLevel.Delegation,
            RequireMutualAuthentication = false
        };

        using var authentication = new NegotiateAuthentication(authOptions);
        byte[] incomingToken = Array.Empty<byte>();
        TsRequestPdu? bindingResponse = null;

        for (int round = 0; round < 16; round++)
        {
            byte[]? outgoingToken = authentication.GetOutgoingBlob(
                incomingToken,
                out NegotiateAuthenticationStatusCode status);

            if (status is not (NegotiateAuthenticationStatusCode.ContinueNeeded or NegotiateAuthenticationStatusCode.Completed))
            {
                throw new RdpNegotiationException($"CredSSP SPNEGO authentication failed with status {status}.");
            }

            if (status == NegotiateAuthenticationStatusCode.Completed)
            {
                byte[] bindingHash = ComputeBindingHash(
                    "CredSSP Client-To-Server Binding Hash\0",
                    clientNonce,
                    publicKey);
                byte[] wrappedBinding = Wrap(authentication, bindingHash);
                await WriteRequestAsync(
                    stream,
                    new TsRequestPdu
                    {
                        Version = 6,
                        NegoToken = outgoingToken is { Length: > 0 } ? outgoingToken : null,
                        PubKeyAuth = wrappedBinding,
                        ClientNonce = clientNonce
                    },
                    cancellationToken).ConfigureAwait(false);

                bindingResponse = await ReadRequestAsync(stream, cancellationToken).ConfigureAwait(false);
                break;
            }

            if (outgoingToken is not { Length: > 0 })
            {
                throw new RdpNegotiationException("CredSSP SPNEGO requested continuation without an output token.");
            }

            await WriteRequestAsync(
                stream,
                new TsRequestPdu { Version = 6, NegoToken = outgoingToken },
                cancellationToken).ConfigureAwait(false);
            TsRequestPdu serverRequest = await ReadRequestAsync(stream, cancellationToken).ConfigureAwait(false);
            ThrowIfServerRejected(serverRequest);
            incomingToken = serverRequest.NegoToken
                ?? throw new RdpNegotiationException("CredSSP server response omitted the SPNEGO token.");
        }

        if (!authentication.IsAuthenticated || bindingResponse == null)
        {
            throw new RdpNegotiationException("CredSSP SPNEGO authentication did not complete.");
        }

        ThrowIfServerRejected(bindingResponse);
        byte[] wrappedServerBinding = bindingResponse.PubKeyAuth
            ?? throw new RdpNegotiationException("CredSSP server response omitted public-key authentication.");
        byte[] serverBinding = Unwrap(authentication, wrappedServerBinding);
        byte[] expectedServerBinding = ComputeBindingHash(
            "CredSSP Server-To-Client Binding Hash\0",
            clientNonce,
            publicKey);
        if (!CryptographicOperations.FixedTimeEquals(serverBinding, expectedServerBinding))
        {
            throw new RdpNegotiationException("CredSSP server public-key binding validation failed.");
        }

        byte[] credentials = EncodePasswordCredentials(_domain ?? string.Empty, _username, _password);
        await WriteRequestAsync(
            stream,
            new TsRequestPdu
            {
                Version = 6,
                AuthInfo = Wrap(authentication, credentials)
            },
            cancellationToken).ConfigureAwait(false);
    }

    private static void ThrowIfServerRejected(TsRequestPdu response)
    {
        if (response.ErrorCode.HasValue && response.ErrorCode.Value != 0)
        {
            throw new RdpNegotiationException(
                $"CredSSP authentication rejected by server with error code: 0x{response.ErrorCode.Value:X8}");
        }
    }

    private static byte[] Wrap(NegotiateAuthentication authentication, ReadOnlySpan<byte> value)
    {
        var writer = new ArrayBufferWriter<byte>();
        NegotiateAuthenticationStatusCode status = authentication.Wrap(value, writer, requestEncryption: true, out bool encrypted);
        if (status != NegotiateAuthenticationStatusCode.Completed || !encrypted)
        {
            throw new RdpNegotiationException($"CredSSP message encryption failed with status {status}.");
        }
        return writer.WrittenSpan.ToArray();
    }

    private static byte[] Unwrap(NegotiateAuthentication authentication, ReadOnlySpan<byte> value)
    {
        var writer = new ArrayBufferWriter<byte>();
        NegotiateAuthenticationStatusCode status = authentication.Unwrap(value, writer, out bool encrypted);
        if (status != NegotiateAuthenticationStatusCode.Completed || !encrypted)
        {
            throw new RdpNegotiationException($"CredSSP message decryption failed with status {status}.");
        }
        return writer.WrittenSpan.ToArray();
    }

    private static byte[] ComputeBindingHash(string label, ReadOnlySpan<byte> nonce, ReadOnlySpan<byte> publicKey)
    {
        byte[] labelBytes = Encoding.ASCII.GetBytes(label);
        byte[] input = new byte[labelBytes.Length + nonce.Length + publicKey.Length];
        labelBytes.CopyTo(input, 0);
        nonce.CopyTo(input.AsSpan(labelBytes.Length));
        publicKey.CopyTo(input.AsSpan(labelBytes.Length + nonce.Length));
        return SHA256.HashData(input);
    }

    private static async Task WriteRequestAsync(
        Stream stream,
        TsRequestPdu request,
        CancellationToken cancellationToken)
    {
        byte[] encoded = request.Encode();
        await stream.WriteAsync(encoded, cancellationToken).ConfigureAwait(false);
        await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task<TsRequestPdu> ReadRequestAsync(Stream stream, CancellationToken cancellationToken)
    {
        try
        {
            return await ReadRequestCoreAsync(stream, cancellationToken).ConfigureAwait(false);
        }
        catch (RdpNegotiationException)
        {
            throw;
        }
        catch (Exception ex) when (ex is IOException or EndOfStreamException or OverflowException)
        {
            throw new RdpNegotiationException(
                "CredSSP server returned an invalid or truncated TSRequest.",
                ex);
        }
    }

    private static async Task<TsRequestPdu> ReadRequestCoreAsync(Stream stream, CancellationToken cancellationToken)
    {
        byte[] prefix = new byte[2];
        await ReadExactAsync(stream, prefix, cancellationToken).ConfigureAwait(false);
        if (prefix[0] != 0x30)
        {
            throw new RdpNegotiationException("CredSSP server returned an invalid ASN.1 sequence.");
        }

        int length;
        byte[] lengthBytes;
        if (prefix[1] < 0x80)
        {
            length = prefix[1];
            lengthBytes = Array.Empty<byte>();
        }
        else
        {
            int lengthByteCount = prefix[1] & 0x7F;
            if (lengthByteCount is <= 0 or > 4)
            {
                throw new RdpNegotiationException("CredSSP server returned an invalid ASN.1 length.");
            }

            lengthBytes = new byte[lengthByteCount];
            await ReadExactAsync(stream, lengthBytes, cancellationToken).ConfigureAwait(false);
            length = 0;
            for (int i = 0; i < lengthBytes.Length; i++)
            {
                length = checked((length << 8) | lengthBytes[i]);
            }
        }

        if (length is <= 0 or > 1024 * 1024)
        {
            throw new RdpNegotiationException("CredSSP server response exceeds the allowed size.");
        }

        byte[] payload = new byte[length];
        await ReadExactAsync(stream, payload, cancellationToken).ConfigureAwait(false);
        byte[] encoded = new byte[prefix.Length + lengthBytes.Length + payload.Length];
        prefix.CopyTo(encoded, 0);
        lengthBytes.CopyTo(encoded, prefix.Length);
        payload.CopyTo(encoded, prefix.Length + lengthBytes.Length);
        if (!TsRequestPdu.TryParse(encoded, out TsRequestPdu response))
        {
            throw new RdpNegotiationException("CredSSP server returned an invalid TSRequest.");
        }
        return response;
    }

    private static async Task ReadExactAsync(Stream stream, Memory<byte> destination, CancellationToken cancellationToken)
    {
        int readTotal = 0;
        while (readTotal < destination.Length)
        {
            int read = await stream.ReadAsync(destination[readTotal..], cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                throw new EndOfStreamException("CredSSP server closed the transport during authentication.");
            }
            readTotal += read;
        }
    }

    private static byte[] EncodePasswordCredentials(string domain, string username, string password)
    {
        byte[] passwordCredentials = EncodeSequence(
            EncodeExplicitOctetString(0, Encoding.Unicode.GetBytes(domain)),
            EncodeExplicitOctetString(1, Encoding.Unicode.GetBytes(username)),
            EncodeExplicitOctetString(2, Encoding.Unicode.GetBytes(password)));

        return EncodeSequence(
            EncodeExplicitInteger(0, 1),
            EncodeExplicitOctetString(1, passwordCredentials));
    }

    private static byte[] EncodeExplicitInteger(byte index, int value)
    {
        return EncodeTagged((byte)(0xA0 | index), new byte[] { 0x02, 0x01, checked((byte)value) });
    }

    private static byte[] EncodeExplicitOctetString(byte index, byte[] value)
    {
        return EncodeTagged((byte)(0xA0 | index), EncodeTagged(0x04, value));
    }

    private static byte[] EncodeSequence(params byte[][] values)
    {
        int length = 0;
        foreach (byte[] value in values)
            length = checked(length + value.Length);
        byte[] payload = new byte[length];
        int offset = 0;
        foreach (byte[] value in values)
        {
            value.CopyTo(payload, offset);
            offset += value.Length;
        }
        return EncodeTagged(0x30, payload);
    }

    private static byte[] EncodeTagged(byte tag, byte[] value)
    {
        byte[] length = value.Length < 0x80
            ? new byte[] { (byte)value.Length }
            : value.Length <= byte.MaxValue
                ? new byte[] { 0x81, (byte)value.Length }
                : new byte[] { 0x82, (byte)(value.Length >> 8), (byte)value.Length };
        byte[] result = new byte[1 + length.Length + value.Length];
        result[0] = tag;
        length.CopyTo(result, 1);
        value.CopyTo(result, 1 + length.Length);
        return result;
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
