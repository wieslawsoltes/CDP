using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.LogicalTree;
using Avalonia.VisualTree;

namespace Avalonia.Diagnostics.Cdp;

public static class CdpVisualTreeHelper
{
    public static IEnumerable<Visual> GetChildren(Visual visual, bool useLogicalTree)
    {
        var list = new List<Visual>();
        
        if (useLogicalTree)
        {
            list.AddRange(GetLogicalChildren(visual));
        }
        else
        {
            list.AddRange(visual.GetVisualChildren());
        }

        // Anchor secondary windows and open popups to the main window
        var mainWin = CdpServer.GetPrimaryWindow();
        if (mainWin != null && visual == mainWin)
        {
            
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
        var mainWin = CdpServer.GetPrimaryWindow();

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

        if (useLogicalTree)
        {
            return SelectorEngine.GetLogicalParent(visual);
        }
        else
        {
            return visual.GetVisualParent();
        }
    }

    public static Point TranslatePointToWindow(Visual? visual, Point point, TopLevel? window)
    {
        if (visual == null || window == null) return point;
        var visualTop = TopLevel.GetTopLevel(visual);
        if (visualTop == null) return point;
        if (visualTop == window)
        {
            var p = visual.TranslatePoint(point, window);
            return p ?? point;
        }
        else
        {
            // Translate point to visualTop coordinates
            var pInTop = visual.TranslatePoint(point, visualTop);
            if (!pInTop.HasValue) return point;
            
            // Translate visualTop client point to screen pixel point
            var screenPixelPoint = visualTop.PointToScreen(pInTop.Value);
            
            // Translate screen pixel point to window client point
            var windowClientPoint = window.PointToClient(screenPixelPoint);
            return windowClientPoint;
        }
    }

    public static Visual? GetPopupContent(Popup popup)
    {
        if (popup.Child != null)
        {
            return popup.Child;
        }
        return GetPopupHost(popup);
    }

    public static Visual? GetPopupHost(Popup popup)
    {
        try
        {
            var hostProp = typeof(Popup).GetProperty("Host", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            return hostProp?.GetValue(popup) as Visual;
        }
        catch
        {
            return null;
        }
    }

    private static Popup? FindPopupForRoot(Visual visual)
    {
        foreach (var target in CdpServer.GetWindows())
        {
            var openPopups = new List<Popup>();
            var visited = new HashSet<Visual>();
            FindOpenPopups(target.Window, openPopups, visited);
            foreach (var popup in openPopups)
            {
                if (popup.Child == visual || GetPopupHost(popup) == visual)
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

        if (visual is ILogical logical)
        {
            foreach (var child in logical.LogicalChildren)
            {
                if (child is Visual childVisual)
                {
                    FindOpenPopups(childVisual, popups, visited);
                }
            }
        }

        foreach (var child in visual.GetVisualChildren())
        {
            FindOpenPopups(child, popups, visited);
        }
    }

    private static IEnumerable<Visual> GetLogicalChildren(Visual visual)
    {
        if (visual is ILogical logical)
        {
            return CdpSession.GetLogicalVisualChildren(logical);
        }
        return Enumerable.Empty<Visual>();
    }
}
