using System;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Foundation;

namespace WinUI.Diagnostics.Cdp.Domains;

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
                    if (session.Window?.Content is FrameworkElement fe)
                    {
                        await session.Window.DispatcherQueue.InvokeAsync(() =>
                        {
                            fe.InvalidateMeasure();
                            fe.InvalidateArrange();
                            fe.UpdateLayout();
                        });
                    }
                    return new JsonObject();
                }

            case "bringToFront":
                {
                    if (session.Window != null)
                    {
                        await session.Window.DispatcherQueue.InvokeAsync(() =>
                        {
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

        return await session.Window.DispatcherQueue.InvokeAsync(async () =>
        {
            try
            {
                if (session.Window.Content != null)
                {
                    var rtb = new RenderTargetBitmap();
                    await rtb.RenderAsync(session.Window.Content);
                    
                    var pixelBuffer = await rtb.GetPixelsAsync();
                    byte[] bgraPixels = new byte[pixelBuffer.Length];
                    using (var reader = Windows.Storage.Streams.DataReader.FromBuffer(pixelBuffer))
                    {
                        reader.ReadBytes(bgraPixels);
                    }

                    using var skBitmap = new SkiaSharp.SKBitmap();
                    var info = new SkiaSharp.SKImageInfo(rtb.PixelWidth, rtb.PixelHeight, SkiaSharp.SKColorType.Bgra8888, SkiaSharp.SKAlphaType.Premul);
                    var gcHandle = System.Runtime.InteropServices.GCHandle.Alloc(bgraPixels, System.Runtime.InteropServices.GCHandleType.Pinned);
                    try
                    {
                        skBitmap.InstallPixels(info, gcHandle.AddrOfPinnedObject(), info.RowBytes, delegate { gcHandle.Free(); });
                    }
                    catch
                    {
                        gcHandle.Free();
                        throw;
                    }

                    using var ms = new MemoryStream();
                    using (var wstream = new SkiaSharp.SKManagedWStream(ms))
                    {
                        SkiaSharp.SKPixmap.Encode(wstream, skBitmap, SkiaSharp.SKEncodedImageFormat.Png, 100);
                    }
                    var bytes = ms.ToArray();
                    if (bytes.Length > 0)
                    {
                        return Convert.ToBase64String(bytes);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[CDP] CaptureScreenshotAsync failed, returning fallback mock image: {ex.Message}");
            }

            return "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNkYAAAAAYAAjCB0C8AAAAASUVORK5CYII=";
        }).Unwrap();
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
