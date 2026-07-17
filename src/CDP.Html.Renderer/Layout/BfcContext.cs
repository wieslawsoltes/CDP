using System;
using System.Collections.Generic;
using System.Linq;
using SkiaSharp;
using CDP.Html.Renderer.Style;

namespace CDP.Html.Renderer.Layout;

public class BfcContext
{
    public LayoutBox BfcParent { get; }
    public List<SKRect> LeftFloats { get; } = new();
    public List<SKRect> RightFloats { get; } = new();

    public BfcContext(LayoutBox bfcParent)
    {
        BfcParent = bfcParent;
    }

    public float GetClearY(ClearType clearType)
    {
        float maxBottom = 0f;
        if (clearType == ClearType.Left || clearType == ClearType.Both)
        {
            if (LeftFloats.Count > 0)
            {
                maxBottom = Math.Max(maxBottom, LeftFloats.Max(r => r.Bottom));
            }
        }
        if (clearType == ClearType.Right || clearType == ClearType.Both)
        {
            if (RightFloats.Count > 0)
            {
                maxBottom = Math.Max(maxBottom, RightFloats.Max(r => r.Bottom));
            }
        }
        return maxBottom;
    }

    public (float Left, float Right) GetAvailableHorizontalSpan(float y, float height, float parentWidth)
    {
        float left = 0f;
        float right = parentWidth;

        foreach (var rect in LeftFloats)
        {
            if (rect.Bottom > y && rect.Top < y + height)
            {
                left = Math.Max(left, rect.Right);
            }
        }

        foreach (var rect in RightFloats)
        {
            if (rect.Bottom > y && rect.Top < y + height)
            {
                right = Math.Min(right, rect.Left);
            }
        }

        return (left, right);
    }

    public void RegisterFloat(LayoutBox child, float relX, float relY)
    {
        float left = relX - child.MarginLeft;
        float top = relY - child.MarginTop;
        float right = relX + child.Width + child.MarginRight;
        float bottom = relY + child.Height + child.MarginBottom;

        var rect = new SKRect(left, top, right, bottom);
        if (child.Style.Float == FloatType.Left)
        {
            LeftFloats.Add(rect);
        }
        else if (child.Style.Float == FloatType.Right)
        {
            RightFloats.Add(rect);
        }
    }
}
