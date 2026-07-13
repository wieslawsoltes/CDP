using System.Collections.Generic;

namespace CdpInspectorApp.Models;

public class DominatorNodeModel
{
    public string TypeName { get; set; } = "";
    public long RetainedSize { get; set; }
    public double RetainedPct { get; set; }
    public int InstanceCount { get; set; }
    public List<DominatorNodeModel> Children { get; set; } = new();
}
