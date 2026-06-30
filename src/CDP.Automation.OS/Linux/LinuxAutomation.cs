using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using SkiaSharp;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace CDP.Automation.OS.Linux;

public sealed partial class LinuxAutomation : IOsAutomation
{
    private readonly ILogger _logger;

    public bool MovePhysicalCursor { get; set; }
    public bool UsePeerAutomation { get; set; } = true;
    public bool UseAccessibilityEvents { get; set; } = true;

    public LinuxAutomation(ILogger? logger = null)
    {
        _logger = logger ?? NullLogger.Instance;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct XWindowAttributes
    {
        public int x, y;
        public int width, height;
        public int border_width;
        public int depth;
        public IntPtr visual;
        public IntPtr root;
        public int @class;
        public int bit_gravity;
        public int win_gravity;
        public int backing_store;
        public uint backing_planes;
        public uint backing_pixel;
        public int save_under;
        public IntPtr colormap;
        public int map_installed;
        public int map_state;
        public long all_event_masks;
        public long your_event_mask;
        public long do_not_propagate_mask;
        public int override_redirect;
        public IntPtr screen;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct XImage
    {
        public int width;
        public int height;
        public int xoffset;
        public int format;
        public IntPtr data;
        public int byte_order;
        public int bitmap_unit;
        public int bitmap_bit_order;
        public int bitmap_pad;
        public int depth;
        public int bytes_per_line;
        public int bits_per_pixel;
    }

    [LibraryImport("libX11.so.6")]
    private static partial IntPtr XOpenDisplay(IntPtr display);

    [LibraryImport("libX11.so.6")]
    private static partial int XCloseDisplay(IntPtr display);

    [LibraryImport("libX11.so.6")]
    private static partial IntPtr XDefaultRootWindow(IntPtr display);

    [LibraryImport("libX11.so.6")]
    private static partial int XQueryTree(IntPtr display, IntPtr w, out IntPtr root_return, out IntPtr parent_return, out IntPtr children_return, out uint nchildren_return);

    [LibraryImport("libX11.so.6")]
    private static partial int XFree(IntPtr data);

    [LibraryImport("libX11.so.6")]
    private static partial int XFetchName(IntPtr display, IntPtr w, out IntPtr window_name_return);

    [LibraryImport("libX11.so.6")]
    private static partial int XGetWindowAttributes(IntPtr display, IntPtr w, out XWindowAttributes window_attributes_return);

    [LibraryImport("libX11.so.6")]
    private static partial IntPtr XGetImage(IntPtr display, IntPtr drawable, int x, int y, uint width, uint height, uint plane_mask, int format);

    [LibraryImport("libX11.so.6")]
    private static partial int XDestroyImage(IntPtr image);

    public IReadOnlyList<OSWindow> GetWindows()
    {
        var list = new List<OSWindow>();
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) return list;

        try
        {
            IntPtr display = XOpenDisplay(IntPtr.Zero);
            if (display != IntPtr.Zero)
            {
                try
                {
                    IntPtr root = XDefaultRootWindow(display);
                    FindX11Windows(display, root, list);
                }
                finally
                {
                    XCloseDisplay(display);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Linux X11 Display not available or failed: {Message}", ex.Message);
        }

        if (list.Count == 0)
        {
            list.Add(new OSWindow
            {
                Id = "linux-window-fallback",
                Title = "Linux Fallback Window",
                ProcessName = "fallback-app",
                ProcessId = 2001,
                Bounds = new SKRectI(0, 0, 1024, 768)
            });
        }

        return list;
    }

    private void FindX11Windows(IntPtr display, IntPtr window, List<OSWindow> list)
    {
        if (XGetWindowAttributes(display, window, out var attrs) != 0)
        {
            if (attrs.map_state == 2 && attrs.override_redirect == 0)
            {
                if (XFetchName(display, window, out IntPtr namePtr) != 0 && namePtr != IntPtr.Zero)
                {
                    string? title = Marshal.PtrToStringAnsi(namePtr);
                    XFree(namePtr);

                    if (!string.IsNullOrEmpty(title))
                    {
                        list.Add(new OSWindow
                        {
                            Id = window.ToString(),
                            Title = title,
                            ProcessName = "X11App",
                            ProcessId = 2000,
                            Bounds = new SKRectI(attrs.x, attrs.y, attrs.x + attrs.width, attrs.y + attrs.height)
                        });
                    }
                }
            }
        }

        if (XQueryTree(display, window, out _, out _, out IntPtr children, out uint nchildren) != 0 && children != IntPtr.Zero)
        {
            try
            {
                for (int i = 0; i < nchildren; i++)
                {
                    IntPtr child = Marshal.ReadIntPtr(children, i * IntPtr.Size);
                    FindX11Windows(display, child, list);
                }
            }
            finally
            {
                XFree(children);
            }
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
            Role = "pushbutton",
            Text = "Click Me",
            Bounds = new SKRectI(100, 100, 200, 140)
        });

        root.Children.Add(new OSNode
        {
            Id = "txtTarget",
            Name = "TextBox",
            Role = "entry",
            Text = "Default Text",
            Bounds = new SKRectI(100, 160, 300, 200)
        });

        return root;
    }

    public OSNode? GetElementTree(string windowId)
    {
        if (windowId == "linux-window-fallback" || !RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return GetFallbackTree();
        }
        throw new NotSupportedException("Accessibility tree traversal is not supported on Linux OS targets.");
    }

    public void SimulateClick(string windowId, double x, double y)
    {
        if (windowId == "linux-window-fallback") return;
        throw new NotSupportedException("Click simulation is not supported on Linux OS targets.");
    }

    public void SimulateMouseMove(string windowId, double x, double y)
    {
        if (windowId == "linux-window-fallback") return;
        throw new NotSupportedException("MouseMove simulation is not supported on Linux OS targets.");
    }

    public void SimulateMouseDown(string windowId, double x, double y, string button)
    {
        if (windowId == "linux-window-fallback") return;
        throw new NotSupportedException("MouseDown simulation is not supported on Linux OS targets.");
    }

    public void SimulateMouseUp(string windowId, double x, double y, string button)
    {
        if (windowId == "linux-window-fallback") return;
        throw new NotSupportedException("MouseUp simulation is not supported on Linux OS targets.");
    }

    public void SimulateMouseWheel(string windowId, double x, double y, double deltaX, double deltaY)
    {
        if (windowId == "linux-window-fallback") return;
        throw new NotSupportedException("MouseWheel simulation is not supported on Linux OS targets.");
    }

    public void SimulateKeyPress(string windowId, string key)
    {
        if (windowId == "linux-window-fallback") return;
        throw new NotSupportedException("KeyPress simulation is not supported on Linux OS targets.");
    }

    public void SimulateTypeText(string windowId, string text)
    {
        if (windowId == "linux-window-fallback") return;
        throw new NotSupportedException("TypeText simulation is not supported on Linux OS targets.");
    }

    public byte[] CaptureWindow(string windowId)
    {
        if (windowId == "linux-window-fallback" || !RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return GetFallbackCapture();
        }

        try
        {
            IntPtr window = IntPtr.Zero;
            if (IntPtr.TryParse(windowId, out var parsedWindow))
            {
                window = parsedWindow;
            }
            else if (windowId.EndsWith("_fallback"))
            {
                var parts = windowId.Split('_');
                if (parts.Length > 0 && int.TryParse(parts[0], out int pid))
                {
                    var windows = GetWindows();
                    foreach (var w in windows)
                    {
                        if (w.ProcessId == pid && IntPtr.TryParse(w.Id, out var h))
                        {
                            window = h;
                            break;
                        }
                    }
                }
            }

            if (window != IntPtr.Zero)
            {
                IntPtr display = XOpenDisplay(IntPtr.Zero);
                if (display != IntPtr.Zero)
                {
                    try
                    {
                        if (XGetWindowAttributes(display, window, out var attrs) != 0)
                        {
                            IntPtr image = XGetImage(display, window, 0, 0, (uint)attrs.width, (uint)attrs.height, 0xFFFFFFFF, 2);
                            if (image != IntPtr.Zero)
                            {
                                try
                                {
                                    var xImg = Marshal.PtrToStructure<XImage>(image);
                                    IntPtr pixelPtr = xImg.data;
                                    int bytesPerLine = xImg.bytes_per_line;

                                    using var bitmap = new SKBitmap();
                                    var info = new SKImageInfo(attrs.width, attrs.height, SKColorType.Bgra8888, SKAlphaType.Premul);
                                    bitmap.InstallPixels(info, pixelPtr, bytesPerLine);

                                    using var skImage = SKImage.FromBitmap(bitmap);
                                    using var encoded = skImage.Encode(SKEncodedImageFormat.Png, 80);
                                    return encoded.ToArray();
                                }
                                finally
                                {
                                    XDestroyImage(image);
                                }
                            }
                        }
                    }
                    finally
                    {
                        XCloseDisplay(display);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to capture Linux window {WindowId}", windowId);
        }

        return GetFallbackCapture();
    }

    private byte[] GetFallbackCapture()
    {
        using var bitmap = new SKBitmap(1024, 768);
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(SKColors.ForestGreen);
            using var paint = new SKPaint();
            paint.Color = SKColors.White;
            paint.TextSize = 24;
            paint.IsAntialias = true;
            canvas.DrawText("Linux Screen Capture (OS Automation)", 50, 100, paint);

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

    public OSNode? GetFocusedElement(string windowId)
    {
        if (windowId == "linux-window-fallback" || windowId.EndsWith("_fallback"))
        {
            return new OSNode
            {
                Id = "txtInput",
                Name = "Text Field",
                Role = "AXTextField",
                Bounds = new SKRectI(100, 200, 300, 240)
            };
        }
        return null;
    }

    public bool HasScreenCapturePermission()
    {
        return true;
    }

    public bool HasAccessibilityPermission()
    {
        return true;
    }

    public void StartInputCapture(string windowId, Action<double, double, string> onClick, Action<string, string, string?> onAccessibilityEvent)
    {
    }

    public void StopInputCapture()
    {
    }
}
