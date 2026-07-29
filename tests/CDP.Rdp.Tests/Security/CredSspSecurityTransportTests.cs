using Avalonia.Headless.XUnit;
namespace CDP.Rdp.Tests.Security;

using System;
using System.IO;
using System.Linq;
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

    [AvaloniaTheory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void CredSspBinding_LegacyVersions_UseModifiedPublicKeyProof(int version)
    {
        byte[] nonce = Enumerable.Repeat((byte)0x44, 32).ToArray();
        byte[] publicKey = [0x10, 0x20, 0x30, 0x40];

        Assert.Equal(
            publicKey,
            CredSspSecurityTransport.CreateClientBinding(version, nonce, publicKey));
        Assert.Equal(
            new byte[] { 0x11, 0x20, 0x30, 0x40 },
            CredSspSecurityTransport.CreateExpectedServerBinding(version, nonce, publicKey));
    }

    [AvaloniaTheory]
    [InlineData(5)]
    [InlineData(6)]
    public void CredSspBinding_ModernVersions_UseNonceBoundHashes(int version)
    {
        byte[] nonce = Enumerable.Repeat((byte)0x44, 32).ToArray();
        byte[] publicKey = [0x10, 0x20, 0x30, 0x40];

        byte[] clientBinding = CredSspSecurityTransport.CreateClientBinding(version, nonce, publicKey);
        byte[] serverBinding = CredSspSecurityTransport.CreateExpectedServerBinding(version, nonce, publicKey);

        Assert.Equal(32, clientBinding.Length);
        Assert.Equal(32, serverBinding.Length);
        Assert.NotEqual(clientBinding, serverBinding);
        Assert.NotEqual(publicKey, clientBinding);
    }

    [AvaloniaFact]
    public void CredSspVersion_RejectsVersionsBelowTwoAndCapsFutureVersions()
    {
        Assert.Throws<RdpNegotiationException>(() => CredSspSecurityTransport.NegotiateVersion(1));
        Assert.Equal(6, CredSspSecurityTransport.NegotiateVersion(99));
    }

    [AvaloniaFact]
    public void ContinuationResponse_EchoedClientSpnegoToken_IsRejected()
    {
        byte[] clientToken = [0x01, 0x02, 0x03, 0x04];
        TsRequestPdu serverResponse = new TsRequestPdu
        {
            Version = 2,
            NegoToken = clientToken,
            ErrorCode = 0
        };

        RdpNegotiationException exception = Assert.Throws<RdpNegotiationException>(
            () => CredSspSecurityTransport.ValidateContinuationResponse(serverResponse, clientToken));

        Assert.Contains("SPNEGO authentication failed", exception.Message);
    }

    [AvaloniaFact]
    public void ServerErrorResponse_ThrowsRdpNegotiationException()
    {
        TsRequestPdu serverResponse = new TsRequestPdu
        {
            Version = 2,
            ErrorCode = 0x6D // Logon failure
        };

        RdpNegotiationException ex = Assert.Throws<RdpNegotiationException>(
            () => CredSspSecurityTransport.ThrowIfServerRejected(serverResponse));

        Assert.Contains("0x0000006D", ex.Message);
    }

    [AvaloniaFact]
    public async Task HandshakeAsync_EmptyUsername_ThrowsRdpNegotiationException()
    {
        var ct = TestContext.Current.CancellationToken;
        using MemoryStream stream = new MemoryStream();

        using CredSspSecurityTransport transport = new CredSspSecurityTransport(
            stream,
            "",
            "testpass",
            "DOMAIN");

        var ex = await Assert.ThrowsAsync<RdpNegotiationException>(() => transport.HandshakeAsync("localhost", ct));

        Assert.Contains("Username credential was not specified", ex.Message);
    }
}
