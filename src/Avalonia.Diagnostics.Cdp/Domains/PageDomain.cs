using System;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace Avalonia.Diagnostics.Cdp.Domains;

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
                    Console.WriteLine($"[CDP PLAYWRIGHT DEBUG] Page.enable called, IsEnabled={state.IsEnabled}, sessionHash={session.GetHashCode()}");
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
                    string format = @params != null && @params.ContainsKey("format") ? @params["format"]?.GetValue<string>() ?? "png" : "png";
                    int? quality = @params != null && @params.ContainsKey("quality") ? @params["quality"]?.GetValue<int>() : null;
                    int? maxWidth = @params != null && @params.ContainsKey("maxWidth") ? @params["maxWidth"]?.GetValue<int>() : null;
                    int? maxHeight = @params != null && @params.ContainsKey("maxHeight") ? @params["maxHeight"]?.GetValue<int>() : null;
                    int? everyNthFrame = @params != null && @params.ContainsKey("everyNthFrame") ? @params["everyNthFrame"]?.GetValue<int>() : null;
                    string? transferMode = @params != null && @params.ContainsKey("transferMode") ? @params["transferMode"]?.GetValue<string>() : null;
                    session.StartScreencast(format, quality, maxWidth, maxHeight, everyNthFrame, transferMode);
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
                    _ = Task.Run(async () =>
                    {
                        await EvaluateScriptsAsync(session, session.ScriptsToEvaluateOnNewDocument.Values);
                        await EvaluateScriptsAsync(session, session.ScriptsToEvaluateOnLoad.Values);
                    });
                    return new JsonObject();
                }

            case "navigate":
                {
                    string url = @params["url"]?.GetValue<string>() ?? "";
                    string loaderId = $"loader-{Guid.NewGuid()}";
                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        var windowType = session.Window.GetType();
                        var navigateMethod = windowType.GetMethod("Navigate", new[] { typeof(string) });
                        if (navigateMethod != null)
                        {
                            navigateMethod.Invoke(session.Window, new object[] { url });
                        }
                    });

                    var nextId = session.NavigationHistory.Count + 1;
                    var historyEntry = new JsonObject
                    {
                        ["id"] = nextId,
                        ["url"] = url,
                        ["userTypedURL"] = url,
                        ["title"] = session.Window?.GetType().Name ?? "Avalonia Window",
                        ["transitionType"] = "link"
                    };

                    if (session.NavigationHistoryIndex >= 0 && session.NavigationHistoryIndex < session.NavigationHistory.Count - 1)
                    {
                        session.NavigationHistory.RemoveRange(session.NavigationHistoryIndex + 1, session.NavigationHistory.Count - (session.NavigationHistoryIndex + 1));
                    }
                    session.NavigationHistory.Add(historyEntry);
                    session.NavigationHistoryIndex = session.NavigationHistory.Count - 1;

                     _ = Task.Run(async () =>
                     {
                         await EmitNavigationEventsAsync(session, url, frameId, loaderId);
                     });

                    return new JsonObject
                    {
                        ["frameId"] = frameId,
                        ["loaderId"] = loaderId
                    };
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
                        ["layoutViewport"] = new JsonObject
                        {
                            ["pageX"] = 0,
                            ["pageY"] = 0,
                            ["clientWidth"] = (int)w,
                            ["clientHeight"] = (int)h
                        },
                        ["visualViewport"] = new JsonObject
                        {
                            ["pageX"] = 0,
                            ["pageY"] = 0,
                            ["offsetX"] = 0,
                            ["offsetY"] = 0,
                            ["clientWidth"] = w,
                            ["clientHeight"] = h,
                            ["scale"] = 1.0,
                            ["zoom"] = 1.0
                        },
                        ["contentSize"] = new JsonObject
                        {
                            ["x"] = 0,
                            ["y"] = 0,
                            ["width"] = w,
                            ["height"] = h
                        },
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
                    var frameTree = new JsonObject
                    {
                        ["frame"] = frame,
                        ["childFrames"] = new JsonArray()
                    };
                    return new JsonObject { ["frameTree"] = frameTree };
                }

            case "getAppManifest":
                {
                    var port = CdpServer.Port;
                    return new JsonObject
                    {
                        ["url"] = $"http://localhost:{port}/manifest.json",
                        ["errors"] = new JsonArray(),
                        ["data"] = "{\"name\": \"CdpSampleApp\", \"short_name\": \"CdpSampleApp\", \"start_url\": \"/\", \"display\": \"standalone\"}"
                    };
                }

            case "getNavigationHistory":
                {
                    var entries = new JsonArray();
                    foreach (var entry in session.NavigationHistory)
                    {
                        entries.Add(entry.DeepClone());
                    }
                    return new JsonObject
                    {
                        ["currentIndex"] = session.NavigationHistoryIndex,
                        ["entries"] = entries
                    };
                }

            case "addScriptToEvaluateOnNewDocument":
                {
                    string source = @params["source"]?.GetValue<string>() ?? "";
                    string worldName = @params["worldName"]?.GetValue<string>() ?? "";
                    string identifier = Guid.NewGuid().ToString();
                    session.ScriptsToEvaluateOnNewDocument[identifier] = source;
                    if (!string.IsNullOrEmpty(worldName))
                    {
                        session.ScriptsToEvaluateOnNewDocumentWorlds[identifier] = worldName;
                        var context = new JsonObject
                        {
                            ["id"] = 2,
                            ["origin"] = $"http://localhost:{CdpServer.Port}",
                            ["name"] = worldName,
                            ["uniqueId"] = "2",
                            ["auxData"] = new JsonObject
                            {
                                ["isDefault"] = false,
                                ["type"] = "isolated",
                                ["name"] = worldName,
                                ["frameId"] = frameId
                            }
                        };
                        var contextParams = new JsonObject { ["context"] = context };
                        _ = session.SendEventAsync("Runtime.executionContextCreated", contextParams);
                    }
                    return new JsonObject { ["identifier"] = identifier };
                }

            case "removeScriptToEvaluateOnNewDocument":
                {
                    string identifier = @params["identifier"]?.GetValue<string>() ?? "";
                    session.ScriptsToEvaluateOnNewDocument.TryRemove(identifier, out _);
                    session.ScriptsToEvaluateOnNewDocumentWorlds.TryRemove(identifier, out _);
                    return new JsonObject();
                }

            case "addScriptToEvaluateOnLoad":
                {
                    string source = @params["source"]?.GetValue<string>() ?? "";
                    string identifier = Guid.NewGuid().ToString();
                    session.ScriptsToEvaluateOnLoad[identifier] = source;
                    return new JsonObject { ["identifier"] = identifier };
                }

            case "removeScriptToEvaluateOnLoad":
                {
                    string identifier = @params["identifier"]?.GetValue<string>() ?? "";
                    session.ScriptsToEvaluateOnLoad.TryRemove(identifier, out _);
                    return new JsonObject();
                }

            case "close":
            case "crash":
                {
                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        if (session.Window is Window win)
                        {
                            win.Close();
                        }
                    });
                    return new JsonObject();
                }

            case "captureSnapshot":
                {
                    string mhtml = CaptureSnapshotMhtml(session);
                    return new JsonObject { ["data"] = mhtml };
                }

            case "createIsolatedWorld":
                {
                    var worldName = @params["worldName"]?.GetValue<string>() ?? "playwright-utility-world";
                    var context = new JsonObject
                    {
                        ["id"] = 2,
                        ["origin"] = $"http://127.0.0.1:{CdpServer.Port}/",
                        ["name"] = worldName,
                        ["uniqueId"] = "2",
                        ["auxData"] = new JsonObject
                        {
                            ["isDefault"] = false,
                            ["type"] = "isolated",
                            ["name"] = worldName,
                            ["frameId"] = frameId
                        }
                    };
                    var contextParams = new JsonObject { ["context"] = context };
                    _ = session.SendEventAsync("Runtime.executionContextCreated", contextParams);
                    return new JsonObject { ["executionContextId"] = 2 };
                }

            case "getAdScriptAncestry":
                return new JsonObject { ["adScriptAncestry"] = new JsonArray() };

            case "getAnnotatedPageContent":
                {
                    string content = "";
                    if (Dispatcher.UIThread.CheckAccess())
                    {
                        content = GetVisualTreeHtml(session.Window);
                    }
                    else
                    {
                        content = Dispatcher.UIThread.Invoke(() => GetVisualTreeHtml(session.Window));
                    }
                    return new JsonObject
                    {
                        ["content"] = content
                    };
                }

            case "getAppId":
                return new JsonObject
                {
                    ["appId"] = "com.cdp.sampleapp",
                    ["recommendedId"] = "com.cdp.sampleapp"
                };

            case "getInstallabilityErrors":
                return new JsonObject
                {
                    ["installabilityErrors"] = new JsonArray()
                };

            case "getManifestIcons":
                return new JsonObject
                {
                    ["primaryIcon"] = "http://localhost:" + CdpServer.Port + "/favicon.ico"
                };

            case "getOriginTrials":
                return new JsonObject
                {
                    ["originTrials"] = new JsonArray()
                };

            case "getPermissionsPolicyState":
                return new JsonObject
                {
                    ["states"] = new JsonArray()
                };

            case "printToPDF":
                {
                    string pdfBase64 = await PrintToPdfAsync(session);
                    return new JsonObject { ["data"] = pdfBase64 };
                }

            case "searchInResource":
                {
                    string url = @params["url"]?.GetValue<string>() ?? "";
                    string query = @params["query"]?.GetValue<string>() ?? "";
                    bool caseSensitive = @params["caseSensitive"]?.GetValue<bool>() ?? false;
                    bool isRegex = @params["isRegex"]?.GetValue<bool>() ?? false;

                    var resultArr = new JsonArray();

                    if (string.IsNullOrEmpty(query))
                    {
                        return new JsonObject { ["result"] = resultArr };
                    }

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

                    if (File.Exists(absolutePath))
                    {
                        var content = await File.ReadAllTextAsync(absolutePath);
                        var lines = content.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
                        
                        var comparison = caseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
                        System.Text.RegularExpressions.Regex? regex = null;
                        if (isRegex)
                        {
                            var options = caseSensitive ? System.Text.RegularExpressions.RegexOptions.None : System.Text.RegularExpressions.RegexOptions.IgnoreCase;
                            regex = new System.Text.RegularExpressions.Regex(query, options);
                        }

                        for (int i = 0; i < lines.Length; i++)
                        {
                            bool isMatch = false;
                            if (isRegex && regex != null)
                                isMatch = regex.IsMatch(lines[i]);
                            else
                                isMatch = lines[i].Contains(query, comparison);

                            if (isMatch)
                            {
                                resultArr.Add(new JsonObject
                                {
                                    ["lineNumber"] = i + 1,
                                    ["lineContent"] = lines[i]
                                });
                            }
                        }
                    }

                    return new JsonObject { ["result"] = resultArr };
                }

            case "getCookies":
                {
                    var cookiesArray = new JsonArray();
                    foreach (var cookie in session.Cookies)
                    {
                        cookiesArray.Add(cookie.DeepClone());
                    }
                    return new JsonObject { ["cookies"] = cookiesArray };
                }

            case "setCookie":
                {
                    string name = @params["name"]?.GetValue<string>() ?? "";
                    string value = @params["value"]?.GetValue<string>() ?? "";
                    string domain = @params["domain"]?.GetValue<string>() ?? "";
                    string path = @params["path"]?.GetValue<string>() ?? "";
                    double expires = @params["expires"]?.GetValue<double>() ?? -1;

                    session.Cookies.RemoveAll(c => 
                        (c["name"]?.GetValue<string>() ?? "") == name && 
                        (c["domain"]?.GetValue<string>() ?? "") == domain && 
                        (c["path"]?.GetValue<string>() ?? "") == path);

                    var cookie = new JsonObject
                    {
                        ["name"] = name,
                        ["value"] = value,
                        ["domain"] = domain,
                        ["path"] = path,
                        ["expires"] = expires
                    };

                    if (@params.ContainsKey("secure")) cookie["secure"] = @params["secure"]?.GetValue<bool>();
                    if (@params.ContainsKey("httpOnly")) cookie["httpOnly"] = @params["httpOnly"]?.GetValue<bool>();
                    if (@params.ContainsKey("sameSite")) cookie["sameSite"] = @params["sameSite"]?.GetValue<string>();

                    session.Cookies.Add(cookie);
                    return new JsonObject();
                }

            case "deleteCookie":
                {
                    string name = @params["name"]?.GetValue<string>() ?? "";
                    string domain = @params["domain"]?.GetValue<string>() ?? "";
                    string path = @params["path"]?.GetValue<string>() ?? "";

                    session.Cookies.RemoveAll(c =>
                    {
                        bool match = c["name"]?.GetValue<string>() == name;
                        if (!string.IsNullOrEmpty(domain))
                        {
                            match = match && c["domain"]?.GetValue<string>() == domain;
                        }
                        if (!string.IsNullOrEmpty(path))
                        {
                            match = match && c["path"]?.GetValue<string>() == path;
                        }
                        return match;
                    });
                    return new JsonObject();
                }

            case "setDocumentContent":
                {
                    string html = @params["html"]?.GetValue<string>() ?? "";
                    if (!string.IsNullOrEmpty(html) && html.Trim().StartsWith("<"))
                    {
                        await Dispatcher.UIThread.InvokeAsync(() =>
                        {
                            try
                            {
                                var newContent = Avalonia.Markup.Xaml.AvaloniaRuntimeXamlLoader.Load(html);
                                if (session.Window is Window win)
                                {
                                    win.Content = newContent;
                                }
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"setDocumentContent XAML loader failed: {ex.Message}");
                            }
                        });
                    }
                    return new JsonObject();
                }

            case "setDeviceMetricsOverride":
            case "clearDeviceMetricsOverride":
                return await EmulationDomain.HandleAsync(session, action, @params);

            case "setGeolocationOverride":
                {
                    session.GeolocationOverride = new JsonObject
                    {
                        ["latitude"] = @params["latitude"]?.GetValue<double>() ?? 0.0,
                        ["longitude"] = @params["longitude"]?.GetValue<double>() ?? 0.0,
                        ["accuracy"] = @params["accuracy"]?.GetValue<double>() ?? 0.0
                    };
                    return new JsonObject();
                }

            case "clearGeolocationOverride":
                {
                    session.GeolocationOverride = null;
                    return new JsonObject();
                }

            case "setDeviceOrientationOverride":
                {
                    session.DeviceOrientationOverride = new JsonObject
                    {
                        ["alpha"] = @params["alpha"]?.GetValue<double>() ?? 0.0,
                        ["beta"] = @params["beta"]?.GetValue<double>() ?? 0.0,
                        ["gamma"] = @params["gamma"]?.GetValue<double>() ?? 0.0
                    };
                    return new JsonObject();
                }

            case "clearDeviceOrientationOverride":
                {
                    session.DeviceOrientationOverride = null;
                    return new JsonObject();
                }

            case "setTouchEmulationEnabled":
                {
                    session.TouchEmulationEnabled = @params["enabled"]?.GetValue<bool>() ?? false;
                    return await EmulationDomain.HandleAsync(session, "setTouchEmulationEnabled", @params);
                }

            case "setLifecycleEventsEnabled":
                {
                    session.LifecycleEventsEnabled = @params["enabled"]?.GetValue<bool>() ?? false;
                    if (session.LifecycleEventsEnabled)
                    {
                        var timestamp = (double)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000.0;
                        var names = new[] { "init", "DOMContentLoaded", "load", "networkAlmostIdle", "networkIdle" };
                        foreach (var name in names)
                        {
                            var evtParams = new JsonObject
                            {
                                ["frameId"] = frameId,
                                ["loaderId"] = "main-loader-id",
                                ["name"] = name,
                                ["timestamp"] = timestamp
                            };
                            _ = session.SendEventAsync("Page.lifecycleEvent", evtParams);
                        }
                        _ = session.SendEventAsync("Page.domContentEventFired", new JsonObject { ["timestamp"] = timestamp });
                        _ = session.SendEventAsync("Page.loadEventFired", new JsonObject { ["timestamp"] = timestamp });
                        _ = session.SendEventAsync("Page.frameStoppedLoading", new JsonObject { ["frameId"] = frameId });
                    }
                    return new JsonObject();
                }

            case "setAdBlockingEnabled":
                {
                    session.AdBlockingEnabled = @params["enabled"]?.GetValue<bool>() ?? false;
                    return new JsonObject();
                }

            case "setBypassCSP":
                {
                    session.BypassCSP = @params["enabled"]?.GetValue<bool>() ?? false;
                    return new JsonObject();
                }

            case "setFontFamilies":
                {
                    session.FontFamilies = @params["fontFamilies"] as JsonObject;
                    var familyName = @params["fontFamilies"]?["standard"]?.GetValue<string>();
                    if (!string.IsNullOrEmpty(familyName))
                    {
                        await Dispatcher.UIThread.InvokeAsync(() =>
                        {
                            try
                            {
                                if (session.Window is Window win)
                                {
                                    win.FontFamily = new Media.FontFamily(familyName);
                                }
                            }
                            catch { }
                        });
                    }
                    return new JsonObject();
                }

            case "setFontSizes":
                {
                    session.FontSizes = @params["fontSizes"] as JsonObject;
                    var standardSize = @params["fontSizes"]?["standard"]?.GetValue<int>();
                    if (standardSize > 0)
                    {
                        await Dispatcher.UIThread.InvokeAsync(() =>
                        {
                            try
                            {
                                if (session.Window is Window win)
                                {
                                    win.FontSize = standardSize.Value;
                                }
                            }
                            catch { }
                        });
                    }
                    return new JsonObject();
                }

            case "stopLoading":
                {
                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        session.Window.InvalidateVisual();
                    });
                    return new JsonObject();
                }

            case "resetNavigationHistory":
                {
                    session.NavigationHistory.Clear();
                    var port = CdpServer.Port;
                    var rootUrl = $"http://localhost:{port}/";
                    session.NavigationHistory.Add(new JsonObject
                    {
                        ["id"] = 1,
                        ["url"] = rootUrl,
                        ["userTypedURL"] = rootUrl,
                        ["title"] = session.Window?.GetType().Name ?? "Avalonia Window",
                        ["transitionType"] = "typed"
                    });
                    session.NavigationHistoryIndex = 0;
                    return new JsonObject();
                }

            case "navigateToHistoryEntry":
                {
                    int entryId = @params["entryId"]?.GetValue<int>() ?? 0;
                    var entry = session.NavigationHistory.FirstOrDefault(e => e["id"]?.GetValue<int>() == entryId);
                    if (entry != null)
                    {
                        var url = entry["url"]?.GetValue<string>() ?? "";
                        string loaderId = $"loader-{Guid.NewGuid()}";
                        session.NavigationHistoryIndex = session.NavigationHistory.IndexOf(entry);
                        
                        await Dispatcher.UIThread.InvokeAsync(() =>
                        {
                            var windowType = session.Window.GetType();
                            var navigateMethod = windowType.GetMethod("Navigate", new[] { typeof(string) });
                            if (navigateMethod != null)
                            {
                                navigateMethod.Invoke(session.Window, new object[] { url });
                            }
                        });
                        
                        _ = Task.Run(async () =>
                        {
                            await EmitNavigationEventsAsync(session, url, frameId, loaderId);
                        });
                    }
                    return new JsonObject();
                }

            case "setDownloadBehavior":
                {
                    session.DownloadBehavior = @params["behavior"]?.GetValue<string>();
                    session.DownloadPath = @params["downloadPath"]?.GetValue<string>();
                    return new JsonObject();
                }

            case "setInterceptFileChooserDialog":
                {
                    session.InterceptFileChooserDialog = @params["enabled"]?.GetValue<bool>() ?? false;
                    return new JsonObject();
                }

            case "setPrerenderingAllowed":
                {
                    session.PrerenderingAllowed = @params["prerenderingAllowed"]?.GetValue<bool>() ?? false;
                    return new JsonObject();
                }

            case "setRPHRegistrationMode":
                {
                    session.RPHRegistrationMode = @params["mode"]?.GetValue<string>();
                    return new JsonObject();
                }

            case "setSPCTransactionMode":
                {
                    session.SPCTransactionMode = @params["mode"]?.GetValue<string>();
                    return new JsonObject();
                }

            case "setWebLifecycleState":
                {
                    session.WebLifecycleState = @params["state"]?.GetValue<string>();
                    return new JsonObject();
                }

            case "addCompilationCache":
                {
                    string url = @params["url"]?.GetValue<string>() ?? "";
                    string data = @params["data"]?.GetValue<string>() ?? "";
                    if (!string.IsNullOrEmpty(url))
                    {
                        session.CompilationCache[url] = data;
                    }
                    return new JsonObject();
                }

            case "clearCompilationCache":
                {
                    session.CompilationCache.Clear();
                    return new JsonObject();
                }

            case "produceCompilationCache":
                {
                    return new JsonObject();
                }

            case "generateTestReport":
                {
                    string message = @params["message"]?.GetValue<string>() ?? "";
                    string group = @params["group"]?.GetValue<string>() ?? "default";
                    Console.WriteLine($"[CDP Page.generateTestReport] Group: {group}, Message: {message}");
                    return new JsonObject();
                }

            case "handleJavaScriptDialog":
                {
                    bool accept = @params["accept"]?.GetValue<bool>() ?? false;
                    string promptText = @params["promptText"]?.GetValue<string>() ?? "";
                    Console.WriteLine($"[CDP Page.handleJavaScriptDialog] Accept: {accept}, PromptText: {promptText}");
                    return new JsonObject();
                }

            case "waitForDebugger":
                {
                    await Task.Delay(50);
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
            try
            {
                var window = session.Window;
                var scale = window.RenderScaling;
                var width = Math.Max(1, (int)(window.Bounds.Width * scale));
                var height = Math.Max(1, (int)(window.Bounds.Height * scale));

                using var bitmap = new RenderTargetBitmap(new PixelSize(width, height), new Vector(96 * scale, 96 * scale));
                bitmap.Render(window);

                using var ms = new MemoryStream();
                bitmap.Save(ms);
                
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

            // Return a 1x1 fallback mock PNG base64 string
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

            // Extreme fallback if SkiaSharp is completely broken (a valid minimal 1x1 black PNG base64)
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

    private static async Task EvaluateScriptsAsync(CdpSession session, System.Collections.Generic.IEnumerable<string> scripts)
    {
        foreach (var script in scripts)
        {
            if (script.Length > 5000 || script.Contains("injectedScript") || script.Contains("injected2.expect") || script.Contains("__commonJS"))
            {
                continue;
            }
            try
            {
                await RuntimeDomain.HandleAsync(session, "evaluate", new JsonObject
                {
                    ["expression"] = script
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error executing page evaluate script: {ex.Message}");
            }
        }
    }

    private static async Task EmitNavigationEventsAsync(CdpSession session, string url, string frameId, string loaderId)
    {
        RuntimeDomain.ClearSessionEngines(session);

        await session.SendEventAsync("Runtime.executionContextDestroyed", new JsonObject { ["executionContextId"] = 1 });
        await session.SendEventAsync("Runtime.executionContextDestroyed", new JsonObject { ["executionContextId"] = 2 });

        var context1 = new JsonObject
        {
            ["id"] = 1,
            ["origin"] = $"http://127.0.0.1:{CdpServer.Port}/",
            ["name"] = "top",
            ["uniqueId"] = "1",
            ["auxData"] = new JsonObject
            {
                ["isDefault"] = true,
                ["type"] = "default",
                ["frameId"] = frameId
            }
        };
        await session.SendEventAsync("Runtime.executionContextCreated", new JsonObject { ["context"] = context1 });

        string? worldName = session.ScriptsToEvaluateOnNewDocumentWorlds.Values.FirstOrDefault();
        if (string.IsNullOrEmpty(worldName))
        {
            worldName = "playwright-utility-world";
        }
        var context2 = new JsonObject
        {
            ["id"] = 2,
            ["origin"] = $"http://127.0.0.1:{CdpServer.Port}/",
            ["name"] = worldName,
            ["uniqueId"] = "2",
            ["auxData"] = new JsonObject
            {
                ["isDefault"] = false,
                ["type"] = "isolated",
                ["name"] = worldName,
                ["frameId"] = frameId
            }
        };
        await session.SendEventAsync("Runtime.executionContextCreated", new JsonObject { ["context"] = context2 });

        await EvaluateScriptsAsync(session, session.ScriptsToEvaluateOnNewDocument.Values);
        await EvaluateScriptsAsync(session, session.ScriptsToEvaluateOnLoad.Values);

        var host = $"localhost:{CdpServer.Port}";
        var frame = new JsonObject
        {
            ["id"] = frameId,
            ["loaderId"] = loaderId,
            ["url"] = url,
            ["domainAndRegistry"] = "localhost",
            ["securityOrigin"] = $"http://{host}",
            ["mimeType"] = "text/html"
        };
        var frameNavigatedParams = new JsonObject { ["frame"] = frame };
        var timestamp = (double)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000.0;
        
        await session.SendEventAsync("Page.frameNavigated", frameNavigatedParams);
        await session.SendEventAsync("Page.domContentEventFired", new JsonObject { ["timestamp"] = timestamp });
        await session.SendEventAsync("Page.loadEventFired", new JsonObject { ["timestamp"] = timestamp });

        if (session.LifecycleEventsEnabled)
        {
            await session.SendEventAsync("Page.lifecycleEvent", new JsonObject
            {
                ["frameId"] = frameId,
                ["loaderId"] = loaderId,
                ["name"] = "init",
                ["timestamp"] = timestamp
            });
            await session.SendEventAsync("Page.lifecycleEvent", new JsonObject
            {
                ["frameId"] = frameId,
                ["loaderId"] = loaderId,
                ["name"] = "DOMContentLoaded",
                ["timestamp"] = timestamp
            });
            await session.SendEventAsync("Page.lifecycleEvent", new JsonObject
            {
                ["frameId"] = frameId,
                ["loaderId"] = loaderId,
                ["name"] = "load",
                ["timestamp"] = timestamp
            });
            await session.SendEventAsync("Page.lifecycleEvent", new JsonObject
            {
                ["frameId"] = frameId,
                ["loaderId"] = loaderId,
                ["name"] = "networkAlmostIdle",
                ["timestamp"] = timestamp
            });
            await session.SendEventAsync("Page.lifecycleEvent", new JsonObject
            {
                ["frameId"] = frameId,
                ["loaderId"] = loaderId,
                ["name"] = "networkIdle",
                ["timestamp"] = timestamp
            });
        }

        await session.SendEventAsync("Page.frameStoppedLoading", new JsonObject { ["frameId"] = frameId });
    }

    private static async Task<string> PrintToPdfAsync(CdpSession session)
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
            using (var imageStream = new MemoryStream())
            {
                bitmap.Save(imageStream);
                imageStream.Position = 0;
                
                using (var skBitmap = SkiaSharp.SKBitmap.Decode(imageStream))
                using (var document = SkiaSharp.SKDocument.CreatePdf(ms))
                {
                    if (skBitmap != null && document != null)
                    {
                        using (var canvas = document.BeginPage(width, height))
                        {
                            canvas.DrawBitmap(skBitmap, 0, 0);
                            document.EndPage();
                        }
                        document.Close();
                    }
                }
            }

            return Convert.ToBase64String(ms.ToArray());
        });
    }

    private static string GetVisualTreeHtml(Visual visual, int depth = 0)
    {
        if (visual == null) return "";
        var indent = new string(' ', depth * 2);
        var typeName = visual.GetType().Name;
        var name = (visual as Controls.Control)?.Name ?? "";
        var attributes = string.IsNullOrEmpty(name) ? "" : $" id=\"{name}\"";

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"{indent}<div class=\"visual-node\"{attributes} data-type=\"{typeName}\">");
        sb.AppendLine($"{indent}  <strong>{typeName}</strong>");
        
        var children = visual.GetVisualChildren();
        if (children.Any())
        {
            sb.AppendLine($"{indent}  <div class=\"children\">");
            foreach (var child in children)
            {
                sb.Append(GetVisualTreeHtml(child, depth + 2));
            }
            sb.AppendLine($"{indent}  </div>");
        }
        
        sb.AppendLine($"{indent}</div>");
        return sb.ToString();
    }

    private static string CaptureSnapshotMhtml(CdpSession session)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("MIME-Version: 1.0");
        sb.AppendLine("Content-Type: multipart/related; boundary=\"----MultipartBoundary\"");
        sb.AppendLine();
        sb.AppendLine("------MultipartBoundary");
        sb.AppendLine("Content-Type: text/html");
        sb.AppendLine("Content-Location: http://localhost/");
        sb.AppendLine();
        sb.AppendLine("<!DOCTYPE html>");
        sb.AppendLine("<html>");
        sb.AppendLine("<head>");
        sb.AppendLine("  <title>Avalonia Visual Tree Snapshot</title>");
        sb.AppendLine("  <style>");
        sb.AppendLine("    body { font-family: monospace; background-color: #202124; color: #e8eaed; padding: 20px; }");
        sb.AppendLine("    .visual-node { border-left: 1px dashed #3c4043; padding-left: 10px; margin: 5px 0; }");
        sb.AppendLine("    .children { margin-left: 15px; }");
        sb.AppendLine("  </style>");
        sb.AppendLine("</head>");
        sb.AppendLine("<body>");
        
        string treeHtml = "";
        if (Dispatcher.UIThread.CheckAccess())
        {
            treeHtml = GetVisualTreeHtml(session.Window);
        }
        else
        {
            treeHtml = Dispatcher.UIThread.Invoke(() => GetVisualTreeHtml(session.Window));
        }
        sb.AppendLine(treeHtml);

        sb.AppendLine("</body>");
        sb.AppendLine("</html>");
        sb.AppendLine("------MultipartBoundary--");
        
        return sb.ToString();
    }
}
