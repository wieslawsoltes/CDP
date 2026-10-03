using Avalonia;
using Avalonia.Controls;
using Avalonia.Diagnostics.Cdp;
using CdpServer = Avalonia.Diagnostics.Cdp.CdpServer;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Threading;
using SkiaSharp;
using Xunit;

namespace CDP.Inspector.Shared.Tests;

/// <summary>
/// Screenshot compositing of secondary windows. These tests need real Skia rendering, which this test
/// assembly enables, so they live here instead of in the headless-drawing CDP server tests.
/// </summary>
public class CdpWindowCompositingTests
{
    private static readonly SKColor MainColor = new(255, 0, 0);
    private static readonly SKColor DialogColor = new(0, 0, 255);

    private static int CountPixels(SKBitmap bitmap, SKColor color)
    {
        int count = 0;
        for (int y = 0; y < bitmap.Height; y++)
        {
            for (int x = 0; x < bitmap.Width; x++)
            {
                var pixel = bitmap.GetPixel(x, y);
                if (pixel.Red == color.Red && pixel.Green == color.Green && pixel.Blue == color.Blue)
                {
                    count++;
                }
            }
        }
        return count;
    }

    private static SKBitmap CreateWhiteBitmap(int width, int height)
    {
        var bitmap = new SKBitmap(width, height);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.White);
        return bitmap;
    }

    [AvaloniaFact]
    public void CompositeAllWindowsAndPopups_DialogRoot_DoesNotDrawOwner_OwnerRootDrawsDialog()
    {
        var mainWindow = new Window
        {
            Title = "Compositing Main Window",
            Width = 400,
            Height = 300,
            Content = new Border { Background = new SolidColorBrush(Color.FromRgb(MainColor.Red, MainColor.Green, MainColor.Blue)) }
        };
        var dialog = new Window
        {
            Title = "Compositing Dialog",
            Width = 200,
            Height = 150,
            Content = new Border { Background = new SolidColorBrush(Color.FromRgb(DialogColor.Red, DialogColor.Green, DialogColor.Blue)) }
        };
        mainWindow.Show();
        CdpServer.Register(mainWindow, "Compositing Main Window");
        dialog.Show(mainWindow);
        CdpServer.Register(dialog, "Compositing Dialog");
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();

        try
        {
            // The owner relation decides what is composited, whether or not mainWindow is the primary window.
            // Dialog session: the owner behind the dialog must not be painted into the dialog screenshot.
            using (var dialogShot = CreateWhiteBitmap(200, 150))
            {
                CdpVisualTreeHelper.CompositeAllWindowsAndPopups(dialog, dialogShot, 1.0);
                Assert.Equal(0, CountPixels(dialogShot, MainColor));
            }

            // Owner session: the owned dialog is still composited on top of the main window.
            using (var mainShot = CreateWhiteBitmap(400, 300))
            {
                Assert.True(CdpVisualTreeHelper.CompositeAllWindowsAndPopups(mainWindow, mainShot, 1.0));
                Assert.True(CountPixels(mainShot, DialogColor) > 100 * 100, "Owned dialog was not composited for the owner session");
            }
        }
        finally
        {
            CdpServer.Unregister(dialog);
            CdpServer.Unregister(mainWindow);
            dialog.Close();
            mainWindow.Close();
        }
    }
}
