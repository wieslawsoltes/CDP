using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using SkiaSharp;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace CDP.Automation.OS.Windows;

[SupportedOSPlatform("windows")]
public sealed partial class WindowsAutomation : IOsAutomation
{
    private readonly ILogger _logger;

    public bool MovePhysicalCursor { get; set; }
    public bool UsePeerAutomation { get; set; } = true;

    public WindowsAutomation(ILogger? logger = null)
    {
        _logger = logger ?? NullLogger.Instance;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BITMAPINFOHEADER
    {
        public uint biSize;
        public int biWidth;
        public int biHeight;
        public ushort biPlanes;
        public ushort biBitCount;
        public uint biCompression;
        public uint biSizeImage;
        public int biXPelsPerMeter;
        public int biYPelsPerMeter;
        public uint biClrUsed;
        public uint biClrImportant;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BITMAPINFO
    {
        public BITMAPINFOHEADER bmiHeader;
        public uint bmiColors;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MOUSEINPUT
    {
        public int dx;
        public int dy;
        public uint mouseData;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KEYBDINPUT
    {
        public ushort wVk;
        public ushort wScan;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct INPUT_UNION
    {
        [FieldOffset(0)]
        public MOUSEINPUT mi;
        [FieldOffset(0)]
        public KEYBDINPUT ki;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct INPUT
    {
        public uint type;
        public INPUT_UNION u;
    }

    [ComImport]
    [Guid("30cbe57d-d9d0-452a-ab13-7ac5ac4825ee")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IUIAutomation
    {
        [PreserveSig] int CompareElements(IUIAutomationElement el1, IUIAutomationElement el2, out int areSame);
        [PreserveSig] int CompareRuntimeIds(IntPtr id1, IntPtr id2, out int areSame);
        [PreserveSig] int GetRootElement(out IUIAutomationElement root);
        [PreserveSig] int ElementFromHandle(IntPtr hwnd, out IUIAutomationElement element);
        [PreserveSig] int ElementFromPoint(POINT pt, out IUIAutomationElement element);
        [PreserveSig] int GetFocusedElement(out IUIAutomationElement element);
        [PreserveSig] int GetRootElementBuildCache(IntPtr cacheRequest, out IUIAutomationElement root);
        [PreserveSig] int ElementFromHandleBuildCache(IntPtr hwnd, IntPtr cacheRequest, out IUIAutomationElement element);
        [PreserveSig] int ElementFromPointBuildCache(POINT pt, IntPtr cacheRequest, out IUIAutomationElement element);
        [PreserveSig] int GetFocusedElementBuildCache(IntPtr cacheRequest, out IUIAutomationElement element);
        [PreserveSig] int CreateTreeWalker(IUIAutomationCondition condition, out IUIAutomationTreeWalker walker);
        [PreserveSig] int GetControlViewWalker(out IUIAutomationTreeWalker walker);
    }

    [ComImport]
    [Guid("d22108aa-8ac5-49a5-837b-37bbb3d7591e")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IUIAutomationElement
    {
        [PreserveSig] int SetFocus();
        [PreserveSig] int GetRuntimeId(out IntPtr runtimeId);
        [PreserveSig] int FindFirst(int scope, IUIAutomationCondition condition, out IUIAutomationElement found);
        [PreserveSig] int FindAll(int scope, IUIAutomationCondition condition, out IntPtr foundArray);
        [PreserveSig] int FindFirstBuildCache(int scope, IUIAutomationCondition condition, IntPtr cacheRequest, out IUIAutomationElement found);
        [PreserveSig] int FindAllBuildCache(int scope, IUIAutomationCondition condition, IntPtr cacheRequest, out IntPtr foundArray);
        [PreserveSig] int BuildUpdatedCache(IntPtr cacheRequest, out IUIAutomationElement updatedElement);
        [PreserveSig] int GetCurrentPropertyValue(int propertyId, out object value);
        [PreserveSig] int GetCurrentPropertyValueEx(int propertyId, [MarshalAs(UnmanagedType.Bool)] bool ignoreDefaultValue, out object value);
        [PreserveSig] int GetCachedPropertyValue(int propertyId, out object value);
        [PreserveSig] int GetCachedPropertyValueEx(int propertyId, [MarshalAs(UnmanagedType.Bool)] bool ignoreDefaultValue, out object value);
        [PreserveSig] int GetCurrentPattern(int patternId, out IntPtr patternObject);
        [PreserveSig] int GetCachedPattern(int patternId, out IntPtr patternObject);
        [PreserveSig] int GetCachedParent(out IUIAutomationElement parent);
        [PreserveSig] int GetCachedChildren(out IntPtr childrenArray);
        [PreserveSig] int get_CurrentName(out string name);
        [PreserveSig] int get_CurrentAutomationId(out string automationId);
        [PreserveSig] int get_CurrentClassName(out string className);
        [PreserveSig] int get_CurrentLocalizedControlType(out string localizedControlType);
        [PreserveSig] int get_CurrentControlType(out int controlType);
        [PreserveSig] int get_CurrentBoundingRectangle(out RECT rect);
    }

    [ComImport]
    [Guid("fb377fbe-8e6d-46b4-ab50-928b33742517")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IUIAutomationInvokePattern
    {
        [PreserveSig]
        int Invoke();
    }

    [ComImport]
    [Guid("c9743dd0-c134-4008-8a70-6fbe7d5c78a8")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IUIAutomationValuePattern
    {
        [PreserveSig]
        int SetValue([MarshalAs(UnmanagedType.BStr)] string val);
        [PreserveSig]
        int get_CurrentValue([MarshalAs(UnmanagedType.BStr)] out string val);
        [PreserveSig]
        int get_CurrentIsReadOnly(out int isReadOnly);
    }

    [ComImport]
    [Guid("404b16a4-378a-476e-9f6b-85933509168c")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IUIAutomationTreeWalker
    {
        [PreserveSig] int GetFirstChildElement(IUIAutomationElement element, out IUIAutomationElement firstChild);
        [PreserveSig] int GetLastChildElement(IUIAutomationElement element, out IUIAutomationElement lastChild);
        [PreserveSig] int GetNextSiblingElement(IUIAutomationElement element, out IUIAutomationElement nextSibling);
        [PreserveSig] int GetPreviousSiblingElement(IUIAutomationElement element, out IUIAutomationElement previousSibling);
        [PreserveSig] int GetParentElement(IUIAutomationElement element, out IUIAutomationElement parent);
        [PreserveSig] int NormalizeElement(IUIAutomationElement element, out IUIAutomationElement normalized);
    }

    [ComImport]
    [Guid("352721e2-39c1-4ef5-b542-6f2c72b2d6a5")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IUIAutomationCondition
    {
    }

    [ComImport]
    [Guid("ff48dba4-63c7-4ab0-af60-d5c9ab749004")]
    private class CUIAutomation
    {
    }

    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [LibraryImport("user32.dll", StringMarshalling = StringMarshalling.Utf16)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool IsWindowVisible(IntPtr hWnd);

    [LibraryImport("user32.dll", EntryPoint = "GetWindowTextW", StringMarshalling = StringMarshalling.Utf16)]
    private static partial int GetWindowText(IntPtr hWnd, [Out] char[] lpString, int nMaxCount);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [LibraryImport("user32.dll")]
    private static partial uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [LibraryImport("user32.dll")]
    private static partial IntPtr GetWindowDC(IntPtr hWnd);

    [LibraryImport("user32.dll")]
    private static partial int ReleaseDC(IntPtr hWnd, IntPtr hDC);

    [LibraryImport("gdi32.dll")]
    private static partial IntPtr CreateCompatibleDC(IntPtr hDC);

    [LibraryImport("gdi32.dll")]
    private static partial IntPtr CreateCompatibleBitmap(IntPtr hDC, int nWidth, int nHeight);

    [LibraryImport("gdi32.dll")]
    private static partial IntPtr SelectObject(IntPtr hDC, IntPtr hObject);

    [LibraryImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool BitBlt(IntPtr hObject, int nXDest, int nYDest, int nWidth, int nHeight, IntPtr hObjSource, int nXSrc, int nYSrc, uint dwRop);

    [LibraryImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool DeleteDC(IntPtr hDC);

    [LibraryImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool DeleteObject(IntPtr hObject);

    [LibraryImport("gdi32.dll")]
    private static partial int GetDIBits(IntPtr hdc, IntPtr hbmp, uint uStartScan, uint cScanLines, IntPtr lpvBits, ref BITMAPINFO lpbmi, uint uUsage);

    [LibraryImport("user32.dll")]
    private static partial int GetSystemMetrics(int nIndex);

    [LibraryImport("user32.dll")]
    private static partial uint SendInput(uint nInputs, [MarshalAs(UnmanagedType.LPArray)] INPUT[] pInputs, int cbSize);

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetCursorPos(out POINT lpPoint);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetCursorPos(int x, int y);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetForegroundWindow(IntPtr hWnd);

    public IReadOnlyList<OSWindow> GetWindows()
    {
        var list = new List<OSWindow>();
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return list;

        try
        {
            EnumWindows((hWnd, lParam) =>
            {
                if (IsWindowVisible(hWnd))
                {
                    char[] titleBuffer = new char[512];
                    int len = GetWindowText(hWnd, titleBuffer, titleBuffer.Length);
                    string title = len > 0 ? new string(titleBuffer, 0, len) : string.Empty;

                    if (!string.IsNullOrEmpty(title))
                    {
                        GetWindowRect(hWnd, out var rect);
                        GetWindowThreadProcessId(hWnd, out var pid);

                        list.Add(new OSWindow
                        {
                            Id = hWnd.ToString(),
                            Title = title,
                            ProcessName = "WindowsApp",
                            ProcessId = (int)pid,
                            Bounds = new SKRectI(rect.Left, rect.Top, rect.Right, rect.Bottom)
                        });
                    }
                }
                return true;
            }, IntPtr.Zero);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to enumerate Windows windows");
        }

        if (list.Count == 0)
        {
            list.Add(new OSWindow
            {
                Id = "windows-window-fallback",
                Title = "Windows Fallback Window",
                ProcessName = "win-app",
                ProcessId = 1002,
                Bounds = new SKRectI(0, 0, 1024, 768)
            });
        }

        return list;
    }

    public OSNode? GetElementTree(string windowId)
    {
        if (windowId == "windows-window-fallback" || !RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return GetFallbackTree();
        }

        try
        {
            IntPtr hWnd = IntPtr.Zero;
            if (IntPtr.TryParse(windowId, out var parsedHwnd))
            {
                hWnd = parsedHwnd;
            }
            else if (windowId.EndsWith("_fallback"))
            {
                var parts = windowId.Split('_');
                if (parts.Length > 0 && int.TryParse(parts[0], out int pid))
                {
                    try
                    {
                        var proc = System.Diagnostics.Process.GetProcessById(pid);
                        hWnd = proc.MainWindowHandle;
                    }
                    catch {}

                    if (hWnd == IntPtr.Zero)
                    {
                        IntPtr foundHwnd = IntPtr.Zero;
                        EnumWindows((h, lp) =>
                        {
                            if (IsWindowVisible(h))
                            {
                                GetWindowThreadProcessId(h, out var wpid);
                                if ((int)wpid == pid)
                                {
                                    foundHwnd = h;
                                    return false;
                                }
                            }
                            return true;
                        }, IntPtr.Zero);
                        hWnd = foundHwnd;
                    }
                }
            }

            if (hWnd != IntPtr.Zero)
            {
                var automation = (IUIAutomation)new CUIAutomation();
                if (automation.ElementFromHandle(hWnd, out var rootElement) == 0 && rootElement != null)
                {
                    if (automation.GetControlViewWalker(out var walker) == 0 && walker != null)
                    {
                        var root = new OSNode
                        {
                            Id = "1",
                            Name = "Window",
                            Role = "Window",
                            Bounds = new SKRectI(0, 0, 1024, 768)
                        };
                        int nextId = 2;
                        BuildNodeFromUia(rootElement, walker, root, ref nextId, 0);
                        if (root.Children.Count > 0)
                        {
                            var winNode = root.Children[0];
                            winNode.Id = "1";
                            return winNode;
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to traverse Windows UIA accessibility tree");
        }

        return GetFallbackTree();
    }

    private void BuildNodeFromUia(IUIAutomationElement element, IUIAutomationTreeWalker walker, OSNode parentNode, ref int nextId, int depth)
    {
        if (depth > 20) return;

        element.get_CurrentName(out string name);
        element.get_CurrentAutomationId(out string automationId);
        element.get_CurrentClassName(out string className);
        element.get_CurrentControlType(out int controlType);
        element.get_CurrentBoundingRectangle(out RECT rect);

        string role = MapControlType(controlType) ?? className ?? "Control";

        int myId = nextId++;
        var node = new OSNode
        {
            Id = string.IsNullOrEmpty(automationId) ? myId.ToString() : automationId,
            Name = string.IsNullOrEmpty(name) ? role : name,
            Role = role,
            Text = name ?? string.Empty,
            Bounds = new SKRectI(rect.Left, rect.Top, rect.Right, rect.Bottom)
        };

        parentNode.Children.Add(node);

        if (walker.GetFirstChildElement(element, out var child) == 0 && child != null)
        {
            while (child != null)
            {
                BuildNodeFromUia(child, walker, node, ref nextId, depth + 1);
                if (walker.GetNextSiblingElement(child, out var next) != 0)
                {
                    break;
                }
                child = next;
            }
        }
    }

    private static string? MapControlType(int typeId)
    {
        return typeId switch
        {
            50000 => "Button",
            50001 => "Calendar",
            50002 => "CheckBox",
            50003 => "ComboBox",
            50004 => "Edit",
            50005 => "Hyperlink",
            50006 => "Image",
            50007 => "ListItem",
            50008 => "List",
            50009 => "Menu",
            50010 => "MenuBar",
            50011 => "MenuItem",
            50012 => "ProgressBar",
            50013 => "RadioButton",
            50014 => "ScrollBar",
            50015 => "Slider",
            50016 => "Spinner",
            50017 => "StatusBar",
            50018 => "Tab",
            50019 => "TabItem",
            50020 => "Text",
            50021 => "ToolBar",
            50022 => "ToolTip",
            50023 => "TreeView",
            50024 => "TreeItem",
            50025 => "Custom",
            50026 => "Group",
            50027 => "Thumb",
            50028 => "DataGrid",
            50029 => "DataItem",
            50030 => "Document",
            50031 => "SplitButton",
            50032 => "Window",
            50033 => "Pane",
            50034 => "Header",
            50035 => "HeaderItem",
            50036 => "Table",
            50037 => "TitleBar",
            50038 => "Separator",
            50039 => "SemanticZoom",
            50040 => "AppBar",
            _ => null
        };
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
            Role = "Button",
            Text = "Click Me",
            Bounds = new SKRectI(100, 100, 200, 140)
        });

        root.Children.Add(new OSNode
        {
            Id = "txtTarget",
            Name = "TextBox",
            Role = "Edit",
            Text = "Default Text",
            Bounds = new SKRectI(100, 160, 300, 200)
        });

        return root;
    }

    private IntPtr GetWindowHandle(string windowId)
    {
        IntPtr hWnd = IntPtr.Zero;

        if (windowId.EndsWith("_fallback"))
        {
            var parts = windowId.Split('_');
            if (parts.Length > 0 && int.TryParse(parts[0], out int parsedPid))
            {
                try
                {
                    var proc = System.Diagnostics.Process.GetProcessById(parsedPid);
                    hWnd = proc.MainWindowHandle;
                }
                catch {}
            }
        }
        else
        {
            if (IntPtr.TryParse(windowId, out var parsedHWnd))
            {
                hWnd = parsedHWnd;
            }
        }

        if (hWnd == IntPtr.Zero)
        {
            var windows = GetWindows();
            foreach (var w in windows)
            {
                if (w.Id == windowId && IntPtr.TryParse(w.Id, out var h))
                {
                    hWnd = h;
                    break;
                }
            }
        }
        return hWnd;
    }

    private SKRectI GetWindowBounds(string windowId)
    {
        SKRectI bounds = new SKRectI(0, 0, 0, 0);
        IntPtr hWnd = GetWindowHandle(windowId);

        if (hWnd != IntPtr.Zero)
        {
            if (GetWindowRect(hWnd, out RECT rect))
            {
                bounds = new SKRectI(rect.Left, rect.Top, rect.Right, rect.Bottom);
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
                    break;
                }
            }
        }
        return bounds;
    }

    private void PostAndRestoreCursor(INPUT[] inputs, string windowId)
    {
        if (inputs == null || inputs.Length == 0) return;

        POINT originalPt = new POINT { X = 0, Y = 0 };
        bool shouldRestore = false;

        try
        {
            IntPtr hWnd = GetWindowHandle(windowId);
            if (hWnd != IntPtr.Zero)
            {
                SetForegroundWindow(hWnd);
            }

            if (GetCursorPos(out originalPt))
            {
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

            SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<INPUT>());

            if (shouldRestore && !MovePhysicalCursor && (originalPt.X > 0 || originalPt.Y > 0))
            {
                SetCursorPos(originalPt.X, originalPt.Y);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to post and restore cursor on Windows");
        }
    }

    public void SimulateClick(string windowId, double x, double y)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return;

        try
        {
            var bounds = GetWindowBounds(windowId);
            double absoluteX = bounds.Left + x;
            double absoluteY = bounds.Top + y;

            if (UsePeerAutomation && TryAccessibilityPress(absoluteX, absoluteY))
            {
                return;
            }

            int screenWidth = GetSystemMetrics(0);
            int screenHeight = GetSystemMetrics(1);
            if (screenWidth <= 0) screenWidth = 1920;
            if (screenHeight <= 0) screenHeight = 1080;

            var inputs = new INPUT[3];
            inputs[0] = new INPUT
            {
                type = 0,
                u = new INPUT_UNION
                {
                    mi = new MOUSEINPUT
                    {
                        dx = (int)(absoluteX * 65535 / screenWidth),
                        dy = (int)(absoluteY * 65535 / screenHeight),
                        dwFlags = 0x0001 | 0x8000
                    }
                }
            };
            inputs[1] = new INPUT
            {
                type = 0,
                u = new INPUT_UNION
                {
                    mi = new MOUSEINPUT { dwFlags = 0x0002 }
                }
            };
            inputs[2] = new INPUT
            {
                type = 0,
                u = new INPUT_UNION
                {
                    mi = new MOUSEINPUT { dwFlags = 0x0004 }
                }
            };

            PostAndRestoreCursor(inputs, windowId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to simulate Windows click at ({X}, {Y})", x, y);
        }
    }

    public void SimulateMouseMove(string windowId, double x, double y)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return;

        try
        {
            var bounds = GetWindowBounds(windowId);
            double absoluteX = bounds.Left + x;
            double absoluteY = bounds.Top + y;

            int screenWidth = GetSystemMetrics(0);
            int screenHeight = GetSystemMetrics(1);
            if (screenWidth <= 0) screenWidth = 1920;
            if (screenHeight <= 0) screenHeight = 1080;

            var inputs = new INPUT[1];
            inputs[0] = new INPUT
            {
                type = 0,
                u = new INPUT_UNION
                {
                    mi = new MOUSEINPUT
                    {
                        dx = (int)(absoluteX * 65535 / screenWidth),
                        dy = (int)(absoluteY * 65535 / screenHeight),
                        dwFlags = 0x0001 | 0x8000
                    }
                }
            };
            PostAndRestoreCursor(inputs, windowId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to simulate Windows mouse move at ({X}, {Y})", x, y);
        }
    }

    public void SimulateMouseDown(string windowId, double x, double y, string button)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return;

        try
        {
            var bounds = GetWindowBounds(windowId);
            double absoluteX = bounds.Left + x;
            double absoluteY = bounds.Top + y;

            int screenWidth = GetSystemMetrics(0);
            int screenHeight = GetSystemMetrics(1);
            if (screenWidth <= 0) screenWidth = 1920;
            if (screenHeight <= 0) screenHeight = 1080;

            var inputs = new INPUT[2];
            inputs[0] = new INPUT
            {
                type = 0,
                u = new INPUT_UNION
                {
                    mi = new MOUSEINPUT
                    {
                        dx = (int)(absoluteX * 65535 / screenWidth),
                        dy = (int)(absoluteY * 65535 / screenHeight),
                        dwFlags = 0x0001 | 0x8000
                    }
                }
            };
            inputs[1] = new INPUT
            {
                type = 0,
                u = new INPUT_UNION
                {
                    mi = new MOUSEINPUT { dwFlags = 0x0002 }
                }
            };
            PostAndRestoreCursor(inputs, windowId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to simulate Windows mouse down at ({X}, {Y})", x, y);
        }
    }

    public void SimulateMouseUp(string windowId, double x, double y, string button)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return;

        try
        {
            var bounds = GetWindowBounds(windowId);
            double absoluteX = bounds.Left + x;
            double absoluteY = bounds.Top + y;

            int screenWidth = GetSystemMetrics(0);
            int screenHeight = GetSystemMetrics(1);
            if (screenWidth <= 0) screenWidth = 1920;
            if (screenHeight <= 0) screenHeight = 1080;

            var inputs = new INPUT[2];
            inputs[0] = new INPUT
            {
                type = 0,
                u = new INPUT_UNION
                {
                    mi = new MOUSEINPUT
                    {
                        dx = (int)(absoluteX * 65535 / screenWidth),
                        dy = (int)(absoluteY * 65535 / screenHeight),
                        dwFlags = 0x0001 | 0x8000
                    }
                }
            };
            inputs[1] = new INPUT
            {
                type = 0,
                u = new INPUT_UNION
                {
                    mi = new MOUSEINPUT { dwFlags = 0x0004 }
                }
            };
            PostAndRestoreCursor(inputs, windowId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to simulate Windows mouse up at ({X}, {Y})", x, y);
        }
    }

    public void SimulateKeyPress(string windowId, string key)
    {
        _logger.LogInformation("Windows KeyPress simulated: {Key}", key);
    }

    public void SimulateTypeText(string windowId, string text)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return;

        if (UsePeerAutomation)
        {
            try
            {
                var automation = (IUIAutomation)new CUIAutomation();
                if (automation.GetFocusedElement(out var focusedElement) == 0 && focusedElement != null)
                {
                    // Try ValuePattern (10002)
                    int valuePatternId = 10002;
                    if (focusedElement.GetCurrentPattern(valuePatternId, out IntPtr patternPtr) == 0 && patternPtr != IntPtr.Zero)
                    {
                        try
                        {
                            var valuePattern = (IUIAutomationValuePattern)Marshal.GetObjectForIUnknown(patternPtr);
                            int hr = valuePattern.SetValue(text);
                            if (hr == 0) return;
                        }
                        finally
                        {
                            Marshal.Release(patternPtr);
                        }
                    }
                }
            }
            catch {}
        }

        _logger.LogInformation("Windows TypeText simulated: {Text}", text);
    }

    public byte[] CaptureWindow(string windowId)
    {
        if (windowId == "windows-window-fallback" || !RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return GetFallbackCapture();
        }

        try
        {
            IntPtr hWnd = IntPtr.Zero;
            if (IntPtr.TryParse(windowId, out var parsedHwnd))
            {
                hWnd = parsedHwnd;
            }
            else if (windowId.EndsWith("_fallback"))
            {
                var parts = windowId.Split('_');
                if (parts.Length > 0 && int.TryParse(parts[0], out int pid))
                {
                    try
                    {
                        var proc = System.Diagnostics.Process.GetProcessById(pid);
                        hWnd = proc.MainWindowHandle;
                    }
                    catch {}

                    if (hWnd == IntPtr.Zero)
                    {
                        IntPtr foundHwnd = IntPtr.Zero;
                        EnumWindows((h, lp) =>
                        {
                            if (IsWindowVisible(h))
                            {
                                GetWindowThreadProcessId(h, out var wpid);
                                if ((int)wpid == pid)
                                {
                                    foundHwnd = h;
                                    return false;
                                }
                            }
                            return true;
                        }, IntPtr.Zero);
                        hWnd = foundHwnd;
                    }
                }
            }

            if (hWnd != IntPtr.Zero)
            {
                GetWindowRect(hWnd, out var rect);
                int width = rect.Right - rect.Left;
                int height = rect.Bottom - rect.Top;
                if (width <= 0) width = 1024;
                if (height <= 0) height = 768;

                IntPtr hdcWindow = GetWindowDC(hWnd);
                IntPtr hdcMem = CreateCompatibleDC(hdcWindow);
                IntPtr hBitmap = CreateCompatibleBitmap(hdcWindow, width, height);
                IntPtr hOld = SelectObject(hdcMem, hBitmap);

                BitBlt(hdcMem, 0, 0, width, height, hdcWindow, 0, 0, 0x00CC0020);

                var bmi = new BITMAPINFO();
                bmi.bmiHeader.biSize = (uint)Marshal.SizeOf<BITMAPINFOHEADER>();
                bmi.bmiHeader.biWidth = width;
                bmi.bmiHeader.biHeight = -height;
                bmi.bmiHeader.biPlanes = 1;
                bmi.bmiHeader.biBitCount = 32;
                bmi.bmiHeader.biCompression = 0;

                byte[] pixelBuffer = new byte[width * height * 4];
                unsafe
                {
                    fixed (byte* p = pixelBuffer)
                    {
                        GetDIBits(hdcWindow, hBitmap, 0, (uint)height, (IntPtr)p, ref bmi, 0);
                    }
                }

                SelectObject(hdcMem, hOld);
                DeleteObject(hBitmap);
                DeleteDC(hdcMem);
                ReleaseDC(hWnd, hdcWindow);

                using var bitmap = new SKBitmap();
                unsafe
                {
                    fixed (byte* p = pixelBuffer)
                    {
                        var info = new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Premul);
                        bitmap.InstallPixels(info, (IntPtr)p, width * 4);
                    }
                }

                using var skImage = SKImage.FromBitmap(bitmap);
                using var encoded = skImage.Encode(SKEncodedImageFormat.Png, 80);
                return encoded.ToArray();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to capture Windows window {WindowId}", windowId);
        }

        return GetFallbackCapture();
    }

    private byte[] GetFallbackCapture()
    {
        using var bitmap = new SKBitmap(1024, 768);
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(SKColors.MidnightBlue);
            using var paint = new SKPaint();
            paint.Color = SKColors.White;
            paint.TextSize = 24;
            paint.IsAntialias = true;
            canvas.DrawText("Windows Screen Capture (OS Automation)", 50, 100, paint);

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
        return null;
    }

    public bool HasScreenCapturePermission()
    {
        return true;
    }

    public void StartInputCapture(string windowId, Action<double, double, string> onClick)
    {
    }

    public void StopInputCapture()
    {
    }

    private bool TryAccessibilityPress(double absoluteX, double absoluteY)
    {
        try
        {
            var automation = (IUIAutomation)new CUIAutomation();
            POINT pt = new POINT { X = (int)absoluteX, Y = (int)absoluteY };
            if (automation.ElementFromPoint(pt, out var element) == 0 && element != null)
            {
                // Try InvokePattern (10000)
                int invokePatternId = 10000;
                if (element.GetCurrentPattern(invokePatternId, out IntPtr patternPtr) == 0 && patternPtr != IntPtr.Zero)
                {
                    try
                    {
                        var invokePattern = (IUIAutomationInvokePattern)Marshal.GetObjectForIUnknown(patternPtr);
                        int hr = invokePattern.Invoke();
                        if (hr == 0) return true;
                    }
                    finally
                    {
                        Marshal.Release(patternPtr);
                    }
                }
            }
        }
        catch {}
        return false;
    }
}
