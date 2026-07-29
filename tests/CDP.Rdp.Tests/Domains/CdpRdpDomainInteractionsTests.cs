namespace CDP.Rdp.Tests.Domains;

using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using CdpRdpApp;
using CdpRdpApp.ViewModels;
using Xunit;

[Xunit.Collection("RdpTests")]
public class CdpRdpDomainInteractionsTests
{
    [AvaloniaFact]
    public void MainWindow_InstantiatesWithCorrectSelectorsAndDataContext()
    {
        var window = new MainWindow();
        Assert.NotNull(window.DataContext);
        Assert.IsType<MainWindowViewModel>(window.DataContext);

        var vm = (MainWindowViewModel)window.DataContext;
        Assert.False(vm.Connection.IsConnected);
        Assert.Equal("127.0.0.1", vm.Host);
        Assert.Equal(3389, vm.Port);

        // Find child controls by name
        var txtHost = window.FindControl<TextBox>("txtHost");
        var txtPort = window.FindControl<TextBox>("txtPort");
        var txtUsername = window.FindControl<TextBox>("txtUsername");
        var txtPassword = window.FindControl<TextBox>("txtPassword");
        var btnConnect = window.FindControl<Button>("btnConnect");
        var btnDisconnect = window.FindControl<Button>("btnDisconnect");
        var btnRefreshTargets = window.FindControl<Button>("btnRefreshTargets");

        Assert.NotNull(txtHost);
        Assert.NotNull(txtPort);
        Assert.NotNull(txtUsername);
        Assert.NotNull(txtPassword);
        Assert.NotNull(btnConnect);
        Assert.NotNull(btnDisconnect);
        Assert.NotNull(btnRefreshTargets);

        var tabPreview = window.FindControl<TabItem>("TabPreview");
        var tabElements = window.FindControl<TabItem>("TabElements");
        var tabRecorder = window.FindControl<TabItem>("TabRecorder");

        Assert.NotNull(tabPreview);
        Assert.NotNull(tabElements);
        Assert.NotNull(tabRecorder);

        var imgScreenshot = window.FindControl<Image>("imgScreenshot");
        var lstVisualTree = window.FindControl<ListBox>("lstVisualTree");
        var panelSelectedElementProps = window.FindControl<Border>("panelSelectedElementProps");
        var btnTestStudioToggleRecord = window.FindControl<Button>("btnTestStudioToggleRecord");
        var btnTestStudioPlay = window.FindControl<Button>("btnTestStudioPlay");
        var lstSteps = window.FindControl<ListBox>("lstSteps");

        Assert.NotNull(imgScreenshot);
        Assert.NotNull(lstVisualTree);
        Assert.NotNull(panelSelectedElementProps);
        Assert.NotNull(btnTestStudioToggleRecord);
        Assert.NotNull(btnTestStudioPlay);
        Assert.NotNull(lstSteps);
    }
}
