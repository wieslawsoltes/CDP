namespace CDP.Rdp.Tests.Controls;

using System;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using WindowsRdpApp.ViewModels;
using WindowsRdpApp.Views;
using Xunit;

public class WindowsRdpAppViewsLayoutTests
{
    [AvaloniaFact]
    public void Test_WindowsRdpApp_Views_Instantiate_Successfully()
    {
        var app = Avalonia.Application.Current;
        Assert.NotNull(app);

        // Load Icons resource dictionary
        var iconStyles = new Avalonia.Markup.Xaml.Styling.StyleInclude(new Uri("avares://CDP.Rdp.Tests/"))
        {
            Source = new Uri("avares://WindowsRdpApp/Styles/Icons.axaml")
        };
        app.Styles.Add(iconStyles);

        try
        {
            var quickConnectView = new QuickConnectView();
            Assert.NotNull(quickConnectView);
            quickConnectView.DataContext = new QuickConnectViewModel();

            var profilesView = new ProfilesView();
            Assert.NotNull(profilesView);
            profilesView.DataContext = new ProfilesViewModel();

            var workspaceView = new SessionWorkspaceView();
            Assert.NotNull(workspaceView);
            workspaceView.DataContext = new SessionWorkspaceViewModel();

            var settingsView = new SettingsView();
            Assert.NotNull(settingsView);
            settingsView.DataContext = new SettingsViewModel();
        }
        finally
        {
            app.Styles.Remove(iconStyles);
        }
    }
}
