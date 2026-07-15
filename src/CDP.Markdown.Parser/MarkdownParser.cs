using System;
using System.Collections;
using System.Collections.Generic;
using Markdig;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using CDP.Markdown.Parser.AST;

namespace CDP.Markdown.Parser;

public static class MarkdownParser
{
    public static AST.MarkdownDocument Parse(string markdown)
    {
        if (markdown == null) throw new ArgumentNullException(nameof(markdown));

        var pipeline = new MarkdownPipelineBuilder()
            .UsePipeTables()
            .UseTaskLists()
            .UseEmphasisExtras()
            .UsePreciseSourceLocation()
            .Build();

        var doc = Markdig.Markdown.Parse(markdown, pipeline);
        var myDoc = (AST.MarkdownDocument)MapBlock(doc)!;
        return myDoc;
    }

    private static AST.SourceSpan MapSpan(Markdig.Syntax.SourceSpan span)
    {
        if (span.IsEmpty) return new AST.SourceSpan(0, 0);
        int length = Math.Max(0, span.End - span.Start + 1);
        return new AST.SourceSpan(span.Start, length);
    }

    private static AST.MarkdownNode? MapBlock(Block block)
    {
        AST.MarkdownNode? node = block switch
        {
            Markdig.Syntax.MarkdownDocument => new AST.MarkdownDocument(),
            Markdig.Syntax.ParagraphBlock => new AST.ParagraphBlock(),
            Markdig.Syntax.HeadingBlock h => new AST.HeadingBlock { Level = h.Level },
            Markdig.Syntax.ListBlock l => MapList(l),
            Markdig.Syntax.ListItemBlock li => MapListItem(li),
            Markdig.Syntax.QuoteBlock => new AST.QuoteBlock(),
            Markdig.Syntax.ThematicBreakBlock => new AST.ThematicBreakBlock(),
            Markdig.Extensions.Tables.Table => new AST.TableBlock(),
            Markdig.Extensions.Tables.TableRow tr => new AST.TableRowBlock { IsHeader = tr.IsHeader },
            Markdig.Extensions.Tables.TableCell tc => MapTableCell(tc),
            Markdig.Syntax.CodeBlock cb => MapCodeBlock(cb),
            Markdig.Syntax.HtmlBlock hb => new AST.HtmlBlock { Html = hb.Lines.ToString() },
            _ => null
        };

        if (node != null)
        {
            node.Span = MapSpan(block.Span);
            if (block is ContainerBlock cb)
            {
                foreach (var child in cb)
                {
                    var mappedChild = MapBlock(child);
                    if (mappedChild != null)
                    {
                        mappedChild.Parent = node;
                        node.Children.Add(mappedChild);
                    }
                }
            }
            else if (block is LeafBlock lb && lb.Inline != null)
            {
                MapInlines(lb.Inline, node);
            }
        }

        return node;
    }

    private static AST.ListBlock MapList(Markdig.Syntax.ListBlock l)
    {
        int startIdx = 1;
        if (!string.IsNullOrEmpty(l.OrderedStart))
        {
            int.TryParse(l.OrderedStart, out startIdx);
            if (startIdx <= 0) startIdx = 1;
        }
        return new AST.ListBlock
        {
            IsOrdered = l.IsOrdered,
            StartIndex = startIdx,
            BulletChar = l.BulletType != '\0' ? l.BulletType.ToString() : "-"
        };
    }

    private static AST.ListItemBlock MapListItem(Markdig.Syntax.ListItemBlock li)
    {
        var item = new AST.ListItemBlock();
        bool? isChecked = null;
        var taskList = FindTaskListInline(li);
        if (taskList != null)
        {
            isChecked = taskList.Checked;
        }
        item.IsChecked = isChecked;
        return item;
    }

    private static Markdig.Extensions.TaskLists.TaskList? FindTaskListInline(ContainerBlock cb)
    {
        foreach (var child in cb)
        {
            if (child is LeafBlock lb && lb.Inline != null)
            {
                var taskList = FindTaskListInlineInInline(lb.Inline);
                if (taskList != null) return taskList;
            }
            else if (child is ContainerBlock innerCb)
            {
                var taskList = FindTaskListInline(innerCb);
                if (taskList != null) return taskList;
            }
        }
        return null;
    }

    private static Markdig.Extensions.TaskLists.TaskList? FindTaskListInlineInInline(Inline inline)
    {
        var current = inline;
        while (current != null)
        {
            if (current is Markdig.Extensions.TaskLists.TaskList tl)
            {
                return tl;
            }
            if (current is ContainerInline ci && ci.FirstChild != null)
            {
                var innerTl = FindTaskListInlineInInline(ci.FirstChild);
                if (innerTl != null) return innerTl;
            }
            current = current.NextSibling;
        }
        return null;
    }

    private static AST.TableCellBlock MapTableCell(Markdig.Extensions.Tables.TableCell tc)
    {
        var cell = new AST.TableCellBlock();
        if (tc.Parent is Markdig.Extensions.Tables.TableRow tr && tr.Parent is Markdig.Extensions.Tables.Table t)
        {
            int cellIndex = tr.IndexOf(tc);
            if (cellIndex >= 0 && cellIndex < t.ColumnDefinitions.Count)
            {
                var align = t.ColumnDefinitions[cellIndex].Alignment;
                cell.Alignment = align switch
                {
                    Markdig.Extensions.Tables.TableColumnAlign.Left => AST.TableCellAlignment.Left,
                    Markdig.Extensions.Tables.TableColumnAlign.Center => AST.TableCellAlignment.Center,
                    Markdig.Extensions.Tables.TableColumnAlign.Right => AST.TableCellAlignment.Right,
                    _ => AST.TableCellAlignment.None
                };
            }
        }
        return cell;
    }

    private static AST.CodeBlock MapCodeBlock(Markdig.Syntax.CodeBlock cb)
    {
        string? lang = null;
        bool isFenced = cb is Markdig.Syntax.FencedCodeBlock;
        if (cb is Markdig.Syntax.FencedCodeBlock fenced)
        {
            lang = fenced.Info;
        }
        return new AST.CodeBlock
        {
            Language = lang,
            Code = cb.Lines.ToString(),
            IsFenced = isFenced
        };
    }

    private static void MapInlines(ContainerInline container, AST.MarkdownNode parentNode)
    {
        var current = container.FirstChild;
        bool skipLeadingSpace = false;
        while (current != null)
        {
            if (current is Markdig.Extensions.TaskLists.TaskList)
            {
                skipLeadingSpace = true;
                current = current.NextSibling;
                continue;
            }

            var mappedInline = MapInline(current);
            if (mappedInline != null)
            {
                if (skipLeadingSpace)
                {
                    StripFirstLeadingSpace(mappedInline);
                    skipLeadingSpace = false;
                }
                mappedInline.Parent = parentNode;
                parentNode.Children.Add(mappedInline);
            }
            else if (current is ContainerInline ci)
            {
                MapInlines(ci, parentNode);
            }
            current = current.NextSibling;
        }
    }

    private static void StripFirstLeadingSpace(AST.MarkdownInline node)
    {
        if (node is AST.LiteralInline lit)
        {
            if (lit.Text.StartsWith(' '))
            {
                lit.Text = lit.Text.Substring(1);
            }
        }
        else if (node.Children.Count > 0 && node.Children[0] is AST.MarkdownInline child)
        {
            StripFirstLeadingSpace(child);
        }
    }

    private static AST.MarkdownInline? MapInline(Inline inline)
    {
        AST.MarkdownInline? node = inline switch
        {
            Markdig.Syntax.Inlines.HtmlEntityInline entity => new AST.LiteralInline { Text = entity.Original.ToString(), IsHtml = true },
            Markdig.Syntax.Inlines.LiteralInline lit => new AST.LiteralInline { Text = lit.Content.ToString() },
            Markdig.Syntax.Inlines.EmphasisInline emp => MapEmphasisOrStrikethrough(emp),
            Markdig.Syntax.Inlines.LinkDelimiterInline linkDelim => new AST.LiteralInline { Text = linkDelim.IsImage ? "![" : "[" },
            Markdig.Syntax.Inlines.DelimiterInline delim => new AST.LiteralInline { Text = "" },
            Markdig.Syntax.Inlines.HtmlInline html => new AST.LiteralInline { Text = html.Tag, IsHtml = true },
            Markdig.Syntax.Inlines.CodeInline code => new AST.CodeInline { Code = code.Content },
            Markdig.Syntax.Inlines.LinkInline link => MapLinkOrImage(link),
            Markdig.Syntax.Inlines.LineBreakInline lb => new AST.LineBreakInline { IsHard = lb.IsHard },
            _ => null
        };

        if (node != null)
        {
            node.Span = MapSpan(inline.Span);
            if (inline is ContainerInline ci && ci.FirstChild != null && !(inline is Markdig.Syntax.Inlines.LinkInline l && l.IsImage))
            {
                MapInlines(ci, node);
            }
        }

        return node;
    }

    private static AST.MarkdownInline MapEmphasisOrStrikethrough(Markdig.Syntax.Inlines.EmphasisInline emp)
    {
        if (emp.DelimiterChar == '~' && emp.DelimiterCount == 2)
        {
            return new AST.StrikeThroughInline();
        }
        return new AST.EmphasisInline { IsStrong = emp.DelimiterCount == 2 };
    }

    private static AST.MarkdownInline MapLinkOrImage(Markdig.Syntax.Inlines.LinkInline link)
    {
        if (link.IsImage)
        {
            return new AST.ImageInline
            {
                Url = link.Url ?? string.Empty,
                AltText = GetInnerText(link.FirstChild)
            };
        }
        return new AST.LinkInline
        {
            Url = link.Url ?? string.Empty,
            Title = link.Title
        };
    }

    private static string GetInnerText(Inline? inline)
    {
        if (inline == null) return string.Empty;
        var sb = new System.Text.StringBuilder();
        AppendInnerText(inline, sb);
        return sb.ToString();
    }

    private static void AppendInnerText(Inline? inline, System.Text.StringBuilder sb)
    {
        var current = inline;
        while (current != null)
        {
            if (current is Markdig.Syntax.Inlines.LiteralInline lit)
            {
                sb.Append(lit.Content.ToString());
            }
            else if (current is Markdig.Syntax.Inlines.CodeInline code)
            {
                sb.Append(code.Content);
            }
            else if (current is ContainerInline ci)
            {
                AppendInnerText(ci.FirstChild, sb);
            }
            current = current.NextSibling;
        }
    }
}
