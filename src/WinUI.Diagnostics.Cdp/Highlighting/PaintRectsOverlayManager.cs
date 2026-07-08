using System;
using System.Linq;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Windows.Foundation;

namespace WinUI.Diagnostics.Cdp;

public static class PaintRectsOverlayManager
{
    public static void ShowPaintRect(Window window, Rect rect)
    {
        window.DispatcherQueue.TryEnqueue(() =>
        {
            if (window.Content == null) return;

            var container = window.Content as Panel;
            if (container == null) return;

            var canvas = container.Children.OfType<Canvas>().FirstOrDefault(c => c.Name == "CdpHighlightOverlayCanvas");
            if (canvas == null) return;

            // Draw a bright translucent green box representing the painted region
            var paintRect = new Rectangle
            {
                Width = rect.Width,
                Height = rect.Height,
                Fill = new SolidColorBrush(ColorHelper.FromArgb(80, 76, 175, 80)), // Translucent green
                Stroke = new SolidColorBrush(Colors.Green),
                StrokeThickness = 1
            };

            Canvas.SetLeft(paintRect, rect.X);
            Canvas.SetTop(paintRect, rect.Y);
            canvas.Children.Add(paintRect);

            // Set a timer to remove the green paint flash rectangle after 250 milliseconds
            var timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(250)
            };
            timer.Tick += (sender, e) =>
            {
                canvas.Children.Remove(paintRect);
                timer.Stop();
            };
            timer.Start();
        });
    }
}
