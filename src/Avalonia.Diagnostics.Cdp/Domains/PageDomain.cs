using System;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
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

            case "getResourceTree":
                {
                    var port = CdpServer.Port;
                    var workspaceRoot = FindWorkspaceRoot();
                    var host = $"127.0.0.1:{port}";
                    
                    var frame = new JsonObject
                    {
                        ["id"] = "main-frame-id",
                        ["loaderId"] = "main-loader-id",
                        ["url"] = $"http://{host}/",
                        ["domainAndRegistry"] = "127.0.0.1",
                        ["securityOrigin"] = $"http://{host}",
                        ["mimeType"] = "text/html"
                    };

                    var resourcesArray = new JsonArray();

                    try
                    {
                        var files = Directory.GetFiles(workspaceRoot, "*.*", SearchOption.AllDirectories);
                        foreach (var file in files)
                        {
                            if (ShouldExclude(file)) continue;

                            var ext = Path.GetExtension(file).ToLowerInvariant();
                            if (ext == ".cs" || ext == ".axaml" || ext == ".md" || ext == ".json" || ext == ".csproj")
                            {
                                var relativePath = Path.GetRelativePath(workspaceRoot, file).Replace('\\', '/');
                                var url = $"http://{host}/workspace/{relativePath}";
                                var type = ext switch
                                {
                                    ".cs" => "Script",
                                    ".axaml" => "Document",
                                    ".md" => "Document",
                                    ".json" => "Document",
                                    _ => "Other"
                                };
                                var mimeType = ext switch
                                {
                                    ".cs" => "text/x-csharp",
                                    ".axaml" => "text/xml",
                                    ".md" => "text/markdown",
                                    ".json" => "application/json",
                                    _ => "text/plain"
                                };

                                resourcesArray.Add(new JsonObject
                                {
                                    ["url"] = url,
                                    ["type"] = type,
                                    ["mimeType"] = mimeType
                                });
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error scanning workspace files: {ex}");
                    }

                    var frameTree = new JsonObject
                    {
                        ["frame"] = frame,
                        ["resources"] = resourcesArray
                    };

                    return new JsonObject { ["frameTree"] = frameTree };
                }

            case "getResourceContent":
                {
                    string url = @params["url"]?.GetValue<string>() ?? "";
                    var port = CdpServer.Port;
                    var prefix = $"http://127.0.0.1:{port}/workspace/";
                    var prefixLocalhost = $"http://localhost:{port}/workspace/";

                    string relativePath = "";
                    if (url.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    {
                        relativePath = url.Substring(prefix.Length);
                    }
                    else if (url.StartsWith(prefixLocalhost, StringComparison.OrdinalIgnoreCase))
                    {
                        relativePath = url.Substring(prefixLocalhost.Length);
                    }
                    else
                    {
                        throw new Exception($"Invalid resource URL: {url}");
                    }

                    relativePath = Uri.UnescapeDataString(relativePath);

                    var workspaceRoot = FindWorkspaceRoot();
                    var absolutePath = Path.GetFullPath(Path.Combine(workspaceRoot, relativePath));

                    if (!absolutePath.StartsWith(workspaceRoot, StringComparison.OrdinalIgnoreCase))
                    {
                        throw new Exception("Access denied: path traversal detected");
                    }

                    if (!File.Exists(absolutePath))
                    {
                        throw new FileNotFoundException($"File not found: {relativePath}");
                    }

                    var content = await File.ReadAllTextAsync(absolutePath);

                    return new JsonObject
                    {
                        ["content"] = content,
                        ["base64Encoded"] = false
                    };
                }

            case "captureScreenshot":
                {
                    string format = @params["format"]?.GetValue<string>() ?? "png";
                    var data = await CaptureScreenshotAsync(session);
                    return new JsonObject { ["data"] = data };
                }

            case "startScreencast":
                {
                    session.StartScreencast();
                    return new JsonObject();
                }

            case "stopScreencast":
                {
                    session.StopScreencast();
                    return new JsonObject();
                }

            case "screencastFrameAck":
                {
                    int sessionId = @params["sessionId"]?.GetValue<int>() ?? 0;
                    session.AcknowledgeScreencastFrame(sessionId);
                    return new JsonObject();
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
                {
                    string url = @params["url"]?.GetValue<string>() ?? "";
                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        var windowType = session.Window.GetType();
                        var navigateMethod = windowType.GetMethod("Navigate", new[] { typeof(string) });
                        if (navigateMethod != null)
                        {
                            navigateMethod.Invoke(session.Window, new object[] { url });
                        }
                    });
                    return new JsonObject();
                }

            case "bringToFront":
                {
                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        if (session.Window is Window win)
                        {
                            win.Activate();
                        }
                    });
                    return new JsonObject();
                }

            case "getLayoutMetrics":
                {
                    double w = 800, h = 600;
                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        w = session.Window.Width;
                        h = session.Window.Height;
                    });

                    return new JsonObject
                    {
                        ["cssLayoutViewport"] = new JsonObject
                        {
                            ["x"] = 0,
                            ["y"] = 0,
                            ["width"] = w,
                            ["height"] = h
                        },
                        ["cssVisualViewport"] = new JsonObject
                        {
                            ["x"] = 0,
                            ["y"] = 0,
                            ["width"] = w,
                            ["height"] = h
                        },
                        ["cssContentSize"] = new JsonObject
                        {
                            ["x"] = 0,
                            ["y"] = 0,
                            ["width"] = w,
                            ["height"] = h
                        }
                    };
                }

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
        return segments.Any(s => s.Equals("bin", StringComparison.OrdinalIgnoreCase)
                              || s.Equals("obj", StringComparison.OrdinalIgnoreCase)
                              || s.Equals(".git", StringComparison.OrdinalIgnoreCase)
                              || s.Equals("node_modules", StringComparison.OrdinalIgnoreCase));
    }
}
