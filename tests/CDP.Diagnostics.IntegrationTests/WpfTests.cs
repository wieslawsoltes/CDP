using System;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using Wpf.Diagnostics.Cdp;
using Xunit;

namespace CDP.Diagnostics.IntegrationTests;

public class WpfTests
{
    private Exception? _threadException;

    private void RunInSta(Action action)
    {
        _threadException = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                _threadException = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (_threadException != null)
        {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(_threadException).Throw();
        }
    }

    [Fact]
    public void TestWpfPopupAndSecondaryWindowSupport()
    {
        RunInSta(() =>
        {
            CdpServer.EnsureInitialized();

            var mainGrid = new Grid { Name = "MainGrid" };
            var mainWindow = new Window
            {
                Title = "MainWindow",
                Content = mainGrid,
                Visibility = Visibility.Visible
            };

            var secondaryGrid = new Grid { Name = "SecondaryGrid" };
            var secondaryWindow = new Window
            {
                Title = "SecondaryWindow",
                Content = secondaryGrid,
                Visibility = Visibility.Visible
            };

            CdpServer.Register(mainWindow, "MainWindow");
            CdpServer.Register(secondaryWindow, "SecondaryWindow");

            try
            {
                // Create Popup and add to mainGrid's children
                var popup = new Popup
                {
                    IsOpen = true
                };
                var popupBtn = new Button { Name = "PopupBtn", Content = "Popup Button" };
                popup.Child = popupBtn;

                mainGrid.Children.Add(popup);

                // 1. Verify GetChildren anchors secondary window and popups to main window
                var children = CdpVisualTreeHelper.GetChildren(mainWindow, false).ToList();
                Assert.Contains(secondaryWindow, children);
                Assert.Contains(popupBtn, children);

                // 2. Verify GetParent maps popup child and secondary window back to main window
                var parentOfSecondary = CdpVisualTreeHelper.GetParent(secondaryWindow, false);
                Assert.Equal(mainWindow, parentOfSecondary);

                var parentOfPopupChild = CdpVisualTreeHelper.GetParent(popupBtn, false);
                Assert.Equal(mainWindow, parentOfPopupChild);

                // 3. Verify Selector locates element inside popup
                var foundBtn = SelectorEngine.QuerySelector(mainWindow, "#PopupBtn");
                Assert.NotNull(foundBtn);
                Assert.Equal(popupBtn, foundBtn);
            }
            finally
            {
                CdpServer.Unregister(mainWindow);
                CdpServer.Unregister(secondaryWindow);
                mainWindow.Close();
                secondaryWindow.Close();
            }
        });
    }
}
