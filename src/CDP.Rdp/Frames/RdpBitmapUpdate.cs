namespace CDP.Rdp.Frames;

using System;

/// <summary>
/// Represents a single dirty rectangle bitmap update parsed from TS_BITMAP_DATA.
/// </summary>
public readonly struct RdpBitmapUpdate
{
    public ushort Left { get; }
    public ushort Top { get; }
    public ushort Width { get; }
    public ushort Height { get; }
    public ushort Bpp { get; }
    public bool Compressed { get; }
    public ReadOnlyMemory<byte> Data { get; }

    public RdpBitmapUpdate(
        ushort left,
        ushort top,
        ushort width,
        ushort height,
        ushort bpp,
        bool compressed,
        ReadOnlyMemory<byte> data)
    {
        Left = left;
        Top = top;
        Width = width;
        Height = height;
        Bpp = bpp;
        Compressed = compressed;
        Data = data;
    }
}
