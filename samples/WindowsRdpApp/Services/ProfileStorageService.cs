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
            if (File.Exists(_filePath))
            {
                string json = await File.ReadAllTextAsync(_filePath);
                if (string.IsNullOrWhiteSpace(json))
                {
                    return _isCustomPath ? new List<RdpConnectionProfile>() : GetDefaultProfiles();
                }
                var profiles = JsonSerializer.Deserialize<List<RdpConnectionProfile>>(json, JsonOptions);
                if (profiles == null) return _isCustomPath ? new List<RdpConnectionProfile>() : GetDefaultProfiles();

                foreach (var profile in profiles)
                {
                    if (profile != null && !string.IsNullOrEmpty(profile.Password))
                    {
                        profile.Password = _credentialProtection.Unprotect(profile.Password);
                    }
                }
                return profiles;
            }
            else
            {
                if (_isCustomPath)
                {
                    return new List<RdpConnectionProfile>();
                }
            }
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

        return GetDefaultProfiles();
    }

    public async Task SaveProfilesAsync(IEnumerable<RdpConnectionProfile> profiles)
    {
        await _fileLock.WaitAsync();
        string? tmpPath = null;
        try
        {
            string? dir = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            var copyList = new List<RdpConnectionProfile>();
            foreach (var p in profiles)
            {
                var copy = new RdpConnectionProfile
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
                };
                copyList.Add(copy);
            }

            string json = JsonSerializer.Serialize(copyList, JsonOptions);
            tmpPath = _filePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
            await File.WriteAllTextAsync(tmpPath, json);
            File.Move(tmpPath, _filePath, overwrite: true);
            tmpPath = null;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.TraceError($"Failed to save profiles to {_filePath}: {ex.Message}");
            throw;
        }
        finally
        {
            if (tmpPath != null && File.Exists(tmpPath))
            {
                try { File.Delete(tmpPath); } catch { }
            }
            _fileLock.Release();
        }
    }

    public async Task AddProfileAsync(RdpConnectionProfile profile)
    {
        var profiles = await LoadProfilesAsync();
        profiles.Add(profile);
        await SaveProfilesAsync(profiles);
    }

    public async Task UpdateProfileAsync(RdpConnectionProfile profile)
    {
        var profiles = await LoadProfilesAsync();
        int idx = profiles.FindIndex(p => p.Id == profile.Id);
        if (idx >= 0)
        {
            profiles[idx] = profile;
        }
        else
        {
            profiles.Add(profile);
        }
        await SaveProfilesAsync(profiles);
    }

    public async Task DeleteProfileAsync(string profileId)
    {
        var profiles = await LoadProfilesAsync();
        profiles.RemoveAll(p => p.Id == profileId);
        await SaveProfilesAsync(profiles);
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
