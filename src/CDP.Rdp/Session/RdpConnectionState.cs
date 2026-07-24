namespace CDP.Rdp.Session;

using System;

public enum RdpConnectionState
{
    Disconnected,
    Connecting,
    Negotiating,
    Authenticating,
    Connected,
    Disconnecting,
    Faulted
}

public sealed class RdpConnectionStateChangedEventArgs : EventArgs
{
    public RdpConnectionState OldState { get; }
    public RdpConnectionState NewState { get; }
    public Exception? Exception { get; }

    public RdpConnectionStateChangedEventArgs(RdpConnectionState oldState, RdpConnectionState newState, Exception? exception = null)
    {
        OldState = oldState;
        NewState = newState;
        Exception = exception;
    }
}
