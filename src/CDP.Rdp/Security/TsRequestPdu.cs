namespace CDP.Rdp.Security;

using System;
using CDP.Rdp.Exceptions;

/// <summary>
/// Represents a CredSSP TSRequest ASN.1 DER PDU (MS-CSSP 2.2.1).
/// TSRequest ::= SEQUENCE {
///     version     [0] INTEGER,
///     negoTokens  [1] NegoData OPTIONAL,
///     authInfo    [2] OCTET STRING OPTIONAL,
///     pubKeyAuth  [3] OCTET STRING OPTIONAL,
///     errorCode   [4] INTEGER OPTIONAL
/// }
/// </summary>
public sealed class TsRequestPdu
{
    public int Version { get; set; } = 2;
    public byte[]? NegoToken { get; set; }
    public byte[]? AuthInfo { get; set; }
    public byte[]? PubKeyAuth { get; set; }
    public int? ErrorCode { get; set; }

    public byte[] Encode()
    {
        byte[] versionBytes = EncodeExplicitInteger(0, Version);
        byte[]? negoTokenBytes = NegoToken != null ? EncodeExplicitNegoData(1, NegoToken) : null;
        byte[]? authInfoBytes = AuthInfo != null ? EncodeExplicitOctetString(2, AuthInfo) : null;
        byte[]? pubKeyAuthBytes = PubKeyAuth != null ? EncodeExplicitOctetString(3, PubKeyAuth) : null;
        byte[]? errorCodeBytes = ErrorCode.HasValue ? EncodeExplicitInteger(4, ErrorCode.Value) : null;

        int totalPayloadLength = versionBytes.Length
            + (negoTokenBytes?.Length ?? 0)
            + (authInfoBytes?.Length ?? 0)
            + (pubKeyAuthBytes?.Length ?? 0)
            + (errorCodeBytes?.Length ?? 0);

        byte[] header = EncodeHeader(0x30, totalPayloadLength);
        byte[] result = new byte[header.Length + totalPayloadLength];

        int offset = 0;
        Buffer.BlockCopy(header, 0, result, offset, header.Length);
        offset += header.Length;

        Buffer.BlockCopy(versionBytes, 0, result, offset, versionBytes.Length);
        offset += versionBytes.Length;

        if (negoTokenBytes != null)
        {
            Buffer.BlockCopy(negoTokenBytes, 0, result, offset, negoTokenBytes.Length);
            offset += negoTokenBytes.Length;
        }

        if (authInfoBytes != null)
        {
            Buffer.BlockCopy(authInfoBytes, 0, result, offset, authInfoBytes.Length);
            offset += authInfoBytes.Length;
        }

        if (pubKeyAuthBytes != null)
        {
            Buffer.BlockCopy(pubKeyAuthBytes, 0, result, offset, pubKeyAuthBytes.Length);
            offset += pubKeyAuthBytes.Length;
        }

        if (errorCodeBytes != null)
        {
            Buffer.BlockCopy(errorCodeBytes, 0, result, offset, errorCodeBytes.Length);
            offset += errorCodeBytes.Length;
        }

        return result;
    }

    public static bool TryParse(ReadOnlySpan<byte> data, out TsRequestPdu pdu)
    {
        pdu = new TsRequestPdu();
        if (data.Length < 4 || data[0] != 0x30)
        {
            return false;
        }

        try
        {
            int offset = 1;
            int length = ReadLength(data, ref offset);
            if (data.Length < offset + length) return false;

            ReadOnlySpan<byte> payload = data.Slice(offset, length);
            int pOffset = 0;

            while (pOffset < payload.Length)
            {
                byte tag = payload[pOffset++];
                int elemLen = ReadLength(payload, ref pOffset);
                if (pOffset + elemLen > payload.Length) return false;
                ReadOnlySpan<byte> elemData = payload.Slice(pOffset, elemLen);
                pOffset += elemLen;

                switch (tag)
                {
                    case 0xA0: // version [0]
                        pdu.Version = ParseInteger(elemData);
                        break;
                    case 0xA1: // negoTokens [1]
                        pdu.NegoToken = ParseNegoData(elemData);
                        break;
                    case 0xA2: // authInfo [2]
                        pdu.AuthInfo = ParseOctetString(elemData);
                        break;
                    case 0xA3: // pubKeyAuth [3]
                        pdu.PubKeyAuth = ParseOctetString(elemData);
                        break;
                    case 0xA4: // errorCode [4]
                        pdu.ErrorCode = ParseInteger(elemData);
                        break;
                }
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

    private static byte[] EncodeExplicitInteger(byte tagIndex, int value)
    {
        byte[] intDer = (value >= 0 && value <= 127)
            ? new byte[] { 0x02, 0x01, (byte)value }
            : new byte[] { 0x02, 0x04, (byte)(value >> 24), (byte)(value >> 16), (byte)(value >> 8), (byte)value };

        byte tag = (byte)(0xA0 | tagIndex);
        byte[] header = EncodeHeader(tag, intDer.Length);
        byte[] res = new byte[header.Length + intDer.Length];
        Buffer.BlockCopy(header, 0, res, 0, header.Length);
        Buffer.BlockCopy(intDer, 0, res, header.Length, intDer.Length);
        return res;
    }

    private static byte[] EncodeExplicitOctetString(byte tagIndex, byte[] content)
    {
        byte[] octHeader = EncodeHeader(0x04, content.Length);
        int octTotalLen = octHeader.Length + content.Length;
        byte tag = (byte)(0xA0 | tagIndex);
        byte[] tagHeader = EncodeHeader(tag, octTotalLen);

        byte[] res = new byte[tagHeader.Length + octTotalLen];
        int off = 0;
        Buffer.BlockCopy(tagHeader, 0, res, off, tagHeader.Length); off += tagHeader.Length;
        Buffer.BlockCopy(octHeader, 0, res, off, octHeader.Length); off += octHeader.Length;
        Buffer.BlockCopy(content, 0, res, off, content.Length);
        return res;
    }

    private static byte[] EncodeExplicitNegoData(byte tagIndex, byte[] token)
    {
        byte[] innerOctet = EncodeExplicitOctetString(0, token);
        byte[] negoTokenSeqHeader = EncodeHeader(0x30, innerOctet.Length);
        int negoTokenSeqLen = negoTokenSeqHeader.Length + innerOctet.Length;

        byte[] negoDataSeqHeader = EncodeHeader(0x30, negoTokenSeqLen);
        int negoDataSeqLen = negoDataSeqHeader.Length + negoTokenSeqLen;

        byte tag = (byte)(0xA0 | tagIndex);
        byte[] tagHeader = EncodeHeader(tag, negoDataSeqLen);

        byte[] res = new byte[tagHeader.Length + negoDataSeqLen];
        int off = 0;
        Buffer.BlockCopy(tagHeader, 0, res, off, tagHeader.Length); off += tagHeader.Length;
        Buffer.BlockCopy(negoDataSeqHeader, 0, res, off, negoDataSeqHeader.Length); off += negoDataSeqHeader.Length;
        Buffer.BlockCopy(negoTokenSeqHeader, 0, res, off, negoTokenSeqHeader.Length); off += negoTokenSeqHeader.Length;
        Buffer.BlockCopy(innerOctet, 0, res, off, innerOctet.Length);
        return res;
    }

    private static byte[] EncodeHeader(byte tag, int length)
    {
        if (length < 128)
        {
            return new byte[] { tag, (byte)length };
        }
        else if (length <= 255)
        {
            return new byte[] { tag, 0x81, (byte)length };
        }
        else
        {
            return new byte[] { tag, 0x82, (byte)(length >> 8), (byte)(length & 0xFF) };
        }
    }

    private static int ReadLength(ReadOnlySpan<byte> data, ref int offset)
    {
        byte b = data[offset++];
        if (b < 128) return b;
        int numBytes = b & 0x7F;
        int len = 0;
        for (int i = 0; i < numBytes; i++)
        {
            len = (len << 8) | data[offset++];
        }
        return len;
    }

    private static int ParseInteger(ReadOnlySpan<byte> data)
    {
        if (data.Length >= 3 && data[0] == 0x02)
        {
            int offset = 1;
            int len = ReadLength(data, ref offset);
            if (offset + len <= data.Length)
            {
                int val = 0;
                for (int i = 0; i < len; i++)
                {
                    val = (val << 8) | data[offset + i];
                }
                return val;
            }
        }
        return 0;
    }

    private static byte[]? ParseOctetString(ReadOnlySpan<byte> data)
    {
        if (data.Length >= 2 && data[0] == 0x04)
        {
            int offset = 1;
            int len = ReadLength(data, ref offset);
            if (offset + len <= data.Length)
            {
                return data.Slice(offset, len).ToArray();
            }
        }
        return data.ToArray();
    }

    private static byte[]? ParseNegoData(ReadOnlySpan<byte> data)
    {
        if (data.IsEmpty) return null;
        int offset = 0;
        try
        {
            if (data[offset] == 0x30)
            {
                offset++;
                ReadLength(data, ref offset);
                if (offset < data.Length && data[offset] == 0x30)
                {
                    offset++;
                    ReadLength(data, ref offset);
                    if (offset < data.Length && data[offset] == 0xA0)
                    {
                        offset++;
                        ReadLength(data, ref offset);
                        if (offset < data.Length && data[offset] == 0x04)
                        {
                            offset++;
                            int len = ReadLength(data, ref offset);
                            if (offset + len <= data.Length)
                            {
                                return data.Slice(offset, len).ToArray();
                            }
                        }
                    }
                }
            }
        }
        catch
        {
            // Fallback
        }
        return data.ToArray();
    }
}
