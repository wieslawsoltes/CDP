using System.Collections.Generic;

namespace CDP.Html.Parser;

public readonly record struct SourceSpan(int Start, int Length);

public abstract class HtmlNode
{
    public HtmlNode? Parent { get; set; }
    
    private List<HtmlNode>? _children;
    public List<HtmlNode> Children => _children ??= new();
    
    public SourceSpan Span { get; set; }
}

public class HtmlElement : HtmlNode
{
    public string TagName { get; set; } = string.Empty;
    
    private Dictionary<string, string>? _attributes;
    public Dictionary<string, string> Attributes => _attributes ??= new(System.StringComparer.OrdinalIgnoreCase);
}

public class HtmlTextNode : HtmlNode
{
    public string Text { get; set; } = string.Empty;
}

public class HtmlDocument : HtmlNode
{
}

