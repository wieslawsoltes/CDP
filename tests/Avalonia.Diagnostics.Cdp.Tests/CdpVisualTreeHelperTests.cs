using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Xunit;

namespace Avalonia.Diagnostics.Cdp.Tests;

public class CdpVisualTreeHelperTests
{
    [AvaloniaFact]
    public void GetParent_ReturnsPrimaryWindow_ForOpenPopupContent()
    {
        var button = new Button { Name = "popupAnchor", Content = "Anchor" };
        var popupContent = new Border { Name = "popupContent", Width = 50, Height = 50, Background = Brushes.Green };
        var popup = new Popup { PlacementTarget = button, Child = popupContent };
        var panel = new StackPanel();
        panel.Children.Add(button);
        panel.Children.Add(popup);
        var window = new Window { Title = "Popup Parent Window", Width = 300, Height = 200, Content = panel };
        window.Show();
        CdpServer.Register(window, "Popup Parent Window");

        try
        {
            popup.IsOpen = true;
            Assert.True(popup.IsOpen);

            var primary = CdpServer.GetPrimaryWindow();
            Assert.NotNull(primary);
            Assert.Same(primary, CdpVisualTreeHelper.GetParent(popupContent, false));
            Assert.Same(primary, CdpVisualTreeHelper.GetParent(popupContent, true));

            var host = CdpVisualTreeHelper.GetPopupHost(popup);
            if (host != null)
            {
                Assert.Same(primary, CdpVisualTreeHelper.GetParent(host, false));
            }

            // Regular controls keep their own parent.
            Assert.Same(panel, CdpVisualTreeHelper.GetParent(button, false));

            popup.IsOpen = false;
            Assert.NotSame(primary, CdpVisualTreeHelper.GetParent(popupContent, true));
        }
        finally
        {
            popup.IsOpen = false;
            CdpServer.Unregister(window);
            window.Close();
        }
    }

    [AvaloniaFact]
    public void QuerySelectorAll_DescendantSelector_DoesNotScanWindowsForPopups()
    {
        var panel = new StackPanel { Name = "rowsHost" };
        for (int i = 0; i < 1500; i++)
        {
            panel.Children.Add(new Border { Child = new TextBlock { Text = "row " + i } });
        }
        var window = new Window { Title = "Large Tree Window", Width = 400, Height = 300, Content = panel };
        window.Show();
        CdpServer.Register(window, "Large Tree Window");

        try
        {
            CdpVisualTreeHelper.EnsureOpenPopupTracking();
            var visitsBefore = CdpVisualTreeHelper.PopupScanVisitCount;

            // No ListBox exists, so every TextBlock walks its full ancestor chain through GetParent.
            // A popup scan of all windows per parent lookup visits about 1500 * tree size visuals.
            foreach (var useLogicalTree in new[] { true, false })
            {
                Assert.Empty(SelectorEngine.QuerySelectorAll(window, "ListBox TextBlock", useLogicalTree));
                Assert.Equal(1500, SelectorEngine.QuerySelectorAll(window, "#rowsHost TextBlock", useLogicalTree).Count);
            }

            Assert.Equal(0, CdpVisualTreeHelper.PopupScanVisitCount - visitsBefore);
        }
        finally
        {
            CdpServer.Unregister(window);
            window.Close();
        }
    }

    [AvaloniaFact]
    public void GetParent_ForContentOfClosedPopups_DoesNotScanWindows()
    {
        var host = new StackPanel { Name = "closedPopupsHost" };
        var contents = new List<Border>();
        for (int i = 0; i < 300; i++)
        {
            var content = new Border { Child = new TextBlock { Text = "popup row " + i } };
            contents.Add(content);
            var row = new StackPanel();
            row.Children.Add(new TextBlock { Text = "row " + i });
            row.Children.Add(new Popup { Child = content });
            host.Children.Add(row);
        }
        var window = new Window { Title = "Closed Popups Window", Width = 400, Height = 300, Content = host };
        window.Show();
        CdpServer.Register(window, "Closed Popups Window");

        try
        {
            CdpVisualTreeHelper.EnsureOpenPopupTracking();
            var visitsBefore = CdpVisualTreeHelper.PopupScanVisitCount;

            // The logical tree reaches the content of every closed popup; none of them is re-parented.
            foreach (var content in contents)
            {
                Assert.IsType<Popup>(CdpVisualTreeHelper.GetParent(content, true));
            }
            Assert.Equal(300, SelectorEngine.QuerySelectorAll(window, "#closedPopupsHost Border > TextBlock", true).Count);

            Assert.Equal(0, CdpVisualTreeHelper.PopupScanVisitCount - visitsBefore);
        }
        finally
        {
            CdpServer.Unregister(window);
            window.Close();
        }
    }

    [AvaloniaFact]
    public void GetParent_ReturnsOwnerWindow_ForOwnedSecondaryWindow()
    {
        var mainWindow = new Window { Title = "Parent Main Window", Width = 400, Height = 300 };
        var dialog = new Window { Title = "Parent Owned Dialog", Width = 200, Height = 150 };
        var nestedDialog = new Window { Title = "Parent Nested Dialog", Width = 100, Height = 80 };
        var freeWindow = new Window { Title = "Parent Free Window", Width = 100, Height = 80 };
        mainWindow.Show();
        CdpServer.Register(mainWindow, "Parent Main Window");
        dialog.Show(mainWindow);
        nestedDialog.Show(dialog);
        freeWindow.Show();
        CdpServer.Register(dialog, "Parent Owned Dialog");
        CdpServer.Register(nestedDialog, "Parent Nested Dialog");
        CdpServer.Register(freeWindow, "Parent Free Window");

        try
        {
            // Windows left registered by other tests can be the primary window, so the free window is
            // checked against whatever window is primary.
            var primary = CdpServer.GetPrimaryWindow();
            Assert.NotNull(primary);

            Assert.Same(mainWindow, CdpVisualTreeHelper.GetParent(dialog, true));
            Assert.Same(dialog, CdpVisualTreeHelper.GetParent(nestedDialog, true));
            Assert.Same(dialog, CdpVisualTreeHelper.GetParent(nestedDialog, false));
            Assert.Same(primary, CdpVisualTreeHelper.GetParent(freeWindow, true));

            // Children follow the same model, so every window appears exactly once in the tree.
            var mainChildren = CdpVisualTreeHelper.GetChildren(mainWindow, true).ToList();
            Assert.Contains(dialog, mainChildren);
            Assert.DoesNotContain(nestedDialog, mainChildren);
            Assert.Contains(freeWindow, CdpVisualTreeHelper.GetChildren(primary!, true));
            Assert.DoesNotContain(nestedDialog, CdpVisualTreeHelper.GetChildren(primary!, true));
            Assert.Contains(nestedDialog, CdpVisualTreeHelper.GetChildren(dialog, true));
            Assert.DoesNotContain(mainWindow, CdpVisualTreeHelper.GetChildren(dialog, true));
        }
        finally
        {
            CdpServer.Unregister(freeWindow);
            CdpServer.Unregister(nestedDialog);
            CdpServer.Unregister(dialog);
            CdpServer.Unregister(mainWindow);
            freeWindow.Close();
            nestedDialog.Close();
            dialog.Close();
            mainWindow.Close();
        }
    }

    [AvaloniaFact]
    public void GetTargetTopLevel_DialogWithoutFocus_StaysOnDialog()
    {
        var mainTextBox = new TextBox { Name = "mainFocusTextBox" };
        var mainWindow = new Window { Title = "Focus Main Window", Width = 400, Height = 300, Content = mainTextBox };
        var dialogButton = new Button { Name = "dialogFocusButton", Content = "Dialog" };
        var dialog = new Window { Title = "Focus Dialog", Width = 200, Height = 150, Content = dialogButton };
        mainWindow.Show();
        CdpServer.Register(mainWindow, "Focus Main Window");
        dialog.Show(mainWindow);
        CdpServer.Register(dialog, "Focus Dialog");

        try
        {
            mainWindow.Activate();
            Assert.True(mainTextBox.Focus());
            Dispatcher.UIThread.RunJobs();
            Assert.Same(mainTextBox, mainWindow.FocusManager?.GetFocusedElement());
            // The dialog's FocusManager reports the owner's focused TextBox as well.
            Assert.NotSame(dialog, TopLevel.GetTopLevel(dialog.FocusManager?.GetFocusedElement() as Visual));

            // The dialog session has no focused element; keys must not leak into the owner behind it.
            Assert.Same(dialog, Domains.InputDomain.GetTargetTopLevel(dialog));
            Assert.Same(mainWindow, Domains.InputDomain.GetTargetTopLevel(mainWindow));

            // Without a visible focused element in the main window, the owner session still reaches the focused dialog.
            dialog.Activate();
            Assert.True(dialogButton.Focus());
            mainTextBox.IsVisible = false;
            Dispatcher.UIThread.RunJobs();
            Assert.Same(dialogButton, dialog.FocusManager?.GetFocusedElement());
            Assert.Same(dialog, Domains.InputDomain.GetTargetTopLevel(mainWindow));
            Assert.Same(dialog, Domains.InputDomain.GetTargetTopLevel(dialog));
        }
        finally
        {
            CdpServer.Unregister(dialog);
            CdpServer.Unregister(mainWindow);
            dialog.Close();
            mainWindow.Close();
        }
    }

    [AvaloniaFact]
    public void HitTestAllRoots_UsesSessionWindow_WhenRootIsSecondaryWindow()
    {
        var mainBorder = new Border { Name = "mainHitBorder", Background = Brushes.Red };
        var mainWindow = new Window { Title = "Routing Main Window", Width = 400, Height = 300, Content = mainBorder };
        var dialogBorder = new Border { Name = "dialogHitBorder", Background = Brushes.Blue };
        var dialog = new Window { Title = "Routing Dialog", Width = 200, Height = 150, Content = dialogBorder };
        mainWindow.Show();
        dialog.Show(mainWindow);
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        CdpServer.Register(mainWindow, "Routing Main Window");
        CdpServer.Register(dialog, "Routing Dialog");

        try
        {
            var point = new Point(50, 50);

            var dialogHit = CdpVisualTreeHelper.HitTestAllRoots(dialog, point);
            Assert.Same(dialog, dialogHit.TargetTopLevel);
            Assert.True(dialogHit.HitVisual == dialogBorder || dialogBorder.IsVisualAncestorOf(dialogHit.HitVisual), $"Dialog hit landed on {dialogHit.HitVisual}");

            // The owner's session still sees the owned dialog stacked on top.
            var mainHit = CdpVisualTreeHelper.HitTestAllRoots(mainWindow, point);
            Assert.Same(dialog, mainHit.TargetTopLevel);

            Assert.False(CdpVisualTreeHelper.HasSecondaryWindowsOrPopups(dialog), "Dialog session must not see its owner as overlay");
            Assert.True(CdpVisualTreeHelper.HasSecondaryWindowsOrPopups(mainWindow), "Owner session must see the owned dialog");
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
