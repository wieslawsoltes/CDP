using System.Collections.Generic;

namespace CDP.Document.Parser.AST;

public readonly record struct SourceSpan(int Start, int Length);

public abstract class DocumentNode
{
    public DocumentNode? Parent { get; set; }
    public List<DocumentNode> Children { get; } = new();
    public SourceSpan Span { get; set; }

    public void AddChild(DocumentNode child)
    {
        child.Parent = this;
        Children.Add(child);
    }
}

public abstract class DocumentRoot : DocumentNode { }

public class WordDocument : DocumentRoot
{
    public string? Header { get; set; }
    public string? Footer { get; set; }
}

public class SpreadsheetDocument : DocumentRoot { }

public class PresentationDocument : DocumentRoot
{
    public List<SlideMasterNode> Masters { get; } = new();
}

public class SlideMasterNode : DocumentNode
{
    public string? Name { get; set; }
    public string? BackgroundColor { get; set; }
}

// Word processing blocks / inlines
public class SectionBlock : DocumentNode { }

public class ParagraphBlock : DocumentNode
{
    public bool IsBullet { get; set; }
    public int BulletLevel { get; set; }
    public string? BulletStyle { get; set; }
}

public class TableBlock : DocumentNode { }

public class TableRowBlock : DocumentNode { }

public class TableCellBlock : DocumentNode { }

public class TextRun : DocumentNode
{
    public string Text { get; set; } = string.Empty;
    public double? FontSize { get; set; }
    public bool Bold { get; set; }
    public bool Italic { get; set; }
    public bool Underline { get; set; }
    public string? Color { get; set; }
}

public class ImageInline : DocumentNode
{
    public string? Source { get; set; }
    public string? AltText { get; set; }
}

public class LineBreakInline : DocumentNode { }

// Spreadsheet nodes
public class WorksheetNode : DocumentNode
{
    public string Name { get; set; } = string.Empty;
    public List<string> MergedCellRanges { get; } = new();
}

public class GridRowNode : DocumentNode
{
    public int RowIndex { get; set; }
}

public class GridCellNode : DocumentNode
{
    public int ColumnIndex { get; set; }
    public string? Formula { get; set; }
    public object? Value { get; set; }
    public string DisplayText { get; set; } = string.Empty;
    public string? Style { get; set; } // style name or string representation
    public bool Bold { get; set; }
    public bool Italic { get; set; }
    public double? FontSize { get; set; }
    public string? Color { get; set; }
    public int RowSpan { get; set; } = 1;
    public int ColumnSpan { get; set; } = 1;
    public bool IsMerged { get; set; }
}

// Slide nodes
public class SlideNode : DocumentNode
{
    public int SlideIndex { get; set; }
    public string? Title { get; set; }
    public string? MasterName { get; set; }
}

public class ShapeNode : DocumentNode
{
    public double? X { get; set; }
    public double? Y { get; set; }
    public double? Width { get; set; }
    public double? Height { get; set; }
    public string? ShapeType { get; set; }
    public string? Text { get; set; }
    public string? ImageSource { get; set; }
    public bool? Bold { get; set; }
    public bool? Italic { get; set; }
    public double? FontSize { get; set; }
    public string? Color { get; set; }
}

public class GroupNode : DocumentNode
{
    public double? X { get; set; }
    public double? Y { get; set; }
    public double? Width { get; set; }
    public double? Height { get; set; }
}

