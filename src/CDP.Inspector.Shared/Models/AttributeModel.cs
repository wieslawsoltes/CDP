namespace CdpInspectorApp.Models;

public class AttributeModel
{
    public string Name { get; }
    public string Value { get; set; }

    public AttributeModel(string name, string val)
    {
        Name = name;
        Value = val;
    }
}
