using System;
using System.Collections.Concurrent;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Threading;

namespace Wpf.Diagnostics.Cdp;

public static class PaintRectsOverlayManager
{
    private static readonly ConcurrentDictionary<Window, (PaintRectsAdorner Adorner, AdornerLayer Layer)> _activeAdorners = new();

    public static void SetEnabled(Window window, bool enabled)
    {
        var dispatcher = window.Dispatcher;
        if (dispatcher.CheckAccess())
        {
            SetEnabledInternal(window, enabled);
        }
        else
        {
            dispatcher.BeginInvoke(() => SetEnabledInternal(window, enabled));
        }
    }

    private static void SetEnabledInternal(Window window, bool enabled)
    {
        if (enabled)
        {
            if (_activeAdorners.ContainsKey(window))
            {
                return;
            }

            var adornerLayer = AdornerLayer.GetAdornerLayer(window);
            if (adornerLayer != null)
            {
                var adorner = new PaintRectsAdorner(window);
                adornerLayer.Add(adorner);
                _activeAdorners[window] = (adorner, adornerLayer);
            }
        }
        else
        {
            if (_activeAdorners.TryRemove(window, out var existing))
            {
                existing.Adorner.Detach();
                existing.Layer.Remove(existing.Adorner);
            }
        }
    }
}
