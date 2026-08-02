using System.Net;
using System.Security.Cryptography;

namespace Chrome.DevTools.Protocol.Inspector;

public sealed class CdpInspectorServerOptions
{
    /// <summary>The server is opt-in and cannot be started while this is false.</summary>
    public bool Enabled { get; set; }

    public IPAddress Address { get; set; } = IPAddress.Loopback;

    public int Port { get; set; } = 9229;

    /// <summary>Required for any non-loopback binding.</summary>
    public bool AllowRemoteConnections { get; set; }

    /// <summary>
    /// Authentication token accepted as a Bearer token or a <c>token</c> query parameter.
    /// A cryptographically random value is generated when omitted.
    /// </summary>
    public string? AccessToken { get; set; }

    public int MaxMessageBytes { get; set; } = 16 * 1024 * 1024;

    public int ReceiveBufferBytes { get; set; } = 16 * 1024;

    public int MaxConcurrentSessions { get; set; } = 4;

    public TimeSpan WebSocketKeepAliveInterval { get; set; } = TimeSpan.FromSeconds(30);

    public ICdpInspectorOriginPolicy OriginPolicy { get; set; } = new DevToolsOriginPolicy();

    public ICdpInspectorLifecycleObserver? LifecycleObserver { get; set; }

    internal string ValidateAndGetAccessToken()
    {
        if (!Enabled)
        {
            throw new InvalidOperationException("The Inspector server is disabled. Set Enabled to true explicitly before starting it.");
        }

        if (Port is < 1 or > 65535)
        {
            throw new ArgumentOutOfRangeException(nameof(Port), "Port must be between 1 and 65535.");
        }

        if (MaxMessageBytes < 1024)
        {
            throw new ArgumentOutOfRangeException(nameof(MaxMessageBytes));
        }

        if (ReceiveBufferBytes < 1024 || ReceiveBufferBytes > MaxMessageBytes)
        {
            throw new ArgumentOutOfRangeException(nameof(ReceiveBufferBytes));
        }

        if (MaxConcurrentSessions < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(MaxConcurrentSessions));
        }

        if (!IPAddress.IsLoopback(Address) && !AllowRemoteConnections)
        {
            throw new InvalidOperationException("Non-loopback Inspector bindings require AllowRemoteConnections=true.");
        }

        if (string.IsNullOrWhiteSpace(AccessToken))
        {
            AccessToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
        }

        if (!IPAddress.IsLoopback(Address) && AccessToken.Length < 32)
        {
            throw new InvalidOperationException("Remote Inspector bindings require an access token of at least 32 characters.");
        }

        ArgumentNullException.ThrowIfNull(OriginPolicy);
        return AccessToken;
    }
}
