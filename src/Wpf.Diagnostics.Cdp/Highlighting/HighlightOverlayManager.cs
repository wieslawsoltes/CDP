using System;
using System.Collections.Concurrent;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Threading;

namespace Wpf.Diagnostics.Cdp;

public static class HighlightOverlayManager
{
    private static readonly ConcurrentDictionary<Window, (HighlightAdorner Adorner, AdornerLayer Layer)> _activeAdorners = new();

    public static void ShowHighlight(Window window, Visual visual)
    {
        var dispatcher = window.Dispatcher;
        if (dispatcher.CheckAccess())
        {
            ShowHighlightInternal(window, visual);
        }
        else
        {
            dispatcher.BeginInvoke(() => ShowHighlightInternal(window, visual));
        }
    }

    public static void HideHighlight(Window window)
    {
        var dispatcher = window.Dispatcher;
        if (dispatcher.CheckAccess())
        {
            HideHighlightInternal(window);
        }
        else
        {
            dispatcher.BeginInvoke(() => HideHighlightInternal(window));
        }
    }

    private static void ShowHighlightInternal(Window window, Visual visual)
    {
        if (_activeAdorners.TryGetValue(window, out var existing))
        {
            if (existing.Adorner.AdornedVisual == visual)
            {
                return;
            }
        }

        HideHighlightInternal(window);

        if (visual is UIElement uiElement)
        {
            var adornerLayer = AdornerLayer.GetAdornerLayer(uiElement);
            if (adornerLayer != null)
            {
                var adorner = new HighlightAdorner(uiElement);
                adornerLayer.Add(adorner);
                _activeAdorners[window] = (adorner, adornerLayer);
            }
        }
    }

    private static void HideHighlightInternal(Window window)
    {
        if (_activeAdorners.TryRemove(window, out var existing))
        {
            existing.Layer.Remove(existing.Adorner);
        }
    }
}
