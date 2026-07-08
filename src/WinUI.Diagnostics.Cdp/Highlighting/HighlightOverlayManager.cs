using System;
using System.Linq;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Windows.Foundation;

namespace WinUI.Diagnostics.Cdp;

public static class HighlightOverlayManager
{
    public static void ShowHighlight(Window window, UIElement element)
    {
        window.DispatcherQueue.TryEnqueue(() =>
        {
            if (window.Content == null) return;

            var canvas = GetOrCreateOverlayCanvas(window);
            canvas.Children.Clear();

            try
            {
                var transform = element.TransformToVisual(window.Content);
                var point = transform.TransformPoint(new Point(0, 0));
                
                double actualWidth = 0;
                double actualHeight = 0;

                if (element is FrameworkElement fe)
                {
                    actualWidth = fe.ActualWidth;
                    actualHeight = fe.ActualHeight;
                }

                if (actualWidth <= 0 || actualHeight <= 0) return;

                // Draw content highlighting box (DodgerBlue translucent)
                var contentRect = new Rectangle
                {
                    Width = actualWidth,
                    Height = actualHeight,
                    Fill = new SolidColorBrush(ColorHelper.FromArgb(60, 33, 150, 243)),
                    Stroke = new SolidColorBrush(Colors.DodgerBlue),
                    StrokeThickness = 1.5
                };

                Canvas.SetLeft(contentRect, point.X);
                Canvas.SetTop(contentRect, point.Y);
                canvas.Children.Add(contentRect);

                // Draw simple tooltip text block above the element
                var textBorder = new Border
                {
                    Background = new SolidColorBrush(ColorHelper.FromArgb(220, 33, 33, 33)),
                    CornerRadius = new CornerRadius(4),
                    Padding = new Thickness(6, 4, 6, 4)
                };

                var textBlock = new TextBlock
                {
                    Text = $"{element.GetType().Name} | {actualWidth:0} x {actualHeight:0}",
                    Foreground = new SolidColorBrush(Colors.White),
                    FontSize = 11
                };

                textBorder.Child = textBlock;

                double tooltipX = Math.Max(10, point.X);
                double tooltipY = Math.Max(10, point.Y - 30);

                Canvas.SetLeft(textBorder, tooltipX);
                Canvas.SetTop(textBorder, tooltipY);
                canvas.Children.Add(textBorder);
            }
            catch
            {
                // Ignore transformation issues
            }
        });
    }

    public static void HideHighlight(Window window)
    {
        window.DispatcherQueue.TryEnqueue(() =>
        {
            if (window.Content == null) return;
            var container = window.Content as Panel;
            if (container != null)
            {
                var canvas = container.Children.OfType<Canvas>().FirstOrDefault(c => c.Name == "CdpHighlightOverlayCanvas");
                if (canvas != null)
                {
                    canvas.Children.Clear();
                }
            }
        });
    }

    private static Panel EnsureOverlayContainer(Window window)
    {
        if (window.Content is Panel panel)
        {
            return panel;
        }

        var originalContent = window.Content;
        var wrapperGrid = new Grid();
        
        window.Content = null;
        wrapperGrid.Children.Add(originalContent);
        window.Content = wrapperGrid;

        return wrapperGrid;
    }

    private static Canvas GetOrCreateOverlayCanvas(Window window)
    {
        var container = EnsureOverlayContainer(window);
        var canvas = container.Children.OfType<Canvas>().FirstOrDefault(c => c.Name == "CdpHighlightOverlayCanvas");
        if (canvas == null)
        {
            canvas = new Canvas
            {
                Name = "CdpHighlightOverlayCanvas",
                IsHitTestVisible = false,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch
            };
            container.Children.Add(canvas);
        }
        return canvas;
    }
}
