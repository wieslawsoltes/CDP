using System;
using CDP.Editor.Splits.Controls;

namespace CDP.Editor.Splits.Models;

public static class SuperSplitDragManager
{
    public static bool IsDragging { get; set; }
    public static BoxNode? SourceNode { get; set; }
    public static BoxTabNode? SourceTab { get; set; }
    public static SuperSplit? SourceSplit { get; set; }
    public static BoxNode? DraggedNode { get; set; }
    public static bool IsOverDropTarget { get; set; }
    public static string? SourceDockGroup { get; set; }
    public static Action<SuperSplit, BoxNode>? FloatNodeCallback { get; set; }

    public static void Reset()
    {
        IsDragging = false;
        SourceNode = null;
        SourceTab = null;
        SourceSplit = null;
        DraggedNode = null;
        IsOverDropTarget = false;
        SourceDockGroup = null;
    }
}
