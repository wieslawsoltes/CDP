using Avalonia.Headless.XUnit;
namespace CDP.Rdp.Tests.Input;

using System;
using Avalonia.Input;
using Avalonia.Diagnostics.Cdp.Rdp;
using CDP.Rdp.Input;
using Xunit;

[Xunit.Collection("RdpTests")]
public class RdpInputMapperTests
{
    [AvaloniaTheory]
    [InlineData(Key.A, true, (ushort)0x1E, RdpKeyboardFlags.Down)]
    [InlineData(Key.A, false, (ushort)0x1E, RdpKeyboardFlags.Release)]
    [InlineData(Key.Enter, true, (ushort)0x1C, RdpKeyboardFlags.Down)]
    [InlineData(Key.Tab, true, (ushort)0x0F, RdpKeyboardFlags.Down)]
    [InlineData(Key.Escape, true, (ushort)0x01, RdpKeyboardFlags.Down)]
    [InlineData(Key.Space, true, (ushort)0x39, RdpKeyboardFlags.Down)]
    public void TryMapKey_StandardKeys_ReturnsCorrectScancodeAndFlags(Key key, bool isDown, ushort expectedCode, RdpKeyboardFlags expectedFlags)
    {
        bool mapped = RdpInputMapper.TryMapKey(key, isDown, out RdpKeyboardEvent kbEvent);

        Assert.True(mapped);
        Assert.Equal(expectedCode, kbEvent.KeyCode);
        Assert.Equal(expectedFlags, kbEvent.Flags);
        Assert.False(kbEvent.IsVirtualKey);
    }

    [AvaloniaTheory]
    [InlineData(Key.Up, true, (ushort)0x48, RdpKeyboardFlags.Down | RdpKeyboardFlags.Extended)]
    [InlineData(Key.Down, true, (ushort)0x50, RdpKeyboardFlags.Down | RdpKeyboardFlags.Extended)]
    [InlineData(Key.Left, true, (ushort)0x4B, RdpKeyboardFlags.Down | RdpKeyboardFlags.Extended)]
    [InlineData(Key.Right, true, (ushort)0x4D, RdpKeyboardFlags.Down | RdpKeyboardFlags.Extended)]
    [InlineData(Key.Delete, false, (ushort)0x53, RdpKeyboardFlags.Release | RdpKeyboardFlags.Extended)]
    public void TryMapKey_ExtendedKeys_ReturnsExtendedFlagAndScancode(Key key, bool isDown, ushort expectedCode, RdpKeyboardFlags expectedFlags)
    {
        bool mapped = RdpInputMapper.TryMapKey(key, isDown, out RdpKeyboardEvent kbEvent);

        Assert.True(mapped);
        Assert.Equal(expectedCode, kbEvent.KeyCode);
        Assert.Equal(expectedFlags, kbEvent.Flags);
        Assert.True((kbEvent.Flags & RdpKeyboardFlags.Extended) != 0);
    }

    [AvaloniaFact]
    public void TryMapKey_UnmappedKey_ReturnsFalse()
    {
        bool mapped = RdpInputMapper.TryMapKey(Key.None, isDown: true, out _);
        Assert.False(mapped);
    }
}
