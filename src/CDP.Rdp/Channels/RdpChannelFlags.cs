namespace CDP.Rdp.Channels;

using System;

/// <summary>
/// Static Virtual Channel PDU Flags (MS-RDPBCGR Section 2.2.6.1).
/// </summary>
[Flags]
public enum ChannelPduFlags : uint
{
    None = 0x00000000,

    /// <summary>
    /// First chunk of a virtual channel message.
    /// </summary>
    First = 0x00000001,

    /// <summary>
    /// Last chunk of a virtual channel message.
    /// </summary>
    Last = 0x00000002,

    /// <summary>
    /// Visible protocol header present.
    /// </summary>
    ShowProtocol = 0x00000010,

    /// <summary>
    /// Suspend channel transmission.
    /// </summary>
    Suspend = 0x00000020,

    /// <summary>
    /// Resume channel transmission.
    /// </summary>
    Resume = 0x00000040,

    /// <summary>
    /// Packet is bulk compressed.
    /// </summary>
    PackedCompressed = 0x00200000
}

/// <summary>
/// Dynamic Virtual Channel Command Codes (MS-RDPEDYC Section 2.2.1).
/// </summary>
public enum DvcCommandCode : byte
{
    /// <summary>
    /// Channel creation request or response.
    /// </summary>
    Create = 0x01,

    /// <summary>
    /// First chunk of a multi-chunk DVC data message.
    /// </summary>
    DataFirst = 0x02,

    /// <summary>
    /// Single-chunk message or subsequent chunk of a multi-chunk DVC data message.
    /// </summary>
    Data = 0x03,

    /// <summary>
    /// Channel close request or response.
    /// </summary>
    Close = 0x04,

    /// <summary>
    /// DVC Capabilities negotiation message.
    /// </summary>
    Capabilities = 0x05
}
