using System;
using System.Collections.Generic;
using CDP.Markdown.Parser.AST;

namespace CDP.Markdown.Parser;

public static class MarkdownSerializer
{
    public static string Serialize(MarkdownNode node)
    {
        return SerializeNode(node, inTable: false);
    }

    private static string SerializeNode(MarkdownNode node, bool inTable)
    {
        if (node == null) return string.Empty;

        return node switch
        {
            MarkdownDocument doc => SerializeDocument(doc),
            ParagraphBlock p => SerializeParagraph(p, inTable),
            HeadingBlock h => SerializeHeading(h),
            ListBlock l => SerializeList(l),
            ListItemBlock li => SerializeListItem(li),
            QuoteBlock q => SerializeQuote(q),
            CodeBlock cb => SerializeCodeBlock(cb),
            HtmlBlock hb => hb.Html,
            TableBlock t => SerializeTable(t),
            TableRowBlock tr => SerializeTableRow(tr),
            TableCellBlock tc => SerializeTableCell(tc),
            ThematicBreakBlock => "---\n",
            LiteralInline lit => EscapeLiteralText(lit.Text, lit.IsHtml, inTable),
            EmphasisInline emp => (emp.IsStrong ? "**" : "*") + SerializeChildren(emp, inTable) + (emp.IsStrong ? "**" : "*"),
            StrikeThroughInline st => "~~" + SerializeChildren(st, inTable) + "~~",
            CodeInline ci => "`" + ci.Code + "`",
            LinkInline link => "[" + SerializeChildren(link, inTable) + "](" + link.Url + (string.IsNullOrEmpty(link.Title) ? "" : $" \"{link.Title.Replace("\"", "\\\"")}\"") + ")",
            ImageInline img => $"![{img.AltText ?? ""}]({img.Url})",
            LineBreakInline lb => lb.IsHard ? "  \n" : "\n",
            _ => SerializeChildren(node, inTable)
        };
    }

    private static string EscapeLiteralText(string text, bool isHtml, bool inTable)
    {
        if (isHtml) return text;
        if (string.IsNullOrEmpty(text)) return string.Empty;
        if (!inTable) return text;
        // Inside table cells, pipe characters must be escaped to prevent
        // them from being interpreted as cell separators on re-parse.
        return text.Replace("|", "\\|");
    }

    private static string SerializeChildren(MarkdownNode parent, bool inTable = false)
    {
        var sb = new System.Text.StringBuilder();
        foreach (var child in parent.Children)
        {
            sb.Append(SerializeNode(child, inTable));
        }
        return sb.ToString();
    }

    private static string SerializeDocument(MarkdownDocument doc)
    {
        var blocks = new List<string>();
        foreach (var child in doc.Children)
        {
            var s = Serialize(child).TrimEnd('\n');
            if (!string.IsNullOrEmpty(s))
            {
                blocks.Add(s);
            }
        }
        return string.Join("\n\n", blocks) + "\n";
    }

    private static string SerializeParagraph(ParagraphBlock p, bool inTable = false)
    {
        return SerializeChildren(p, inTable);
    }

    private static string SerializeHeading(HeadingBlock h)
    {
        var hashes = new string('#', Math.Clamp(h.Level, 1, 6));
        return hashes + " " + SerializeChildren(h);
    }

    private static string GetCodeBlockFence(string code)
    {
        int maxBackticks = 0;
        int currentBackticks = 0;
        foreach (var c in code)
        {
            if (c == '`')
            {
                currentBackticks++;
                if (currentBackticks > maxBackticks)
                {
                    maxBackticks = currentBackticks;
                }
            }
            else
            {
                currentBackticks = 0;
            }
        }
        if (maxBackticks >= 3)
        {
            return new string('`', maxBackticks + 1);
        }
        return "```";
    }

    private static string SerializeCodeBlock(CodeBlock cb)
    {
        var lang = cb.Language ?? "";
        var code = cb.Code ?? string.Empty;
        if (!code.EndsWith('\n') && code.Length > 0)
        {
            code += "\n";
        }
        if (!cb.IsFenced)
        {
            var lines = code.Split('\n');
            var indentedLines = new List<string>();
            for (int i = 0; i < lines.Length; i++)
            {
                if (i == lines.Length - 1 && string.IsNullOrEmpty(lines[i]))
                {
                    break;
                }
                indentedLines.Add(string.IsNullOrEmpty(lines[i]) ? "" : "    " + lines[i]);
            }
            return string.Join("\n", indentedLines) + "\n";
        }
        var fence = GetCodeBlockFence(code);
        return $"{fence}{lang}\n{code}{fence}";
    }

    private static string SerializeQuote(QuoteBlock q)
    {
        var blocks = new List<string>();
        foreach (var child in q.Children)
        {
            var s = Serialize(child).TrimEnd('\n');
            if (!string.IsNullOrEmpty(s))
            {
                blocks.Add(s);
            }
        }
        var content = string.Join("\n\n", blocks);
        var lines = content.Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            lines[i] = lines[i].Length > 0 ? "> " + lines[i] : ">";
        }
        return string.Join("\n", lines);
    }

    private static string SerializeList(ListBlock l)
    {
        var items = new List<string>();
        for (int i = 0; i < l.Children.Count; i++)
        {
            var child = l.Children[i];
            if (child is ListItemBlock li)
            {
                string prefix;
                if (l.IsOrdered)
                {
                    prefix = $"{l.StartIndex + i}. ";
                }
                else
                {
                    prefix = $"{l.BulletChar} ";
                }

                if (li.IsChecked.HasValue)
                {
                    prefix += li.IsChecked.Value ? "[x] " : "[ ] ";
                }

                var itemContent = SerializeListItem(li).TrimEnd('\n');
                var lines = itemContent.Split('\n');
                var sb = new System.Text.StringBuilder();
                sb.Append(prefix);
                sb.Append(lines[0]);

                var indent = new string(' ', prefix.Length);
                for (int j = 1; j < lines.Length; j++)
                {
                    sb.Append('\n');
                    if (lines[j].Length > 0)
                    {
                        sb.Append(indent);
                        sb.Append(lines[j]);
                    }
                }
                items.Add(sb.ToString());
            }
        }
        return string.Join("\n", items);
    }

    private static string SerializeListItem(ListItemBlock li)
    {
        var parts = new List<string>();
        for (int i = 0; i < li.Children.Count; i++)
        {
            var child = li.Children[i];
            var s = Serialize(child);
            if (child is MarkdownBlock)
            {
                s = s.TrimEnd('\n');
            }
            parts.Add(s);
        }

        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < parts.Count; i++)
        {
            if (i > 0)
            {
                var prev = li.Children[i - 1];
                var curr = li.Children[i];
                if (prev is ListBlock || curr is ListBlock)
                {
                    sb.Append('\n');
                }
                else
                {
                    sb.Append("\n\n");
                }
            }
            sb.Append(parts[i]);
        }
        return sb.ToString();
    }

    private static string SerializeTable(TableBlock t)
    {
        if (t.Children.Count == 0) return string.Empty;

        var rows = new List<string>();
        var firstRow = t.Children[0] as TableRowBlock;
        int colCount = firstRow?.Children.Count ?? 0;
        var alignments = new List<TableCellAlignment>();
        for (int i = 0; i < colCount; i++)
        {
            var cell = firstRow!.Children[i] as TableCellBlock;
            alignments.Add(cell?.Alignment ?? TableCellAlignment.None);
        }

        rows.Add(Serialize(t.Children[0]));

        var delimiters = new List<string>();
        foreach (var align in alignments)
        {
            var delim = align switch
            {
                TableCellAlignment.Left => ":---",
                TableCellAlignment.Center => ":---:",
                TableCellAlignment.Right => "---:",
                _ => "---"
            };
            delimiters.Add(delim);
        }
        rows.Add("| " + string.Join(" | ", delimiters) + " |");

        for (int i = 1; i < t.Children.Count; i++)
        {
            rows.Add(Serialize(t.Children[i]));
        }

        return string.Join("\n", rows);
    }

    private static string SerializeTableRow(TableRowBlock tr)
    {
        var cells = new List<string>();
        foreach (var child in tr.Children)
        {
            cells.Add(Serialize(child));
        }
        return "| " + string.Join(" | ", cells) + " |";
    }

    private static string SerializeTableCell(TableCellBlock tc)
    {
        return SerializeChildren(tc, inTable: true);
    }
}
