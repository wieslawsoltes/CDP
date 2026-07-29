namespace CDP.Rdp.Exceptions;

using System;
using CDP.Rdp.Protocol;

/// <summary>
/// Exception thrown when RDP connection negotiation or security handshake fails.
/// </summary>
public class RdpNegotiationException : Exception
{
    public RdpNegotiationFailureCode? FailureCode { get; }

    public RdpNegotiationException(string message) : base(message)
    {
    }

    public RdpNegotiationException(string message, Exception innerException) : base(message, innerException)
    {
    }

    public RdpNegotiationException(RdpNegotiationFailureCode failureCode)
        : base($"RDP Negotiation failed with error code: {failureCode}")
    {
        FailureCode = failureCode;
    }

    public RdpNegotiationException(RdpNegotiationFailureCode failureCode, string message)
        : base(message)
    {
        FailureCode = failureCode;
    }
}
