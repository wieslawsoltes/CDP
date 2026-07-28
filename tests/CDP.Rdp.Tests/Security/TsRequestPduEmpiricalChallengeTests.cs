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
using CDP.Rdp.Security;
using CDP.Rdp.Tests.Fixtures;
using Xunit;

public class TsRequestPduEmpiricalChallengeTests
{
    private static X509Certificate2 CreateTestCertificate()
    {
        using RSA rsa = RSA.Create(2048);
        CertificateRequest req = new CertificateRequest("CN=localhost", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using X509Certificate2 certWithKey = req.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddMinutes(10));
        return new X509Certificate2(certWithKey.Export(X509ContentType.Pkcs12), (string?)null, X509KeyStorageFlags.Exportable | X509KeyStorageFlags.MachineKeySet);
    }

    #region 1. Corrupted ASN.1 DER Tag Sequences

    [AvaloniaTheory]
    [InlineData((byte)0x31)] // SET instead of SEQUENCE
    [InlineData((byte)0x00)] // NULL tag
    [InlineData((byte)0x02)] // INTEGER tag
    [InlineData((byte)0x04)] // OCTET STRING tag
    [InlineData((byte)0x80)] // Context tag
    [InlineData((byte)0xFF)] // Invalid tag
    public void TryParse_CorruptedOuterTag_ReturnsFalse(byte invalidOuterTag)
    {
        byte[] payload = new byte[] { invalidOuterTag, 0x05, 0xA0, 0x03, 0x02, 0x01, 0x02 };
        bool result = TsRequestPdu.TryParse(payload, out TsRequestPdu _);
        Assert.False(result, $"Expected TryParse to fail for outer tag 0x{invalidOuterTag:X2}");
    }

    [AvaloniaFact]
    public void TryParse_UnknownExplicitTags_GracefullySkipped()
    {
        // Tag 0xA5 (unknown tag index 5) inside sequence with Version 2
        // Sequence header: 0x30, 0x0A
        // Element [0] Version 2: 0xA0, 0x03, 0x02, 0x01, 0x02
        // Element [5] Unknown:   0xA5, 0x03, 0x01, 0x01, 0xFF
        byte[] data = new byte[]
        {
            0x30, 0x0A,
            0xA0, 0x03, 0x02, 0x01, 0x02,
            0xA5, 0x03, 0x01, 0x01, 0xFF
        };

        bool parsed = TsRequestPdu.TryParse(data, out TsRequestPdu pdu);
        Assert.True(parsed);
        Assert.Equal(2, pdu.Version);
    }

    [AvaloniaFact]
    public void TryParse_CorruptedVersionTagInnerContent_ReturnsDefaultVersionZero()
    {
        // Tag 0xA0 with non-INTEGER inner tag (0x04 OCTET STRING instead of 0x02 INTEGER)
        byte[] data = new byte[]
        {
            0x30, 0x05,
            0xA0, 0x03, 0x04, 0x01, 0x09
        };

        bool parsed = TsRequestPdu.TryParse(data, out TsRequestPdu pdu);
        Assert.True(parsed);
        Assert.Equal(0, pdu.Version);
    }

    [AvaloniaFact]
    public void TryParse_CorruptedAuthInfoInnerTag_HandlesFallback()
    {
        // Tag 0xA2 with non-OCTET-STRING tag inside
        byte[] data = new byte[]
        {
            0x30, 0x05,
            0xA2, 0x03, 0x02, 0x01, 0x42
        };

        bool parsed = TsRequestPdu.TryParse(data, out TsRequestPdu pdu);
        Assert.True(parsed);
        Assert.NotNull(pdu.AuthInfo);
        Assert.Equal(new byte[] { 0x02, 0x01, 0x42 }, pdu.AuthInfo);
    }

    [AvaloniaFact]
    public void TryParse_CorruptedNegoDataOuterTag_ReturnsFallbackBuffer()
    {
        // Tag 0xA1 containing corrupt sequence (0x31 SET instead of 0x30 SEQUENCE)
        byte[] negoContent = new byte[] { 0x31, 0x04, 0x01, 0x02, 0x03, 0x04 };
        byte[] data = new byte[]
        {
            0x30, 0x08,
            0xA1, 0x06, 0x31, 0x04, 0x01, 0x02, 0x03, 0x04
        };

        bool parsed = TsRequestPdu.TryParse(data, out TsRequestPdu pdu);
        Assert.True(parsed);
        Assert.NotNull(pdu.NegoToken);
        Assert.Equal(negoContent, pdu.NegoToken);
    }

    [AvaloniaFact]
    public void TryParse_DuplicateTagsInSequence_OverwritesWithLastValue()
    {
        // Tag 0xA0 appears twice: first version=1, then version=2
        byte[] data = new byte[]
        {
            0x30, 0x0A,
            0xA0, 0x03, 0x02, 0x01, 0x01,
            0xA0, 0x03, 0x02, 0x01, 0x02
        };

        bool parsed = TsRequestPdu.TryParse(data, out TsRequestPdu pdu);
        Assert.True(parsed);
        Assert.Equal(2, pdu.Version);
    }

    [AvaloniaFact]
    public void TryParse_OutOfOrderTags_ParsesAllFieldsSuccessfully()
    {
        // Sequence with 0xA4 (errorCode=0), 0xA0 (version=2), 0xA2 (authInfo=0x11,0x22)
        // 0xA4 element: 0xA4, 0x03, 0x02, 0x01, 0x00 (5 bytes)
        // 0xA0 element: 0xA0, 0x03, 0x02, 0x01, 0x02 (5 bytes)
        // 0xA2 element: 0xA2, 0x04, 0x04, 0x02, 0x11, 0x22 (6 bytes)
        // Total payload = 16 bytes (0x10)
        byte[] data = new byte[]
        {
            0x30, 0x10,
            0xA4, 0x03, 0x02, 0x01, 0x00, // errorCode = 0
            0xA0, 0x03, 0x02, 0x01, 0x02, // version = 2
            0xA2, 0x04, 0x04, 0x02, 0x11, 0x22 // authInfo = [0x11, 0x22]
        };

        bool parsed = TsRequestPdu.TryParse(data, out TsRequestPdu pdu);
        Assert.True(parsed);
        Assert.Equal(2, pdu.Version);
        Assert.Equal(0, pdu.ErrorCode);
        Assert.NotNull(pdu.AuthInfo);
        Assert.Equal(new byte[] { 0x11, 0x22 }, pdu.AuthInfo);
    }

    #endregion

    #region 2. Invalid Length Bounds & Buffer Truncation

    [AvaloniaFact]
    public void TryParse_DeclaredSequenceLengthExceedsBuffer_ReturnsFalse()
    {
        // Header claims 100 bytes sequence length, but payload is only 5 bytes
        byte[] data = new byte[] { 0x30, 0x64, 0xA0, 0x03, 0x02, 0x01, 0x02 };
        bool result = TsRequestPdu.TryParse(data, out TsRequestPdu _);
        Assert.False(result);
    }

    [AvaloniaFact]
    public void TryParse_ElementLengthExceedsSequenceBoundary_ReturnsFalse()
    {
        // Sequence length is 5, but tag 0xA0 claims length 10
        byte[] data = new byte[] { 0x30, 0x05, 0xA0, 0x0A, 0x02, 0x01, 0x02 };
        bool result = TsRequestPdu.TryParse(data, out TsRequestPdu _);
        Assert.False(result);
    }

    [AvaloniaFact]
    public void TryParse_TruncatedLongFormLengthByte_ReturnsFalse()
    {
        // Sequence header 0x30 0x82 (2-byte length following), but buffer ends immediately
        byte[] data = new byte[] { 0x30, 0x82, 0x00 };
        bool result = TsRequestPdu.TryParse(data, out TsRequestPdu _);
        Assert.False(result);
    }

    [AvaloniaFact]
    public void TryParse_TruncatedThreeByteLengthHeader_ReturnsFalse()
    {
        // Sequence header 0x30 0x83 (3-byte length expected), but only 2 length bytes provided
        byte[] data = new byte[] { 0x30, 0x83, 0x00, 0x05 };
        bool result = TsRequestPdu.TryParse(data, out TsRequestPdu _);
        Assert.False(result);
    }

    [AvaloniaFact]
    public void TryParse_HugeLengthOverflow_ReturnsFalse()
    {
        // Sequence header 0x30 0x84 0x7F 0xFF 0xFF 0xFF (2GB length specification)
        byte[] data = new byte[] { 0x30, 0x84, 0x7F, 0xFF, 0xFF, 0xFF, 0x01, 0x02 };
        bool result = TsRequestPdu.TryParse(data, out TsRequestPdu _);
        Assert.False(result);
    }

    [AvaloniaFact]
    public void TryParse_IndefiniteLengthEncoding_EmpiricalBehaviorCheck()
    {
        // DER forbids indefinite length form 0x80 (X.690 Section 10.1).
        // Current implementation returns true with empty sequence because ReadLength returns 0 for 0x80.
        byte[] data = new byte[] { 0x30, 0x80, 0xA0, 0x03, 0x02, 0x01, 0x02, 0x00, 0x00 };
        bool result = TsRequestPdu.TryParse(data, out TsRequestPdu pdu);
        
        // Empirically document current behavior:
        Assert.True(result); // Note: In strict DER validation, indefinite length 0x80 is non-canonical/invalid.
    }

    [AvaloniaTheory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void TryParse_BufferShorterThanMinimumHeader_ReturnsFalse(int length)
    {
        byte[] buffer = new byte[length];
        if (length > 0) buffer[0] = 0x30;
        bool result = TsRequestPdu.TryParse(buffer, out TsRequestPdu _);
        Assert.False(result);
    }

    #endregion

    #region 3. Malformed TSRequest Error Codes & Exception Formatting

    [AvaloniaFact]
    public void TryParse_NegativeErrorCode_ParsesSuccessfully()
    {
        // Error code -1 encoded as 4-byte 0xFFFFFFFF
        // Tag 0xA4 element: 0xA4, 0x06, 0x02, 0x04, 0xFF, 0xFF, 0xFF, 0xFF (8 bytes)
        // Sequence header: 0x30, 0x08
        byte[] data = new byte[]
        {
            0x30, 0x08,
            0xA4, 0x06, 0x02, 0x04, 0xFF, 0xFF, 0xFF, 0xFF
        };

        bool parsed = TsRequestPdu.TryParse(data, out TsRequestPdu pdu);
        Assert.True(parsed);
        Assert.Equal(-1, pdu.ErrorCode);
    }

    [AvaloniaTheory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(-1)]
    [InlineData(unchecked((int)0x0000006D))]  // STATUS_LOGON_FAILURE
    [InlineData(unchecked((int)0x8009030E))]  // SEC_E_NO_CREDENTIALS
    [InlineData(unchecked((int)0x80090308))]  // SEC_E_INVALID_TOKEN
    [InlineData(int.MinValue)]
    [InlineData(int.MaxValue)]
    public void EncodeAndTryParse_ErrorCodeBoundaryValues_RoundTripsCorrectly(int errorCode)
    {
        TsRequestPdu original = new TsRequestPdu
        {
            Version = 2,
            ErrorCode = errorCode
        };

        byte[] encoded = original.Encode();
        bool parsed = TsRequestPdu.TryParse(encoded, out TsRequestPdu result);

        Assert.True(parsed);
        Assert.Equal(2, result.Version);
        Assert.Equal(errorCode, result.ErrorCode);
    }

    [AvaloniaTheory]
    [InlineData(unchecked((int)0x0000006D), "0x0000006D")]
    [InlineData(unchecked((int)0x8009030E), "0x8009030E")]
    [InlineData(unchecked((int)0x00000001), "0x00000001")]
    [InlineData(-1, "0xFFFFFFFF")]
    public async Task ExecuteCredSspAuthAsync_ServerReturnsVariousErrorCodes_ThrowsWithFormattedHex(int errorCode, string expectedHexSubString)
    {
        var ct = TestContext.Current.CancellationToken;
        using DuplexStreamPair pair = new DuplexStreamPair();

        using X509Certificate2 cert = CreateTestCertificate();

        Task serverTask = Task.Run(async () =>
        {
            using SslStream serverSsl = new SslStream(pair.ServerStream, false);
            await serverSsl.AuthenticateAsServerAsync(cert, false, SslProtocols.Tls12 | SslProtocols.Tls13, false);

            byte[] buf = new byte[1024];
            int readBytes = 0;
            while (readBytes == 0)
            {
                readBytes = await serverSsl.ReadAsync(buf.AsMemory(), ct);
            }
            Assert.True(readBytes > 0);

            TsRequestPdu serverResp = new TsRequestPdu
            {
                Version = 2,
                ErrorCode = errorCode
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

        var ex = await Assert.ThrowsAsync<RdpNegotiationException>(() => transport.HandshakeAsync("localhost", ct));
        await serverTask;

        Assert.Contains(expectedHexSubString, ex.Message);
    }

    #endregion

    #region 4. Truncated Token Payloads & Transport Handshake Robustness

    [AvaloniaFact]
    public void ParseNegoData_TruncatedInnerOctetString_ReturnsFallbackBuffer()
    {
        // NegoData structure with inner OCTET STRING specifying length 100, but only 4 bytes supplied
        byte[] negoDataPayload = new byte[]
        {
            0x30, 0x10, // NegoData sequence
            0x30, 0x0E, // NegoToken sequence
            0xA0, 0x0C, // negoToken [0] tag
            0x04, 0x64, // OCTET STRING tag with length 100
            0x01, 0x02, 0x03, 0x04
        };

        byte[] tsRequestData = new byte[negoDataPayload.Length + 4];
        tsRequestData[0] = 0x30;
        tsRequestData[1] = (byte)(negoDataPayload.Length + 2);
        tsRequestData[2] = 0xA1;
        tsRequestData[3] = (byte)negoDataPayload.Length;
        Buffer.BlockCopy(negoDataPayload, 0, tsRequestData, 4, negoDataPayload.Length);

        bool parsed = TsRequestPdu.TryParse(tsRequestData, out TsRequestPdu pdu);
        Assert.True(parsed);
        Assert.NotNull(pdu.NegoToken);
        Assert.Equal(negoDataPayload, pdu.NegoToken);
    }

    [AvaloniaFact]
    public async Task ExecuteCredSspAuthAsync_ServerSendsTruncatedTsRequest_ThrowsRdpNegotiationException()
    {
        var ct = TestContext.Current.CancellationToken;
        using DuplexStreamPair pair = new DuplexStreamPair();

        using X509Certificate2 cert = CreateTestCertificate();

        Task serverTask = Task.Run(async () =>
        {
            using SslStream serverSsl = new SslStream(pair.ServerStream, false);
            await serverSsl.AuthenticateAsServerAsync(cert, false, SslProtocols.Tls12 | SslProtocols.Tls13, false);

            byte[] buf = new byte[1024];
            int r = await serverSsl.ReadAsync(buf.AsMemory(), ct);
            Assert.True(r > 0);

            // Server writes only 2 truncated bytes
            await serverSsl.WriteAsync(new byte[] { 0x30, 0x10 }, ct);
            await serverSsl.FlushAsync(ct);
        }, ct);

        using CredSspSecurityTransport transport = new CredSspSecurityTransport(
            pair.ClientStream,
            "testuser",
            "testpass",
            certValidation: (s, c, ch, e) => true);

        var ex = await Assert.ThrowsAsync<RdpNegotiationException>(() => transport.HandshakeAsync("localhost", ct));
        await serverTask;

        Assert.Contains("Invalid TSRequest ASN.1 PDU received from server", ex.Message);
    }

    [AvaloniaFact]
    public async Task ExecuteCredSspAuthAsync_ServerSendsJunkBytes_ThrowsRdpNegotiationException()
    {
        var ct = TestContext.Current.CancellationToken;
        using DuplexStreamPair pair = new DuplexStreamPair();

        using X509Certificate2 cert = CreateTestCertificate();

        Task serverTask = Task.Run(async () =>
        {
            using SslStream serverSsl = new SslStream(pair.ServerStream, false);
            await serverSsl.AuthenticateAsServerAsync(cert, false, SslProtocols.Tls12 | SslProtocols.Tls13, false);

            byte[] buf = new byte[1024];
            int r = await serverSsl.ReadAsync(buf.AsMemory(), ct);
            Assert.True(r > 0);

            // Server writes 50 bytes of junk
            byte[] junk = new byte[50];
            Random.Shared.NextBytes(junk);
            junk[0] = 0xDE; // Not 0x30
            await serverSsl.WriteAsync(junk, ct);
            await serverSsl.FlushAsync(ct);
        }, ct);

        using CredSspSecurityTransport transport = new CredSspSecurityTransport(
            pair.ClientStream,
            "testuser",
            "testpass",
            certValidation: (s, c, ch, e) => true);

        var ex = await Assert.ThrowsAsync<RdpNegotiationException>(() => transport.HandshakeAsync("localhost", ct));
        await serverTask;

        Assert.Contains("Invalid TSRequest ASN.1 PDU received from server", ex.Message);
    }

    [AvaloniaFact]
    public async Task ExecuteCredSspAuthAsync_ServerClosesConnectionImmediately_ThrowsException()
    {
        var ct = TestContext.Current.CancellationToken;
        using DuplexStreamPair pair = new DuplexStreamPair();

        using X509Certificate2 cert = CreateTestCertificate();

        Task serverTask = Task.Run(async () =>
        {
            using SslStream serverSsl = new SslStream(pair.ServerStream, false);
            await serverSsl.AuthenticateAsServerAsync(cert, false, SslProtocols.Tls12 | SslProtocols.Tls13, false);

            byte[] buf = new byte[1024];
            int r = await serverSsl.ReadAsync(buf.AsMemory(), ct);
            Assert.True(r > 0);
            // Server closes stream abruptly without writing any response
            serverSsl.Close();
        }, ct);

        using CredSspSecurityTransport transport = new CredSspSecurityTransport(
            pair.ClientStream,
            "testuser",
            "testpass",
            certValidation: (s, c, ch, e) => true);

        // Abrupt connection close over TLS SslStream throws IOException or RdpNegotiationException
        await Assert.ThrowsAnyAsync<Exception>(() => transport.HandshakeAsync("localhost", ct));
        await serverTask;
    }

    [AvaloniaFact]
    public async Task ExecuteCredSspAuthAsync_ServerSendsValidTsRequestWithAuthInfoAndPubKeyAuth_HandshakeSucceeds()
    {
        var ct = TestContext.Current.CancellationToken;
        using DuplexStreamPair pair = new DuplexStreamPair();

        using X509Certificate2 cert = CreateTestCertificate();

        Task serverTask = Task.Run(async () =>
        {
            using SslStream serverSsl = new SslStream(pair.ServerStream, false);
            await serverSsl.AuthenticateAsServerAsync(cert, false, SslProtocols.Tls12 | SslProtocols.Tls13, false);

            byte[] buf = new byte[1024];
            int bytesRead = await serverSsl.ReadAsync(buf.AsMemory(), ct);
            Assert.True(bytesRead > 0);

            TsRequestPdu serverResp = new TsRequestPdu
            {
                Version = 2,
                NegoToken = new byte[] { 0x0A, 0x0B, 0x0C },
                AuthInfo = new byte[] { 0x01, 0x02, 0x03, 0x04 },
                PubKeyAuth = new byte[] { 0xAA, 0xBB, 0xCC, 0xDD },
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
            certValidation: (s, c, ch, e) => true);

        await transport.HandshakeAsync("localhost", ct);
        await serverTask;

        Assert.True(transport.IsEncrypted);
    }

    #endregion
}
