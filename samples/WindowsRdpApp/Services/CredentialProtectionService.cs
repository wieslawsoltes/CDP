using System;

namespace WindowsRdpApp.Services;

public class CredentialProtectionService : ICredentialProtectionService
{
    private const string Prefix = "ENC:";
    private static readonly byte[] KeyBytes = System.Text.Encoding.UTF8.GetBytes("CDP_WindowsRdpApp_SecretKey_2026");

    public string Protect(string plainText)
    {
        if (string.IsNullOrEmpty(plainText))
            return string.Empty;

        try
        {
            byte[] bytes = System.Text.Encoding.UTF8.GetBytes(plainText);
            byte[] encrypted = Transform(bytes);
            return Prefix + Convert.ToBase64String(encrypted);
        }
        catch
        {
            return plainText;
        }
    }

    public string Unprotect(string protectedText)
    {
        if (string.IsNullOrEmpty(protectedText))
            return string.Empty;

        if (!protectedText.StartsWith(Prefix, StringComparison.Ordinal))
            return protectedText;

        try
        {
            string base64 = protectedText.Substring(Prefix.Length);
            byte[] encrypted = Convert.FromBase64String(base64);
            byte[] decrypted = Transform(encrypted);
            return System.Text.Encoding.UTF8.GetString(decrypted);
        }
        catch
        {
            return protectedText;
        }
    }

    private static byte[] Transform(byte[] input)
    {
        byte[] output = new byte[input.Length];
        for (int i = 0; i < input.Length; i++)
        {
            output[i] = (byte)(input[i] ^ KeyBytes[i % KeyBytes.Length]);
        }
        return output;
    }
}
