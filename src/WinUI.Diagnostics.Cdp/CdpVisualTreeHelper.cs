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

        if (mainWin != null && mainWin.Content != null && visual == mainWin.Content)
        {
            // 1. Append other active windows' Content
            foreach (var t in windows)
            {
                var win = t.Window;
                if (win != null && win != mainWin && win.Content != null && !list.Contains(win.Content))
                {
                    list.Add(win.Content);
                }
            }

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
            // Return main window's Content if the visual is secondary window's Content
            foreach (var t in windows)
            {
                var win = t.Window;
                if (win != null && win != mainWin && win.Content == visual)
                {
                    return mainWin.Content;
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

        var windows = CdpServer.GetWindows().ToList();

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
