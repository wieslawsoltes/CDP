using Avalonia.Headless.XUnit;
namespace CDP.Rdp.Tests.Security;

using System;
using System.IO;
using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using CDP.Rdp.Exceptions;
using CDP.Rdp.Protocol;
using CDP.Rdp.Security;
using CDP.Rdp.Tests.Fixtures;

[Xunit.Collection("RdpTests")]
public class CredSspSecurityTransportTests
{
    [AvaloniaFact]
    public void Properties_BeforeHandshake_ReflectsExpectedValues()
    {
        using DuplexStreamPair pair = new DuplexStreamPair();
        using CredSspSecurityTransport transport = new CredSspSecurityTransport(pair.ClientStream, "user", "pass", "DOMAIN");

        Assert.Equal(RdpSecurityProtocol.Hybrid, transport.Protocol);
        Assert.False(transport.IsEncrypted);
    }

    [AvaloniaFact]
    public void TsRequestPdu_EncodeAndTryParse_RoundTripsCorrectly()
    {
        byte[] token = new byte[] { 0x01, 0x02, 0x03, 0x04, 0x05 };
        TsRequestPdu original = new TsRequestPdu
        {
            Version = 2,
            NegoToken = token,
            ErrorCode = 0
        };

        byte[] encoded = original.Encode();
        Assert.NotNull(encoded);
        Assert.True(encoded.Length > 0);

        bool parsed = TsRequestPdu.TryParse(encoded, out TsRequestPdu result);
        Assert.True(parsed);
        Assert.Equal(2, result.Version);
        Assert.NotNull(result.NegoToken);
        Assert.Equal(token, result.NegoToken);
        Assert.Equal(0, result.ErrorCode);
    }

    [AvaloniaFact]
    public void TsRequestPdu_TryParse_InvalidData_ReturnsFalse()
    {
        byte[] invalidData = new byte[] { 0x12, 0x34, 0x56, 0x78 };
        bool parsed = TsRequestPdu.TryParse(invalidData, out TsRequestPdu _);
        Assert.False(parsed);
    }

    private static X509Certificate2 CreateTestCertificate()
    {
        using RSA rsa = RSA.Create(2048);
        CertificateRequest req = new CertificateRequest("CN=localhost", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using X509Certificate2 certWithKey = req.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddMinutes(10));
        return X509CertificateLoader.LoadPkcs12(certWithKey.Export(X509ContentType.Pkcs12), null, X509KeyStorageFlags.Exportable | X509KeyStorageFlags.MachineKeySet);
    }

    [AvaloniaFact]
    public async Task HandshakeAsync_EchoedClientSpnegoToken_IsRejected()
    {
        var ct = TestContext.Current.CancellationToken;
        using DuplexStreamPair pair = new DuplexStreamPair();
        using X509Certificate2 cert = CreateTestCertificate();

        Task serverTask = Task.Run(async () =>
        {
            using SslStream serverSsl = new SslStream(pair.ServerStream, false);
            await serverSsl.AuthenticateAsServerAsync(cert, false, SslProtocols.Tls12 | SslProtocols.Tls13, false);

            byte[] buf = new byte[1024];
            int bytesRead = await serverSsl.ReadAsync(buf, ct);
            Assert.True(bytesRead > 0);

            Assert.True(TsRequestPdu.TryParse(buf.AsSpan(0, bytesRead), out TsRequestPdu clientReq));
            Assert.NotNull(clientReq.NegoToken);

            TsRequestPdu serverResp = new TsRequestPdu
            {
                Version = 2,
                NegoToken = clientReq.NegoToken,
                ErrorCode = 0
            };
            byte[] respData = serverResp.Encode();
            await serverSsl.WriteAsync(respData, ct);
            await serverSsl.FlushAsync(ct);
        }, ct);

        using CredSspSecurityTransport transport = new CredSspSecurityTransport(
            pair.ClientStream,
            "testuser",
            "testpass",
            "DOMAIN",
            certValidation: (s, c, ch, e) => true);

        var exception = await Assert.ThrowsAsync<RdpNegotiationException>(
            () => transport.HandshakeAsync("localhost", ct));
        await serverTask;

        Assert.Contains("SPNEGO authentication failed", exception.Message);
    }

    [AvaloniaFact]
    public async Task HandshakeAsync_ServerReturnsErrorCode_ThrowsRdpNegotiationException()
    {
        var ct = TestContext.Current.CancellationToken;
        using DuplexStreamPair pair = new DuplexStreamPair();
        using X509Certificate2 cert = CreateTestCertificate();

        Task serverTask = Task.Run(async () =>
        {
            using SslStream serverSsl = new SslStream(pair.ServerStream, false);
            await serverSsl.AuthenticateAsServerAsync(cert, false, SslProtocols.Tls12 | SslProtocols.Tls13, false);

            byte[] buf = new byte[1024];
            int bytesRead = await serverSsl.ReadAsync(buf, ct);

            TsRequestPdu serverResp = new TsRequestPdu
            {
                Version = 2,
                ErrorCode = 0x6D // Logon failure
            };
            await serverSsl.WriteAsync(serverResp.Encode(), ct);
            await serverSsl.FlushAsync(ct);
        }, ct);

        using CredSspSecurityTransport transport = new CredSspSecurityTransport(
            pair.ClientStream,
            "testuser",
            "testpass",
            "DOMAIN",
            certValidation: (s, c, ch, e) => true);

        var ex = await Assert.ThrowsAsync<RdpNegotiationException>(() => transport.HandshakeAsync("localhost", ct));
        await serverTask;

        Assert.Contains("0x0000006D", ex.Message);
    }

    [AvaloniaFact]
    public async Task HandshakeAsync_EmptyUsername_ThrowsRdpNegotiationException()
    {
        var ct = TestContext.Current.CancellationToken;
        using DuplexStreamPair pair = new DuplexStreamPair();
        using X509Certificate2 cert = CreateTestCertificate();

        Task serverTask = Task.Run(async () =>
        {
            try
            {
                using SslStream serverSsl = new SslStream(pair.ServerStream, false);
                await serverSsl.AuthenticateAsServerAsync(cert, false, SslProtocols.Tls12 | SslProtocols.Tls13, false);
            }
            catch { }
        }, ct);

        using CredSspSecurityTransport transport = new CredSspSecurityTransport(
            pair.ClientStream,
            "",
            "testpass",
            "DOMAIN",
            certValidation: (s, c, ch, e) => true);

        var ex = await Assert.ThrowsAsync<RdpNegotiationException>(() => transport.HandshakeAsync("localhost", ct));
        pair.Dispose();
        try { await serverTask; } catch { }

        Assert.Contains("Username credential was not specified", ex.Message);
    }
}
