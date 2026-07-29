namespace CDP.Rdp.Session;

using System;
using CDP.Rdp.Protocol;

public sealed record RdpSessionOptions
{
    public string Host { get; init; } = "127.0.0.1";
    public int Port { get; init; } = 3389;
    public string? Username { get; init; }
    public string? Password { get; init; }
    public string? Domain { get; init; }
    public ushort Width { get; init; } = 1920;
    public ushort Height { get; init; } = 1080;
    public ushort ColorDepth { get; init; } = 32;
    public RdpSecurityProtocol RequestedProtocols { get; init; } = RdpSecurityProtocol.Ssl | RdpSecurityProtocol.Hybrid;
    public bool EnableFastPath { get; init; } = true;
    public TimeSpan ConnectTimeout { get; init; } = TimeSpan.FromSeconds(15);
    public System.Net.Security.RemoteCertificateValidationCallback? ServerCertificateValidationCallback { get; init; }
    public bool AcceptUntrustedCertificates { get; init; }
}
