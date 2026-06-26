using Avalonia.Headless.XUnit;
using Xunit;
using CdpInspectorApp.Views;
using Avalonia.Controls;
using System;

namespace Avalonia.Diagnostics.Cdp.Tests;

public class ViewsLayoutTests
{
    [AvaloniaFact]
    public void Test_Views_Instantiate_And_Load_Successfully()
    {
        var app = Avalonia.Application.Current;
        Assert.NotNull(app);

        // Load the shared stylesheet to ensure Resource references (like FolderIcon, PlayIcon) resolve correctly
        var sharedStyles = new Avalonia.Markup.Xaml.Styling.StyleInclude(new Uri("avares://Avalonia.Diagnostics.Cdp.Tests/"))
        {
            Source = new Uri("avares://CDP.Inspector.Shared/Styles.axaml")
        };
        app.Styles.Add(sharedStyles);

        try
        {
            // Instantiate views to trigger XAML loading, parsing, and resource resolution
            var testStudioView = new TestStudioView();
            Assert.NotNull(testStudioView);

            var recorderView = new RecorderView();
            Assert.NotNull(recorderView);

            var simulationView = new SimulationView();
            Assert.NotNull(simulationView);

            var videoPlaybackWindow = new VideoPlaybackWindow();
            Assert.NotNull(videoPlaybackWindow);

            var networkView = new NetworkView();
            Assert.NotNull(networkView);
        }
        finally
        {
            app.Styles.Remove(sharedStyles);
        }
    }
}
