#nullable enable

namespace CdpInspectorApp.ViewModels;

public class ElementPropertyInfo
{
    public string Name { get; set; } = "";
    public string Type { get; set; } = "";
    public string Value { get; set; } = "";

    public override string ToString() => Name;
}
