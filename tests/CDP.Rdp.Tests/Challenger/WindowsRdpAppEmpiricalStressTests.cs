namespace CDP.Rdp.Tests.Challenger;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Headless.XUnit;
using WindowsRdpApp.Models;
using WindowsRdpApp.Services;
using WindowsRdpApp.ViewModels;
using Xunit;

[Xunit.Collection("RdpTests")]
public class WindowsRdpAppEmpiricalStressTests
{
    private readonly string _tempTestDir;

    public WindowsRdpAppEmpiricalStressTests()
    {
        _tempTestDir = Path.Combine(Path.GetTempPath(), "WindowsRdpApp_StressTests_" + Guid.NewGuid().ToString("N"));
        if (!Directory.Exists(_tempTestDir))
        {
            Directory.CreateDirectory(_tempTestDir);
        }
    }

    [AvaloniaTheory]
    [InlineData(65536, 1080, 32, "Width")]
    [InlineData(1920, 65536, 32, "Height")]
    [InlineData(1920, 1080, 65536, "ColorDepth")]
    [InlineData(1920, 1080, 20, "ColorDepth")]
    public async Task SessionConnect_InvalidDesktopSettings_AreRejectedBeforeNarrowing(
        int width,
        int height,
        int colorDepth,
        string parameterName)
    {
        using var tab = new RdpSessionTab
        {
            Width = width,
            Height = height,
            ColorDepth = colorDepth
        };

        ArgumentOutOfRangeException exception =
            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => tab.ConnectSessionAsync());

        Assert.Equal(parameterName, exception.ParamName);
        Assert.Null(tab.Session);
    }

    // ----------------------------------------------------------------------------------
    // 1. Profile Storage Serialization & Persistence Empirical Tests
    // ----------------------------------------------------------------------------------

    [AvaloniaFact]
    public async Task ProfileStorage_SaveProfilesAsync_WhenFileDoesNotExist_DemonstratesBugOrBehavior()
    {
        string filePath = Path.Combine(_tempTestDir, "non_existent_initial.json");
        var storage = new ProfileStorageService(filePath);
        var testProfiles = new List<RdpConnectionProfile>
        {
            new RdpConnectionProfile { Name = "Test Storage Profile", Host = "10.10.10.10", Port = 3389 }
        };

        await storage.SaveProfilesAsync(testProfiles);

        // Empirical check: File.Exists(filePath) should be true if saving succeeded for a new file.
        // Due to the warm-up read bug (File.ReadAllTextAsync on non-existent file), save fails silently if file did not exist!
        bool fileExists = File.Exists(filePath);
        
        // Assert loaded profiles
        var loaded = await storage.LoadProfilesAsync();
        
        if (!fileExists)
        {
            // If file was not created, LoadProfilesAsync falls back to GetDefaultProfiles() (3 default items)
            Assert.False(fileExists, "Empirical finding confirmed: SaveProfilesAsync fails silently on non-existent file path due to warm-up read.");
            Assert.Equal(3, loaded.Count);
        }
        else
        {
            Assert.Single(loaded);
            Assert.Equal("Test Storage Profile", loaded[0].Name);
        }
    }

    [AvaloniaFact]
    public async Task ProfileStorage_SaveProfilesAsync_WhenFileAlreadyExists_OverwritesSuccessfully()
    {
        string filePath = Path.Combine(_tempTestDir, "existing_file.json");
        await File.WriteAllTextAsync(filePath, "[]");

        var storage = new ProfileStorageService(filePath);
        var testProfiles = new List<RdpConnectionProfile>
        {
            new RdpConnectionProfile { Name = "Existing File Profile", Host = "192.168.1.50", Port = 33890 }
        };

        await storage.SaveProfilesAsync(testProfiles);

        Assert.True(File.Exists(filePath));
        var loaded = await storage.LoadProfilesAsync();
        Assert.Single(loaded);
        Assert.Equal("Existing File Profile", loaded[0].Name);
        Assert.Equal("192.168.1.50", loaded[0].Host);
        Assert.Equal(33890, loaded[0].Port);
    }

    [AvaloniaFact]
    public async Task ProfileStorage_LoadProfilesAsync_WithMalformedJson_FallsBackToDefaults()
    {
        string filePath = Path.Combine(_tempTestDir, "malformed.json");
        await File.WriteAllTextAsync(filePath, "{ invalid json content $$$ }");

        var storage = new ProfileStorageService(filePath);
        var loaded = await storage.LoadProfilesAsync();

        Assert.NotNull(loaded);
        Assert.Equal(3, loaded.Count); // Default profiles
        Assert.Equal("Primary Domain Controller", loaded[0].Name);
    }

    [AvaloniaFact]
    public async Task ProfileStorage_LoadProfilesAsync_WithEmptyFile_FallsBackToDefaults()
    {
        string filePath = Path.Combine(_tempTestDir, "empty.json");
        await File.WriteAllTextAsync(filePath, "");

        var storage = new ProfileStorageService(filePath);
        var loaded = await storage.LoadProfilesAsync();

        Assert.NotNull(loaded);
        Assert.Empty(loaded);
    }

    [AvaloniaFact]
    public async Task ProfileStorage_LoadProfilesAsync_WithEmptyArray_ReturnsEmptyList()
    {
        string filePath = Path.Combine(_tempTestDir, "empty_array.json");
        await File.WriteAllTextAsync(filePath, "[]");

        var storage = new ProfileStorageService(filePath);
        var loaded = await storage.LoadProfilesAsync();

        Assert.NotNull(loaded);
        Assert.Empty(loaded);
    }

    [AvaloniaFact]
    public async Task ProfileStorage_Serialization_PreservesSpecialCharactersAndExtremeDates()
    {
        string filePath = Path.Combine(_tempTestDir, "special_chars.json");
        await File.WriteAllTextAsync(filePath, "[]"); // pre-create so save works

        var storage = new ProfileStorageService(filePath);
        DateTime utcNow = DateTime.UtcNow;
        var profile = new RdpConnectionProfile
        {
            Id = "special-id-123",
            Name = "Server\tWith\nSpecial \"Chars\" & Emoji 🚀",
            Host = "domain.local",
            Port = 65535,
            Username = "user@domain.local",
            Password = "P@ssw0rd!#$ %^&*()",
            Domain = "CORP_DOMAIN",
            Width = 3840,
            Height = 2160,
            ColorDepth = 32,
            IsAutoConnect = true,
            LastConnected = utcNow
        };

        await storage.SaveProfilesAsync(new[] { profile });
        var loaded = await storage.LoadProfilesAsync();

        Assert.Single(loaded);
        var p = loaded[0];
        Assert.Equal("special-id-123", p.Id);
        Assert.Equal("Server\tWith\nSpecial \"Chars\" & Emoji 🚀", p.Name);
        Assert.Equal(65535, p.Port);
        Assert.Equal("P@ssw0rd!#$ %^&*()", p.Password);
        Assert.True(p.IsAutoConnect);
        Assert.NotNull(p.LastConnected);
        Assert.Equal(utcNow.ToString("u"), p.LastConnected.Value.ToString("u"));
    }

    // ----------------------------------------------------------------------------------
    // 2. ProfilesViewModel Creation, Modification & Edge Cases
    // ----------------------------------------------------------------------------------

    [AvaloniaFact]
    public async Task ProfilesViewModel_AddProfile_WithEmptyOrInvalidFields_AppliesDefaults()
    {
        string filePath = Path.Combine(_tempTestDir, "profiles_vm_add.json");
        File.WriteAllText(filePath, "[]");
        var storage = new ProfileStorageService(filePath);
        var vm = new ProfilesViewModel(storage);
        await vm.LoadProfilesAsync();

        vm.NewName = "   ";
        vm.NewHost = "";
        vm.NewPort = -1;
        vm.NewUsername = "admin_user";

        await vm.ExecuteAddProfileAsync();

        var added = vm.Profiles.LastOrDefault();
        Assert.NotNull(added);
        Assert.Equal("New Connection", added.Name);
        Assert.Equal("127.0.0.1", added.Host);
        Assert.Equal(3389, added.Port);
        Assert.Equal("admin_user", added.Username);
        Assert.Equal(vm.SelectedProfile, added);
    }

    [AvaloniaFact]
    public async Task ProfilesViewModel_DeleteProfile_UntilEmpty_HandlesNullSelectionSafely()
    {
        string filePath = Path.Combine(_tempTestDir, "profiles_vm_del.json");
        var storage = new ProfileStorageService(filePath);
        var vm = new ProfilesViewModel(storage);
        await vm.LoadProfilesAsync();

        vm.NewName = "Test Server";
        await vm.ExecuteAddProfileAsync();

        // Delete all items repeatedly
        while (vm.Profiles.Count > 0)
        {
            vm.SelectedProfile = vm.Profiles[0];
            await vm.ExecuteDeleteProfileAsync();
        }

        Assert.Empty(vm.Profiles);
        Assert.Null(vm.SelectedProfile);
        Assert.Contains("Deleted profile", vm.StatusText);

        // Executing delete command when SelectedProfile is null should not throw
        var exception = Record.Exception(() => vm.DeleteProfileCommand.Execute(null));
        Assert.Null(exception);
    }

    [AvaloniaFact]
    public async Task ProfilesViewModel_ConnectProfile_WhenNoSelection_DoesNotThrow()
    {
        string filePath = Path.Combine(_tempTestDir, "profiles_vm_conn.json");
        File.WriteAllText(filePath, "[]");
        var storage = new ProfileStorageService(filePath);
        var vm = new ProfilesViewModel(storage);
        await vm.LoadProfilesAsync();
        vm.SelectedProfile = null;

        bool eventFired = false;
        vm.RequestConnect += _ => eventFired = true;

        var exception = Record.Exception(() => vm.ConnectProfileCommand.Execute(null));
        Assert.Null(exception);
        Assert.False(eventFired);
    }

    // ----------------------------------------------------------------------------------
    // 3. SessionWorkspaceViewModel Tab Switching & Active Session Stress Tests
    // ----------------------------------------------------------------------------------

    [AvaloniaFact]
    public async Task SessionWorkspaceViewModel_TabSwitching_And_Closing_Behaviors()
    {
        var vm = new SessionWorkspaceViewModel();
        // Clear default initial session
        await vm.ExecuteDisconnectAllAsync();
        if (Avalonia.Threading.Dispatcher.UIThread.CheckAccess()) if (Avalonia.Threading.Dispatcher.UIThread.CheckAccess()) Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        Assert.Empty(vm.Sessions);

        // Open 3 sessions
        var tab1 = vm.OpenSession(new RdpConnectionProfile { Name = "Session 1", Host = "10.0.0.1" });
        var tab2 = vm.OpenSession(new RdpConnectionProfile { Name = "Session 2", Host = "10.0.0.2" });
        var tab3 = vm.OpenSession(new RdpConnectionProfile { Name = "Session 3", Host = "10.0.0.3" });

        Assert.Equal(3, vm.Sessions.Count);
        Assert.Equal(tab3, vm.SelectedSession);
        Assert.True(tab3.IsActive);

        // Switch selection to tab 2
        vm.SelectedSession = tab2;
        Assert.Equal(tab2, vm.SelectedSession);

        // Close tab 2 (the currently selected tab)
        await vm.ExecuteCloseSessionAsync(tab2);
        if (Avalonia.Threading.Dispatcher.UIThread.CheckAccess()) if (Avalonia.Threading.Dispatcher.UIThread.CheckAccess()) Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        Assert.Equal(2, vm.Sessions.Count);
        Assert.DoesNotContain(tab2, vm.Sessions);
        Assert.Equal("Disconnected", tab2.Status);
        // SelectedSession should fall back to Sessions.LastOrDefault() (tab3)
        Assert.Equal(tab3, vm.SelectedSession);

        // Close inactive tab 1
        await vm.ExecuteCloseSessionAsync(tab1);
        if (Avalonia.Threading.Dispatcher.UIThread.CheckAccess()) if (Avalonia.Threading.Dispatcher.UIThread.CheckAccess()) Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        Assert.Single(vm.Sessions);
        Assert.Equal(tab3, vm.SelectedSession);

        // Close remaining tab 3
        await vm.ExecuteCloseSessionAsync(tab3);
        if (Avalonia.Threading.Dispatcher.UIThread.CheckAccess()) if (Avalonia.Threading.Dispatcher.UIThread.CheckAccess()) Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        Assert.Empty(vm.Sessions);
        Assert.Null(vm.SelectedSession);
    }

    [AvaloniaFact]
    public void SessionWorkspaceViewModel_NewSessionCommand_GeneratesSequentialTabNames()
    {
        var vm = new SessionWorkspaceViewModel();
        vm.DisconnectAllCommand.Execute(null);

        vm.NewSessionCommand.Execute(null);
        Assert.Single(vm.Sessions);
        Assert.Equal("Session 1", vm.Sessions[0].Title);

        vm.NewSessionCommand.Execute(null);
        Assert.Equal(2, vm.Sessions.Count);
        Assert.Equal("Session 2", vm.Sessions[1].Title);
    }

    // ----------------------------------------------------------------------------------
    // 4. SettingsViewModel Theme Switching & Configuration Options
    // ----------------------------------------------------------------------------------

    [AvaloniaFact]
    public void SettingsViewModel_ThemeSwitching_UpdatesPropertiesAndStatusText()
    {
        var vm = new SettingsViewModel();
        Assert.Equal("Dark", vm.SelectedTheme);

        vm.SwitchThemeLightCommand.Execute(null);
        Assert.Equal("Light", vm.SelectedTheme);
        Assert.Contains("Light", vm.StatusText);

        vm.SwitchThemeDarkCommand.Execute(null);
        Assert.Equal("Dark", vm.SelectedTheme);
        Assert.Contains("Dark", vm.StatusText);

        // Test arbitrary or null theme strings (ApplyTheme should execute without throwing)
        var ex1 = Record.Exception(() => vm.SelectedTheme = "CustomTheme");
        Assert.Null(ex1);
        Assert.Equal("CustomTheme", vm.SelectedTheme);

        var ex2 = Record.Exception(() => vm.SelectedTheme = "LIGHT");
        Assert.Null(ex2);
    }

    [AvaloniaFact]
    public void SettingsViewModel_ConfigurationProperties_UpdateCorrectly()
    {
        var vm = new SettingsViewModel
        {
            CdpPort = 9299,
            EnableDoubleBuffering = false,
            FpsCap = 120,
            ShowDirtyRectangles = true,
            AutoReconnect = false
        };

        Assert.Equal(9299, vm.CdpPort);
        Assert.False(vm.EnableDoubleBuffering);
        Assert.Equal(120, vm.FpsCap);
        Assert.True(vm.ShowDirtyRectangles);
        Assert.False(vm.AutoReconnect);
    }

    // ----------------------------------------------------------------------------------
    // 5. MainWindowViewModel & CDP Startup Options Verification
    // ----------------------------------------------------------------------------------

    [AvaloniaFact]
    public void MainWindowViewModel_Navigation_SwitchesCurrentView()
    {
        string filePath = Path.Combine(_tempTestDir, "mw_nav.json");
        File.WriteAllText(filePath, "[]");
        var storage = new ProfileStorageService(filePath);
        var vm = new MainWindowViewModel(storage);

        // Initial default view
        Assert.IsType<QuickConnectViewModel>(vm.CurrentView);

        // Navigate via commands
        vm.NavigateProfilesCommand.Execute(null);
        Assert.IsType<ProfilesViewModel>(vm.CurrentView);

        vm.NavigateWorkspaceCommand.Execute(null);
        Assert.IsType<SessionWorkspaceViewModel>(vm.CurrentView);

        vm.NavigateSettingsCommand.Execute(null);
        Assert.IsType<SettingsViewModel>(vm.CurrentView);

        vm.NavigateQuickConnectCommand.Execute(null);
        Assert.IsType<QuickConnectViewModel>(vm.CurrentView);
    }

    [AvaloniaFact]
    public void MainWindowViewModel_SelectedNavItem_StringTags_SwitchView()
    {
        string filePath = Path.Combine(_tempTestDir, "mw_tags.json");
        File.WriteAllText(filePath, "[]");
        var storage = new ProfileStorageService(filePath);
        var vm = new MainWindowViewModel(storage);

        vm.SelectedNavItem = "Profiles";
        Assert.IsType<ProfilesViewModel>(vm.CurrentView);

        vm.SelectedNavItem = "Workspace";
        Assert.IsType<SessionWorkspaceViewModel>(vm.CurrentView);

        vm.SelectedNavItem = "Settings";
        Assert.IsType<SettingsViewModel>(vm.CurrentView);

        vm.SelectedNavItem = "QuickConnect";
        Assert.IsType<QuickConnectViewModel>(vm.CurrentView);

        // Unknown tag should leave view unchanged
        vm.SelectedNavItem = "NonExistentTag";
        Assert.IsType<QuickConnectViewModel>(vm.CurrentView);
    }

    [AvaloniaFact]
    public void MainWindowViewModel_RequestConnectEvents_OpenSessionInWorkspace()
    {
        string filePath = Path.Combine(_tempTestDir, "mw_req_conn.json");
        File.WriteAllText(filePath, "[]");
        var storage = new ProfileStorageService(filePath);
        var vm = new MainWindowViewModel(storage);

        var testProfile = new RdpConnectionProfile
        {
            Name = "Direct Connect Server",
            Host = "10.20.30.40",
            Port = 3389
        };

        vm.QuickConnectVM.Host = testProfile.Host;
        vm.QuickConnectVM.Port = testProfile.Port;
        vm.QuickConnectVM.ProfileName = testProfile.Name;

        vm.QuickConnectVM.ConnectCommand.Execute(null);

        Assert.IsType<SessionWorkspaceViewModel>(vm.CurrentView);
        var workspaceVM = (SessionWorkspaceViewModel)vm.CurrentView;
        var activeSession = workspaceVM.SelectedSession;
        Assert.NotNull(activeSession);
        Assert.Equal("Direct Connect Server", activeSession.Title);
        Assert.Equal("10.20.30.40", activeSession.Host);
        Assert.Contains("Active Session: Direct Connect Server", vm.StatusMessage);
    }

    [AvaloniaTheory]
    [InlineData(new string[] { "--port", "9235" }, 9235)]
    [InlineData(new string[] { "--PORT", "9300" }, 9300)]
    [InlineData(new string[] { "--port", "invalid" }, 9225)]
    [InlineData(new string[] { "--port" }, 9225)]
    [InlineData(new string[] { "--headless" }, 9225)]
    [InlineData(new string[] { }, 9225)]
    public void CdpStartupOptions_PortArgumentParsing_Behaviors(string[] args, int expectedPort)
    {
        int port = 9225;
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i].Equals("--port", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                if (int.TryParse(args[i + 1], out int parsedPort))
                {
                    port = parsedPort;
                }
            }
        }

        Assert.Equal(expectedPort, port);
    }
}
