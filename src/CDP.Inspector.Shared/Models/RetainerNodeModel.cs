using System.Collections.Generic;

namespace CdpInspectorApp.Models;

public class RetainerNodeModel
{
    public string Name { get; set; } = "";
    public string Type { get; set; } = "";
    public int HashCode { get; set; }
    public List<RetainerNodeModel> Retainers { get; set; } = new();
}
