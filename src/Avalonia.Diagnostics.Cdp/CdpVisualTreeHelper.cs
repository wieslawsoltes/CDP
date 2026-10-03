using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.Media.Imaging;
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

        var mainWin = CdpServer.GetPrimaryWindow();
        if (mainWin != null && visual is TopLevel topLevel && (topLevel == mainWin || CdpServer.IsRegistered(topLevel)))
        {
            // 1. Append the secondary windows whose parent is this window: owned windows below their owner,
            //    all other windows below the main window.
            foreach (var target in CdpServer.GetWindows())
            {
                var win = target.Window;
                if (win != null && win != mainWin && win != topLevel && win.IsVisible &&
                    GetWindowParent(win, mainWin) == topLevel && !list.Contains(win))
                {
                    list.Add(win);
                }
            }
        }

        // Anchor open popups to the main window
        if (mainWin != null && visual == mainWin)
        {
            // 2. Append all open popups' contents as children
            foreach (var popup in GetOpenPopups())
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
            return GetWindowParent(win, mainWin);
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

    /// <summary>
    /// Parent of a secondary window in the CDP tree: its owner window when the owner is a shown CDP window,
    /// otherwise the main window. This matches <see cref="IsOverlayWindowFor"/>, where a window session
    /// only sees the windows it owns.
    /// </summary>
    internal static TopLevel? GetWindowParent(TopLevel window, TopLevel? mainWin)
    {
        if (window is Window win && win.Owner is TopLevel owner && owner != win && owner.IsVisible &&
            (owner == mainWin || CdpServer.IsRegistered(owner)))
        {
            return owner;
        }
        return mainWin;
    }

    private static Popup? FindPopupForRoot(Visual visual)
    {
        // Only a popup host or the logical child of a Popup can be popup content.
        if (visual is not PopupRoot && visual is not OverlayPopupHost && visual.GetLogicalParent() is not Popup)
        {
            return null;
        }

        foreach (var popup in GetOpenPopups())
        {
            if (popup.Child == visual || GetPopupHost(popup) == visual)
            {
                return popup;
            }
        }
        return null;
    }

    private static readonly object _openPopupsGate = new();
    private static readonly List<Popup> _openPopups = new();
    private static int _openPopupTrackingStarted;
    private static int _popupScanVisitCount;

    /// <summary>Number of visuals visited by <see cref="FindOpenPopups"/> scans; used to check scan complexity.</summary>
    internal static int PopupScanVisitCount => Volatile.Read(ref _popupScanVisitCount);

    /// <summary>
    /// Starts tracking open popups through Popup.IsOpen changes, so popup lookups do not walk the window trees.
    /// Popups that were already open are found by one scan of the CDP windows.
    /// </summary>
    internal static void EnsureOpenPopupTracking()
    {
        if (Interlocked.Exchange(ref _openPopupTrackingStarted, 1) == 1) return;

        Popup.IsOpenProperty.Changed.AddClassHandler<Popup>((popup, _) => OnPopupIsOpenChanged(popup));

        var popups = new List<Popup>();
        var visited = new HashSet<Visual>();
        foreach (var target in CdpServer.GetWindows())
        {
            FindOpenPopups(target.Window, popups, visited);
        }
        foreach (var popup in popups)
        {
            OnPopupIsOpenChanged(popup);
        }
    }

    private static void OnPopupIsOpenChanged(Popup popup)
    {
        lock (_openPopupsGate)
        {
            if (popup.IsOpen)
            {
                if (!_openPopups.Contains(popup)) _openPopups.Add(popup);
            }
            else
            {
                _openPopups.Remove(popup);
            }
        }
    }

    /// <summary>
    /// Open popups that belong to a CDP window, in opening order. Replaces scanning all window trees.
    /// </summary>
    internal static List<Popup> GetOpenPopups()
    {
        EnsureOpenPopupTracking();
        List<Popup> snapshot;
        lock (_openPopupsGate)
        {
            snapshot = new List<Popup>(_openPopups);
        }

        var mainWin = CdpServer.GetPrimaryWindow();
        snapshot.RemoveAll(popup =>
        {
            if (!popup.IsOpen) return true;
            var owner = GetPopupOwnerWindow(popup);
            return owner == null || (owner != mainWin && !CdpServer.IsRegistered(owner));
        });
        return snapshot;
    }

    /// <summary>The window a popup is opened from, following the logical parents and the placement target.</summary>
    internal static TopLevel? GetPopupOwnerWindow(Popup popup)
    {
        ILogical? current = popup;
        while (current != null)
        {
            if (current is PopupRoot popupRoot)
            {
                current = popupRoot.ParentTopLevel;
                continue;
            }
            if (current is TopLevel topLevel) return topLevel;
            current = current.LogicalParent;
        }

        var anchor = popup.PlacementTarget ?? (popup as Visual);
        var anchorTop = TopLevel.GetTopLevel(anchor);
        return anchorTop is PopupRoot root ? root.ParentTopLevel : anchorTop;
    }

    public static void FindOpenPopups(Visual visual, List<Popup> popups, HashSet<Visual> visited)
    {
        if (visual == null || !visited.Add(visual)) return;
        Interlocked.Increment(ref _popupScanVisitCount);

        if (visual is Popup popup)
        {
            if (popup.IsOpen && !popups.Contains(popup))
            {
                popups.Add(popup);
            }
        }

        if (visual is Control ctrl)
        {
            if (ctrl.ContextMenu != null && ctrl.ContextMenu.IsOpen)
            {
                var internalPopup = GetInternalPopup(ctrl.ContextMenu);
                if (internalPopup != null && internalPopup.IsOpen && !popups.Contains(internalPopup))
                {
                    popups.Add(internalPopup);
                }
            }

            var attachedFlyout = FlyoutBase.GetAttachedFlyout(ctrl);
            if (attachedFlyout != null && attachedFlyout.IsOpen)
            {
                var internalPopup = GetInternalPopup(attachedFlyout);
                if (internalPopup != null && internalPopup.IsOpen && !popups.Contains(internalPopup))
                {
                    popups.Add(internalPopup);
                }
            }
        }

        if (visual is Button btn && btn.Flyout != null && btn.Flyout.IsOpen)
        {
            var internalPopup = GetInternalPopup(btn.Flyout);
            if (internalPopup != null && internalPopup.IsOpen && !popups.Contains(internalPopup))
            {
                popups.Add(internalPopup);
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

    public static Popup? GetInternalPopup(object target)
    {
        if (target == null) return null;
        try
        {
            var type = target.GetType();
            while (type != null && type != typeof(object))
            {
                var prop = type.GetProperty("Popup", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (prop != null)
                {
                    var val = prop.GetValue(target) as Popup;
                    if (val != null) return val;
                }
                var field = type.GetField("_popup", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (field != null)
                {
                    var val = field.GetValue(target) as Popup;
                    if (val != null) return val;
                }
                type = type.BaseType;
            }
        }
        catch { }
        return null;
    }

    private static IEnumerable<Visual> GetLogicalChildren(Visual visual)
    {
        if (visual is ILogical logical)
        {
            return CdpSession.GetLogicalVisualChildren(logical);
        }
        return Enumerable.Empty<Visual>();
    }

    /// <summary>
    /// Returns true when <paramref name="window"/> is shown on top of <paramref name="rootWindow"/>
    /// from the point of view of a session attached to <paramref name="rootWindow"/>.
    /// The primary window sees all other windows; a secondary window only sees the windows it owns.
    /// </summary>
    public static bool IsOverlayWindowFor(TopLevel? rootWindow, TopLevel? window)
    {
        if (rootWindow == null || window == null || window == rootWindow) return false;
        if (rootWindow == CdpServer.GetPrimaryWindow()) return true;

        var owner = (window as Window)?.Owner;
        while (owner != null)
        {
            if (owner == rootWindow) return true;
            owner = (owner as Window)?.Owner;
        }
        return false;
    }

    public static bool HasSecondaryWindowsOrPopups(TopLevel? primaryWindow)
    {
        if (primaryWindow == null) return false;
        try
        {
            foreach (var target in CdpServer.GetWindows())
            {
                if (IsOverlayWindowFor(primaryWindow, target.Window) && target.Window.IsVisible && target.Window.Bounds.Width > 0 && target.Window.Bounds.Height > 0)
                {
                    return true;
                }
            }

            if (HasOpenPopups(primaryWindow))
            {
                return true;
            }
        }
        catch { }
        return false;
    }

    private static bool HasOpenPopups(Visual visual, HashSet<Visual>? visited = null)
    {
        visited ??= new HashSet<Visual>();
        if (visual == null || !visited.Add(visual)) return false;

        if (visual is Popup p && p.IsOpen) return true;

        if (visual is Control ctrl)
        {
            if (ctrl.ContextMenu != null && ctrl.ContextMenu.IsOpen) return true;
            var flyout = FlyoutBase.GetAttachedFlyout(ctrl);
            if (flyout != null && flyout.IsOpen) return true;
        }

        if (visual is Button btn && btn.Flyout != null && btn.Flyout.IsOpen) return true;

        if (visual is ILogical logical)
        {
            foreach (var child in logical.LogicalChildren)
            {
                if (child is Visual childVisual)
                {
                    if (HasOpenPopups(childVisual, visited)) return true;
                }
            }
        }

        foreach (var child in visual.GetVisualChildren())
        {
            if (HasOpenPopups(child, visited)) return true;
        }

        return false;
    }

    public static bool CompositeAllWindowsAndPopups(TopLevel? primaryWindow, SkiaSharp.SKBitmap? baseSkBitmap, double scale, string? targetViewId = null)
    {
        if (primaryWindow == null || baseSkBitmap == null) return false;
        if (targetViewId == "main") return false;
        if (!HasSecondaryWindowsOrPopups(primaryWindow)) return false;

        bool compositedAny = false;
        try
        {
            foreach (var target in CdpServer.GetWindows())
            {
                var win = target.Window;
                if (IsOverlayWindowFor(primaryWindow, win) && win.IsVisible && win.Bounds.Width > 0 && win.Bounds.Height > 0)
                {
                    try
                    {
                        var winScreen = win.PointToScreen(new Point(0, 0));
                        var clientPos = primaryWindow.PointToClient(winScreen);
                        double winX = clientPos.X;
                        double winY = clientPos.Y;

                        int winPixelWidth = Math.Max(1, (int)(win.Bounds.Width * scale));
                        int winPixelHeight = Math.Max(1, (int)(win.Bounds.Height * scale));

                        using var winBitmap = new RenderTargetBitmap(new PixelSize(winPixelWidth, winPixelHeight), new Vector(96 * scale, 96 * scale));
                        winBitmap.Render(win);

                        using var winMs = new MemoryStream();
                        winBitmap.Save(winMs);
                        winMs.Position = 0;
                        using var winSkBitmap = SkiaSharp.SKBitmap.Decode(winMs);
                        if (winSkBitmap != null)
                        {
                            using var canvas = new SkiaSharp.SKCanvas(baseSkBitmap);
                            canvas.DrawBitmap(winSkBitmap, (float)(winX * scale), (float)(winY * scale));
                            compositedAny = true;
                        }
                    }
                    catch { }
                }
            }

            if (CompositeOpenPopups(primaryWindow, baseSkBitmap, scale))
            {
                compositedAny = true;
            }
        }
        catch
        {
            // Ignore compositing errors gracefully
        }

        return compositedAny;
    }

    public static bool CompositeOpenPopups(TopLevel? topLevel, SkiaSharp.SKBitmap? baseSkBitmap, double scale)
    {
        if (topLevel == null || baseSkBitmap == null) return false;

        bool compositedAny = false;
        try
        {
            var openPopups = new List<Popup>();
            var visited = new HashSet<Visual>();

            FindOpenPopups(topLevel, openPopups, visited);
            foreach (var target in CdpServer.GetWindows())
            {
                if (IsOverlayWindowFor(topLevel, target.Window))
                {
                    FindOpenPopups(target.Window, openPopups, visited);
                }
            }

            var overlayVisuals = new List<Visual>();

            // 1. Collect render targets from open Popup objects (popup.Child or GetPopupHost)
            foreach (var popup in openPopups)
            {
                Visual? renderTarget = popup.Child ?? GetPopupHost(popup) as Visual;
                if (renderTarget != null && renderTarget.IsVisible && renderTarget.Bounds.Width > 0 && renderTarget.Bounds.Height > 0)
                {
                    if (!overlayVisuals.Contains(renderTarget))
                    {
                        overlayVisuals.Add(renderTarget);
                    }
                }
            }

            // 2. Collect overlay visual children from OverlayLayer
            var overlayLayer = OverlayLayer.GetOverlayLayer(topLevel);
            if (overlayLayer != null)
            {
                foreach (var child in overlayLayer.GetVisualChildren())
                {
                    if (child != null && child.IsVisible && child.Bounds.Width > 0 && child.Bounds.Height > 0)
                    {
                        if (!overlayVisuals.Contains(child))
                        {
                            overlayVisuals.Add(child);
                        }
                    }
                }
            }

            if (overlayVisuals.Count == 0) return false;

            foreach (var renderTarget in overlayVisuals)
            {
                double popupX = 0;
                double popupY = 0;

                try
                {
                    PixelPoint screenPos = renderTarget.PointToScreen(new Point(0, 0));
                    Point clientPos = topLevel.PointToClient(screenPos);
                    popupX = clientPos.X;
                    popupY = clientPos.Y;
                }
                catch
                {
                    Point? relativePoint = renderTarget.TranslatePoint(new Point(0, 0), topLevel);
                    popupX = relativePoint?.X ?? 0;
                    popupY = relativePoint?.Y ?? 0;
                }

                int popupPixelWidth = Math.Max(1, (int)(renderTarget.Bounds.Width * scale));
                int popupPixelHeight = Math.Max(1, (int)(renderTarget.Bounds.Height * scale));

                using var popupBitmap = new RenderTargetBitmap(new PixelSize(popupPixelWidth, popupPixelHeight), new Vector(96 * scale, 96 * scale));
                popupBitmap.Render(renderTarget);

                using var popupMs = new MemoryStream();
                popupBitmap.Save(popupMs);
                popupMs.Position = 0;
                using var popupSkBitmap = SkiaSharp.SKBitmap.Decode(popupMs);
                if (popupSkBitmap != null)
                {
                    using var canvas = new SkiaSharp.SKCanvas(baseSkBitmap);
                    float drawX = (float)(popupX * scale);
                    float drawY = (float)(popupY * scale);
                    canvas.DrawBitmap(popupSkBitmap, drawX, drawY);
                    compositedAny = true;
                }
            }
        }
        catch
        {
            // Ignore popup compositing errors gracefully
        }

        return compositedAny;
    }

    public class RootHitResult
    {
        public TopLevel TargetTopLevel { get; set; } = null!;
        public Visual TargetVisual { get; set; } = null!;
        public Point LocalPoint { get; set; }
        public Visual HitVisual { get; set; } = null!;
    }

    public static RootHitResult HitTestAllRoots(TopLevel rootWindow, Point clientPoint, string? targetViewId = null)
    {
        if (rootWindow == null)
        {
            return new RootHitResult { LocalPoint = clientPoint };
        }

        try
        {
            var mainHitResult = (rootWindow as InputElement)?.InputHitTest(clientPoint) as Visual;
            var defaultHit = new RootHitResult
            {
                TargetTopLevel = rootWindow,
                TargetVisual = rootWindow,
                LocalPoint = clientPoint,
                HitVisual = mainHitResult ?? rootWindow
            };

            if (targetViewId == "main")
            {
                return defaultHit;
            }
            var screenPoint = rootWindow.PointToScreen(clientPoint);

            var openPopups = new List<Popup>();
            var visited = new HashSet<Visual>();
            FindOpenPopups(rootWindow, openPopups, visited);

            var secondaryWindows = new List<TopLevel>();
            foreach (var target in CdpServer.GetWindows())
            {
                if (IsOverlayWindowFor(rootWindow, target.Window) && target.Window.IsVisible && target.Window.Bounds.Width > 0 && target.Window.Bounds.Height > 0)
                {
                    secondaryWindows.Add(target.Window);
                    FindOpenPopups(target.Window, openPopups, visited);
                }
            }

            var overlayRoots = new List<Visual>();

            foreach (var popup in openPopups)
            {
                var host = GetPopupHost(popup) as Visual ?? popup.Child;
                if (host != null && host.IsVisible && host.Bounds.Width > 0 && host.Bounds.Height > 0)
                {
                    if (!overlayRoots.Contains(host)) overlayRoots.Add(host);
                }
            }

            foreach (var secWin in secondaryWindows)
            {
                if (!overlayRoots.Contains(secWin)) overlayRoots.Add(secWin);
            }

            var overlayLayer = OverlayLayer.GetOverlayLayer(rootWindow);
            if (overlayLayer != null)
            {
                foreach (var child in overlayLayer.GetVisualChildren())
                {
                    if (child != null && child.IsVisible && child.Bounds.Width > 0 && child.Bounds.Height > 0)
                    {
                        if (!overlayRoots.Contains(child)) overlayRoots.Add(child);
                    }
                }
            }

            for (int i = overlayRoots.Count - 1; i >= 0; i--)
            {
                var host = overlayRoots[i];
                try
                {
                    Point localPoint;
                    var hostTop = TopLevel.GetTopLevel(host);
                    if (hostTop != null)
                    {
                        localPoint = host.PointToClient(screenPoint);
                    }
                    else
                    {
                        Point? rel = host.TranslatePoint(new Point(0, 0), rootWindow);
                        localPoint = new Point(clientPoint.X - (rel?.X ?? 0), clientPoint.Y - (rel?.Y ?? 0));
                    }

                    if (localPoint.X >= 0 && localPoint.X <= host.Bounds.Width &&
                        localPoint.Y >= 0 && localPoint.Y <= host.Bounds.Height)
                    {
                        Visual? hit = (host as InputElement)?.InputHitTest(localPoint) as Visual;
                        if (hit != null)
                        {
                            var targetTopLevel = TopLevel.GetTopLevel(hit) ?? host as TopLevel ?? rootWindow;

                            return new RootHitResult
                            {
                                TargetTopLevel = targetTopLevel,
                                TargetVisual = host,
                                LocalPoint = localPoint,
                                HitVisual = hit
                            };
                        }
                    }
                }
                catch { }
            }

            var mainHit = (rootWindow as InputElement)?.InputHitTest(clientPoint) as Visual;
            return new RootHitResult
            {
                TargetTopLevel = rootWindow,
                TargetVisual = rootWindow,
                LocalPoint = clientPoint,
                HitVisual = mainHit ?? rootWindow
            };
        }
        catch
        {
            var mainHit = (rootWindow as InputElement)?.InputHitTest(clientPoint) as Visual;
            return new RootHitResult
            {
                TargetTopLevel = rootWindow,
                TargetVisual = rootWindow,
                LocalPoint = clientPoint,
                HitVisual = mainHit ?? rootWindow
            };
        }
    }
}
