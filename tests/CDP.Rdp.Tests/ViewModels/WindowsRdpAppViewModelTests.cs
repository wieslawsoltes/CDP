namespace CDP.Rdp.Tests.ViewModels;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using WindowsRdpApp.Models;
using WindowsRdpApp.Services;
using WindowsRdpApp.ViewModels;
using Xunit;

public class WindowsRdpAppViewModelTests
{
    [Fact]
    public void MainWindowViewModel_InitialState_DefaultsToQuickConnect()
    {
        var vm = new MainWindowViewModel();

        Assert.NotNull(vm.CurrentView);
        Assert.IsType<QuickConnectViewModel>(vm.CurrentView);
        Assert.Equal(9225, vm.CdpPort);
        Assert.Contains("CDP Server active", vm.StatusMessage);
    }

    [Fact]
    public void QuickConnectViewModel_ExecuteConnect_EmitsRequestConnectEvent()
    {
        var vm = new QuickConnectViewModel
        {
            Host = "192.168.1.50",
            Port = 3389,
            Username = "testadmin",
            Password = "secretpassword",
            Domain = "TESTDOMAIN",
            ProfileName = "Test Server"
        };

        RdpConnectionProfile? receivedProfile = null;
        vm.RequestConnect += p => receivedProfile = p;

        vm.ConnectCommand.Execute(null);

        Assert.NotNull(receivedProfile);
        Assert.Equal("192.168.1.50", receivedProfile.Host);
        Assert.Equal(3389, receivedProfile.Port);
        Assert.Equal("testadmin", receivedProfile.Username);
        Assert.Equal("TESTDOMAIN", receivedProfile.Domain);
        Assert.Equal("Test Server", receivedProfile.Name);
    }

    [Fact]
    public void QuickConnectViewModel_ExecuteClear_ResetsFields()
    {
        var vm = new QuickConnectViewModel
        {
            Host = "10.0.0.99",
            Port = 4444,
            Username = "user123"
        };

        vm.ClearCommand.Execute(null);

        Assert.Equal("127.0.0.1", vm.Host);
        Assert.Equal(3389, vm.Port);
        Assert.Equal("admin", vm.Username);
    }

    [Fact]
    public async Task ProfilesViewModel_AddAndDeleteProfile_UpdatesCollection()
    {
        var mockStorage = new ProfileStorageService("/tmp/test_profiles_mock.json");
        var vm = new ProfilesViewModel(mockStorage);
        await vm.LoadProfilesAsync();

        vm.NewName = "New Test Server";
        vm.NewHost = "172.16.0.5";
        vm.NewPort = 3389;
        vm.NewUsername = "sysadmin";

        int countBefore = vm.Profiles.Count;
        vm.AddProfileCommand.Execute(null);

        Assert.Equal(countBefore + 1, vm.Profiles.Count);
        var added = vm.Profiles.LastOrDefault();
        Assert.NotNull(added);
        Assert.Equal("New Test Server", added.Name);
        Assert.Equal("172.16.0.5", added.Host);

        vm.SelectedProfile = added;
        vm.DeleteProfileCommand.Execute(null);

        Assert.Equal(countBefore, vm.Profiles.Count);
    }

    [Fact]
    public void SessionWorkspaceViewModel_OpenSession_AddsSessionTab()
    {
        var vm = new SessionWorkspaceViewModel();
        int initialCount = vm.Sessions.Count;

        var profile = new RdpConnectionProfile
        {
            Name = "QA VM",
            Host = "10.0.1.20",
            Port = 3389,
            Username = "qauser"
        };

        var tab = vm.OpenSession(profile);

        Assert.Equal(initialCount + 1, vm.Sessions.Count);
        Assert.Equal("QA VM", tab.Title);
        Assert.Equal("10.0.1.20", tab.Host);
        Assert.Equal(vm.SelectedSession, tab);
    }

    [Fact]
    public void SessionWorkspaceViewModel_DisconnectAll_ClearsAllSessions()
    {
        var vm = new SessionWorkspaceViewModel();
        vm.OpenSession(new RdpConnectionProfile { Name = "Session 1" });
        vm.OpenSession(new RdpConnectionProfile { Name = "Session 2" });

        Assert.True(vm.Sessions.Count >= 2);

        vm.DisconnectAllCommand.Execute(null);

        Assert.Empty(vm.Sessions);
        Assert.Null(vm.SelectedSession);
    }

    [Fact]
    public void SettingsViewModel_SwitchTheme_UpdatesSelectedTheme()
    {
        var vm = new SettingsViewModel();
        Assert.Equal("Dark", vm.SelectedTheme);

        vm.SwitchThemeLightCommand.Execute(null);
        Assert.Equal("Light", vm.SelectedTheme);

        vm.SwitchThemeDarkCommand.Execute(null);
        Assert.Equal("Dark", vm.SelectedTheme);
    }

    [Fact]
    public async Task ProfileStorageService_SaveProfilesAsync_CreatesFileAndParentDirectory_WhenFileDoesNotExist()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "cdp_test_" + Guid.NewGuid().ToString("N"));
        string testFilePath = Path.Combine(tempDir, "nested_dir", "profiles.json");

        try
        {
            Assert.False(File.Exists(testFilePath));
            Assert.False(Directory.Exists(Path.GetDirectoryName(testFilePath)));

            var storageService = new ProfileStorageService(testFilePath);
            var profilesToSave = new List<RdpConnectionProfile>
            {
                new RdpConnectionProfile
                {
                    Id = "test-1",
                    Name = "Test Host Profile",
                    Host = "192.168.1.100",
                    Port = 3389,
                    Username = "testuser"
                }
            };

            await storageService.SaveProfilesAsync(profilesToSave);

            Assert.True(File.Exists(testFilePath));
            string json = await File.ReadAllTextAsync(testFilePath);
            Assert.Contains("Test Host Profile", json);
            Assert.Contains("192.168.1.100", json);

            var loadedProfiles = await storageService.LoadProfilesAsync();
            Assert.Single(loadedProfiles);
            Assert.Equal("Test Host Profile", loadedProfiles[0].Name);
            Assert.Equal("192.168.1.100", loadedProfiles[0].Host);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }
}
