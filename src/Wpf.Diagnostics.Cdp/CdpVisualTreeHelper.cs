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

        // Anchor secondary windows and open popups to the main window
        var firstWindowTuple = CdpServer.GetWindows().FirstOrDefault();
        if (firstWindowTuple.Window != null && visual == firstWindowTuple.Window)
        {
            var mainWin = firstWindowTuple.Window;

            // 1. Append other active Windows as children
            foreach (var target in CdpServer.GetWindows())
            {
                var win = target.Window;
                if (win != null && win != mainWin && win.IsVisible && !list.Contains(win))
                {
                    list.Add(win);
                }
            }

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
        var firstWindowTuple = CdpServer.GetWindows().FirstOrDefault();
        var mainWin = firstWindowTuple.Window;

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
                return mainWin;
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

    private static Popup? FindPopupForRoot(Visual visual)
    {
        foreach (var target in CdpServer.GetWindows())
        {
            var openPopups = new List<Popup>();
            var visited = new HashSet<Visual>();
            if (target.Window != null)
            {
                FindOpenPopups(target.Window, openPopups, visited);
            }
            foreach (var popup in openPopups)
            {
                if (popup.Child == visual)
                {
                    return popup;
                }
            }
        }
        return null;
    }

    public static void FindOpenPopups(Visual visual, List<Popup> popups, HashSet<Visual> visited)
    {
        if (visual == null || !visited.Add(visual)) return;

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
}
