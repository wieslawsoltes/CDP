using System;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace Wpf.Diagnostics.Cdp.Domains;

public static class PageDomain
{
    private class PageState
    {
        public bool IsEnabled { get; set; }
    }

    private static readonly ConditionalWeakTable<CdpSession, PageState> _sessionStates = new();

    public static async Task<JsonObject> HandleAsync(CdpSession session, string action, JsonObject @params)
    {
        var frameId = session.Target?.Id ?? "main-frame-id";
        switch (action)
        {
            case "enable":
                {
                    var state = _sessionStates.GetOrCreateValue(session);
                    if (!state.IsEnabled)
                    {
                        state.IsEnabled = true;
                        var port = CdpServer.Port;
                        var host = $"localhost:{port}";
                        var frame = new JsonObject
                        {
                            ["id"] = frameId,
                            ["loaderId"] = "main-loader-id",
                            ["url"] = $"http://{host}/",
                            ["domainAndRegistry"] = "localhost",
                            ["securityOrigin"] = $"http://{host}",
                            ["mimeType"] = "text/html"
                        };
                        var frameNavigatedParams = new JsonObject { ["frame"] = frame };
                        var timestamp = (double)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000.0;
                        _ = session.SendEventAsync("Page.frameNavigated", frameNavigatedParams);
                        _ = session.SendEventAsync("Page.domContentEventFired", new JsonObject { ["timestamp"] = timestamp });
                        _ = session.SendEventAsync("Page.loadEventFired", new JsonObject { ["timestamp"] = timestamp });
                        _ = session.SendEventAsync("Page.frameStoppedLoading", new JsonObject { ["frameId"] = frameId });
                    }
                    return new JsonObject();
                }

            case "disable":
                {
                    if (_sessionStates.TryGetValue(session, out var state))
                    {
                        state.IsEnabled = false;
                    }
                    return new JsonObject();
                }

            case "getResourceTree":
                {
                    var port = CdpServer.Port;
                    var workspaceRoot = FindWorkspaceRoot();
                    var host = $"localhost:{port}";
                    
                    var frame = new JsonObject
                    {
                        ["id"] = frameId,
                        ["loaderId"] = "main-loader-id",
                        ["url"] = $"http://{host}/",
                        ["domainAndRegistry"] = "localhost",
                        ["securityOrigin"] = $"http://{host}",
                        ["mimeType"] = "text/html"
                    };

                    var resourcesArray = new JsonArray();

                    try
                    {
                        await Task.Run(() => ScanResourcesRecursive(workspaceRoot, workspaceRoot, host, resourcesArray));
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error scanning workspace files: {ex}");
                    }

                    var frameTree = new JsonObject
                    {
                        ["frame"] = frame,
                        ["childFrames"] = new JsonArray(),
                        ["resources"] = resourcesArray
                    };

                    return new JsonObject { ["frameTree"] = frameTree };
                }

            case "getResourceContent":
                {
                    string path = @params["url"]?.GetValue<string>() ?? "";
                    if (path.StartsWith("http://") || path.StartsWith("https://"))
                    {
                        try
                        {
                            var uri = new Uri(path);
                            path = uri.LocalPath.TrimStart('/');
                        }
                        catch { }
                    }

                    var workspaceRoot = FindWorkspaceRoot();
                    var fullPath = Path.GetFullPath(Path.Combine(workspaceRoot, path));

                    var relative = Path.GetRelativePath(workspaceRoot, fullPath);
                    if (relative.StartsWith("..") || Path.IsPathRooted(relative))
                    {
                        throw new Exception("Access denied: path traversal detected");
                    }

                    if (File.Exists(fullPath))
                    {
                        var content = await File.ReadAllTextAsync(fullPath);
                        return new JsonObject
                        {
                            ["content"] = content,
                            ["base64Encoded"] = false
                        };
                    }

                    throw new Exception($"Resource file not found: {path}");
                }

            case "captureScreenshot":
                {
                    var data = await CaptureScreenshotAsync(session);
                    return new JsonObject { ["data"] = data };
                }

            case "startScreencast":
                {
                    string format = @params != null && @params.ContainsKey("format") ? @params["format"]?.GetValue<string>() ?? "png" : "png";
                    int? quality = @params != null && @params.ContainsKey("quality") ? @params["quality"]?.GetValue<int>() : null;
                    int? maxWidth = @params != null && @params.ContainsKey("maxWidth") ? @params["maxWidth"]?.GetValue<int>() : null;
                    int? maxHeight = @params != null && @params.ContainsKey("maxHeight") ? @params["maxHeight"]?.GetValue<int>() : null;
                    int? everyNthFrame = @params != null && @params.ContainsKey("everyNthFrame") ? @params["everyNthFrame"]?.GetValue<int>() : null;
                    string? transferMode = @params != null && @params.ContainsKey("transferMode") ? @params["transferMode"]?.GetValue<string>() : null;
                    session.CurrentTargetSession?.StartScreencast(format, quality, maxWidth, maxHeight, everyNthFrame, transferMode);
                    return new JsonObject();
                }

            case "stopScreencast":
                {
                    session.CurrentTargetSession?.StopScreencast();
                    return new JsonObject();
                }

            case "screencastFrameAck":
                {
                    int sessionId = @params["sessionId"]?.GetValue<int>() ?? 0;
                    session.CurrentTargetSession?.AcknowledgeScreencastFrame(sessionId);
                    return new JsonObject();
                }

            case "reload":
                {
                    if (session.Window != null)
                    {
                        await session.Window.Dispatcher.InvokeAsync(() =>
                        {
                            session.Window.InvalidateMeasure();
                            session.Window.InvalidateArrange();
                            session.Window.UpdateLayout();
                        });
                    }
                    return new JsonObject();
                }

            case "bringToFront":
                {
                    if (session.Window != null)
                    {
                        await session.Window.Dispatcher.InvokeAsync(() =>
                        {
                            if (session.Window.WindowState == WindowState.Minimized)
                            {
                                session.Window.WindowState = WindowState.Normal;
                            }
                            session.Window.Activate();
                        });
                    }
                    return new JsonObject();
                }

            default:
                throw new Exception($"Method Page.{action} is not implemented");
        }
    }

    private static async Task<string> CaptureScreenshotAsync(CdpSession session)
    {
        if (session.Window == null) return "";

        return await session.Window.Dispatcher.InvokeAsync(() =>
        {
            try
            {
                var window = session.Window;
                var scale = 1.0;
                var source = PresentationSource.FromVisual(window);
                if (source?.CompositionTarget != null)
                {
                    scale = source.CompositionTarget.TransformToDevice.M11;
                }

                var width = Math.Max(1, (int)(window.ActualWidth * scale));
                var height = Math.Max(1, (int)(window.ActualHeight * scale));

                var rtb = new RenderTargetBitmap(width, height, 96 * scale, 96 * scale, PixelFormats.Pbgra30);
                rtb.Render(window);

                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(rtb));
                using var ms = new MemoryStream();
                encoder.Save(ms);
                
                var bytes = ms.ToArray();
                if (bytes.Length > 0)
                {
                    return Convert.ToBase64String(bytes);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[CDP] CaptureScreenshotAsync failed, returning fallback mock image: {ex.Message}");
            }

            // Return a 100x100 fallback mock PNG base64 string
            try
            {
                using var bitmap = new SkiaSharp.SKBitmap(100, 100);
                using var canvas = new SkiaSharp.SKCanvas(bitmap);
                canvas.Clear(SkiaSharp.SKColors.CornflowerBlue);
                using var image = SkiaSharp.SKImage.FromBitmap(bitmap);
                using var data = image.Encode(SkiaSharp.SKEncodedImageFormat.Png, 100);
                var fallbackBytes = data?.ToArray() ?? Array.Empty<byte>();
                if (fallbackBytes.Length > 0)
                {
                    return Convert.ToBase64String(fallbackBytes);
                }
            }
            catch { }

            return "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNkYAAAAAYAAjCB0C8AAAAASUVORK5CYII=";
        });
    }

    private static string FindWorkspaceRoot()
    {
        var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (dir != null)
        {
            if (dir.GetFiles("*.sln").Any() || dir.GetFiles("*.slnx").Any() || dir.GetDirectories(".git").Any())
            {
                return dir.FullName;
            }
            dir = dir.Parent;
        }
        return Directory.GetCurrentDirectory();
    }

    private static bool ShouldExclude(string path)
    {
        var segments = path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return segments.Any(s => s.Equals("bin", StringComparison.OrdinalIgnoreCase) ||
                                 s.Equals("obj", StringComparison.OrdinalIgnoreCase) ||
                                 s.Equals(".git", StringComparison.OrdinalIgnoreCase) ||
                                 s.Equals(".idea", StringComparison.OrdinalIgnoreCase) ||
                                 s.Equals(".vs", StringComparison.OrdinalIgnoreCase) ||
                                 s.Equals("node_modules", StringComparison.OrdinalIgnoreCase));
    }

    private static void ScanResourcesRecursive(string currentDir, string rootDir, string host, JsonArray resourcesArray)
    {
        if (ShouldExclude(currentDir)) return;

        foreach (var file in Directory.GetFiles(currentDir))
        {
            var relativePath = Path.GetRelativePath(rootDir, file).Replace('\\', '/');
            var ext = Path.GetExtension(file).ToLowerInvariant();
            var mime = ext switch
            {
                ".cs" => "text/plain",
                ".xaml" => "text/xml",
                ".xml" => "text/xml",
                ".json" => "application/json",
                ".png" => "image/png",
                ".jpg" => "image/jpeg",
                ".jpeg" => "image/jpeg",
                _ => "text/plain"
            };

            var type = ext switch
            {
                ".cs" => "Script",
                ".xaml" => "Document",
                ".json" => "Document",
                ".png" => "Image",
                ".jpg" => "Image",
                ".jpeg" => "Image",
                _ => "Other"
            };

            var resource = new JsonObject
            {
                ["url"] = $"http://{host}/{relativePath}",
                ["type"] = type,
                ["mimeType"] = mime,
                ["lastModified"] = (double)new FileInfo(file).LastWriteTimeUtc.Ticks
            };
            resourcesArray.Add(resource);
        }

        foreach (var dir in Directory.GetDirectories(currentDir))
        {
            ScanResourcesRecursive(dir, rootDir, host, resourcesArray);
        }
    }
}
