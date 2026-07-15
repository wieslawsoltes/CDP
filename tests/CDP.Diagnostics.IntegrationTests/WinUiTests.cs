using System;
using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using WinUI.Diagnostics.Cdp;
using Xunit;

namespace CDP.Diagnostics.IntegrationTests;

public class WinUiTests
{
    [Fact]
    public void TestWinUiPopupAndSecondaryWindowSupport()
    {
        // Initialize CdpServer
        CdpServer.EnsureInitialized();

        var mainGrid = new Grid { Name = "MainGrid" };
        var mainWindow = new Window();
        mainWindow.Content = mainGrid;

        var secondaryGrid = new Grid { Name = "SecondaryGrid" };
        var secondaryWindow = new Window();
        secondaryWindow.Content = secondaryGrid;

        CdpServer.Register(mainWindow, "MainWindow");
        CdpServer.Register(secondaryWindow, "SecondaryWindow");

        try
        {
            // Set up mock XamlRoot using MainWindow.Content's XamlRoot if available
            var xamlRoot = mainGrid.XamlRoot;

            // Create Popup and add to PopupRoot or visual tree if xamlRoot is available
            var popup = new Popup
            {
                IsOpen = true
            };
            var popupBtn = new Button { Name = "PopupBtn", Content = "Popup Button" };
            popup.Child = popupBtn;

            mainGrid.Children.Add(popup);

            // 1. Verify GetChildren anchors secondary window content and popups to main content
            var children = CdpVisualTreeHelper.GetChildren(mainGrid, false).ToList();
            Assert.Contains(secondaryGrid, children);
            Assert.Contains(popupBtn, children);

            // 2. Verify GetParent maps popup children and secondary window content back to main window Content
            var parentOfSecondary = CdpVisualTreeHelper.GetParent(secondaryGrid, false);
            Assert.Equal(mainGrid, parentOfSecondary);

            var parentOfPopupChild = CdpVisualTreeHelper.GetParent(popupBtn, false);
            Assert.Equal(mainGrid, parentOfPopupChild);

            // 3. Verify QuerySelector locates element inside popup
            var foundBtn = SelectorEngine.QuerySelector(mainGrid, "#PopupBtn");
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
    }
}
