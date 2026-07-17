namespace CDP.Markdown.Renderer.Rendering;

using SkiaSharp;

public class RenderContext
{
    public RenderResources Resources { get; }
    public SKRect Viewport { get; }
    public System.Action? OnImageLoaded { get; set; }

    public RenderContext(RenderResources resources, SKRect viewport)
    {
        Resources = resources;
        Viewport = viewport;
    }
}
