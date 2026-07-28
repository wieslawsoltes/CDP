using Avalonia.Headless.XUnit;
namespace CDP.Rdp.Tests.Input;

using System;
using System.Collections.Generic;
using CDP.Rdp.Input;
using CDP.Rdp.Protocol;
using Xunit;

public class RdpFastPathMultiEventTests
{
    [AvaloniaTheory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(5)]
    [InlineData(10)]
    [InlineData(15)]
    public void FastPath_MultiEventPacking_SinglePdu_RoundTrip(int eventCount)
    {
        var events = new List<RdpFastPathInputEvent>(eventCount);

        for (int i = 0; i < eventCount; i++)
        {
            switch (i % 6)
            {
                case 0:
                    events.Add(new RdpFastPathInputEvent(FastPathKeyboardFlags.None, (byte)(0x1E + i)));
                    break;
                case 1:
                    events.Add(new RdpFastPathInputEvent(FastPathKeyboardFlags.Release | FastPathKeyboardFlags.Extended, (byte)(0x20 + i)));
                    break;
                case 2:
                    events.Add(new RdpFastPathInputEvent(FastPathInputEventCode.Mouse, RdpPointerFlags.Move, (ushort)(100 + i * 10), (ushort)(200 + i * 10)));
                    break;
                case 3:
                    events.Add(new RdpFastPathInputEvent(FastPathInputEventCode.Mouse, RdpPointerFlags.Button1 | RdpPointerFlags.Down, (ushort)(100 + i * 10), (ushort)(200 + i * 10)));
                    break;
                case 4:
                    events.Add(new RdpFastPathInputEvent((byte)RdpSyncToggleFlags.CapsLock));
                    break;
                case 5:
                    events.Add(new RdpFastPathInputEvent(FastPathInputEventCode.ReleaseAll));
                    break;
            }
        }

        // Calculate expected event payload size
        int totalPayloadBytes = 0;
        foreach (var ev in events)
        {
            switch (ev.Code)
            {
                case FastPathInputEventCode.ScanCode: totalPayloadBytes += 2; break;
                case FastPathInputEventCode.Mouse:
                case FastPathInputEventCode.MouseX: totalPayloadBytes += 7; break;
                case FastPathInputEventCode.Sync: totalPayloadBytes += 2; break;
                case FastPathInputEventCode.ReleaseAll: totalPayloadBytes += 1; break;
                default: totalPayloadBytes += 1; break;
            }
        }

        ushort pduLength = (ushort)(2 + totalPayloadBytes); // 2 bytes header (short form length <= 127)

        byte[] buffer = new byte[256];
        var writer = new RdpPacketWriter(buffer);

        // 1. Write Header
        RdpInputPduWriter.WriteFastPathHeader(ref writer, (byte)events.Count, pduLength, action: 0x00, securityFlags: 0x00);

        // 2. Write all packed events sequentially
        foreach (var ev in events)
        {
            ev.Write(ref writer);
        }

        Assert.Equal(pduLength, writer.WrittenCount);

        // 3. Read & Verify Header
        var reader = new RdpPacketReader(buffer);
        bool headerRead = RdpInputPduReader.TryReadFastPathHeader(ref reader, out byte action, out byte numEvents, out byte securityFlags, out ushort parsedPduLength);

        Assert.True(headerRead);
        Assert.Equal(0, action);
        Assert.Equal(events.Count, numEvents);
        Assert.Equal(0, securityFlags);
        Assert.Equal(pduLength, parsedPduLength);

        // 4. Read & Verify all events
        var parsedEvents = new List<RdpFastPathInputEvent>();
        for (int i = 0; i < numEvents; i++)
        {
            bool evRead = RdpFastPathInputEvent.TryRead(ref reader, out var parsedEv);
            Assert.True(evRead, $"Failed reading fastpath event index {i}");
            parsedEvents.Add(parsedEv);
        }

        Assert.Equal(events.Count, parsedEvents.Count);
        for (int i = 0; i < events.Count; i++)
        {
            Assert.Equal(events[i].Code, parsedEvents[i].Code);
            Assert.Equal(events[i].KeyCode, parsedEvents[i].KeyCode);
            Assert.Equal(events[i].KeyboardFlags, parsedEvents[i].KeyboardFlags);
            Assert.Equal(events[i].PointerFlags, parsedEvents[i].PointerFlags);
            Assert.Equal(events[i].XPos, parsedEvents[i].XPos);
            Assert.Equal(events[i].YPos, parsedEvents[i].YPos);
            Assert.Equal(events[i].ToggleFlags, parsedEvents[i].ToggleFlags);
        }
    }

    [AvaloniaFact]
    public void FastPath_Max15Events_All15DistinctTypes_PackedInSinglePdu()
    {
        var events = new RdpFastPathInputEvent[15]
        {
            new RdpFastPathInputEvent(FastPathKeyboardFlags.None, 0x1E), // 1. ScanCode A Down (2B)
            new RdpFastPathInputEvent(FastPathKeyboardFlags.Release, 0x1E), // 2. ScanCode A Up (2B)
            new RdpFastPathInputEvent(FastPathKeyboardFlags.Extended, 0x1D), // 3. ScanCode E0 Down (2B)
            new RdpFastPathInputEvent(FastPathKeyboardFlags.Extended | FastPathKeyboardFlags.Release, 0x1D), // 4. ScanCode E0 Up (2B)
            new RdpFastPathInputEvent(FastPathKeyboardFlags.Extended1, 0x45), // 5. ScanCode E1 Down (2B)
            new RdpFastPathInputEvent(FastPathKeyboardFlags.Extended1 | FastPathKeyboardFlags.Release, 0x45), // 6. ScanCode E1 Up (2B)
            new RdpFastPathInputEvent(FastPathInputEventCode.Mouse, RdpPointerFlags.Move, 300, 400), // 7. Mouse Move (7B)
            new RdpFastPathInputEvent(FastPathInputEventCode.Mouse, RdpPointerFlags.Button1 | RdpPointerFlags.Down, 300, 400), // 8. Mouse Btn1 Down (7B)
            new RdpFastPathInputEvent(FastPathInputEventCode.Mouse, RdpPointerFlags.Button1, 300, 400), // 9. Mouse Btn1 Up (7B)
            new RdpFastPathInputEvent(FastPathInputEventCode.Mouse, RdpPointerFlags.Wheel | (RdpPointerFlags)120, 300, 400), // 10. Wheel Up (7B)
            new RdpFastPathInputEvent(FastPathInputEventCode.Mouse, RdpPointerFlags.Wheel | RdpPointerFlags.WheelNegative | (RdpPointerFlags)120, 300, 400), // 11. Wheel Down (7B)
            new RdpFastPathInputEvent(FastPathInputEventCode.MouseX, RdpPointerFlags.Button2 | RdpPointerFlags.Down, 800, 600), // 12. MouseX Btn2 (7B)
            new RdpFastPathInputEvent((byte)(RdpSyncToggleFlags.NumLock | RdpSyncToggleFlags.CapsLock)), // 13. Sync (2B)
            new RdpFastPathInputEvent(FastPathInputEventCode.ReleaseAll), // 14. ReleaseAll (1B)
            new RdpFastPathInputEvent(FastPathKeyboardFlags.None, 0x2C) // 15. ScanCode Z Down (2B)
        };

        // Event payload bytes calculation:
        // 6 * 2B + 6 * 7B + 2B + 1B + 2B = 12 + 42 + 2 + 1 + 2 = 59 bytes.
        // Total PDU length = 2 bytes (header) + 59 = 61 bytes.
        ushort expectedPduLength = 61;

        byte[] buffer = new byte[128];
        var writer = new RdpPacketWriter(buffer);

        RdpInputPduWriter.WriteFastPathHeader(ref writer, numEvents: 15, pduLength: expectedPduLength);
        foreach (var ev in events)
        {
            ev.Write(ref writer);
        }

        Assert.Equal(expectedPduLength, writer.WrittenCount);

        var reader = new RdpPacketReader(buffer);
        Assert.True(RdpInputPduReader.TryReadFastPathHeader(ref reader, out _, out byte numEv, out _, out ushort pduLen));
        Assert.Equal(15, numEv);
        Assert.Equal(expectedPduLength, pduLen);

        for (int i = 0; i < 15; i++)
        {
            Assert.True(RdpFastPathInputEvent.TryRead(ref reader, out var parsed), $"Failed reading event {i}");
            Assert.Equal(events[i].Code, parsed.Code);
            Assert.Equal(events[i].KeyCode, parsed.KeyCode);
            Assert.Equal(events[i].KeyboardFlags, parsed.KeyboardFlags);
            Assert.Equal(events[i].PointerFlags, parsed.PointerFlags);
            Assert.Equal(events[i].XPos, parsed.XPos);
            Assert.Equal(events[i].YPos, parsed.YPos);
            Assert.Equal(events[i].ToggleFlags, parsed.ToggleFlags);
        }

        Assert.Equal(expectedPduLength, reader.Position);
    }

    [AvaloniaFact]
    public void FastPathHeader_ExtendedLength_PduLengthGreaterThan127_RoundTrip()
    {
        // 20 mouse events * 7 bytes = 140 bytes payload.
        // FastPath header with length > 127 bytes will be 3 bytes (1 byte header + 2 bytes length).
        // Total PDU length = 3 + 140 = 143 bytes.
        ushort pduLength = 143;
        byte numEvents = 15;

        byte[] buffer = new byte[256];
        var writer = new RdpPacketWriter(buffer);

        RdpInputPduWriter.WriteFastPathHeader(ref writer, numEvents, pduLength, action: 0x01, securityFlags: 0x02);

        Assert.Equal(3, writer.WrittenCount);
        Assert.Equal((byte)(0x80 | 0x00), buffer[1]); // High bit set on length byte 1
        Assert.Equal(143, buffer[2]);

        var reader = new RdpPacketReader(buffer);
        bool success = RdpInputPduReader.TryReadFastPathHeader(ref reader, out byte action, out byte numEv, out byte secFlags, out ushort readLen);

        Assert.True(success);
        Assert.Equal(3, reader.Position);
        Assert.Equal(0x01, action);
        Assert.Equal(15, numEv);
        Assert.Equal(0x02, secFlags);
        Assert.Equal(143, readLen);
    }
}
