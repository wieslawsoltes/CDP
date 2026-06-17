using System;
using System.Collections.Concurrent;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Threading;

namespace Avalonia.Diagnostics.Cdp;

public static class HighlightOverlayManager
{
    private static readonly ConcurrentDictionary<TopLevel, HighlightAdorner> _activeAdorners = new();

    public static void ShowHighlight(TopLevel window, Visual visual)
    {
        if (Dispatcher.UIThread.CheckAccess())
        {
            ShowHighlightInternal(window, visual);
        }
        else
        {
            Dispatcher.UIThread.Post(() => ShowHighlightInternal(window, visual));
        }
    }

    public static void HideHighlight(TopLevel window)
    {
        if (Dispatcher.UIThread.CheckAccess())
        {
            HideHighlightInternal(window);
        }
        else
        {
            Dispatcher.UIThread.Post(() => HideHighlightInternal(window));
        }
    }

    private static void ShowHighlightInternal(TopLevel window, Visual visual)
    {
        if (_activeAdorners.TryGetValue(window, out var existingAdorner))
        {
            if (existingAdorner.AdornedVisual == visual)
            {
                // Already highlighting this visual, avoid flickering or recreating
                return;
            }
        }

        HideHighlightInternal(window);

        var adornerLayer = AdornerLayer.GetAdornerLayer(visual);
        if (adornerLayer != null)
        {
            var adorner = new HighlightAdorner(visual);
            adornerLayer.Children.Add(adorner);
            _activeAdorners[window] = adorner;
        }
    }

    private static void HideHighlightInternal(TopLevel window)
    {
        if (_activeAdorners.TryRemove(window, out var adorner))
        {
            var adornerLayer = AdornerLayer.GetAdornerLayer(adorner);
            if (adornerLayer != null)
            {
                adornerLayer.Children.Remove(adorner);
            }
        }
    }
}
