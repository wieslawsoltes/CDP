using System;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using CdpInspectorApp.Controls;
using Xunit;

namespace Avalonia.Diagnostics.Cdp.Tests;

public class DockSplitPanelTests
{
    [AvaloniaFact]
    public void Test_DockSplitPanel_Defaults_And_Toggle_Modes()
    {
        var app = Avalonia.Application.Current;
        Assert.NotNull(app);

        var sharedStyles = new Avalonia.Markup.Xaml.Styling.StyleInclude(new Uri("avares://Avalonia.Diagnostics.Cdp.Tests/"))
        {
            Source = new Uri("avares://CDP.Inspector.Shared/Styles.axaml")
        };
        app.Styles.Add(sharedStyles);

        try
        {
            var dockPanel = new DockSplitPanel
            {
                Header = "Advanced Manual Interaction Tools",
                MainContent = new TextBlock { Text = "Main screencast area" },
                Content = new TextBlock { Text = "Sub-panel content" },
                IsExpanded = false,
                IsPinned = true,
                PanelHeight = 250.0
            };

            var window = new Window { Content = dockPanel };
            window.Show();

            try
            {
                Assert.False(dockPanel.IsExpanded);
                Assert.True(dockPanel.IsPinned);
                Assert.Equal(250.0, dockPanel.PanelHeight);

                // Toggle expand
                dockPanel.IsExpanded = true;
                Assert.True(dockPanel.IsExpanded);

                // Toggle pin (switch to overlay mode)
                dockPanel.IsPinned = false;
                Assert.False(dockPanel.IsPinned);

                // Re-pin (switch to inline mode)
                dockPanel.IsPinned = true;
                Assert.True(dockPanel.IsPinned);

                // Adjust panel height
                dockPanel.PanelHeight = 320.0;
                Assert.Equal(320.0, dockPanel.PanelHeight);
            }
            finally
            {
                window.Close();
            }
        }
        finally
        {
            app.Styles.Remove(sharedStyles);
        }
    }

    [Fact]
    public void Test_DockSplitPanel_MinMax_Height_Bounds()
    {
        var dockPanel = new DockSplitPanel
        {
            MinPanelHeight = 100.0,
            MaxPanelHeight = 400.0,
            PanelHeight = 250.0
        };

        Assert.Equal(100.0, dockPanel.MinPanelHeight);
        Assert.Equal(400.0, dockPanel.MaxPanelHeight);

        dockPanel.PanelHeight = 50.0;
        Assert.Equal(50.0, dockPanel.PanelHeight);

        dockPanel.PanelHeight = 500.0;
        Assert.Equal(500.0, dockPanel.PanelHeight);
    }

    [AvaloniaFact]
    public void Test_DockSplitPanel_ContentHost_SizeChanged_Updates_PanelHeight()
    {
        var app = Avalonia.Application.Current;
        Assert.NotNull(app);

        var sharedStyles = new Avalonia.Markup.Xaml.Styling.StyleInclude(new Uri("avares://Avalonia.Diagnostics.Cdp.Tests/"))
        {
            Source = new Uri("avares://CDP.Inspector.Shared/Styles.axaml")
        };
        app.Styles.Add(sharedStyles);

        try
        {
            var dockPanel = new DockSplitPanel
            {
                Header = "Test Panel",
                MainContent = new TextBlock { Text = "Main" },
                Content = new TextBlock { Text = "Sub" },
                IsExpanded = true,
                IsPinned = true,
                MinPanelHeight = 100.0,
                MaxPanelHeight = 500.0,
                PanelHeight = 220.0
            };

            var window = new Window { Content = dockPanel, Width = 800, Height = 600 };
            window.Show();

            try
            {
                Assert.Equal(220.0, dockPanel.PanelHeight);
            }
            finally
            {
                window.Close();
            }
        }
        finally
        {
            app.Styles.Remove(sharedStyles);
        }
    }
}
