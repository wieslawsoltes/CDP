using System;
using System.IO;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Threading;

namespace Avalonia.Diagnostics.Cdp.Domains;

public static class PageDomain
{
    public static async Task<JsonObject> HandleAsync(CdpSession session, string action, JsonObject @params)
    {
        switch (action)
        {
            case "enable":
            case "disable":
                return new JsonObject();

            case "captureScreenshot":
                {
                    string format = @params["format"]?.GetValue<string>() ?? "png";
                    var data = await CaptureScreenshotAsync(session);
                    return new JsonObject { ["data"] = data };
                }

            case "reload":
                {
                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        session.Window.InvalidateMeasure();
                        session.Window.InvalidateArrange();
                        session.Window.InvalidateVisual();
                    });
                    return new JsonObject();
                }

            case "navigate":
                return new JsonObject();

            default:
                throw new Exception($"Method Page.{action} is not implemented");
        }
    }

    private static async Task<string> CaptureScreenshotAsync(CdpSession session)
    {
        return await Dispatcher.UIThread.InvokeAsync(() =>
        {
            var window = session.Window;
            var scale = window.RenderScaling;
            var width = Math.Max(1, (int)(window.Bounds.Width * scale));
            var height = Math.Max(1, (int)(window.Bounds.Height * scale));

            using var bitmap = new RenderTargetBitmap(new PixelSize(width, height), new Vector(96 * scale, 96 * scale));
            bitmap.Render(window);

            using var ms = new MemoryStream();
            bitmap.Save(ms);
            
            return Convert.ToBase64String(ms.ToArray());
        });
    }
}
