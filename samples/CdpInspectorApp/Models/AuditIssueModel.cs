namespace CdpInspectorApp.Models;

public class AuditIssueModel
{
    public string Category { get; set; } = "";
    public string Severity { get; set; } = "";
    public int NodeId { get; set; }
    public string ControlType { get; set; } = "";
    public string Message { get; set; } = "";

    public string CategoryBrush
    {
        get
        {
            if (Category == "Accessibility") return "#fdd663"; // yellow
            if (Category == "Layout") return "#f28b82";        // red
            return "#8ab4f8";                                  // blue
        }
    }
}
