namespace CDP.Rdp.Session;

using System;
using System.Buffers.Binary;
using System.IO;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;

/// <summary>
/// Implements the client side of the MS-RDPELE new-license flow used during
/// post-security RDP activation.
/// </summary>
internal sealed class RdpLicenseSession
{
    private const byte ServerLicenseRequest = 0x01;
    private const byte ServerPlatformChallenge = 0x02;
    private const byte ServerNewLicense = 0x03;
    private const byte ServerUpgradeLicense = 0x04;
    private const byte ClientNewLicenseRequest = 0x13;
    private const byte ClientPlatformChallengeResponse = 0x15;
    private const byte LicenseErrorAlert = 0xFF;
    private const ushort LicensePacketSecurityFlag = 0x0080;
    private const uint KeyExchangeAlgorithmRsa = 1;
    private const uint PlatformId = 0x04010000;
    private const int MaximumLicensePacketLength = 65_535;

    private readonly string _username;
    private readonly string _machineName;
    private readonly X509Certificate2? _transportCertificate;
    private byte[]? _clientRandom;
    private byte[]? _serverRandom;
    private byte[]? _preMasterSecret;
    private byte[]? _macSaltKey;
    private byte[]? _licensingEncryptionKey;
    private LicenseState _state;

    public RdpLicenseSession(
        RdpSessionOptions options,
        X509Certificate2? transportCertificate)
    {
        _username = string.IsNullOrWhiteSpace(options.Username) ? "username" : options.Username;
        _machineName = string.IsNullOrWhiteSpace(Environment.MachineName) ? "rdp-client" : Environment.MachineName;
        _transportCertificate = transportCertificate;
    }

    public bool IsComplete => _state == LicenseState.Complete;

    /// <summary>
    /// Processes a server licensing packet, returning a complete security
    /// payload to send on the MCS global channel when a response is required.
    /// </summary>
    public byte[]? ProcessServerPacket(ReadOnlySpan<byte> packet)
    {
        LicensePreamble preamble = ReadPreamble(packet);
        ReadOnlySpan<byte> body = packet.Slice(4, preamble.MessageSize - 4);

        return preamble.MessageType switch
        {
            ServerLicenseRequest => ProcessLicenseRequest(body),
            ServerPlatformChallenge => ProcessPlatformChallenge(body),
            ServerNewLicense or ServerUpgradeLicense => ProcessIssuedLicense(body),
            LicenseErrorAlert => ProcessErrorAlert(body),
            _ => throw new InvalidDataException(
                $"The server returned unsupported RDP licensing message 0x{preamble.MessageType:X2}.")
        };
    }

    private byte[] ProcessLicenseRequest(ReadOnlySpan<byte> body)
    {
        if (_state is not LicenseState.Initial)
        {
            throw new InvalidDataException("The server sent an out-of-sequence RDP license request.");
        }

        var reader = new LicenseReader(body);
        _serverRandom = reader.ReadBytes(32, "server random");

        _ = reader.ReadUInt32("product version");
        int companyNameLength = reader.ReadLength("company name");
        reader.Skip(companyNameLength, "company name");
        int productIdLength = reader.ReadLength("product id");
        reader.Skip(productIdLength, "product id");

        LicenseBlob keyExchange = reader.ReadBlob("key-exchange list");
        if (keyExchange.Type != 0x000D ||
            keyExchange.Data.Length < sizeof(uint) ||
            BinaryPrimitives.ReadUInt32LittleEndian(keyExchange.Data) != KeyExchangeAlgorithmRsa)
        {
            throw new InvalidDataException("The server did not offer the required RSA licensing key exchange.");
        }

        LicenseBlob certificateBlob = reader.ReadBlob("server certificate");
        if (certificateBlob.Type != 0x0003)
        {
            throw new InvalidDataException("The server license request contains an invalid certificate BLOB.");
        }

        int scopeCount = reader.ReadLength("scope count");
        if (scopeCount > 1024)
        {
            throw new InvalidDataException("The server license request contains too many scopes.");
        }
        for (int i = 0; i < scopeCount; i++)
        {
            _ = reader.ReadBlob("scope");
        }
        reader.EnsureConsumed("server license request");

        using RSA rsa = LoadServerPublicKey(certificateBlob.Data);
        _clientRandom = RandomNumberGenerator.GetBytes(32);
        _preMasterSecret = RandomNumberGenerator.GetBytes(48);
        DeriveKeys(_preMasterSecret, _clientRandom, _serverRandom, out _macSaltKey, out _licensingEncryptionKey);

        byte[] encryptedPreMaster = rsa.Encrypt(_preMasterSecret, RSAEncryptionPadding.Pkcs1);
        Array.Reverse(encryptedPreMaster);
        int modulusLength = rsa.KeySize / 8;
        if (encryptedPreMaster.Length != modulusLength)
        {
            throw new CryptographicException("The encrypted licensing premaster secret has an invalid length.");
        }

        using var payload = new MemoryStream(256);
        using var writer = new BinaryWriter(payload, Encoding.UTF8, leaveOpen: true);
        writer.Write(KeyExchangeAlgorithmRsa);
        writer.Write(PlatformId);
        writer.Write(_clientRandom);
        WriteBlob(writer, 0x0002, encryptedPreMaster, trailingPadding: 8);
        WriteBlob(writer, 0x000F, EncodeAnsiZ(_username));
        WriteBlob(writer, 0x0010, EncodeAnsiZ(_machineName));

        _state = LicenseState.NewLicenseRequested;
        return CreateLicenseSecurityPayload(ClientNewLicenseRequest, payload.ToArray());
    }

    private byte[] ProcessPlatformChallenge(ReadOnlySpan<byte> body)
    {
        EnsureKeys(LicenseState.NewLicenseRequested);
        var reader = new LicenseReader(body);
        _ = reader.ReadUInt32("platform-challenge connect flags");
        LicenseBlob encryptedChallenge = reader.ReadBlob("encrypted platform challenge");
        if (encryptedChallenge.Type is not (0x0001 or 0x0009))
        {
            throw new InvalidDataException("The server platform challenge contains an invalid encrypted BLOB.");
        }
        byte[] expectedMac = reader.ReadBytes(16, "platform-challenge MAC");
        reader.EnsureConsumed("server platform challenge");

        byte[] challenge = Rc4Transform(_licensingEncryptionKey!, encryptedChallenge.Data);
        byte[] actualMac = GenerateMac(_macSaltKey!, challenge);
        if (!CryptographicOperations.FixedTimeEquals(actualMac, expectedMac))
        {
            throw new InvalidDataException("The server platform challenge failed MAC validation.");
        }

        byte[] hardwareId = CreateHardwareId(_machineName);
        byte[] responseData = new byte[8 + challenge.Length];
        BinaryPrimitives.WriteUInt16LittleEndian(responseData, 0x0100);
        BinaryPrimitives.WriteUInt16LittleEndian(responseData.AsSpan(2), 0xFF00);
        BinaryPrimitives.WriteUInt16LittleEndian(responseData.AsSpan(4), 0x0003);
        BinaryPrimitives.WriteUInt16LittleEndian(
            responseData.AsSpan(6),
            checked((ushort)challenge.Length));
        challenge.CopyTo(responseData, 8);

        byte[] macInput = new byte[responseData.Length + hardwareId.Length];
        responseData.CopyTo(macInput, 0);
        hardwareId.CopyTo(macInput, responseData.Length);

        using var payload = new MemoryStream(128);
        using var writer = new BinaryWriter(payload, Encoding.UTF8, leaveOpen: true);
        WriteBlob(writer, 0x0009, Rc4Transform(_licensingEncryptionKey!, responseData));
        WriteBlob(writer, 0x0009, Rc4Transform(_licensingEncryptionKey!, hardwareId));
        writer.Write(GenerateMac(_macSaltKey!, macInput));

        _state = LicenseState.ChallengeAnswered;
        return CreateLicenseSecurityPayload(ClientPlatformChallengeResponse, payload.ToArray());
    }

    private byte[]? ProcessIssuedLicense(ReadOnlySpan<byte> body)
    {
        EnsureKeys(LicenseState.ChallengeAnswered);
        var reader = new LicenseReader(body);
        LicenseBlob encryptedLicense = reader.ReadBlob("issued license");
        if (encryptedLicense.Type is not (0x0001 or 0x0009))
        {
            throw new InvalidDataException("The server returned an invalid issued-license BLOB.");
        }
        byte[] expectedMac = reader.ReadBytes(16, "issued-license MAC");
        reader.EnsureConsumed("issued license");

        byte[] license = Rc4Transform(_licensingEncryptionKey!, encryptedLicense.Data);
        byte[] actualMac = GenerateMac(_macSaltKey!, license);
        if (!CryptographicOperations.FixedTimeEquals(actualMac, expectedMac))
        {
            throw new InvalidDataException("The issued RDP license failed MAC validation.");
        }

        _state = LicenseState.Complete;
        return null;
    }

    private byte[]? ProcessErrorAlert(ReadOnlySpan<byte> body)
    {
        var reader = new LicenseReader(body);
        uint errorCode = reader.ReadUInt32("licensing error code");
        uint stateTransition = reader.ReadUInt32("licensing state transition");
        _ = reader.ReadBlob("licensing error information");
        reader.EnsureConsumed("licensing error alert");

        if (errorCode != 0x00000007 || stateTransition != 0x00000002)
        {
            throw new InvalidDataException(
                $"The RDP server rejected licensing (error 0x{errorCode:X8}, transition 0x{stateTransition:X8}).");
        }

        _state = LicenseState.Complete;
        return null;
    }

    private void EnsureKeys(LicenseState requiredState)
    {
        if (_state != requiredState || _macSaltKey == null || _licensingEncryptionKey == null)
        {
            throw new InvalidDataException("The server sent an out-of-sequence RDP licensing packet.");
        }
    }

    private RSA LoadServerPublicKey(ReadOnlySpan<byte> certificateBlob)
    {
        if (certificateBlob.IsEmpty)
        {
            return _transportCertificate?.GetRSAPublicKey()
                ?? throw new InvalidDataException(
                    "The server omitted its licensing certificate and the secure transport has no RSA certificate.");
        }

        if (certificateBlob.Length < 4)
        {
            throw new InvalidDataException("The server licensing certificate is truncated.");
        }

        uint version = BinaryPrimitives.ReadUInt32LittleEndian(certificateBlob) & 0x7FFFFFFF;
        return version switch
        {
            1 => LoadProprietaryPublicKey(certificateBlob),
            2 => LoadX509ChainPublicKey(certificateBlob),
            _ => throw new InvalidDataException($"Unsupported RDP server certificate version {version}.")
        };
    }

    private static RSA LoadProprietaryPublicKey(ReadOnlySpan<byte> certificate)
    {
        var reader = new LicenseReader(certificate);
        _ = reader.ReadUInt32("certificate version");
        _ = reader.ReadUInt32("signature algorithm");
        _ = reader.ReadUInt32("key algorithm");
        LicenseBlob publicKey = reader.ReadBlob("RSA public key");
        if (publicKey.Type != 0x0006 || publicKey.Data.Length < 20 ||
            BinaryPrimitives.ReadUInt32LittleEndian(publicKey.Data) != 0x31415352)
        {
            throw new InvalidDataException("The proprietary RDP certificate has an invalid RSA public key.");
        }

        int bitLength = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(publicKey.Data.AsSpan(8)));
        int modulusLength = bitLength / 8;
        if (bitLength <= 0 || (bitLength & 7) != 0 || publicKey.Data.Length < 20 + modulusLength)
        {
            throw new InvalidDataException("The proprietary RDP certificate has an invalid RSA modulus length.");
        }

        uint exponentValue = BinaryPrimitives.ReadUInt32LittleEndian(publicKey.Data.AsSpan(16));
        byte[] modulus = publicKey.Data.AsSpan(20, modulusLength).ToArray();
        Array.Reverse(modulus);
        byte[] exponent = ToBigEndian(exponentValue);

        RSA rsa = RSA.Create();
        try
        {
            rsa.ImportParameters(new RSAParameters { Modulus = modulus, Exponent = exponent });
            return rsa;
        }
        catch
        {
            rsa.Dispose();
            throw;
        }
    }

    private static RSA LoadX509ChainPublicKey(ReadOnlySpan<byte> certificate)
    {
        var reader = new LicenseReader(certificate);
        _ = reader.ReadUInt32("certificate version");
        int count = reader.ReadLength("certificate count");
        if (count is <= 0 or > 256)
        {
            throw new InvalidDataException("The RDP certificate chain count is invalid.");
        }

        RSA? result = null;
        for (int i = 0; i < count; i++)
        {
            int certificateLength = reader.ReadLength("X.509 certificate");
            byte[] encoded = reader.ReadBytes(certificateLength, "X.509 certificate");
            using X509Certificate2 parsed = X509CertificateLoader.LoadCertificate(encoded);
            RSA? publicKey = parsed.GetRSAPublicKey();
            if (publicKey != null)
            {
                result?.Dispose();
                result = publicKey;
            }
        }
        reader.EnsureConsumed("X.509 certificate chain");
        return result ?? throw new InvalidDataException("The RDP certificate chain has no RSA public key.");
    }

    private static LicensePreamble ReadPreamble(ReadOnlySpan<byte> packet)
    {
        if (packet.Length < 4)
        {
            throw new InvalidDataException("The server licensing packet is truncated.");
        }

        ushort messageSize = BinaryPrimitives.ReadUInt16LittleEndian(packet.Slice(2));
        if (messageSize < 4 || messageSize > packet.Length || messageSize > MaximumLicensePacketLength)
        {
            throw new InvalidDataException("The server licensing packet has an invalid declared length.");
        }
        if ((packet[1] & 0x0F) is not (2 or 3))
        {
            throw new InvalidDataException("The server licensing packet has an unsupported preamble version.");
        }
        return new LicensePreamble(packet[0], messageSize);
    }

    private static byte[] CreateLicenseSecurityPayload(byte messageType, byte[] body)
    {
        int licenseLength = checked(4 + body.Length);
        byte[] payload = new byte[4 + licenseLength];
        BinaryPrimitives.WriteUInt16LittleEndian(payload, LicensePacketSecurityFlag);
        payload[4] = messageType;
        payload[5] = 0x83;
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(6), checked((ushort)licenseLength));
        body.CopyTo(payload, 8);
        return payload;
    }

    private static void WriteBlob(
        BinaryWriter writer,
        ushort type,
        ReadOnlySpan<byte> data,
        int trailingPadding = 0)
    {
        writer.Write(type);
        writer.Write(checked((ushort)(data.Length + trailingPadding)));
        writer.Write(data);
        if (trailingPadding > 0)
        {
            writer.Write(new byte[trailingPadding]);
        }
    }

    private static byte[] EncodeAnsiZ(string value)
    {
        byte[] encoded = new byte[value.Length + 1];
        for (int i = 0; i < value.Length; i++)
        {
            encoded[i] = value[i] <= 0x7F ? (byte)value[i] : (byte)'?';
        }
        return encoded;
    }

    private static byte[] CreateHardwareId(string machineName)
    {
        byte[] hardwareId = new byte[20];
        BinaryPrimitives.WriteUInt32LittleEndian(hardwareId, PlatformId);
        MD5.HashData(Encoding.ASCII.GetBytes(machineName), hardwareId.AsSpan(4));
        return hardwareId;
    }

    private static void DeriveKeys(
        ReadOnlySpan<byte> preMasterSecret,
        ReadOnlySpan<byte> clientRandom,
        ReadOnlySpan<byte> serverRandom,
        out byte[] macSaltKey,
        out byte[] licensingEncryptionKey)
    {
        byte[] masterSecret = Concat(
            SaltedHash(preMasterSecret, "A"u8, clientRandom, serverRandom),
            SaltedHash(preMasterSecret, "BB"u8, clientRandom, serverRandom),
            SaltedHash(preMasterSecret, "CCC"u8, clientRandom, serverRandom));
        byte[] sessionKeyBlob = Concat(
            SaltedHash(masterSecret, "A"u8, serverRandom, clientRandom),
            SaltedHash(masterSecret, "BB"u8, serverRandom, clientRandom),
            SaltedHash(masterSecret, "CCC"u8, serverRandom, clientRandom));

        macSaltKey = sessionKeyBlob.AsSpan(0, 16).ToArray();
        licensingEncryptionKey = MD5.HashData(
            Concat(sessionKeyBlob.AsSpan(16, 16), clientRandom, serverRandom));
        CryptographicOperations.ZeroMemory(masterSecret);
        CryptographicOperations.ZeroMemory(sessionKeyBlob);
    }

    private static byte[] SaltedHash(
        ReadOnlySpan<byte> secret,
        ReadOnlySpan<byte> salt,
        ReadOnlySpan<byte> firstRandom,
        ReadOnlySpan<byte> secondRandom)
    {
        byte[] shaInput = Concat(salt, secret, firstRandom, secondRandom);
        byte[] sha = SHA1.HashData(shaInput);
        return MD5.HashData(Concat(secret, sha));
    }

    private static byte[] GenerateMac(ReadOnlySpan<byte> key, ReadOnlySpan<byte> data)
    {
        byte[] length = new byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(length, checked((uint)data.Length));
        byte[] pad1 = new byte[40];
        byte[] pad2 = new byte[48];
        Array.Fill(pad1, (byte)0x36);
        Array.Fill(pad2, (byte)0x5C);
        byte[] sha = SHA1.HashData(Concat(key, pad1, length, data));
        return MD5.HashData(Concat(key, pad2, sha));
    }

    private static byte[] Rc4Transform(ReadOnlySpan<byte> key, ReadOnlySpan<byte> input)
    {
        Span<byte> state = stackalloc byte[256];
        for (int i = 0; i < state.Length; i++)
        {
            state[i] = (byte)i;
        }

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
        CryptographicOperations.ZeroMemory(state);
        return output;
    }

    private static byte[] Concat(
        ReadOnlySpan<byte> first,
        ReadOnlySpan<byte> second)
    {
        byte[] result = new byte[checked(first.Length + second.Length)];
        first.CopyTo(result);
        second.CopyTo(result.AsSpan(first.Length));
        return result;
    }

    private static byte[] Concat(
        ReadOnlySpan<byte> first,
        ReadOnlySpan<byte> second,
        ReadOnlySpan<byte> third)
    {
        byte[] result = new byte[checked(first.Length + second.Length + third.Length)];
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
        byte[] result = new byte[checked(first.Length + second.Length + third.Length + fourth.Length)];
        first.CopyTo(result);
        second.CopyTo(result.AsSpan(first.Length));
        third.CopyTo(result.AsSpan(first.Length + second.Length));
        fourth.CopyTo(result.AsSpan(first.Length + second.Length + third.Length));
        return result;
    }

    private static byte[] ToBigEndian(uint value)
    {
        Span<byte> buffer = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(buffer, value);
        int offset = 0;
        while (offset < buffer.Length - 1 && buffer[offset] == 0)
        {
            offset++;
        }
        return buffer[offset..].ToArray();
    }

    private enum LicenseState
    {
        Initial,
        NewLicenseRequested,
        ChallengeAnswered,
        Complete
    }

    private readonly record struct LicensePreamble(byte MessageType, ushort MessageSize);
    private readonly record struct LicenseBlob(ushort Type, byte[] Data);

    private ref struct LicenseReader
    {
        private readonly ReadOnlySpan<byte> _source;
        private int _offset;

        public LicenseReader(ReadOnlySpan<byte> source)
        {
            _source = source;
        }

        public uint ReadUInt32(string field)
        {
            Ensure(sizeof(uint), field);
            uint value = BinaryPrimitives.ReadUInt32LittleEndian(_source.Slice(_offset));
            _offset += sizeof(uint);
            return value;
        }

        public int ReadLength(string field)
        {
            uint value = ReadUInt32(field);
            if (value > int.MaxValue)
            {
                throw new InvalidDataException($"The {field} length is too large.");
            }
            return (int)value;
        }

        public byte[] ReadBytes(int length, string field)
        {
            Ensure(length, field);
            byte[] value = _source.Slice(_offset, length).ToArray();
            _offset += length;
            return value;
        }

        public void Skip(int length, string field)
        {
            Ensure(length, field);
            _offset += length;
        }

        public LicenseBlob ReadBlob(string field)
        {
            Ensure(4, field);
            ushort type = BinaryPrimitives.ReadUInt16LittleEndian(_source.Slice(_offset));
            ushort length = BinaryPrimitives.ReadUInt16LittleEndian(_source.Slice(_offset + 2));
            _offset += 4;
            return new LicenseBlob(type, ReadBytes(length, field));
        }

        public void EnsureConsumed(string packet)
        {
            if (_offset != _source.Length)
            {
                throw new InvalidDataException($"The {packet} contains unexpected trailing bytes.");
            }
        }

        private void Ensure(int length, string field)
        {
            if (length < 0 || _offset > _source.Length - length)
            {
                throw new InvalidDataException($"The server licensing {field} field is truncated.");
            }
        }
    }
}
