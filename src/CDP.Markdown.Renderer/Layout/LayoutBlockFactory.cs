namespace CDP.Markdown.Renderer.Layout;

using System;
using CDP.Markdown.Parser.AST;

public static class LayoutBlockFactory
{
    public static ILayoutBlock Create(MarkdownBlock block)
    {
        return block switch
        {
            ParagraphBlock paragraph => new ParagraphLayoutBlock(paragraph),
            HeadingBlock heading => new HeadingLayoutBlock(heading),
            ListBlock list => new ListLayoutBlock(list),
            ListItemBlock listItem => new ListItemLayoutBlock(listItem),
            QuoteBlock quote => new QuoteLayoutBlock(quote),
            CodeBlock code => new CodeLayoutBlock(code),
            ThematicBreakBlock thematicBreak => new ThematicBreakLayoutBlock(thematicBreak),
            TableBlock table => new TableLayoutBlock(table),
            TableRowBlock row => new TableRowLayoutBlock(row),
            HtmlBlock html => new HtmlLayoutBlock(html),
            _ => throw new NotSupportedException($"Markdown Block type '{block.GetType().Name}' is not supported.")
        };
    }
}
