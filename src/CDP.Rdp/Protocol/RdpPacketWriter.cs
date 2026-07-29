namespace CDP.Rdp.Protocol;

using System;
using System.Buffers.Binary;

/// <summary>
/// High-performance allocation-free binary writer over Span&lt;byte&gt;.
/// </summary>
public ref struct RdpPacketWriter
{
    private Span<byte> _span;
    private int _offset;

    public RdpPacketWriter(Span<byte> span)
    {
        _span = span;
        _offset = 0;
    }

    public readonly int WrittenCount => _offset;
    public readonly int RemainingCapacity => _span.Length - _offset;

    public void WriteByte(byte value)
    {
        if (_offset >= _span.Length)
            throw new InvalidOperationException("Buffer capacity exceeded.");
        _span[_offset++] = value;
    }

    public void WriteUInt16BE(ushort value)
    {
        if (RemainingCapacity < 2)
            throw new InvalidOperationException("Buffer capacity exceeded.");
        BinaryPrimitives.WriteUInt16BigEndian(_span.Slice(_offset, 2), value);
        _offset += 2;
    }

    public void WriteUInt16LE(ushort value)
    {
        if (RemainingCapacity < 2)
            throw new InvalidOperationException("Buffer capacity exceeded.");
        BinaryPrimitives.WriteUInt16LittleEndian(_span.Slice(_offset, 2), value);
        _offset += 2;
    }

    public void WriteUInt32BE(uint value)
    {
        if (RemainingCapacity < 4)
            throw new InvalidOperationException("Buffer capacity exceeded.");
        BinaryPrimitives.WriteUInt32BigEndian(_span.Slice(_offset, 4), value);
        _offset += 4;
    }

    public void WriteUInt32LE(uint value)
    {
        if (RemainingCapacity < 4)
            throw new InvalidOperationException("Buffer capacity exceeded.");
        BinaryPrimitives.WriteUInt32LittleEndian(_span.Slice(_offset, 4), value);
        _offset += 4;
    }

    public void WriteSpan(ReadOnlySpan<byte> source)
    {
        if (RemainingCapacity < source.Length)
            throw new InvalidOperationException("Buffer capacity exceeded.");
        source.CopyTo(_span.Slice(_offset));
        _offset += source.Length;
    }
}
