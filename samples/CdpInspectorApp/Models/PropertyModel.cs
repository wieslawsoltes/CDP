namespace CdpInspectorApp.Models;

public class PropertyModel
{
    public string Name { get; }
    public string Value { get; set; }
    public string Type { get; }

    public PropertyModel(string name, string val, string type)
    {
        Name = name;
        Value = val;
        Type = type;
    }
}
