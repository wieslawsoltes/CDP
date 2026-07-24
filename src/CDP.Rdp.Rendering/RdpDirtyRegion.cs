namespace CDP.Rdp.Rendering;

using System;
using System.Collections.Generic;
using SkiaSharp;

/// <summary>
/// Manages and optimizes dirty regions for frame repaints, merging overlapping/adjacent rectangles.
/// Aggregates into a total bounding box if rectangle count exceeds threshold (16).
/// </summary>
public sealed class RdpDirtyRegion
{
    private readonly List<SKRectI> _rectangles = new();
    public const int MaxRectanglesBeforeUnion = 16;

    public IReadOnlyList<SKRectI> Rectangles => _rectangles;

    public bool IsEmpty => _rectangles.Count == 0;

    public SKRectI TotalBounds
    {
        get
        {
            if (_rectangles.Count == 0) return SKRectI.Empty;
            SKRectI bounds = _rectangles[0];
            for (int i = 1; i < _rectangles.Count; i++)
            {
                bounds = SKRectI.Union(bounds, _rectangles[i]);
            }
            return bounds;
        }
    }

    public void AddRect(SKRectI rect)
    {
        if (rect.IsEmpty || rect.Width <= 0 || rect.Height <= 0)
            return;

        _rectangles.Add(rect);
        Optimize();
    }

    public void AddRect(int left, int top, int width, int height)
    {
        AddRect(SKRectI.Create(left, top, width, height));
    }

    public void Clear()
    {
        _rectangles.Clear();
    }

    /// <summary>
    /// Merges overlapping and adjacent dirty rectangles.
    /// If count exceeds MaxRectanglesBeforeUnion (16), merges all rectangles into TotalBounds.
    /// </summary>
    public void Optimize()
    {
        if (_rectangles.Count <= 1)
            return;

        if (_rectangles.Count > MaxRectanglesBeforeUnion)
        {
            SKRectI total = TotalBounds;
            _rectangles.Clear();
            _rectangles.Add(total);
            return;
        }

        bool merged = true;
        while (merged && _rectangles.Count > 1)
        {
            merged = false;
            for (int i = 0; i < _rectangles.Count; i++)
            {
                for (int j = i + 1; j < _rectangles.Count; j++)
                {
                    if (ShouldMerge(_rectangles[i], _rectangles[j]))
                    {
                        _rectangles[i] = SKRectI.Union(_rectangles[i], _rectangles[j]);
                        _rectangles.RemoveAt(j);
                        merged = true;
                        break;
                    }
                }
                if (merged) break;
            }
        }
    }

    /// <summary>
    /// Checks if two rectangles overlap or touch along an edge/corner.
    /// </summary>
    public static bool ShouldMerge(SKRectI r1, SKRectI r2)
    {
        bool intersectOrTouchHorizontal = r1.Left <= r2.Right && r1.Right >= r2.Left;
        bool intersectOrTouchVertical = r1.Top <= r2.Bottom && r1.Bottom >= r2.Top;

        return intersectOrTouchHorizontal && intersectOrTouchVertical;
    }

    public RdpDirtyRegion Clone()
    {
        var clone = new RdpDirtyRegion();
        clone._rectangles.AddRange(_rectangles);
        return clone;
    }
}
