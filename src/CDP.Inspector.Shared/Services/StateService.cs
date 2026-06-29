#nullable enable

using System;
using System.Collections.Concurrent;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using Chrome.DevTools.Protocol;

namespace CdpInspectorApp.Services;

/// <summary>
/// Provides versioned, error-resilient saving and restoring of application state.
/// </summary>
public class StateService : IStateService
{
    private static readonly ILogger Logger = CdpLogging.CreateLogger<StateService>();
    private readonly ConcurrentDictionary<string, IStateProvider> _providers = new(StringComparer.OrdinalIgnoreCase);
    private readonly string _filePath;

    /// <summary>
    /// Current schema version of the application state file.
    /// </summary>
    public const int CurrentVersion = 1;

    public StateService(string? filePath = null)
    {
        _filePath = filePath ?? GetDefaultStateFilePath();
    }

    public void RegisterProvider(IStateProvider provider)
    {
        if (provider == null) throw new ArgumentNullException(nameof(provider));
        _providers[provider.StateKey] = provider;
    }

    public void UnregisterProvider(string stateKey)
    {
        if (stateKey == null) throw new ArgumentNullException(nameof(stateKey));
        _providers.TryRemove(stateKey, out _);
    }

    public void Save()
    {
        try
        {
            var root = new JsonObject();
            root["version"] = CurrentVersion;
            root["timestamp"] = DateTime.UtcNow.ToString("o");

            var providersNode = new JsonObject();
            foreach (var pair in _providers)
            {
                try
                {
                    var providerNode = pair.Value.SaveState();
                    if (providerNode != null)
                    {
                        providersNode[pair.Key] = providerNode;
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "Failed to save state for provider '{Key}'", pair.Key);
                }
            }

            root["providers"] = providersNode;

            var options = new JsonSerializerOptions { WriteIndented = true };
            var json = root.ToJsonString(options);

            var dir = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            File.WriteAllText(_filePath, json);
            Logger.LogInformation("Saved application state to {FilePath}", _filePath);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to save application state to {FilePath}", _filePath);
        }
    }

    public void Load()
    {
        try
        {
            if (!File.Exists(_filePath))
            {
                Logger.LogInformation("No application state file found at {FilePath}", _filePath);
                return;
            }

            var json = File.ReadAllText(_filePath);
            if (string.IsNullOrWhiteSpace(json))
            {
                Logger.LogWarning("State file at {FilePath} is empty", _filePath);
                return;
            }

            var root = JsonNode.Parse(json) as JsonObject;
            if (root == null)
            {
                Logger.LogWarning("Failed to parse state file at {FilePath} as JSON object", _filePath);
                return;
            }

            var versionVal = (int?)root["version"];
            if (versionVal == null)
            {
                Logger.LogWarning("State file at {FilePath} has no version, assuming version 1", _filePath);
            }
            else if (versionVal.Value > CurrentVersion)
            {
                Logger.LogWarning("State file version {SavedVersion} is newer than current version {CurrentVersion}. Load may fail.", versionVal.Value, CurrentVersion);
            }

            var providersNode = root["providers"] as JsonObject;
            if (providersNode == null)
            {
                Logger.LogWarning("No 'providers' node found in state file at {FilePath}", _filePath);
                return;
            }

            foreach (var pair in _providers)
            {
                try
                {
                    if (providersNode.TryGetPropertyValue(pair.Key, out var providerNode))
                    {
                        pair.Value.LoadState(providerNode);
                    }
                    else
                    {
                        Logger.LogInformation("No state found for provider '{Key}' in state file", pair.Key);
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "Failed to load state for provider '{Key}'", pair.Key);
                }
            }

            Logger.LogInformation("Loaded application state from {FilePath}", _filePath);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to load application state from {FilePath}", _filePath);
        }
    }

    private static string GetDefaultStateFilePath()
    {
        try
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            if (string.IsNullOrEmpty(appData))
            {
                appData = Path.GetTempPath();
            }
            var dir = Path.Combine(appData, "CDP.Inspector");
            return Path.Combine(dir, "state.json");
        }
        catch
        {
            return "state.json";
        }
    }
}
