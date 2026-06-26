using System.Collections.Generic;

namespace CdpInspectorApp.Models;

public class BackgroundServiceMetadataModel
{
    public string Key { get; set; } = "";
    public string Value { get; set; } = "";
}

public class BackgroundServiceEventModel
{
    public double Timestamp { get; set; }
    public string TimestampString { get; set; } = "";
    public string Origin { get; set; } = "";
    public string ServiceWorkerRegistrationId { get; set; } = "";
    public string Service { get; set; } = "";
    public string EventName { get; set; } = "";
    public string InstanceId { get; set; } = "";
    public List<BackgroundServiceMetadataModel> Metadata { get; set; } = new();
}
