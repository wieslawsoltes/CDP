namespace CDP.Rdp.Tests.Services;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Diagnostics.Cdp.Rdp;
using WindowsRdpApp.Models;
using WindowsRdpApp.Services;
using WindowsRdpApp.ViewModels;
using Xunit;

public class WindowsRdpAppProfileStorageServiceTests
{
    private readonly string _tempDir;

    public WindowsRdpAppProfileStorageServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "WindowsRdpApp_ProfileTests_" + Guid.NewGuid().ToString("N"));
        if (!Directory.Exists(_tempDir))
        {
            Directory.CreateDirectory(_tempDir);
        }
    }

    [Fact]
    public async Task ProfileStorage_AtomicWrite_CreatesFileViaTmpAndDirectory()
    {
        string filePath = Path.Combine(_tempDir, "subDir", "atomic_profiles.json");
        var storage = new ProfileStorageService(filePath);

        var profiles = new List<RdpConnectionProfile>
        {
            new RdpConnectionProfile { Name = "Atomic Server 1", Host = "192.168.1.5", Port = 3389 },
            new RdpConnectionProfile { Name = "Atomic Server 2", Host = "192.168.1.6", Port = 3389 }
        };

        await storage.SaveProfilesAsync(profiles);

        Assert.True(File.Exists(filePath), "Atomic write file must exist after SaveProfilesAsync.");
        var loaded = await storage.LoadProfilesAsync();
        Assert.Equal(2, loaded.Count);
        Assert.Equal("Atomic Server 1", loaded[0].Name);
        Assert.Equal("Atomic Server 2", loaded[1].Name);
    }

    [Fact]
    public async Task ProfileStorage_CredentialProtection_EncryptsPasswordOnDiskAndDecryptsOnLoad()
    {
        string filePath = Path.Combine(_tempDir, "protected_profiles.json");
        var storage = new ProfileStorageService(filePath);

        string rawSecretPassword = "P@ssw0rd!SpecialSecret123";
        var profile = new RdpConnectionProfile
        {
            Id = "prof-secret-1",
            Name = "Protected Server",
            Host = "10.0.0.100",
            Username = "admin",
            Password = rawSecretPassword
        };

        await storage.SaveProfilesAsync(new[] { profile });

        // Check raw file text on disk
        string rawJsonOnDisk = await File.ReadAllTextAsync(filePath);
        Assert.DoesNotContain(rawSecretPassword, rawJsonOnDisk);
        Assert.Contains("ENC:", rawJsonOnDisk);

        // Load profiles and verify decrypted password matches original plain text
        var loaded = await storage.LoadProfilesAsync();
        Assert.Single(loaded);
        Assert.Equal(rawSecretPassword, loaded[0].Password);
    }

    [Fact]
    public async Task ProfileStorage_CRUD_AddUpdateDeleteOperations()
    {
        string filePath = Path.Combine(_tempDir, "crud_profiles.json");
        var storage = new ProfileStorageService(filePath);

        var p1 = new RdpConnectionProfile { Id = "c1", Name = "CRUD 1", Host = "10.0.0.1" };
        var p2 = new RdpConnectionProfile { Id = "c2", Name = "CRUD 2", Host = "10.0.0.2" };

        await storage.AddProfileAsync(p1);
        await storage.AddProfileAsync(p2);

        var loaded1 = await storage.LoadProfilesAsync();
        Assert.Equal(2, loaded1.Count);

        // Update p1
        p1.Name = "CRUD 1 Updated";
        await storage.UpdateProfileAsync(p1);

        var loaded2 = await storage.LoadProfilesAsync();
        Assert.Equal("CRUD 1 Updated", loaded2.First(x => x.Id == "c1").Name);

        // Delete p2
        await storage.DeleteProfileAsync("c2");

        var loaded3 = await storage.LoadProfilesAsync();
        Assert.Single(loaded3);
        Assert.Equal("c1", loaded3[0].Id);
    }

    [Fact]
    public async Task ProfilesViewModel_SearchFilter_FiltersProfilesByNameHostUsernameDomain()
    {
        string filePath = Path.Combine(_tempDir, "filter_profiles.json");
        File.WriteAllText(filePath, "[]");
        var storage = new ProfileStorageService(filePath);
        var vm = new ProfilesViewModel(storage);
        await vm.LoadProfilesAsync();

        vm.NewName = "Production Controller";
        vm.NewHost = "10.0.0.50";
        vm.NewUsername = "prod_admin";
        vm.AddProfileCommand.Execute(null);

        vm.NewName = "Staging Sandbox";
        vm.NewHost = "10.0.0.60";
        vm.NewUsername = "qa_user";
        vm.AddProfileCommand.Execute(null);

        Assert.Equal(2, vm.Profiles.Count);

        // Search for "Prod"
        vm.SearchQuery = "Prod";
        Assert.Single(vm.FilteredProfiles);
        Assert.Equal("Production Controller", vm.FilteredProfiles[0].Name);

        // Search for "qa"
        vm.SearchQuery = "qa";
        Assert.Single(vm.FilteredProfiles);
        Assert.Equal("Staging Sandbox", vm.FilteredProfiles[0].Name);

        // Clear search
        vm.SearchQuery = "";
        Assert.Equal(2, vm.FilteredProfiles.Count);
    }

    [Fact]
    public async Task ProfilesViewModel_ImportExport_SerializesAndDeserializesProfiles()
    {
        string filePath = Path.Combine(_tempDir, "vm_import_export.json");
        File.WriteAllText(filePath, "[]");
        var storage = new ProfileStorageService(filePath);
        var vm = new ProfilesViewModel(storage);
        await vm.LoadProfilesAsync();

        vm.NewName = "Export Server 1";
        vm.AddProfileCommand.Execute(null);

        string exportPath = Path.Combine(_tempDir, "exported_profiles.json");
        await vm.ExecuteExportProfilesAsync(exportPath);

        Assert.True(File.Exists(exportPath));

        // Create new vm and import
        string filePath2 = Path.Combine(_tempDir, "vm_import_2.json");
        File.WriteAllText(filePath2, "[]");
        var vm2 = new ProfilesViewModel(new ProfileStorageService(filePath2));
        await vm2.LoadProfilesAsync();

        await vm2.ExecuteImportProfilesAsync(exportPath);
        Assert.Single(vm2.Profiles);
        Assert.Equal("Export Server 1", vm2.Profiles[0].Name);
    }

    [Fact]
    public void RdpControl_ScaleFactor_TranslatesCoordinatesWithScaling()
    {
        var rdpControl = new RdpControl
        {
            Width = 1000,
            Height = 500,
            ScaleFactor = 2.0
        };
        rdpControl.InitFrameBuffer(1000, 500);

        rdpControl.TranslateCoordinates(new Point(250, 125), out ushort x, out ushort y);
        Assert.Equal(125, (int)x);
        Assert.Equal(62, (int)y);
    }
}
