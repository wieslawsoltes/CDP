using System;
using AvaloniaEdit.Document;

namespace XamlPlayground.Editor.Minimap.Inline;

public static class EditorInlineLayout
{
    public static int GetInsertionLineNumber(TextDocument document, int lineNumber, EditorInlinePlacement placement)
    {
        if (document is null)
        {
            return 1;
        }

        var clampedLine = Math.Clamp(lineNumber, 1, Math.Max(1, document.LineCount));
        return placement == EditorInlinePlacement.AfterLine && clampedLine < document.LineCount
            ? clampedLine + 1
            : clampedLine;
    }

    public static int GetInsertionOffset(TextDocument document, int lineNumber, EditorInlinePlacement placement)
    {
        if (document is null || document.LineCount == 0)
        {
            return 0;
        }

        var insertionLine = GetInsertionLineNumber(document, lineNumber, placement);
        if (placement == EditorInlinePlacement.AfterLine && insertionLine == document.LineCount && lineNumber >= document.LineCount)
        {
            return document.TextLength;
        }

        return document.GetLineByNumber(insertionLine).Offset;
    }
}
