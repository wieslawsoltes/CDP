using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using Windows.Foundation;

namespace WinUI.Diagnostics.Cdp;

public static class CdpVisualTreeHelper
{
    [StructLayout(LayoutKind.Sequential)]
    private struct Win32Point
    {
        public int X;
        public int Y;
    }

    [DllImport("user32.dll")]
    private static extern bool ClientToScreen(IntPtr hWnd, ref Win32Point lpPoint);

    [DllImport("user32.dll")]
    private static extern bool ScreenToClient(IntPtr hWnd, ref Win32Point lpPoint);

    [DllImport("user32.dll")]
    private static extern IntPtr GetWindow(IntPtr hWnd, uint uCmd);

    private const uint GW_OWNER = 4;

    /// <summary>
    /// The registered window that owns <paramref name="window"/> through the Win32 owner relation, or null.
    /// WinUI has no XAML owner property; owned windows are created through the HWND owner.
    /// </summary>
    internal static Window? GetOwnerWindow(Window window)
    {
        if (!OperatingSystem.IsWindows()) return null;
        var hwnd = GetWindowHandle(window);
        if (hwnd == IntPtr.Zero) return null;
        var ownerHwnd = GetWindow(hwnd, GW_OWNER);
        if (ownerHwnd == IntPtr.Zero) return null;
        return CdpServer.GetWindows().Select(x => x.Window).FirstOrDefault(w => w != null && w != window && GetWindowHandle(w) == ownerHwnd);
    }

    /// <summary>
    /// Returns true when <paramref name="window"/> is shown on top of <paramref name="rootWindow"/>
    /// from the point of view of a session attached to <paramref name="rootWindow"/>.
    /// The primary window sees all other windows; a secondary window only sees the windows it owns.
    /// </summary>
    public static bool IsOverlayWindowFor(Window? rootWindow, Window? window)
    {
        if (rootWindow == null || window == null || window == rootWindow) return false;
        if (rootWindow == CdpServer.GetPrimaryWindow()) return true;

        var owner = GetOwnerWindow(window);
        for (int depth = 0; owner != null && depth < 32; depth++)
        {
            if (owner == rootWindow) return true;
            owner = GetOwnerWindow(owner);
        }
        return false;
    }

    /// <summary>
    /// Parent window of a secondary window in the CDP tree: its owner window when it has a registered owner,
    /// otherwise the main window. This matches <see cref="IsOverlayWindowFor"/>.
    /// </summary>
    internal static Window? GetWindowParent(Window window, Window? mainWin)
    {
        var owner = GetOwnerWindow(window);
        return owner != null && owner.Content != null ? owner : mainWin;
    }

    public static IEnumerable<UIElement> GetChildren(UIElement visual, bool useLogicalTree)
    {
        var list = new List<UIElement>();

        if (useLogicalTree)
        {
            list.AddRange(CdpSession.GetLogicalVisualChildren(visual));
        }
        else
        {
            list.AddRange(visual.GetVisualChildren());
        }

        var windows = CdpServer.GetWindows().ToList();
        var mainWin = CdpServer.GetPrimaryWindow();

        var visualWindow = mainWin != null && mainWin.Content != null
            ? windows.Select(t => t.Window).FirstOrDefault(w => w != null && w.Content == visual)
            : null;
        if (mainWin != null && mainWin.Content != null && visualWindow != null)
        {
            // 1. Append the Content of secondary windows whose parent is this window: owned windows below
            //    their owner, all other windows below the main window.
            foreach (var t in windows)
            {
                var win = t.Window;
                if (win != null && win != mainWin && win != visualWindow && win.Content != null &&
                    GetWindowParent(win, mainWin) == visualWindow && !list.Contains(win.Content))
                {
                    list.Add(win.Content);
                }
            }
        }

        if (mainWin != null && mainWin.Content != null && visual == mainWin.Content)
        {
            // 2. Append all open popup contents as children
            foreach (var t in windows)
            {
                var win = t.Window;
                if (win != null && win.Content != null && win.Content.XamlRoot != null)
                {
                    var popups = VisualTreeHelper.GetOpenPopupsForXamlRoot(win.Content.XamlRoot);
                    if (popups != null)
                    {
                        foreach (var popup in popups)
                        {
                            if (popup != null && popup.Child is UIElement popupChild && !list.Contains(popupChild))
                            {
                                list.Add(popupChild);
                            }
                        }
                    }
                }
            }
        }

        return list;
    }

    public static UIElement? GetParent(UIElement visual, bool useLogicalTree)
    {
        var windows = CdpServer.GetWindows().ToList();
        var mainWin = CdpServer.GetPrimaryWindow();

        if (mainWin != null && mainWin.Content != null)
        {
            // A secondary window's Content belongs below its owner window's Content, otherwise below the main window's Content
            foreach (var t in windows)
            {
                var win = t.Window;
                if (win != null && win != mainWin && win.Content == visual)
                {
                    return GetWindowParent(win, mainWin)?.Content ?? mainWin.Content;
                }
            }

            // Return main window's Content if the visual is an open popup's Child
            foreach (var t in windows)
            {
                var win = t.Window;
                if (win != null && win.Content != null && win.Content.XamlRoot != null)
                {
                    var popups = VisualTreeHelper.GetOpenPopupsForXamlRoot(win.Content.XamlRoot);
                    if (popups != null)
                    {
                        foreach (var popup in popups)
                        {
                            if (popup != null && popup.Child == visual)
                            {
                                return mainWin.Content;
                            }
                        }
                    }
                }
            }
        }

        if (useLogicalTree)
        {
            return SelectorEngine.GetLogicalParent(visual);
        }
        else
        {
            return visual.GetVisualParent();
        }
    }

    public static Point TranslatePointToWindow(UIElement? visual, Point point, Window? window)
    {
        if (visual == null || window == null || window.Content == null)
        {
            return point;
        }

        if (visual.XamlRoot != null && visual.XamlRoot == window.Content.XamlRoot)
        {
            try
            {
                return visual.TransformToVisual(window.Content).TransformPoint(point);
            }
            catch
            {
                // Fallback
            }
        }

        // Different XamlRoot contexts
        if (OperatingSystem.IsWindows())
        {
            try
            {
                var visualWindow = CdpServer.GetWindows()
                    .Select(x => x.Window)
                    .FirstOrDefault(w => w.Content != null && w.Content.XamlRoot == visual.XamlRoot);

                if (visualWindow != null && visualWindow != window)
                {
                    IntPtr hwndVisual = GetWindowHandle(visualWindow);
                    IntPtr hwndTarget = GetWindowHandle(window);

                    if (hwndVisual != IntPtr.Zero && hwndTarget != IntPtr.Zero)
                    {
                        var localInVisualWindow = visual.TransformToVisual(visualWindow.Content).TransformPoint(point);
                        var win32Point = new Win32Point { X = (int)localInVisualWindow.X, Y = (int)localInVisualWindow.Y };

                        if (ClientToScreen(hwndVisual, ref win32Point))
                        {
                            if (ScreenToClient(hwndTarget, ref win32Point))
                            {
                                return new Point(win32Point.X, win32Point.Y);
                            }
                        }
                    }
                }
            }
            catch
            {
                // Fallback
            }
        }

        return point;
    }

    private static IntPtr GetWindowHandle(Window window)
    {
        if (window == null) return IntPtr.Zero;
        try
        {
            var type = typeof(Window).Assembly.GetType("WinRT.Interop.WindowNative");
            if (type != null)
            {
                var method = type.GetMethod("GetWindowHandle", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                if (method != null)
                {
                    var result = method.Invoke(null, new object[] { window });
                    if (result is IntPtr hwnd)
                    {
                        return hwnd;
                    }
                }
            }
        }
        catch
        {
            // ignore
        }
        return IntPtr.Zero;
    }

    public record HitTestResult(UIElement? Target, Window? TargetWindow, Point LocalPoint);

    public static HitTestResult HitTestAllRoots(Window primaryWindow, Point mousePos, string targetViewMode = "composite")
    {
        if (primaryWindow == null || primaryWindow.Content == null)
        {
            return new HitTestResult(null, null, mousePos);
        }

        // Only the session window and the windows shown on top of it take part; a dialog session
        // must not hit its owner behind it.
        var windows = CdpServer.GetWindows()
            .Where(t => t.Window == primaryWindow || IsOverlayWindowFor(primaryWindow, t.Window))
            .ToList();

        // Check popups in main and secondary windows
        foreach (var target in windows)
        {
            var win = target.Window;
            if (win != null && win.Content != null && win.Content.XamlRoot != null)
            {
                var popups = VisualTreeHelper.GetOpenPopupsForXamlRoot(win.Content.XamlRoot);
                if (popups != null)
                {
                    foreach (var popup in popups)
                    {
                        if (popup != null && popup.Child is UIElement childUI && childUI.Visibility == Visibility.Visible)
                        {
                            Point popupLocalPoint = mousePos;
                            try
                            {
                                var transform = primaryWindow.Content.TransformToVisual(childUI);
                                popupLocalPoint = transform.TransformPoint(mousePos);
                            }
                            catch { }

                            var elements = VisualTreeHelper.FindElementsInHostCoordinates(popupLocalPoint, childUI);
                            var targetElem = elements.FirstOrDefault() ?? childUI;
                            if (targetElem != null)
                            {
                                return new HitTestResult(targetElem, primaryWindow, popupLocalPoint);
                            }
                        }
                    }
                }
            }
        }

        // Check secondary windows
        foreach (var target in windows)
        {
            var win = target.Window;
            if (win != null && win != primaryWindow && win.Content != null)
            {
                var winPoint = TranslatePointToWindow(win.Content, mousePos, primaryWindow);
                var elements = VisualTreeHelper.FindElementsInHostCoordinates(winPoint, win.Content);
                var targetElem = elements.FirstOrDefault();
                if (targetElem != null)
                {
                    return new HitTestResult(targetElem, win, winPoint);
                }
            }
        }

        var primaryElements = VisualTreeHelper.FindElementsInHostCoordinates(mousePos, primaryWindow.Content);
        return new HitTestResult(primaryElements.FirstOrDefault(), primaryWindow, mousePos);
    }
}
