using System.Collections.Generic;

namespace CDP.Markdown.Parser.AST;

public readonly record struct SourceSpan(int Start, int Length);

public abstract class MarkdownNode
{
    public MarkdownNode? Parent { get; set; }
    public List<MarkdownNode> Children { get; } = new();
    public SourceSpan Span { get; set; }
}

public abstract class MarkdownBlock : MarkdownNode { }
public abstract class MarkdownInline : MarkdownNode { }

public class MarkdownDocument : MarkdownBlock { }
public class ParagraphBlock : MarkdownBlock { }
public class HeadingBlock : MarkdownBlock
{
    public int Level { get; set; } // 1 - 6
}
public class ListBlock : MarkdownBlock
{
    public bool IsOrdered { get; set; }
    public int StartIndex { get; set; } = 1;
    public string BulletChar { get; set; } = "-";
}
public class ListItemBlock : MarkdownBlock
{
    public bool? IsChecked { get; set; } // GFM checklist: true = [x], false = [ ], null = standard bullet
}
public class CodeBlock : MarkdownBlock
{
    public string? Language { get; set; }
    public string Code { get; set; } = string.Empty;
    public bool IsFenced { get; set; } = true;
}
public class HtmlBlock : MarkdownBlock
{
    public string Html { get; set; } = string.Empty;
}
public class QuoteBlock : MarkdownBlock { }
public class TableBlock : MarkdownBlock { }
public class TableRowBlock : MarkdownBlock
{
    public bool IsHeader { get; set; }
}
public class TableCellBlock : MarkdownBlock
{
    public TableCellAlignment Alignment { get; set; }
}
public enum TableCellAlignment { None, Left, Center, Right }
public class ThematicBreakBlock : MarkdownBlock { }

public class LiteralInline : MarkdownInline
{
    public string Text { get; set; } = string.Empty;
    public bool IsHtml { get; set; }
}
public class EmphasisInline : MarkdownInline
{
    public bool IsStrong { get; set; } // true = strong (bold), false = emphasis (italic)
}
public class StrikeThroughInline : MarkdownInline { }
public class CodeInline : MarkdownInline
{
    public string Code { get; set; } = string.Empty;
}
public class LinkInline : MarkdownInline
{
    public string Url { get; set; } = string.Empty;
    public string? Title { get; set; }
}
public class ImageInline : MarkdownInline
{
    public string Url { get; set; } = string.Empty;
    public string? AltText { get; set; }
}
public class LineBreakInline : MarkdownInline
{
    public bool IsHard { get; set; }
}
