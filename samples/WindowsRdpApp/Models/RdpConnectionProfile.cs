using System;

namespace WindowsRdpApp.Models;

public class RdpConnectionProfile
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public string Host { get; set; } = "127.0.0.1";
    public int Port { get; set; } = 3389;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string Domain { get; set; } = string.Empty;
    public int Width { get; set; } = 1920;
    public int Height { get; set; } = 1080;
    public int ColorDepth { get; set; } = 32;
    public bool IsAutoConnect { get; set; }
    public DateTime? LastConnected { get; set; }

    public string DisplaySubtitle => $"{Username}@{Host}:{Port}";
}
