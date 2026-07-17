using System.Collections.Generic;
using CDP.Html.Renderer.Style;

namespace CDP.Html.Renderer.Layout;

public struct LineFragment
{
    public LayoutBox Box { get; set; }
    public string? Text { get; set; } // Null if it's an inline-block/element, non-null if text run
    public float Width { get; set; }
    public float Height { get; set; }
    public float BaselineOffset { get; set; } // Distance from top of fragment to baseline
    public float X { get; set; } // Relative X within the line box
    public float Y { get; set; } // Relative Y within the line box
}

public class LineBox
{
    public List<LineFragment> Fragments { get; } = new();
    public float Width { get; set; }
    public float Height { get; set; }
    public float Baseline { get; set; } // Distance from top of line box to line baseline
    public float LineLeft { get; set; }
    public float AvailableWidth { get; set; }
}
