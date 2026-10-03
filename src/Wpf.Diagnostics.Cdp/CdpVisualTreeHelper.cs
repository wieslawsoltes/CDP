using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace Wpf.Diagnostics.Cdp;

public static class CdpVisualTreeHelper
{
    public static IEnumerable<Visual> GetChildren(Visual visual, bool useLogicalTree)
    {
        var list = new List<Visual>();

        if (useLogicalTree)
        {
            list.AddRange(CdpSession.GetLogicalVisualChildren(visual));
        }
        else
        {
            list.AddRange(visual.GetVisualChildren());
        }

        var mainWin = CdpServer.GetPrimaryWindow();
        if (mainWin != null && visual is Window topWindow && (topWindow == mainWin || CdpServer.IsRegistered(topWindow)))
        {
            // 1. Append the secondary windows whose parent is this window: owned windows below their owner,
            //    all other windows below the main window.
            foreach (var target in CdpServer.GetWindows())
            {
                var win = target.Window;
                if (win != null && win != mainWin && win != topWindow && win.IsVisible &&
                    GetWindowParent(win, mainWin) == topWindow && !list.Contains(win))
                {
                    list.Add(win);
                }
            }
        }

        // Anchor open popups to the main window
        if (mainWin != null && visual == mainWin)
        {
            // 2. Append all open popups' contents as children
            var openPopups = new List<Popup>();
            var visited = new HashSet<Visual>();
            foreach (var target in CdpServer.GetWindows())
            {
                if (target.Window != null)
                {
                    FindOpenPopups(target.Window, openPopups, visited);
                }
            }
            foreach (var popup in openPopups)
            {
                var content = GetPopupContent(popup);
                if (content != null && !list.Contains(content))
                {
                    list.Add(content);
                }
            }
        }

        return list;
    }

    public static Visual? GetParent(Visual visual, bool useLogicalTree)
    {
        var mainWin = CdpServer.GetPrimaryWindow();

        if (mainWin != null)
        {
            // Check if this visual is the content of an open popup
            var popup = FindPopupForRoot(visual);
            if (popup != null)
            {
                return mainWin;
            }

            // Check if this visual is a secondary Window
            if (visual is Window win && win != mainWin)
            {
                return GetWindowParent(win, mainWin);
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

    public static Point TranslatePointToWindow(Visual? visual, Point point, Window? window)
    {
        if (visual == null || window == null) return point;

        var visualSource = PresentationSource.FromVisual(visual);
        var windowSource = PresentationSource.FromVisual(window);

        if (visualSource == windowSource && visualSource != null)
        {
            try
            {
                return visual.TranslatePoint(point, window);
            }
            catch
            {
                // Fallback
            }
        }

        try
        {
            var screenPoint = point;
            if (visual is UIElement uiVisual)
            {
                screenPoint = uiVisual.PointToScreen(point);
            }
            var windowPoint = window.PointFromScreen(screenPoint);
            return windowPoint;
        }
        catch
        {
            return point;
        }
    }

    public static Visual? GetPopupContent(Popup popup)
    {
        return popup.Child;
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

        var owner = window.Owner;
        while (owner != null)
        {
            if (owner == rootWindow) return true;
            owner = owner.Owner;
        }
        return false;
    }

    /// <summary>
    /// Parent of a secondary window in the CDP tree: its owner window when the owner is a shown CDP window,
    /// otherwise the main window. This matches <see cref="IsOverlayWindowFor"/>.
    /// </summary>
    internal static Window? GetWindowParent(Window window, Window? mainWin)
    {
        var owner = window.Owner;
        if (owner != null && owner != window && owner.IsVisible && (owner == mainWin || CdpServer.IsRegistered(owner)))
        {
            return owner;
        }
        return mainWin;
    }

    private static int _popupScanVisitCount;

    /// <summary>Number of visuals visited by <see cref="FindOpenPopups"/> scans; used to check scan complexity.</summary>
    internal static int PopupScanVisitCount => System.Threading.Volatile.Read(ref _popupScanVisitCount);

    private static Popup? FindPopupForRoot(Visual visual)
    {
        // Popup content is the logical child of its Popup, so the Popup is found without scanning the windows.
        if (LogicalTreeHelper.GetParent(visual) is not Popup popup || !popup.IsOpen || popup.Child != visual)
        {
            return null;
        }

        var owner = GetPopupOwnerWindow(popup);
        return owner != null && (owner == CdpServer.GetPrimaryWindow() || CdpServer.IsRegistered(owner)) ? popup : null;
    }

    /// <summary>The window whose logical or visual tree contains the popup.</summary>
    internal static Window? GetPopupOwnerWindow(Popup popup)
    {
        DependencyObject? current = popup;
        while (current != null)
        {
            if (current is Window window) return window;
            current = LogicalTreeHelper.GetParent(current) ?? (current is Visual currentVisual ? VisualTreeHelper.GetParent(currentVisual) : null);
        }
        return null;
    }

    /// <summary>The window a popup belongs to is <paramref name="rootWindow"/> or shown on top of it.</summary>
    private static bool IsPopupVisibleFor(Window rootWindow, Popup popup)
    {
        var owner = GetPopupOwnerWindow(popup);
        return owner == null || owner == rootWindow || IsOverlayWindowFor(rootWindow, owner);
    }

    public static void FindOpenPopups(Visual visual, List<Popup> popups, HashSet<Visual> visited)
    {
        if (visual == null || !visited.Add(visual)) return;
        System.Threading.Interlocked.Increment(ref _popupScanVisitCount);

        if (visual is Popup popup)
        {
            if (popup.IsOpen)
            {
                popups.Add(popup);
            }
        }

        foreach (var child in CdpSession.GetLogicalVisualChildren(visual))
        {
            FindOpenPopups(child, popups, visited);
        }

        foreach (var child in visual.GetVisualChildren())
        {
            FindOpenPopups(child, popups, visited);
        }
    }

    public record HitTestResult(Visual? Target, Window? TargetWindow, Point LocalPoint);

    public static HitTestResult HitTestAllRoots(Window primaryWindow, Point mousePos, string targetViewMode = "composite")
    {
        if (primaryWindow == null)
        {
            return new HitTestResult(null, null, mousePos);
        }

        // Only the session window and the windows shown on top of it take part; a dialog session
        // must not hit its owner behind it.
        var openPopups = new List<Popup>();
        var visited = new HashSet<Visual>();
        foreach (var target in CdpServer.GetWindows())
        {
            if (target.Window != null && (target.Window == primaryWindow || IsOverlayWindowFor(primaryWindow, target.Window)))
            {
                FindOpenPopups(target.Window, openPopups, visited);
            }
        }

        foreach (var popup in openPopups)
        {
            if (popup != null && IsPopupVisibleFor(primaryWindow, popup) && popup.Child is UIElement childUI && childUI.IsVisible)
            {
                Point popupLocalPoint = mousePos;
                try
                {
                    var screenPoint = primaryWindow.PointToScreen(mousePos);
                    popupLocalPoint = childUI.PointFromScreen(screenPoint);
                }
                catch { }

                var hit = VisualTreeHelper.HitTest(childUI, popupLocalPoint)?.VisualHit;
                if (hit != null)
                {
                    return new HitTestResult(hit, primaryWindow, popupLocalPoint);
                }
            }
        }

        var secondaryWindows = CdpServer.GetWindows()
            .Select(t => t.Window)
            .Where(w => IsOverlayWindowFor(primaryWindow, w) && w.IsVisible)
            .ToList();

        foreach (var win in secondaryWindows)
        {
            if (win != null)
            {
                Point winLocalPoint = TranslatePointToWindow(primaryWindow, mousePos, win);
                var hit = VisualTreeHelper.HitTest(win, winLocalPoint)?.VisualHit;
                if (hit != null)
                {
                    return new HitTestResult(hit, win, winLocalPoint);
                }
            }
        }

        var primaryHit = VisualTreeHelper.HitTest(primaryWindow, mousePos)?.VisualHit;
        return new HitTestResult(primaryHit, primaryWindow, mousePos);
    }
}
