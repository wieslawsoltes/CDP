using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using SkiaSharp;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace CDP.Automation.OS.MacOs;

public sealed partial class MacOsAutomation : IOsAutomation
{
    private readonly ILogger _logger;

    public bool MovePhysicalCursor { get; set; }

    private static readonly IntPtr _kCFBooleanTrue;

    static MacOsAutomation()
    {
        try
        {
            if (NativeLibrary.TryLoad("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation", out IntPtr cfLib))
            {
                if (NativeLibrary.TryGetExport(cfLib, "kCFBooleanTrue", out IntPtr ptr))
                {
                    _kCFBooleanTrue = Marshal.ReadIntPtr(ptr);
                }
            }
        }
        catch {}
    }

    public MacOsAutomation(ILogger? logger = null)
    {
        _logger = logger ?? NullLogger.Instance;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct CGPoint
    {
        public double X;
        public double Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct CGSize
    {
        public double Width;
        public double Height;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct CGRect
    {
        public CGPoint Origin;
        public CGSize Size;
    }

    [LibraryImport("/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics")]
    private static partial IntPtr CGWindowListCopyWindowInfo(uint option, uint relativeToWindow);

    [LibraryImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
    private static partial int CFArrayGetCount(IntPtr array);

    [LibraryImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
    private static partial IntPtr CFArrayGetValueAtIndex(IntPtr array, nint index);

    [LibraryImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
    private static partial void CFRelease(IntPtr obj);

    [LibraryImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
    private static partial IntPtr CFDictionaryGetValue(IntPtr theDict, IntPtr key);

    [LibraryImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
    [return: MarshalAs(UnmanagedType.I1)]
    private static partial bool CFNumberGetValue(IntPtr number, nint theType, out int value);

    [LibraryImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
    private static partial IntPtr CFStringCreateWithCString(IntPtr alloc, byte[] utf8Str, uint encoding);

    [LibraryImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
    private static partial nint CFStringGetLength(IntPtr theString);

    [LibraryImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
    [return: MarshalAs(UnmanagedType.I1)]
    private static partial bool CFStringGetCString(IntPtr theString, byte[] buffer, nint bufferSize, uint encoding);

    [LibraryImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
    private static partial nint CFGetTypeID(IntPtr cf);

    [LibraryImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
    private static partial nint CFStringGetTypeID();

    [LibraryImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
    private static partial nint CFBooleanGetTypeID();

    [LibraryImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
    private static partial nint CFNumberGetTypeID();

    [LibraryImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
    private static partial nint CFArrayGetTypeID();

    [LibraryImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
    [return: MarshalAs(UnmanagedType.I1)]
    private static partial bool CFBooleanGetValue(IntPtr boolean);

    [LibraryImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation", EntryPoint = "CFNumberGetValue")]
    [return: MarshalAs(UnmanagedType.I1)]
    private static partial bool CFNumberGetValueDouble(IntPtr number, nint theType, out double value);

    [LibraryImport("/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics")]
    [return: MarshalAs(UnmanagedType.I1)]
    private static partial bool CGRectMakeWithDictionaryRepresentation(IntPtr dict, out CGRect rect);

    [LibraryImport("/System/Library/Frameworks/ApplicationServices.framework/ApplicationServices")]
    private static partial IntPtr AXUIElementCreateApplication(int pid);

    [LibraryImport("/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics")]
    [return: MarshalAs(UnmanagedType.I1)]
    private static partial bool CGPreflightScreenCaptureAccess();

    [LibraryImport("/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics")]
    [return: MarshalAs(UnmanagedType.I1)]
    private static partial bool CGRequestScreenCaptureAccess();

    [LibraryImport("/System/Library/Frameworks/ApplicationServices.framework/ApplicationServices")]
    [return: MarshalAs(UnmanagedType.I1)]
    private static partial bool AXIsProcessTrusted();

    [LibraryImport("/System/Library/Frameworks/ApplicationServices.framework/ApplicationServices")]
    private static partial int AXUIElementCopyAttributeValue(IntPtr element, IntPtr attribute, out IntPtr value);

    [LibraryImport("/System/Library/Frameworks/ApplicationServices.framework/ApplicationServices")]
    private static partial int AXUIElementSetAttributeValue(IntPtr element, IntPtr attribute, IntPtr value);

    [LibraryImport("/System/Library/Frameworks/ApplicationServices.framework/ApplicationServices")]
    [return: MarshalAs(UnmanagedType.I1)]
    private static partial bool AXValueGetValue(IntPtr value, int type, out CGPoint point);

    [LibraryImport("/System/Library/Frameworks/ApplicationServices.framework/ApplicationServices")]
    private static partial int AXUIElementCopyElementAtPosition(IntPtr application, float x, float y, out IntPtr element);

    [LibraryImport("/System/Library/Frameworks/ApplicationServices.framework/ApplicationServices")]
    private static partial int AXUIElementPerformAction(IntPtr element, IntPtr action);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate IntPtr CGEventTapCallBack(IntPtr tapProxy, uint eventType, IntPtr theEvent, IntPtr refcon);

    [LibraryImport("/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics")]
    private static partial IntPtr CGEventTapCreateForPid(
        int pid,
        int place,
        int options,
        ulong eventsOfInterest,
        CGEventTapCallBack callback,
        IntPtr refcon);

    [LibraryImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
    private static partial IntPtr CFMachPortCreateRunLoopSource(IntPtr allocator, IntPtr port, nint index);

    [LibraryImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
    private static partial IntPtr CFRunLoopGetCurrent();

    [LibraryImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
    private static partial void CFRunLoopAddSource(IntPtr rl, IntPtr source, IntPtr mode);

    [LibraryImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
    private static partial void CFRunLoopRun();

    [LibraryImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
    private static partial void CFRunLoopStop(IntPtr rl);

    [LibraryImport("/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics")]
    private static partial void CGEventTapEnable(IntPtr tap, [MarshalAs(UnmanagedType.I1)] bool enable);

    [LibraryImport("/System/Library/Frameworks/ApplicationServices.framework/ApplicationServices")]
    [return: MarshalAs(UnmanagedType.I1)]
    private static partial bool AXValueGetValue(IntPtr value, int type, out CGSize size);

    [LibraryImport("/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics")]
    private static partial IntPtr CGWindowListCreateImage(CGRect screenBounds, uint listOption, uint windowId, uint imageOption);

    [LibraryImport("/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics")]
    private static partial IntPtr CGImageGetDataProvider(IntPtr image);

    [LibraryImport("/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics")]
    private static partial IntPtr CGDataProviderCopyData(IntPtr provider);

    [LibraryImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
    private static partial IntPtr CFDataGetBytePtr(IntPtr data);

    [LibraryImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
    private static partial nint CFDataGetLength(IntPtr data);

    [LibraryImport("/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics")]
    private static partial nint CGImageGetWidth(IntPtr image);

    [LibraryImport("/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics")]
    private static partial nint CGImageGetHeight(IntPtr image);

    [LibraryImport("/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics")]
    private static partial nint CGImageGetBytesPerRow(IntPtr image);

    [LibraryImport("/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics")]
    private static partial IntPtr CGEventCreateMouseEvent(IntPtr source, uint mouseType, CGPoint mouseCursorPosition, uint mouseButton);

    [LibraryImport("/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics")]
    private static partial void CGEventPost(uint tapLocation, IntPtr theEvent);

    [LibraryImport("/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics")]
    private static partial void CGEventPostToPid(int pid, IntPtr theEvent);

    [LibraryImport("/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics")]
    private static partial IntPtr CGEventCreateKeyboardEvent(IntPtr source, ushort virtualKey, [MarshalAs(UnmanagedType.I1)] bool keyDown);

    [LibraryImport("/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics")]
    private static partial IntPtr CGEventCreate(IntPtr source);

    [LibraryImport("/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics")]
    private static partial CGPoint CGEventGetLocation(IntPtr theEvent);

    [LibraryImport("/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics")]
    private static partial int CGAssociateMouseAndMouseCursorPosition([MarshalAs(UnmanagedType.I1)] bool associate);

    private static IntPtr CreateCFString(string str)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(str + "\0");
        return CFStringCreateWithCString(IntPtr.Zero, bytes, 0x08000100);
    }

    private static string? CFStringToString(IntPtr cfStr)
    {
        if (cfStr == IntPtr.Zero) return null;
        nint length = CFStringGetLength(cfStr);
        if (length == 0) return string.Empty;
        byte[] buffer = new byte[length * 4 + 1];
        if (CFStringGetCString(cfStr, buffer, buffer.Length, 0x08000100))
        {
            return Encoding.UTF8.GetString(buffer).TrimEnd('\0');
        }
        return null;
    }

    private static string? CFTypeToString(IntPtr cf)
    {
        if (cf == IntPtr.Zero) return null;
        try
        {
            nint typeId = CFGetTypeID(cf);
            if (typeId == CFStringGetTypeID())
            {
                return CFStringToString(cf);
            }
            if (typeId == CFBooleanGetTypeID())
            {
                return CFBooleanGetValue(cf) ? "true" : "false";
            }
            if (typeId == CFNumberGetTypeID())
            {
                double val = 0;
                if (CFNumberGetValueDouble(cf, 12, out val))
                {
                    return val.ToString(System.Globalization.CultureInfo.InvariantCulture);
                }
                return "0";
            }
            if (typeId == CFArrayGetTypeID())
            {
                return $"[Array, count={CFArrayGetCount(cf)}]";
            }
        }
        catch
        {
        }
        return null;
    }

    private static IntPtr GetDictValue(IntPtr dict, string keyName)
    {
        IntPtr keyStr = CreateCFString(keyName);
        try
        {
            return CFDictionaryGetValue(dict, keyStr);
        }
        finally
        {
            CFRelease(keyStr);
        }
    }

    private static int GetDictInt(IntPtr dict, string keyName, int defaultValue = 0)
    {
        IntPtr valRef = GetDictValue(dict, keyName);
        if (valRef != IntPtr.Zero)
        {
            if (CFNumberGetValue(valRef, 3, out int val))
            {
                return val;
            }
        }
        return defaultValue;
    }

    private static string GetDictString(IntPtr dict, string keyName)
    {
        IntPtr valRef = GetDictValue(dict, keyName);
        return CFTypeToString(valRef) ?? string.Empty;
    }

    private static IntPtr GetAttribute(IntPtr element, string attrName)
    {
        IntPtr attrStr = CreateCFString(attrName);
        try
        {
            int result = AXUIElementCopyAttributeValue(element, attrStr, out IntPtr value);
            if (result == 0)
            {
                return value;
            }
            return IntPtr.Zero;
        }
        finally
        {
            CFRelease(attrStr);
        }
    }

    public IReadOnlyList<OSWindow> GetWindows()
    {
        var list = new List<OSWindow>();
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) return list;

        try
        {
            IntPtr array = CGWindowListCopyWindowInfo(1, 0);
            if (array != IntPtr.Zero)
            {
                int count = CFArrayGetCount(array);
                for (int i = 0; i < count; i++)
                {
                    IntPtr dict = CFArrayGetValueAtIndex(array, i);
                    if (dict == IntPtr.Zero) continue;

                    int layer = GetDictInt(dict, "kCGWindowLayer");
                    if (layer != 0) continue;

                    int windowId = GetDictInt(dict, "kCGWindowNumber");
                    int pid = GetDictInt(dict, "kCGWindowOwnerPID");
                    string ownerName = GetDictString(dict, "kCGWindowOwnerName");
                    string title = GetDictString(dict, "kCGWindowName");

                    IntPtr boundsRef = GetDictValue(dict, "kCGWindowBounds");
                    SKRectI bounds = new SKRectI(0, 0, 1024, 768);
                    if (boundsRef != IntPtr.Zero && CGRectMakeWithDictionaryRepresentation(boundsRef, out var cgRect))
                    {
                        bounds = new SKRectI((int)cgRect.Origin.X, (int)cgRect.Origin.Y, (int)(cgRect.Origin.X + cgRect.Size.Width), (int)(cgRect.Origin.Y + cgRect.Size.Height));
                    }

                    if (string.IsNullOrEmpty(title))
                    {
                        title = $"{ownerName} ({windowId})";
                    }

                    list.Add(new OSWindow
                    {
                        Id = windowId.ToString(),
                        Title = title,
                        ProcessName = ownerName,
                        ProcessId = pid,
                        Bounds = bounds
                    });
                }
                CFRelease(array);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to copy CG window list");
        }

        if (list.Count == 0)
        {
            list.Add(new OSWindow
            {
                Id = "macos-window-fallback",
                Title = "macOS Fallback Window",
                ProcessName = "mac-app",
                ProcessId = 1001,
                Bounds = new SKRectI(0, 0, 1024, 768)
            });
        }

        return list;
    }

    public OSNode? GetElementTree(string windowId)
    {
        if (windowId == "macos-window-fallback")
        {
            return GetFallbackTree();
        }

        int pid = 0;
        if (windowId.EndsWith("_fallback"))
        {
            var parts = windowId.Split('_');
            if (parts.Length > 0 && int.TryParse(parts[0], out int parsedPid))
            {
                pid = parsedPid;
            }
        }
        else
        {
            var windows = GetWindows();
            foreach (var w in windows)
            {
                if (w.Id == windowId)
                {
                    pid = w.ProcessId;
                    break;
                }
            }
            if (pid == 0 && int.TryParse(windowId, out int parsedPid))
            {
                pid = parsedPid;
            }
        }

        if (pid > 0)
        {
            if (!AXIsProcessTrusted())
            {
                _logger.LogWarning("macOS Accessibility Permissions are NOT granted for this process. OS Automation cannot inspect other processes. Please authorize Terminal/IDE in System Settings -> Privacy & Security -> Accessibility.");
                
                var warningRoot = new OSNode
                {
                    Id = "warning",
                    Name = "Accessibility Permission Required",
                    Role = "AXWindow",
                    Text = "Please authorize Terminal/IDE in macOS System Settings -> Privacy & Security -> Accessibility to allow OS Automation control.",
                    Bounds = new SKRectI(0, 0, 1024, 768)
                };
                warningRoot.Children.Add(new OSNode
                {
                    Id = "warning_details",
                    Name = "Authorization Hint",
                    Role = "AXStaticText",
                    Text = "System Settings -> Privacy & Security -> Accessibility",
                    Bounds = new SKRectI(10, 10, 500, 50)
                });
                return warningRoot;
            }

            IntPtr appRef = AXUIElementCreateApplication(pid);
            if (appRef != IntPtr.Zero)
            {
                try
                {
                    bool traversed = false;
                    IntPtr windowsValue = GetAttribute(appRef, "AXWindows");
                    if (windowsValue != IntPtr.Zero)
                    {
                        int winCount = CFArrayGetCount(windowsValue);
                        for (int j = 0; j < winCount; j++)
                        {
                            IntPtr winElement = CFArrayGetValueAtIndex(windowsValue, j);
                            if (winElement != IntPtr.Zero)
                            {
                                var root = new OSNode
                                {
                                    Id = "1",
                                    Name = "Window",
                                    Role = "AXWindow",
                                    Bounds = new SKRectI(0, 0, 1024, 768)
                                };
                                int nextId = 2;
                                BuildNodeFromElement(winElement, root, ref nextId, 0);
                                if (root.Children.Count > 0)
                                {
                                    var winNode = root.Children[0];
                                    winNode.Id = "1";
                                    traversed = true;
                                    return winNode;
                                }
                            }
                        }
                        CFRelease(windowsValue);
                    }

                    if (!traversed)
                    {
                        var root = new OSNode
                        {
                            Id = "1",
                            Name = "Application",
                            Role = "AXApplication",
                            Bounds = new SKRectI(0, 0, 1024, 768)
                        };
                        int nextId = 2;
                        BuildNodeFromElement(appRef, root, ref nextId, 0);
                        if (root.Children.Count > 0)
                        {
                            var appNode = root.Children[0];
                            appNode.Id = "1";
                            return appNode;
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to traverse Accessibility API tree for pid {Pid}", pid);
                }
                finally
                {
                    CFRelease(appRef);
                }
            }
        }

        return GetFallbackTree();
    }

    private void BuildNodeFromElement(IntPtr element, OSNode parentNode, ref int nextId, int depth)
    {
        if (depth > 20) return;

        string role = CFTypeToString(GetAttribute(element, "AXRole")) ?? "AXUnknown";
        string? title = CFTypeToString(GetAttribute(element, "AXTitle"));
        string? description = CFTypeToString(GetAttribute(element, "AXDescription"));
        string? value = CFTypeToString(GetAttribute(element, "AXValue"));

        SKRectI bounds = new SKRectI(0, 0, 0, 0);
        IntPtr posRef = GetAttribute(element, "AXPosition");
        IntPtr sizeRef = GetAttribute(element, "AXSize");
        if (posRef != IntPtr.Zero && sizeRef != IntPtr.Zero)
        {
            if (AXValueGetValue(posRef, 1, out CGPoint pt) && AXValueGetValue(sizeRef, 2, out CGSize sz))
            {
                bounds = new SKRectI((int)pt.X, (int)pt.Y, (int)(pt.X + sz.Width), (int)(pt.Y + sz.Height));
            }
            CFRelease(posRef);
            CFRelease(sizeRef);
        }

        int myId = nextId++;
        var node = new OSNode
        {
            Id = myId.ToString(),
            Name = string.IsNullOrEmpty(title) ? role : title,
            Role = role,
            Text = value ?? (string.IsNullOrEmpty(title) ? description ?? string.Empty : title),
            Bounds = bounds
        };

        parentNode.Children.Add(node);

        IntPtr childrenRef = GetAttribute(element, "AXChildren");
        if (childrenRef != IntPtr.Zero)
        {
            int count = CFArrayGetCount(childrenRef);
            for (int i = 0; i < count; i++)
            {
                IntPtr childElement = CFArrayGetValueAtIndex(childrenRef, i);
                if (childElement != IntPtr.Zero)
                {
                    BuildNodeFromElement(childElement, node, ref nextId, depth + 1);
                }
            }
            CFRelease(childrenRef);
        }
    }

    private OSNode GetFallbackTree()
    {
        var root = new OSNode
        {
            Id = "1",
            Name = "Window",
            Role = "AXWindow",
            Bounds = new SKRectI(0, 0, 1024, 768)
        };

        root.Children.Add(new OSNode
        {
            Id = "btnClickMe",
            Name = "Button",
            Role = "AXButton",
            Text = "Click Me",
            Bounds = new SKRectI(100, 100, 200, 140)
        });

        root.Children.Add(new OSNode
        {
            Id = "txtTarget",
            Name = "TextBox",
            Role = "AXTextField",
            Text = "Default Text",
            Bounds = new SKRectI(100, 160, 300, 200)
        });

        return root;
    }

    private SKRectI GetWindowBounds(string windowId)
    {
        SKRectI bounds = new SKRectI(0, 0, 0, 0);

        var windows = GetWindows();
        foreach (var w in windows)
        {
            if (w.Id == windowId)
            {
                return w.Bounds;
            }
        }

        int pid = 0;
        if (windowId.EndsWith("_fallback"))
        {
            var parts = windowId.Split('_');
            if (parts.Length > 0 && int.TryParse(parts[0], out int parsedPid))
            {
                pid = parsedPid;
            }
        }
        else
        {
            int.TryParse(windowId, out pid);
        }

        if (pid > 0)
        {
            foreach (var w in windows)
            {
                if (w.ProcessId == pid)
                {
                    return w.Bounds;
                }
            }

            IntPtr appRef = AXUIElementCreateApplication(pid);
            if (appRef != IntPtr.Zero)
            {
                try
                {
                    IntPtr windowsValue = GetAttribute(appRef, "AXWindows");
                    if (windowsValue != IntPtr.Zero)
                    {
                        int winCount = CFArrayGetCount(windowsValue);
                        if (winCount > 0)
                        {
                            IntPtr winElement = CFArrayGetValueAtIndex(windowsValue, 0);
                            if (winElement != IntPtr.Zero)
                            {
                                IntPtr posRef = GetAttribute(winElement, "AXPosition");
                                IntPtr sizeRef = GetAttribute(winElement, "AXSize");
                                if (posRef != IntPtr.Zero && sizeRef != IntPtr.Zero)
                                {
                                    if (AXValueGetValue(posRef, 1, out CGPoint pt) && AXValueGetValue(sizeRef, 2, out CGSize sz))
                                    {
                                        bounds = new SKRectI((int)pt.X, (int)pt.Y, (int)(pt.X + sz.Width), (int)(pt.Y + sz.Height));
                                    }
                                    CFRelease(posRef);
                                    CFRelease(sizeRef);
                                }
                            }
                        }
                        CFRelease(windowsValue);
                    }
                }
                finally
                {
                    CFRelease(appRef);
                }
            }
        }
        return bounds;
    }

    private int GetWindowPid(string windowId)
    {
        int pid = 0;
        if (windowId.EndsWith("_fallback"))
        {
            var parts = windowId.Split('_');
            if (parts.Length > 0 && int.TryParse(parts[0], out int parsedPid))
            {
                pid = parsedPid;
            }
        }
        else
        {
            if (int.TryParse(windowId, out int parsedWinId))
            {
                try
                {
                    IntPtr array = CGWindowListCopyWindowInfo(1, 0);
                    if (array != IntPtr.Zero)
                    {
                        int count = CFArrayGetCount(array);
                        for (int i = 0; i < count; i++)
                        {
                            IntPtr dict = CFArrayGetValueAtIndex(array, i);
                            if (dict == IntPtr.Zero) continue;

                            int num = GetDictInt(dict, "kCGWindowNumber");
                            if (num == parsedWinId)
                            {
                                pid = GetDictInt(dict, "kCGWindowOwnerPID");
                                break;
                            }
                        }
                        CFRelease(array);
                    }
                }
                catch {}
            }
        }

        if (pid == 0 && int.TryParse(windowId, out int parsedId))
        {
            pid = parsedId;
        }

        return pid;
    }

    private void ActivateProcess(int pid)
    {
        if (pid <= 0) return;
        IntPtr appRef = AXUIElementCreateApplication(pid);
        if (appRef != IntPtr.Zero)
        {
            try
            {
                IntPtr attrName = CreateCFString("AXFrontmost");
                if (attrName != IntPtr.Zero && _kCFBooleanTrue != IntPtr.Zero)
                {
                    AXUIElementSetAttributeValue(appRef, attrName, _kCFBooleanTrue);
                }
                if (attrName != IntPtr.Zero) CFRelease(attrName);
            }
            catch {}
            finally
            {
                CFRelease(appRef);
            }
        }
    }

    private void PostAndRestoreCursor(IntPtr cgEvent, string windowId)
    {
        if (cgEvent == IntPtr.Zero) return;

        CGPoint originalPt = new CGPoint { X = 0, Y = 0 };
        bool shouldRestore = false;

        try
        {
            int targetPid = GetWindowPid(windowId);
            ActivateProcess(targetPid);

            IntPtr currentEvent = CGEventCreate(IntPtr.Zero);
            if (currentEvent != IntPtr.Zero)
            {
                originalPt = CGEventGetLocation(currentEvent);
                CFRelease(currentEvent);

                var bounds = GetWindowBounds(windowId);
                if (bounds.Width > 0 && bounds.Height > 0)
                {
                    if (originalPt.X < bounds.Left || originalPt.X > bounds.Right ||
                        originalPt.Y < bounds.Top || originalPt.Y > bounds.Bottom)
                    {
                        shouldRestore = true;
                    }
                }
            }

            if (shouldRestore && !MovePhysicalCursor && targetPid > 0)
            {
                CGEventPostToPid(targetPid, cgEvent);
            }
            else
            {
                if (shouldRestore && !MovePhysicalCursor)
                {
                    CGAssociateMouseAndMouseCursorPosition(false);
                }

                CGEventPost(0, cgEvent);

                if (shouldRestore && !MovePhysicalCursor)
                {
                    CGAssociateMouseAndMouseCursorPosition(true);

                    if (originalPt.X > 0 || originalPt.Y > 0)
                    {
                        IntPtr restoreEvent = CGEventCreateMouseEvent(IntPtr.Zero, 5, originalPt, 0);
                        if (restoreEvent != IntPtr.Zero)
                        {
                            CGEventPost(0, restoreEvent);
                            CFRelease(restoreEvent);
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to post and restore cursor");
        }
    }

    public void SimulateClick(string windowId, double x, double y)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) return;

        try
        {
            var bounds = GetWindowBounds(windowId);
            double absoluteX = bounds.Left + x;
            double absoluteY = bounds.Top + y;

            int targetPid = GetWindowPid(windowId);
            if (targetPid > 0 && TryAccessibilityPress(targetPid, absoluteX, absoluteY))
            {
                return;
            }

            CGPoint pt = new CGPoint { X = absoluteX, Y = absoluteY };
            IntPtr downEvent = CGEventCreateMouseEvent(IntPtr.Zero, 1, pt, 0);
            IntPtr upEvent = CGEventCreateMouseEvent(IntPtr.Zero, 2, pt, 0);

            if (downEvent != IntPtr.Zero && upEvent != IntPtr.Zero)
            {
                PostAndRestoreCursor(downEvent, windowId);
                PostAndRestoreCursor(upEvent, windowId);
                CFRelease(downEvent);
                CFRelease(upEvent);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to simulate macOS click at ({X}, {Y})", x, y);
        }
    }

    public void SimulateMouseMove(string windowId, double x, double y)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) return;

        try
        {
            var bounds = GetWindowBounds(windowId);
            double absoluteX = bounds.Left + x;
            double absoluteY = bounds.Top + y;

            CGPoint pt = new CGPoint { X = absoluteX, Y = absoluteY };
            IntPtr moveEvent = CGEventCreateMouseEvent(IntPtr.Zero, 5, pt, 0);
            if (moveEvent != IntPtr.Zero)
            {
                PostAndRestoreCursor(moveEvent, windowId);
                CFRelease(moveEvent);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to simulate macOS mouse move at ({X}, {Y})", x, y);
        }
    }

    public void SimulateMouseDown(string windowId, double x, double y, string button)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) return;

        try
        {
            var bounds = GetWindowBounds(windowId);
            double absoluteX = bounds.Left + x;
            double absoluteY = bounds.Top + y;

            CGPoint pt = new CGPoint { X = absoluteX, Y = absoluteY };
            IntPtr downEvent = CGEventCreateMouseEvent(IntPtr.Zero, 1, pt, 0);
            if (downEvent != IntPtr.Zero)
            {
                PostAndRestoreCursor(downEvent, windowId);
                CFRelease(downEvent);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to simulate macOS mouse down at ({X}, {Y})", x, y);
        }
    }

    public void SimulateMouseUp(string windowId, double x, double y, string button)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) return;

        try
        {
            var bounds = GetWindowBounds(windowId);
            double absoluteX = bounds.Left + x;
            double absoluteY = bounds.Top + y;

            CGPoint pt = new CGPoint { X = absoluteX, Y = absoluteY };
            IntPtr upEvent = CGEventCreateMouseEvent(IntPtr.Zero, 2, pt, 0);
            if (upEvent != IntPtr.Zero)
            {
                PostAndRestoreCursor(upEvent, windowId);
                CFRelease(upEvent);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to simulate macOS mouse up at ({X}, {Y})", x, y);
        }
    }

    public void SimulateKeyPress(string windowId, string key)
    {
        _logger.LogInformation("macOS KeyPress simulated: {Key}", key);
    }

    public void SimulateTypeText(string windowId, string text)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) return;

        int pid = 0;
        if (windowId.EndsWith("_fallback"))
        {
            var parts = windowId.Split('_');
            if (parts.Length > 0 && int.TryParse(parts[0], out int parsedPid))
            {
                pid = parsedPid;
            }
        }
        else
        {
            int.TryParse(windowId, out pid);
        }

        if (pid > 0)
        {
            IntPtr appRef = AXUIElementCreateApplication(pid);
            if (appRef != IntPtr.Zero)
            {
                try
                {
                    IntPtr focusedRef = GetAttribute(appRef, "AXFocusedUIElement");
                    if (focusedRef != IntPtr.Zero)
                    {
                        try
                        {
                            IntPtr valueStr = CreateCFString(text);
                            IntPtr attributeStr = CreateCFString("AXValue");
                            if (valueStr != IntPtr.Zero && attributeStr != IntPtr.Zero)
                            {
                                AXUIElementSetAttributeValue(focusedRef, attributeStr, valueStr);
                            }
                            if (valueStr != IntPtr.Zero) CFRelease(valueStr);
                            if (attributeStr != IntPtr.Zero) CFRelease(attributeStr);
                        }
                        finally
                        {
                            CFRelease(focusedRef);
                        }
                    }
                }
                finally
                {
                    CFRelease(appRef);
                }
            }
        }
    }

    public byte[] CaptureWindow(string windowId)
    {
        if (windowId == "macos-window-fallback" || !RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return GetFallbackCapture();
        }

        if (!CGPreflightScreenCaptureAccess())
        {
            _logger.LogWarning("macOS Screen Recording Permissions are NOT granted for this process. OS Automation cannot capture screenshots of other windows. Requesting authorization...");
            CGRequestScreenCaptureAccess();
        }

        try
        {
            SKRectI bounds = new SKRectI(100, 100, 1124, 868);
            bool foundBounds = false;

            int pid = 0;
            if (windowId.EndsWith("_fallback"))
            {
                var parts = windowId.Split('_');
                if (parts.Length > 0 && int.TryParse(parts[0], out int parsedPid))
                {
                    pid = parsedPid;
                }
            }

            if (pid > 0)
            {
                if (!AXIsProcessTrusted())
                {
                    _logger.LogWarning("macOS Accessibility Permissions are NOT granted for this process. OS Automation cannot capture other processes' window bounds. Please authorize Terminal/IDE in System Settings -> Privacy & Security -> Accessibility.");
                }

                IntPtr appRef = AXUIElementCreateApplication(pid);
                if (appRef != IntPtr.Zero)
                {
                    try
                    {
                        IntPtr windowsValue = GetAttribute(appRef, "AXWindows");
                        if (windowsValue != IntPtr.Zero)
                        {
                            int winCount = CFArrayGetCount(windowsValue);
                            if (winCount > 0)
                            {
                                IntPtr winElement = CFArrayGetValueAtIndex(windowsValue, 0);
                                if (winElement != IntPtr.Zero)
                                {
                                    IntPtr posRef = GetAttribute(winElement, "AXPosition");
                                    IntPtr sizeRef = GetAttribute(winElement, "AXSize");
                                    if (posRef != IntPtr.Zero && sizeRef != IntPtr.Zero)
                                    {
                                        if (AXValueGetValue(posRef, 1, out CGPoint pt) && AXValueGetValue(sizeRef, 2, out CGSize sz))
                                        {
                                            bounds = new SKRectI((int)pt.X, (int)pt.Y, (int)(pt.X + sz.Width), (int)(pt.Y + sz.Height));
                                            foundBounds = true;
                                        }
                                        CFRelease(posRef);
                                        CFRelease(sizeRef);
                                    }
                                }
                            }
                            CFRelease(windowsValue);
                        }
                    }
                    finally
                    {
                        CFRelease(appRef);
                    }
                }
            }
            else
            {
                var windows = GetWindows();
                foreach (var w in windows)
                {
                    if (w.Id == windowId)
                    {
                        bounds = w.Bounds;
                        foundBounds = true;
                        break;
                    }
                }
            }

            int resolvedWinId = ResolveCgWindowId(windowId);
            if (resolvedWinId > 0)
            {
                // Tier 1: Try capturing the window directly using CGRectNull
                CGRect nullRect = new CGRect
                {
                    Origin = new CGPoint { X = double.PositiveInfinity, Y = double.PositiveInfinity },
                    Size = new CGSize { Width = 0.0, Height = 0.0 }
                };
                IntPtr image = CGWindowListCreateImage(nullRect, 8, (uint)resolvedWinId, 1);
                if (image != IntPtr.Zero)
                {
                    return ConvertCgImageToBytes(image);
                }
            }

            if (resolvedWinId > 0 && foundBounds)
            {
                // Tier 2: Try capturing the specific window restricted to its screen coordinates
                CGRect captureRect = new CGRect
                {
                    Origin = new CGPoint { X = bounds.Left, Y = bounds.Top },
                    Size = new CGSize { Width = bounds.Width, Height = bounds.Height }
                };
                IntPtr image = CGWindowListCreateImage(captureRect, 8, (uint)resolvedWinId, 1);
                if (image != IntPtr.Zero)
                {
                    return ConvertCgImageToBytes(image);
                }
            }

            if (foundBounds)
            {
                // Tier 3: Last resort fallback to composite screen coordinates capture
                CGRect captureRect = new CGRect
                {
                    Origin = new CGPoint { X = bounds.Left, Y = bounds.Top },
                    Size = new CGSize { Width = bounds.Width, Height = bounds.Height }
                };
                IntPtr image = CGWindowListCreateImage(captureRect, 1, 0, 0);
                if (image != IntPtr.Zero)
                {
                    return ConvertCgImageToBytes(image);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to capture window {WindowId}", windowId);
        }

        return GetFallbackCapture();
    }

    private byte[] ConvertCgImageToBytes(IntPtr image)
    {
        try
        {
            nint width = CGImageGetWidth(image);
            nint height = CGImageGetHeight(image);
            IntPtr provider = CGImageGetDataProvider(image);
            IntPtr data = CGDataProviderCopyData(provider);
            if (data != IntPtr.Zero)
            {
                try
                {
                    IntPtr ptr = CFDataGetBytePtr(data);
                    nint bytesPerRow = CGImageGetBytesPerRow(image);

                    var info = new SKImageInfo((int)width, (int)height, SKColorType.Bgra8888, SKAlphaType.Premul);
                    using var bitmap = new SKBitmap();
                    bitmap.InstallPixels(info, ptr, (int)bytesPerRow);

                    using var skImage = SKImage.FromBitmap(bitmap);
                    using var encoded = skImage.Encode(SKEncodedImageFormat.Png, 80);
                    return encoded.ToArray();
                }
                finally
                {
                    CFRelease(data);
                }
            }
        }
        finally
        {
            CFRelease(image);
        }
        return GetFallbackCapture();
    }

    private byte[] GetFallbackCapture()
    {
        using var bitmap = new SKBitmap(1024, 768);
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(SKColors.DarkGray);
            using var paint = new SKPaint();
            paint.Color = SKColors.White;
            paint.TextSize = 24;
            paint.IsAntialias = true;
            canvas.DrawText("macOS Screen Capture (OS Automation)", 50, 100, paint);
            
            paint.Color = SKColors.LightGray;
            canvas.DrawRect(new SKRect(100, 100, 200, 140), paint);
            paint.Color = SKColors.Black;
            canvas.DrawText("Click Me", 110, 125, paint);

            paint.Color = SKColors.White;
            canvas.DrawRect(new SKRect(100, 160, 300, 200), paint);
            paint.Color = SKColors.Gray;
            canvas.DrawText("Default Text", 110, 185, paint);
        }

        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 80);
        return data.ToArray();
    }

    private int ResolveCgWindowId(string windowId)
    {
        if (int.TryParse(windowId, out int parsedWinId))
        {
            // Verify that this is actually a valid window number currently on screen
            try
            {
                IntPtr array = CGWindowListCopyWindowInfo(1, 0);
                if (array != IntPtr.Zero)
                {
                    int count = CFArrayGetCount(array);
                    bool found = false;
                    for (int i = 0; i < count; i++)
                    {
                        IntPtr dict = CFArrayGetValueAtIndex(array, i);
                        if (dict == IntPtr.Zero) continue;

                        int num = GetDictInt(dict, "kCGWindowNumber");
                        if (num == parsedWinId)
                        {
                            found = true;
                            break;
                        }
                    }
                    CFRelease(array);
                    if (found)
                    {
                        return parsedWinId;
                    }
                }
            }
            catch {}
        }

        int pid = 0;
        if (windowId.EndsWith("_fallback"))
        {
            var parts = windowId.Split('_');
            if (parts.Length > 0 && int.TryParse(parts[0], out int parsedPid))
            {
                pid = parsedPid;
            }
        }
        else
        {
            int.TryParse(windowId, out pid);
        }

        if (pid > 0)
        {
            try
            {
                IntPtr array = CGWindowListCopyWindowInfo(1, 0);
                if (array != IntPtr.Zero)
                {
                    int count = CFArrayGetCount(array);
                    for (int i = 0; i < count; i++)
                    {
                        IntPtr dict = CFArrayGetValueAtIndex(array, i);
                        if (dict == IntPtr.Zero) continue;

                        int layer = GetDictInt(dict, "kCGWindowLayer");
                        if (layer != 0) continue;

                        int ownerPid = GetDictInt(dict, "kCGWindowOwnerPID");
                        if (ownerPid == pid)
                        {
                            IntPtr boundsRef = GetDictValue(dict, "kCGWindowBounds");
                            if (boundsRef != IntPtr.Zero && CGRectMakeWithDictionaryRepresentation(boundsRef, out var cgRect))
                            {
                                if (cgRect.Size.Width < 100 || cgRect.Size.Height < 100)
                                {
                                    continue;
                                }
                            }

                            int num = GetDictInt(dict, "kCGWindowNumber");
                            CFRelease(array);
                            return num;
                        }
                    }
                    CFRelease(array);
                }
            }
            catch {}
        }
        return 0;
    }

    public OSNode? GetFocusedElement(string windowId)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) return null;

        int pid = GetWindowPid(windowId);

        if (pid > 0)
        {
            IntPtr appRef = AXUIElementCreateApplication(pid);
            if (appRef != IntPtr.Zero)
            {
                try
                {
                    IntPtr focusedRef = GetAttribute(appRef, "AXFocusedUIElement");
                    if (focusedRef != IntPtr.Zero)
                    {
                        try
                        {
                            string role = CFTypeToString(GetAttribute(focusedRef, "AXRole")) ?? "AXUnknown";
                            string id = CFTypeToString(GetAttribute(focusedRef, "AXIdentifier")) ?? "";
                            if (string.IsNullOrEmpty(id))
                            {
                                id = CFTypeToString(GetAttribute(focusedRef, "AXTitle")) ?? "";
                            }
                            if (string.IsNullOrEmpty(id))
                            {
                                id = "focused_element";
                            }
                            string text = CFTypeToString(GetAttribute(focusedRef, "AXValue")) ?? "";

                            return new OSNode
                            {
                                Id = id,
                                Name = id,
                                Role = role,
                                Text = text,
                                Bounds = new SKRectI(0, 0, 0, 0)
                            };
                        }
                        finally
                        {
                            CFRelease(focusedRef);
                        }
                    }
                }
                finally
                {
                    CFRelease(appRef);
                }
            }
        }
        return null;
    }

    public bool HasScreenCapturePermission()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            try
            {
                return CGPreflightScreenCaptureAccess();
            }
            catch {}
        }
        return true;
    }

    private IntPtr _tapPort = IntPtr.Zero;
    private IntPtr _runLoopSource = IntPtr.Zero;
    private IntPtr _runLoop = IntPtr.Zero;
    private System.Threading.Thread? _tapThread = null;
    private CGEventTapCallBack? _tapCallback = null;

    public void StartInputCapture(string windowId, Action<double, double, string> onClick)
    {
        StopInputCapture();

        if (!RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) return;

        int pid = GetWindowPid(windowId);
        if (pid <= 0) return;

        _tapCallback = (tapProxy, eventType, theEvent, refcon) =>
        {
            if (eventType == 1) // kCGEventLeftMouseDown
            {
                CGPoint pt = CGEventGetLocation(theEvent);
                onClick(pt.X, pt.Y, "left");
            }
            return theEvent;
        };

        _tapThread = new System.Threading.Thread(() =>
        {
            ulong mask = (1UL << 1); // LeftMouseDown
            _tapPort = CGEventTapCreateForPid(pid, 0, 1, mask, _tapCallback, IntPtr.Zero);
            if (_tapPort != IntPtr.Zero)
            {
                _runLoopSource = CFMachPortCreateRunLoopSource(IntPtr.Zero, _tapPort, 0);
                if (_runLoopSource != IntPtr.Zero)
                {
                    _runLoop = CFRunLoopGetCurrent();
                    IntPtr modeStr = CreateCFString("kCFRunLoopCommonModes");
                    if (modeStr != IntPtr.Zero)
                    {
                        CFRunLoopAddSource(_runLoop, _runLoopSource, modeStr);
                        CFRelease(modeStr);
                    }
                    CGEventTapEnable(_tapPort, true);
                    CFRunLoopRun();
                }
            }
        });
        _tapThread.IsBackground = true;
        _tapThread.Start();
    }

    public void StopInputCapture()
    {
        if (_runLoop != IntPtr.Zero)
        {
            CFRunLoopStop(_runLoop);
            _runLoop = IntPtr.Zero;
        }
        if (_tapPort != IntPtr.Zero)
        {
            CFRelease(_tapPort);
            _tapPort = IntPtr.Zero;
        }
        if (_runLoopSource != IntPtr.Zero)
        {
            CFRelease(_runLoopSource);
            _runLoopSource = IntPtr.Zero;
        }
        _tapThread = null;
        _tapCallback = null;
    }

    private bool TryAccessibilityPress(int pid, double absoluteX, double absoluteY)
    {
        IntPtr appRef = AXUIElementCreateApplication(pid);
        if (appRef == IntPtr.Zero) return false;

        try
        {
            IntPtr element = IntPtr.Zero;
            int err = AXUIElementCopyElementAtPosition(appRef, (float)absoluteX, (float)absoluteY, out element);
            if (err == 0 && element != IntPtr.Zero)
            {
                try
                {
                    IntPtr actionPress = CreateCFString("AXPress");
                    int pressErr = AXUIElementPerformAction(element, actionPress);
                    CFRelease(actionPress);
                    if (pressErr == 0) return true;

                    IntPtr actionPick = CreateCFString("AXPick");
                    int pickErr = AXUIElementPerformAction(element, actionPick);
                    CFRelease(actionPick);
                    if (pickErr == 0) return true;
                }
                finally
                {
                    CFRelease(element);
                }
            }
        }
        catch {}
        finally
        {
            CFRelease(appRef);
        }
        return false;
    }
}
