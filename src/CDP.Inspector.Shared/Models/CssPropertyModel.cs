namespace CdpInspectorApp.Models;

public class CssPropertyModel
{
    public string Name { get; }
    public string Value { get; set; }

    public CssPropertyModel(string name, string val)
    {
        Name = name;
        Value = val;
    }
}
