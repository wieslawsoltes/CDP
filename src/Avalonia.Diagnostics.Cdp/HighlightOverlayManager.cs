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
        Dispatcher.UIThread.Post(() =>
        {
            HideHighlight(window);

            var adornerLayer = AdornerLayer.GetAdornerLayer(visual);
            if (adornerLayer != null)
            {
                var adorner = new HighlightAdorner(visual);
                adornerLayer.Children.Add(adorner);
                _activeAdorners[window] = adorner;
            }
        });
    }

    public static void HideHighlight(TopLevel window)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (_activeAdorners.TryRemove(window, out var adorner))
            {
                var adornerLayer = AdornerLayer.GetAdornerLayer(adorner);
                if (adornerLayer != null)
                {
                    adornerLayer.Children.Remove(adorner);
                }
                else
                {
                    var winAdornerLayer = AdornerLayer.GetAdornerLayer(window);
                    winAdornerLayer?.Children.Remove(adorner);
                }
            }
        });
    }
}
