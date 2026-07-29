using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using WindowsRdpApp.Models;

namespace WindowsRdpApp.Services;

public class ProfileStorageService : IProfileStorageService
{
    private readonly string _filePath;
    private readonly bool _isCustomPath;
    private readonly ICredentialProtectionService _credentialProtection;
    private readonly SemaphoreSlim _fileLock = new(1, 1);
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public ProfileStorageService(string? customPath = null, ICredentialProtectionService? credentialProtection = null)
    {
        _credentialProtection = credentialProtection ?? new CredentialProtectionService();
        if (!string.IsNullOrEmpty(customPath))
        {
            _filePath = customPath;
            _isCustomPath = true;
        }
        else
        {
            _isCustomPath = false;
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string dir = Path.Combine(appData, "WindowsRdpApp");
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
            _filePath = Path.Combine(dir, "profiles.json");
        }
    }

    public async Task<List<RdpConnectionProfile>> LoadProfilesAsync()
    {
        await _fileLock.WaitAsync();
        try
        {
            return await LoadProfilesCoreAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.TraceError($"Failed to load profiles from {_filePath}: {ex.Message}");
            return GetDefaultProfiles();
        }
        finally
        {
            _fileLock.Release();
        }
    }

    public async Task SaveProfilesAsync(IEnumerable<RdpConnectionProfile> profiles)
    {
        await _fileLock.WaitAsync();
        try
        {
            await SaveProfilesCoreAsync(profiles);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.TraceError($"Failed to save profiles to {_filePath}: {ex.Message}");
            throw;
        }
        finally
        {
            _fileLock.Release();
        }
    }

    public async Task AddProfileAsync(RdpConnectionProfile profile)
    {
        await MutateProfilesAsync(profiles => profiles.Add(profile));
    }

    public async Task UpdateProfileAsync(RdpConnectionProfile profile)
    {
        await MutateProfilesAsync(profiles =>
        {
            int idx = profiles.FindIndex(p => p.Id == profile.Id);
            if (idx >= 0)
                profiles[idx] = profile;
            else
                profiles.Add(profile);
        });
    }

    public async Task DeleteProfileAsync(string profileId)
    {
        await MutateProfilesAsync(profiles => profiles.RemoveAll(p => p.Id == profileId));
    }

    private async Task MutateProfilesAsync(Action<List<RdpConnectionProfile>> mutation)
    {
        await _fileLock.WaitAsync();
        try
        {
            List<RdpConnectionProfile> profiles = await LoadProfilesCoreAsync();
            mutation(profiles);
            await SaveProfilesCoreAsync(profiles);
        }
        finally
        {
            _fileLock.Release();
        }
    }

    private async Task<List<RdpConnectionProfile>> LoadProfilesCoreAsync()
    {
        if (!File.Exists(_filePath))
            return _isCustomPath ? new List<RdpConnectionProfile>() : GetDefaultProfiles();

        string json = await File.ReadAllTextAsync(_filePath);
        if (string.IsNullOrWhiteSpace(json))
            return _isCustomPath ? new List<RdpConnectionProfile>() : GetDefaultProfiles();

        List<RdpConnectionProfile?>? loaded =
            JsonSerializer.Deserialize<List<RdpConnectionProfile?>>(json, JsonOptions);
        if (loaded == null)
            return _isCustomPath ? new List<RdpConnectionProfile>() : GetDefaultProfiles();

        List<RdpConnectionProfile> profiles = loaded.OfType<RdpConnectionProfile>().ToList();
        foreach (RdpConnectionProfile profile in profiles)
        {
            if (!string.IsNullOrEmpty(profile.Password))
                profile.Password = _credentialProtection.Unprotect(profile.Password);
        }
        return profiles;
    }

    private async Task SaveProfilesCoreAsync(IEnumerable<RdpConnectionProfile> profiles)
    {
        string? directory = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        List<RdpConnectionProfile> copies = profiles.Select(p => new RdpConnectionProfile
        {
            Id = p.Id,
            Name = p.Name,
            Host = p.Host,
            Port = p.Port,
            Username = p.Username,
            Password = string.IsNullOrEmpty(p.Password) ? string.Empty : _credentialProtection.Protect(p.Password),
            Domain = p.Domain,
            Width = p.Width,
            Height = p.Height,
            ColorDepth = p.ColorDepth,
            IsAutoConnect = p.IsAutoConnect,
            LastConnected = p.LastConnected
        }).ToList();

        string json = JsonSerializer.Serialize(copies, JsonOptions);
        string temporaryPath = _filePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            await File.WriteAllTextAsync(temporaryPath, json);
            File.Move(temporaryPath, _filePath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                try { File.Delete(temporaryPath); } catch { }
            }
        }
    }

    public static List<RdpConnectionProfile> GetDefaultProfiles()
    {
        return new List<RdpConnectionProfile>
        {
            new RdpConnectionProfile
            {
                Id = "profile-1",
                Name = "Primary Domain Controller",
                Host = "192.168.1.10",
                Port = 3389,
                Username = "Administrator",
                Domain = "CORP",
                LastConnected = DateTime.UtcNow.AddHours(-2)
            },
            new RdpConnectionProfile
            {
                Id = "profile-2",
                Name = "Dev Workspace VM",
                Host = "10.0.0.42",
                Port = 3389,
                Username = "developer",
                Domain = "DEV",
                LastConnected = DateTime.UtcNow.AddDays(-1)
            },
            new RdpConnectionProfile
            {
                Id = "profile-3",
                Name = "Staging Server",
                Host = "rdp.internal.net",
                Port = 33890,
                Username = "qa-agent",
                Domain = "STAGING",
                LastConnected = DateTime.UtcNow.AddDays(-5)
            }
        };
    }
}
