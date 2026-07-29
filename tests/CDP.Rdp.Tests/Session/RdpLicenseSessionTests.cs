using Avalonia.Headless.XUnit;

namespace CDP.Rdp.Tests.Session;

using System;
using System.Buffers.Binary;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using CDP.Rdp.Session;
using Xunit;

[Xunit.Collection("RdpTests")]
public sealed class RdpLicenseSessionTests
{
    [AvaloniaFact]
    public void ProcessServerPacket_ValidClientError_CompletesLicensing()
    {
        var session = new RdpLicenseSession(new RdpSessionOptions(), null);
        byte[] body = new byte[12];
        BinaryPrimitives.WriteUInt32LittleEndian(body, 7);
        BinaryPrimitives.WriteUInt32LittleEndian(body.AsSpan(4), 2);
        byte[] packet = CreatePacket(0xFF, body);

        Assert.Null(session.ProcessServerPacket(packet));
        Assert.True(session.IsComplete);
    }

    [AvaloniaFact]
    public void ProcessServerPacket_NewLicenseFlow_AnswersChallengeAndCompletes()
    {
        using RSA rsa = RSA.Create(1024);
        var session = new RdpLicenseSession(
            new RdpSessionOptions { Username = "alice" },
            null);
        byte[] serverRandom = Enumerable.Range(1, 32).Select(value => (byte)value).ToArray();

        byte[] request = CreateServerLicenseRequest(rsa, serverRandom);
        byte[] newLicenseRequest = Assert.IsType<byte[]>(session.ProcessServerPacket(request));
        Assert.Equal(0x0080, BinaryPrimitives.ReadUInt16LittleEndian(newLicenseRequest));
        Assert.Equal(0x13, newLicenseRequest[4]);

        ReadOnlySpan<byte> requestBody = newLicenseRequest.AsSpan(8);
        Assert.Equal(1u, BinaryPrimitives.ReadUInt32LittleEndian(requestBody));
        Assert.Equal(0x04010000u, BinaryPrimitives.ReadUInt32LittleEndian(requestBody.Slice(4)));
        byte[] clientRandom = requestBody.Slice(8, 32).ToArray();
        ushort encryptedLength = BinaryPrimitives.ReadUInt16LittleEndian(requestBody.Slice(42));
        Assert.Equal(rsa.KeySize / 8 + 8, encryptedLength);
        byte[] encryptedPreMaster = requestBody.Slice(44, rsa.KeySize / 8).ToArray();
        Array.Reverse(encryptedPreMaster);
        byte[] preMaster = rsa.Decrypt(encryptedPreMaster, RSAEncryptionPadding.Pkcs1);
        Assert.Equal(48, preMaster.Length);

        DeriveKeys(preMaster, clientRandom, serverRandom, out byte[] macKey, out byte[] encryptionKey);
        byte[] challenge = Encoding.ASCII.GetBytes("test-platform-challenge");
        byte[] challengeBody = CreatePlatformChallengeBody(challenge, macKey, encryptionKey);
        byte[] challengeResponse = Assert.IsType<byte[]>(
            session.ProcessServerPacket(CreatePacket(0x02, challengeBody)));
        Assert.Equal(0x15, challengeResponse[4]);

        var responseReader = new BlobReader(challengeResponse.AsSpan(8));
        byte[] responseData = Rc4(encryptionKey, responseReader.ReadBlob(0x0009));
        byte[] hardwareId = Rc4(encryptionKey, responseReader.ReadBlob(0x0009));
        byte[] responseMac = responseReader.ReadBytes(16);
        Assert.Equal(0x0100, BinaryPrimitives.ReadUInt16LittleEndian(responseData));
        Assert.Equal(0xFF00, BinaryPrimitives.ReadUInt16LittleEndian(responseData.AsSpan(2)));
        Assert.Equal(challenge, responseData.AsSpan(8).ToArray());
        Assert.Equal(0x04010000u, BinaryPrimitives.ReadUInt32LittleEndian(hardwareId));
        Assert.Equal(
            GenerateMac(macKey, Concat(responseData, hardwareId)),
            responseMac);

        byte[] issuedLicense = Encoding.ASCII.GetBytes("issued-client-access-license");
        byte[] issuedBody = CreateBlob(0x0009, Rc4(encryptionKey, issuedLicense))
            .Concat(GenerateMac(macKey, issuedLicense))
            .ToArray();
        Assert.Null(session.ProcessServerPacket(CreatePacket(0x03, issuedBody)));
        Assert.True(session.IsComplete);
    }

    [AvaloniaFact]
    public void ProcessServerPacket_ChallengeBeforeRequest_IsRejected()
    {
        var session = new RdpLicenseSession(new RdpSessionOptions(), null);
        byte[] body = new byte[24];

        Assert.Throws<InvalidDataException>(
            () => session.ProcessServerPacket(CreatePacket(0x02, body)));
    }

    private static byte[] CreateServerLicenseRequest(RSA rsa, byte[] serverRandom)
    {
        using var body = new MemoryStream();
        using var writer = new BinaryWriter(body, Encoding.UTF8, leaveOpen: true);
        writer.Write(serverRandom);
        writer.Write(0x00060000u);
        writer.Write(2u);
        writer.Write(new byte[] { (byte)'M', 0 });
        writer.Write(2u);
        writer.Write(new byte[] { (byte)'A', 0 });
        writer.Write(CreateBlob(0x000D, BitConverter.GetBytes(1u)));
        writer.Write(CreateBlob(0x0003, CreateProprietaryCertificate(rsa)));
        writer.Write(0u);
        return CreatePacket(0x01, body.ToArray());
    }

    private static byte[] CreateProprietaryCertificate(RSA rsa)
    {
        RSAParameters parameters = rsa.ExportParameters(false);
        byte[] modulus = parameters.Modulus!;
        Array.Reverse(modulus);
        uint exponent = 0;
        foreach (byte value in parameters.Exponent!)
        {
            exponent = (exponent << 8) | value;
        }

        byte[] key = new byte[20 + modulus.Length + 8];
        BinaryPrimitives.WriteUInt32LittleEndian(key, 0x31415352);
        BinaryPrimitives.WriteUInt32LittleEndian(key.AsSpan(4), checked((uint)(modulus.Length + 8)));
        BinaryPrimitives.WriteUInt32LittleEndian(key.AsSpan(8), checked((uint)(modulus.Length * 8)));
        BinaryPrimitives.WriteUInt32LittleEndian(key.AsSpan(12), checked((uint)(modulus.Length - 1)));
        BinaryPrimitives.WriteUInt32LittleEndian(key.AsSpan(16), exponent);
        modulus.CopyTo(key, 20);

        using var certificate = new MemoryStream();
        using var writer = new BinaryWriter(certificate, Encoding.UTF8, leaveOpen: true);
        writer.Write(1u);
        writer.Write(1u);
        writer.Write(1u);
        writer.Write(CreateBlob(0x0006, key));
        writer.Write(CreateBlob(0x0008, new byte[72]));
        return certificate.ToArray();
    }

    private static byte[] CreatePlatformChallengeBody(
        byte[] challenge,
        byte[] macKey,
        byte[] encryptionKey)
    {
        using var body = new MemoryStream();
        using var writer = new BinaryWriter(body, Encoding.UTF8, leaveOpen: true);
        writer.Write(0u);
        writer.Write(CreateBlob(0x0009, Rc4(encryptionKey, challenge)));
        writer.Write(GenerateMac(macKey, challenge));
        return body.ToArray();
    }

    private static byte[] CreatePacket(byte type, byte[] body)
    {
        byte[] packet = new byte[4 + body.Length];
        packet[0] = type;
        packet[1] = 3;
        BinaryPrimitives.WriteUInt16LittleEndian(packet.AsSpan(2), checked((ushort)packet.Length));
        body.CopyTo(packet, 4);
        return packet;
    }

    private static byte[] CreateBlob(ushort type, byte[] data)
    {
        byte[] blob = new byte[4 + data.Length];
        BinaryPrimitives.WriteUInt16LittleEndian(blob, type);
        BinaryPrimitives.WriteUInt16LittleEndian(blob.AsSpan(2), checked((ushort)data.Length));
        data.CopyTo(blob, 4);
        return blob;
    }

    private static void DeriveKeys(
        byte[] preMaster,
        byte[] clientRandom,
        byte[] serverRandom,
        out byte[] macKey,
        out byte[] encryptionKey)
    {
        byte[] master = Concat(
            SaltedHash(preMaster, "A"u8, clientRandom, serverRandom),
            SaltedHash(preMaster, "BB"u8, clientRandom, serverRandom),
            SaltedHash(preMaster, "CCC"u8, clientRandom, serverRandom));
        byte[] session = Concat(
            SaltedHash(master, "A"u8, serverRandom, clientRandom),
            SaltedHash(master, "BB"u8, serverRandom, clientRandom),
            SaltedHash(master, "CCC"u8, serverRandom, clientRandom));
        macKey = session.AsSpan(0, 16).ToArray();
        encryptionKey = MD5.HashData(Concat(session.AsSpan(16, 16), clientRandom, serverRandom));
    }

    private static byte[] SaltedHash(
        ReadOnlySpan<byte> secret,
        ReadOnlySpan<byte> salt,
        ReadOnlySpan<byte> firstRandom,
        ReadOnlySpan<byte> secondRandom)
    {
        byte[] sha = SHA1.HashData(Concat(salt, secret, firstRandom, secondRandom));
        return MD5.HashData(Concat(secret, sha));
    }

    private static byte[] GenerateMac(byte[] key, byte[] data)
    {
        byte[] length = new byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(length, checked((uint)data.Length));
        byte[] pad1 = Enumerable.Repeat((byte)0x36, 40).ToArray();
        byte[] pad2 = Enumerable.Repeat((byte)0x5C, 48).ToArray();
        byte[] sha = SHA1.HashData(Concat(key, pad1, length, data));
        return MD5.HashData(Concat(key, pad2, sha));
    }

    private static byte[] Rc4(byte[] key, byte[] input)
    {
        byte[] state = Enumerable.Range(0, 256).Select(value => (byte)value).ToArray();
        int j = 0;
        for (int i = 0; i < state.Length; i++)
        {
            j = (j + state[i] + key[i % key.Length]) & 0xFF;
            (state[i], state[j]) = (state[j], state[i]);
        }

        byte[] output = new byte[input.Length];
        int x = 0;
        j = 0;
        for (int i = 0; i < input.Length; i++)
        {
            x = (x + 1) & 0xFF;
            j = (j + state[x]) & 0xFF;
            (state[x], state[j]) = (state[j], state[x]);
            output[i] = (byte)(input[i] ^ state[(state[x] + state[j]) & 0xFF]);
        }
        return output;
    }

    private static byte[] Concat(params byte[][] values)
    {
        int length = values.Sum(value => value.Length);
        byte[] result = new byte[length];
        int offset = 0;
        foreach (byte[] value in values)
        {
            value.CopyTo(result, offset);
            offset += value.Length;
        }
        return result;
    }

    private static byte[] Concat(
        ReadOnlySpan<byte> first,
        ReadOnlySpan<byte> second)
    {
        byte[] result = new byte[first.Length + second.Length];
        first.CopyTo(result);
        second.CopyTo(result.AsSpan(first.Length));
        return result;
    }

    private static byte[] Concat(
        ReadOnlySpan<byte> first,
        ReadOnlySpan<byte> second,
        ReadOnlySpan<byte> third)
    {
        byte[] result = new byte[first.Length + second.Length + third.Length];
        first.CopyTo(result);
        second.CopyTo(result.AsSpan(first.Length));
        third.CopyTo(result.AsSpan(first.Length + second.Length));
        return result;
    }

    private static byte[] Concat(
        ReadOnlySpan<byte> first,
        ReadOnlySpan<byte> second,
        ReadOnlySpan<byte> third,
        ReadOnlySpan<byte> fourth)
    {
        byte[] result = new byte[first.Length + second.Length + third.Length + fourth.Length];
        first.CopyTo(result);
        second.CopyTo(result.AsSpan(first.Length));
        third.CopyTo(result.AsSpan(first.Length + second.Length));
        fourth.CopyTo(result.AsSpan(first.Length + second.Length + third.Length));
        return result;
    }

    private ref struct BlobReader
    {
        private ReadOnlySpan<byte> _source;

        public BlobReader(ReadOnlySpan<byte> source)
        {
            _source = source;
        }

        public byte[] ReadBlob(ushort expectedType)
        {
            Assert.True(_source.Length >= 4);
            Assert.Equal(expectedType, BinaryPrimitives.ReadUInt16LittleEndian(_source));
            int length = BinaryPrimitives.ReadUInt16LittleEndian(_source.Slice(2));
            _source = _source.Slice(4);
            return ReadBytes(length);
        }

        public byte[] ReadBytes(int length)
        {
            Assert.True(_source.Length >= length);
            byte[] value = _source.Slice(0, length).ToArray();
            _source = _source.Slice(length);
            return value;
        }
    }
}
