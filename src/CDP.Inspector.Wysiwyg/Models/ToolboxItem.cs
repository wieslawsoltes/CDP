namespace CDP.Inspector.Wysiwyg.Models;

/// <summary>
/// Represents a single UI element in the designer toolbox.
/// </summary>
public class ToolboxItem
{
    public string Name { get; }
    public string Category { get; }
    public string TargetPlatform { get; }
    public string DefaultXaml { get; }

    public ToolboxItem(string name, string category, string targetPlatform, string defaultXaml)
    {
        Name = name;
        Category = category;
        TargetPlatform = targetPlatform;
        DefaultXaml = defaultXaml;
    }

    public override string ToString() => Name;
}
