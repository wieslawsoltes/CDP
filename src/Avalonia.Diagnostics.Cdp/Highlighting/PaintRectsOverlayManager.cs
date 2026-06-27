using System;
using System.Collections.Concurrent;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Threading;

namespace Avalonia.Diagnostics.Cdp;

public static class PaintRectsOverlayManager
{
    private static readonly ConcurrentDictionary<TopLevel, (PaintRectsAdorner Adorner, AdornerLayer Layer)> _activeAdorners = new();

    public static void SetEnabled(TopLevel window, bool enabled)
    {
        if (Dispatcher.UIThread.CheckAccess())
        {
            SetEnabledInternal(window, enabled);
        }
        else
        {
            Dispatcher.UIThread.Post(() => SetEnabledInternal(window, enabled));
        }
    }

    private static void SetEnabledInternal(TopLevel window, bool enabled)
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
                adornerLayer.Children.Add(adorner);
                _activeAdorners[window] = (adorner, adornerLayer);
            }
        }
        else
        {
            if (_activeAdorners.TryRemove(window, out var existing))
            {
                existing.Layer.Children.Remove(existing.Adorner);
            }
        }
    }
}
