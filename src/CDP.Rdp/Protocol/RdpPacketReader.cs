namespace CDP.Rdp.Protocol;

using System;
using System.Buffers.Binary;

/// <summary>
/// High-performance allocation-free binary reader over ReadOnlySpan&lt;byte&gt;.
/// </summary>
public ref struct RdpPacketReader
{
    private ReadOnlySpan<byte> _span;
    private int _offset;

    public RdpPacketReader(ReadOnlySpan<byte> span)
    {
        _span = span;
        _offset = 0;
    }

    public readonly int Length => _span.Length;
    public readonly int Position => _offset;
    public readonly int UnreadLength => _span.Length - _offset;

    public byte ReadByte()
    {
        if (_offset >= _span.Length)
            throw new InvalidOperationException("End of span reached.");
        return _span[_offset++];
    }

    public ushort ReadUInt16BE()
    {
        if (UnreadLength < 2)
            throw new InvalidOperationException("Insufficient bytes for UInt16BE.");
        ushort value = BinaryPrimitives.ReadUInt16BigEndian(_span.Slice(_offset, 2));
        _offset += 2;
        return value;
    }

    public ushort ReadUInt16LE()
    {
        if (UnreadLength < 2)
            throw new InvalidOperationException("Insufficient bytes for UInt16LE.");
        ushort value = BinaryPrimitives.ReadUInt16LittleEndian(_span.Slice(_offset, 2));
        _offset += 2;
        return value;
    }

    public uint ReadUInt32BE()
    {
        if (UnreadLength < 4)
            throw new InvalidOperationException("Insufficient bytes for UInt32BE.");
        uint value = BinaryPrimitives.ReadUInt32BigEndian(_span.Slice(_offset, 4));
        _offset += 4;
        return value;
    }

    public uint ReadUInt32LE()
    {
        if (UnreadLength < 4)
            throw new InvalidOperationException("Insufficient bytes for UInt32LE.");
        uint value = BinaryPrimitives.ReadUInt32LittleEndian(_span.Slice(_offset, 4));
        _offset += 4;
        return value;
    }

    public ReadOnlySpan<byte> ReadSpan(int length)
    {
        if (UnreadLength < length)
            throw new InvalidOperationException($"Insufficient bytes. Required: {length}, Available: {UnreadLength}");
        ReadOnlySpan<byte> slice = _span.Slice(_offset, length);
        _offset += length;
        return slice;
    }

    public void Advance(int count)
    {
        if (UnreadLength < count)
            throw new InvalidOperationException("Cannot advance past end of span.");
        _offset += count;
    }
}
