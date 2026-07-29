using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace WindowsRdpApp.Services;

public class CredentialProtectionService : ICredentialProtectionService
{
    private const string Prefix = "ENC:";
    private const byte DpapiEnvelope = 1;
    private const byte AesEnvelope = 2;
    private const int NonceSize = 12;
    private const int TagSize = 16;
    private readonly byte[]? _key;
    private static readonly byte[] DpapiEntropy = Encoding.UTF8.GetBytes("WindowsRdpApp/ProfileCredential/v2");

    public CredentialProtectionService(string? keyFilePath = null)
    {
        if (!OperatingSystem.IsWindows())
        {
            _key = LoadOrCreateUserKey(keyFilePath ?? GetDefaultKeyPath());
        }
    }

    public string Protect(string plainText)
    {
        if (string.IsNullOrEmpty(plainText))
            return string.Empty;

        if (plainText.StartsWith(Prefix, StringComparison.Ordinal))
        {
            try
            {
                _ = UnprotectCore(plainText);
                return plainText;
            }
            catch (Exception ex) when (ex is FormatException or CryptographicException or PlatformNotSupportedException)
            {
                // A literal password may begin with the envelope marker.
            }
        }

        byte[] plaintext = Encoding.UTF8.GetBytes(plainText);
        if (OperatingSystem.IsWindows())
        {
            try
            {
                byte[] protectedBytes = ProtectedData.Protect(plaintext, DpapiEntropy, DataProtectionScope.CurrentUser);
                byte[] dpapiEnvelope = new byte[protectedBytes.Length + 1];
                dpapiEnvelope[0] = DpapiEnvelope;
                protectedBytes.CopyTo(dpapiEnvelope, 1);
                return Prefix + Convert.ToBase64String(dpapiEnvelope);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(plaintext);
            }
        }

        byte[] nonce = RandomNumberGenerator.GetBytes(NonceSize);
        byte[] ciphertext = new byte[plaintext.Length];
        byte[] tag = new byte[TagSize];
        using var aes = new AesGcm(_key!, TagSize);
        aes.Encrypt(nonce, plaintext, ciphertext, tag);

        byte[] envelope = new byte[1 + NonceSize + TagSize + ciphertext.Length];
        envelope[0] = AesEnvelope;
        nonce.CopyTo(envelope, 1);
        tag.CopyTo(envelope, 1 + NonceSize);
        ciphertext.CopyTo(envelope, 1 + NonceSize + TagSize);
        CryptographicOperations.ZeroMemory(plaintext);
        return Prefix + Convert.ToBase64String(envelope);
    }

    public string Unprotect(string protectedText)
    {
        if (string.IsNullOrEmpty(protectedText))
            return string.Empty;

        if (!protectedText.StartsWith(Prefix, StringComparison.Ordinal))
            return protectedText;

        try
        {
            return UnprotectCore(protectedText);
        }
        catch (Exception ex) when (ex is FormatException or CryptographicException or PlatformNotSupportedException)
        {
            return protectedText;
        }
    }

    private string UnprotectCore(string protectedText)
    {
        byte[] envelope = Convert.FromBase64String(protectedText[Prefix.Length..]);
        if (envelope.Length < 1)
        {
            throw new CryptographicException("The protected credential payload is truncated.");
        }

        if (envelope[0] == DpapiEnvelope)
        {
            if (!OperatingSystem.IsWindows())
            {
                throw new PlatformNotSupportedException("This credential was protected for a Windows user account.");
            }

            byte[] plaintextBytes = ProtectedData.Unprotect(envelope.AsSpan(1).ToArray(), DpapiEntropy, DataProtectionScope.CurrentUser);
            try
            {
                return Encoding.UTF8.GetString(plaintextBytes);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(plaintextBytes);
            }
        }

        if (envelope[0] != AesEnvelope || envelope.Length < 1 + NonceSize + TagSize)
        {
            throw new CryptographicException("The protected credential payload has an unsupported format.");
        }

        ReadOnlySpan<byte> nonce = envelope.AsSpan(1, NonceSize);
        ReadOnlySpan<byte> tag = envelope.AsSpan(1 + NonceSize, TagSize);
        ReadOnlySpan<byte> ciphertext = envelope.AsSpan(1 + NonceSize + TagSize);
        byte[] plaintext = new byte[ciphertext.Length];
        using var aes = new AesGcm(_key!, TagSize);
        aes.Decrypt(nonce, ciphertext, tag, plaintext);
        string result = Encoding.UTF8.GetString(plaintext);
        CryptographicOperations.ZeroMemory(plaintext);
        return result;
    }

    private static string GetDefaultKeyPath()
    {
        string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return Path.Combine(appData, "WindowsRdpApp", "credential.key");
    }

    private static byte[] LoadOrCreateUserKey(string keyPath)
    {
        string? directory = Path.GetDirectoryName(keyPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
            if (!OperatingSystem.IsWindows())
            {
                File.SetUnixFileMode(
                    directory,
                    UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            }
        }

        if (File.Exists(keyPath))
        {
            byte[] existing = File.ReadAllBytes(keyPath);
            if (existing.Length != 32)
                throw new CryptographicException("The per-user credential key has an invalid length.");
            return existing;
        }

        byte[] generated = RandomNumberGenerator.GetBytes(32);
        using (var stream = new FileStream(keyPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        {
            stream.Write(generated);
            stream.Flush(flushToDisk: true);
        }

        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(keyPath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
        return generated;
    }
}
