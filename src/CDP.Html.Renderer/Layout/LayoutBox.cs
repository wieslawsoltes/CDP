using System.Collections.Generic;
using CDP.Html.Parser;
using CDP.Html.Renderer.Style;

namespace CDP.Html.Renderer.Layout;

public abstract class LayoutBox
{
    public HtmlNode? Node { get; set; }
    public ComputedStyle Style { get; set; } = new();
    public LayoutBox? Parent { get; set; }
    public List<LayoutBox> Children { get; } = new();
    public List<LineBox> LineBoxes { get; } = new();

    // Cache properties for layout
    public bool NeedsLayout { get; set; } = true;
    public float? LayoutCacheAvailableWidth { get; set; }
    public float? LayoutCacheAvailableHeight { get; set; }

    // Box model coordinates (relative to parent content top-left)
    public float X { get; set; }
    public float Y { get; set; }

    // Dimensions (BorderBox sizes)
    public float Width { get; set; }
    public float Height { get; set; }

    // Resolved margins (in px)
    public float MarginTop { get; set; }
    public float MarginRight { get; set; }
    public float MarginBottom { get; set; }
    public float MarginLeft { get; set; }

    // Resolved paddings (in px)
    public float PaddingTop { get; set; }
    public float PaddingRight { get; set; }
    public float PaddingBottom { get; set; }
    public float PaddingLeft { get; set; }

    // Resolved border widths (in px)
    public float BorderTop { get; set; }
    public float BorderRight { get; set; }
    public float BorderBottom { get; set; }
    public float BorderLeft { get; set; }

    public bool IsAnonymous => Node == null;

    public float ContentWidth => Math.Max(0f, Width - PaddingLeft - PaddingRight - BorderLeft - BorderRight);
    public float ContentHeight => Math.Max(0f, Height - PaddingTop - PaddingBottom - BorderTop - BorderBottom);

    public virtual bool IsBlockLevel => Style.Display == DisplayType.Block || Style.Display == DisplayType.Flex;
    public virtual bool IsInlineLevel => Style.Display == DisplayType.Inline;

    public void AddChild(LayoutBox child)
    {
        child.Parent = this;
        Children.Add(child);
    }
}

public class LayoutBlockBox : LayoutBox
{
    public override bool IsBlockLevel => true;
    public override bool IsInlineLevel => false;
}

public class LayoutInlineBox : LayoutBox
{
    public override bool IsBlockLevel => false;
    public override bool IsInlineLevel => true;
}

public class LayoutFlexBox : LayoutBox
{
    public override bool IsBlockLevel => true;
    public override bool IsInlineLevel => false;
}

public class LayoutTextBox : LayoutBox
{
    public string Text { get; set; } = string.Empty;
    public override bool IsBlockLevel => false;
    public override bool IsInlineLevel => true;
}
