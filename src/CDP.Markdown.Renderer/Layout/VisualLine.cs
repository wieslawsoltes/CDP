namespace CDP.Markdown.Renderer.Layout;

using System;
using System.Collections.Generic;
using SkiaSharp;

public class VisualLine : IDisposable
{
    public List<VisualTextRun> Runs { get; } = new();
    public SKRect Bounds { get; set; } // Bounds relative to the block top-left
    
    public int StartOffset => Runs.Count > 0 ? Runs[0].Span.Start : 0;
    public int EndOffset => Runs.Count > 0 ? Runs[^1].Span.Start + Runs[^1].Span.Length : 0;

    public void Dispose()
    {
        foreach (var run in Runs)
        {
            run.Dispose();
        }
        Runs.Clear();
    }
}
