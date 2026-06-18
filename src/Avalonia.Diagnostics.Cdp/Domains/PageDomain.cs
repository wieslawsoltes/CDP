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
                        await Task.Run(() => ScanResourcesRecursive(workspaceRoot, workspaceRoot, host, resourcesArray));
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
                    var relative = Path.GetRelativePath(workspaceRoot, absolutePath);
                    if (relative.StartsWith("..") || Path.IsPathRooted(relative))
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
                        var size = session.Window.ClientSize;
                        if (!double.IsNaN(size.Width) && size.Width > 0) w = size.Width;
                        if (!double.IsNaN(size.Height) && size.Height > 0) h = size.Height;
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

            case "getFrameTree":
                {
                    var port = CdpServer.Port;
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
                    var frameTree = new JsonObject
                    {
                        ["frame"] = frame
                    };
                    return new JsonObject { ["frameTree"] = frameTree };
                }

            case "getAppManifest":
                {
                    return new JsonObject
                    {
                        ["url"] = "",
                        ["errors"] = new JsonArray()
                    };
                }

            case "getNavigationHistory":
                {
                    var port = CdpServer.Port;
                    var entries = new JsonArray
                    {
                        new JsonObject
                        {
                            ["id"] = 1,
                            ["url"] = $"http://127.0.0.1:{port}/",
                            ["userTypedURL"] = $"http://127.0.0.1:{port}/",
                            ["title"] = session.Window?.GetType().Name ?? "Avalonia Window",
                            ["transitionType"] = "typed"
                        }
                    };
                    return new JsonObject
                    {
                        ["currentIndex"] = 0,
                        ["entries"] = entries
                    };
                }

            case "addScriptToEvaluateOnNewDocument":
                {
                    return new JsonObject { ["identifier"] = "1" };
                }

            case "removeScriptToEvaluateOnNewDocument":
            case "setDeviceMetricsOverride":
            case "clearDeviceMetricsOverride":
            case "setGeolocationOverride":
            case "clearGeolocationOverride":
            case "setDeviceOrientationOverride":
            case "clearDeviceOrientationOverride":
            case "setTouchEmulationEnabled":
            case "setLifecycleEventsEnabled":
            case "setAdBlockingEnabled":
            case "setBypassCSP":
            case "setFontFamilies":
            case "setFontSizes":
                {
                    return new JsonObject();
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

    private static void ScanResourcesRecursive(string dir, string workspaceRoot, string host, JsonArray resourcesArray)
    {
        string dirName = Path.GetFileName(dir);
        if (dirName.Equals("bin", StringComparison.OrdinalIgnoreCase)
            || dirName.Equals("obj", StringComparison.OrdinalIgnoreCase)
            || dirName.Equals(".git", StringComparison.OrdinalIgnoreCase)
            || dirName.Equals(".vs", StringComparison.OrdinalIgnoreCase)
            || dirName.Equals(".idea", StringComparison.OrdinalIgnoreCase)
            || dirName.Equals("node_modules", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        try
        {
            foreach (var file in Directory.GetFiles(dir))
            {
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

            foreach (var subDir in Directory.GetDirectories(dir))
            {
                ScanResourcesRecursive(subDir, workspaceRoot, host, resourcesArray);
            }
        }
        catch { }
    }
}
