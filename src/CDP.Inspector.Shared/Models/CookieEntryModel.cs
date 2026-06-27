namespace CdpInspectorApp.Models;

public class CookieEntryModel
{
    public string Name { get; set; } = "";
    public string Value { get; set; } = "";
    public string Domain { get; set; } = "";
    public string Path { get; set; } = "";
    public double Expires { get; set; } = -1;
}
