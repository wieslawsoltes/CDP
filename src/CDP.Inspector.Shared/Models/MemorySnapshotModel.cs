using System;
using System.Collections.Generic;

namespace CdpInspectorApp.Models;

public class MemorySnapshotModel
{
    public string Name { get; set; } = "";
    public DateTime Timestamp { get; set; }
    public List<ControlCountModel> Entries { get; set; } = new();
}
