using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Input;
using ReactiveUI;
using WindowsRdpApp.Models;
using WindowsRdpApp.Services;

namespace WindowsRdpApp.ViewModels;

public class ProfilesViewModel : ReactiveObject
{
    private readonly IProfileStorageService _storageService;
    private readonly ICredentialProtectionService _credentialProtection;
    private RdpConnectionProfile? _selectedProfile;
    private string _searchQuery = string.Empty;
    private string _newName = "New Server";
    private string _newHost = "192.168.1.100";
    private int _newPort = 3389;
    private string _newUsername = "admin";
    private string _newPassword = string.Empty;
    private string _newDomain = string.Empty;
    private string _statusText = "Profiles loaded";

    public ObservableCollection<RdpConnectionProfile> Profiles { get; } = new();
    public ObservableCollection<RdpConnectionProfile> FilteredProfiles { get; } = new();

    public RdpConnectionProfile? SelectedProfile
    {
        get => _selectedProfile;
        set => this.RaiseAndSetIfChanged(ref _selectedProfile, value);
    }

    public string SearchQuery
    {
        get => _searchQuery;
        set
        {
            this.RaiseAndSetIfChanged(ref _searchQuery, value);
            ApplyFilter();
        }
    }

    public string NewName
    {
        get => _newName;
        set => this.RaiseAndSetIfChanged(ref _newName, value);
    }

    public string NewHost
    {
        get => _newHost;
        set => this.RaiseAndSetIfChanged(ref _newHost, value);
    }

    public int NewPort
    {
        get => _newPort;
        set => this.RaiseAndSetIfChanged(ref _newPort, value);
    }

    public string NewUsername
    {
        get => _newUsername;
        set => this.RaiseAndSetIfChanged(ref _newUsername, value);
    }

    public string NewPassword
    {
        get => _newPassword;
        set => this.RaiseAndSetIfChanged(ref _newPassword, value);
    }

    public string NewDomain
    {
        get => _newDomain;
        set => this.RaiseAndSetIfChanged(ref _newDomain, value);
    }

    public string StatusText
    {
        get => _statusText;
        set => this.RaiseAndSetIfChanged(ref _statusText, value);
    }

    public ICommand ConnectProfileCommand { get; }
    public ICommand AddProfileCommand { get; }
    public ICommand DeleteProfileCommand { get; }
    public ICommand SaveProfileCommand { get; }
    public ICommand ImportProfilesCommand { get; }
    public ICommand ExportProfilesCommand { get; }

    public event Action<RdpConnectionProfile>? RequestConnect;

    public ProfilesViewModel(IProfileStorageService? storageService = null, ICredentialProtectionService? credentialProtection = null)
    {
        _storageService = storageService ?? new ProfileStorageService();
        _credentialProtection = credentialProtection ?? new CredentialProtectionService();

        ConnectProfileCommand = ReactiveCommand.CreateFromTask(ExecuteConnectProfileAsync);
        AddProfileCommand = ReactiveCommand.CreateFromTask(ExecuteAddProfileAsync);
        DeleteProfileCommand = ReactiveCommand.CreateFromTask(ExecuteDeleteProfileAsync);
        SaveProfileCommand = ReactiveCommand.CreateFromTask(ExecuteSaveProfileAsync);
        ImportProfilesCommand = ReactiveCommand.CreateFromTask<string>(ExecuteImportProfilesAsync);
        ExportProfilesCommand = ReactiveCommand.CreateFromTask<string>(ExecuteExportProfilesAsync);

        _ = LoadProfilesAsync();
    }

    public async Task LoadProfilesAsync()
    {
        var loaded = await _storageService.LoadProfilesAsync();
        RunOnUIThread(() =>
        {
            lock (Profiles)
            {
                Profiles.Clear();
                foreach (var p in loaded)
                {
                    Profiles.Add(p);
                }
            }

            ApplyFilter();
            SelectedProfile = FilteredProfiles.FirstOrDefault();
        });
    }

    private void ApplyFilter()
    {
        RunOnUIThread(() =>
        {
            FilteredProfiles.Clear();
            var query = SearchQuery?.Trim() ?? string.Empty;

            List<RdpConnectionProfile> snapshot;
            lock (Profiles)
            {
                snapshot = Profiles.ToList();
            }

            var items = string.IsNullOrEmpty(query)
                ? snapshot
                : snapshot.Where(p =>
                    p.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                    p.Host.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                    p.Username.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                    p.Domain.Contains(query, StringComparison.OrdinalIgnoreCase));

            foreach (var p in items)
            {
                FilteredProfiles.Add(p);
            }

            if (SelectedProfile != null && !FilteredProfiles.Contains(SelectedProfile))
            {
                SelectedProfile = FilteredProfiles.FirstOrDefault();
            }
        });
    }

    private async Task ExecuteConnectProfileAsync()
    {
        if (SelectedProfile != null)
        {
            SelectedProfile.LastConnected = DateTime.UtcNow;
            StatusText = $"Connecting to profile '{SelectedProfile.Name}'...";
            List<RdpConnectionProfile> snapshot;
            lock (Profiles) { snapshot = Profiles.ToList(); }
            await _storageService.SaveProfilesAsync(snapshot);
            RequestConnect?.Invoke(SelectedProfile);
        }
    }

    public async Task ExecuteAddProfileAsync()
    {
        var profile = new RdpConnectionProfile
        {
            Name = string.IsNullOrWhiteSpace(NewName) ? "New Connection" : NewName,
            Host = string.IsNullOrWhiteSpace(NewHost) ? "127.0.0.1" : NewHost,
            Port = NewPort <= 0 ? 3389 : NewPort,
            Username = NewUsername,
            Password = NewPassword,
            Domain = NewDomain
        };
        lock (Profiles)
        {
            Profiles.Add(profile);
        }
        ApplyFilter();
        SelectedProfile = profile;
        StatusText = $"Added profile '{profile.Name}'";
        List<RdpConnectionProfile> snapshot;
        lock (Profiles) { snapshot = Profiles.ToList(); }
        await _storageService.SaveProfilesAsync(snapshot);
    }

    private async Task ExecuteSaveProfileAsync()
    {
        if (SelectedProfile != null)
        {
            List<RdpConnectionProfile> snapshot;
            lock (Profiles) { snapshot = Profiles.ToList(); }
            await _storageService.SaveProfilesAsync(snapshot);
            StatusText = $"Saved profile '{SelectedProfile.Name}'";
        }
    }

    public async Task ExecuteDeleteProfileAsync()
    {
        if (SelectedProfile != null)
        {
            string name = SelectedProfile.Name;
            lock (Profiles)
            {
                Profiles.Remove(SelectedProfile);
            }
            ApplyFilter();
            SelectedProfile = FilteredProfiles.FirstOrDefault();
            List<RdpConnectionProfile> snapshot;
            lock (Profiles) { snapshot = Profiles.ToList(); }
            await _storageService.SaveProfilesAsync(snapshot);
            StatusText = $"Deleted profile '{name}'";
        }
    }

    private static void RunOnUIThread(Action action)
    {
        if (Avalonia.Threading.Dispatcher.UIThread.CheckAccess())
        {
            action();
        }
        else
        {
            Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(action).GetAwaiter().GetResult();
        }
    }

    public async Task ExecuteImportProfilesAsync(string filePath)
    {
        if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
            return;

        try
        {
            string json = await File.ReadAllTextAsync(filePath);
            var imported = JsonSerializer.Deserialize<List<RdpConnectionProfile>>(json);
            if (imported != null)
            {
                foreach (var p in imported)
                {
                    p.Password = _credentialProtection.Unprotect(p.Password);
                }
                RunOnUIThread(() =>
                {
                    lock (Profiles)
                    {
                        foreach (var p in imported)
                        {
                            Profiles.Add(p);
                        }
                    }
                    ApplyFilter();
                });
                List<RdpConnectionProfile> snapshot;
                lock (Profiles) { snapshot = Profiles.ToList(); }
                await _storageService.SaveProfilesAsync(snapshot);
                RunOnUIThread(() =>
                {
                    StatusText = $"Imported {imported.Count} profiles from '{Path.GetFileName(filePath)}'";
                });
            }
        }
        catch (Exception ex)
        {
            RunOnUIThread(() =>
            {
                StatusText = $"Import failed: {ex.Message}";
            });
        }
    }

    public async Task ExecuteExportProfilesAsync(string filePath)
    {
        if (string.IsNullOrEmpty(filePath))
            return;

        try
        {
            List<RdpConnectionProfile> snapshot;
            lock (Profiles) { snapshot = Profiles.ToList(); }
            var exportList = snapshot.Select(p => new RdpConnectionProfile
            {
                Id = p.Id,
                Name = p.Name,
                Host = p.Host,
                Port = p.Port,
                Username = p.Username,
                Password = _credentialProtection.Protect(p.Password),
                Domain = p.Domain,
                Width = p.Width,
                Height = p.Height,
                ColorDepth = p.ColorDepth,
                IsAutoConnect = p.IsAutoConnect,
                LastConnected = p.LastConnected
            }).ToList();
            string json = JsonSerializer.Serialize(exportList, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(filePath, json);
            StatusText = $"Exported {snapshot.Count} profiles to '{Path.GetFileName(filePath)}'";
        }
        catch (Exception ex)
        {
            StatusText = $"Export failed: {ex.Message}";
        }
    }
}
