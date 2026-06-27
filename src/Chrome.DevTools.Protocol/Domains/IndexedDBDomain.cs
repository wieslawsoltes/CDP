using System;
using System.Collections.Concurrent;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace Chrome.DevTools.Protocol.Domains;

public static class IndexedDBDomain
{
    private static readonly ConcurrentDictionary<string, ConcurrentDictionary<string, ConcurrentDictionary<string, string>>> _databases = new();

    static IndexedDBDomain()
    {
        ResetMockData();
    }

    private static void ResetMockData()
    {
        _databases.Clear();
        var db = _databases.GetOrAdd("AppLocalCache", _ => new ConcurrentDictionary<string, ConcurrentDictionary<string, string>>());
        
        var preferences = db.GetOrAdd("Preferences", _ => new ConcurrentDictionary<string, string>());
        preferences["Theme"] = "Dark";
        preferences["FontSize"] = "14px";
        preferences["Language"] = "en-US";

        var offlineSync = db.GetOrAdd("OfflineSync", _ => new ConcurrentDictionary<string, string>());
        offlineSync["SyncJob1"] = "Pending";
        offlineSync["SyncJob2"] = "Completed";
    }

    public static Task<JsonObject> HandleAsync(CdpSession session, string action, JsonObject @params)
    {
        switch (action)
        {
            case "enable":
            case "disable":
                return Task.FromResult(new JsonObject());

            case "requestDatabaseNames":
                {
                    var names = new JsonArray();
                    foreach (var dbName in _databases.Keys)
                    {
                        names.Add(dbName);
                    }
                    return Task.FromResult(new JsonObject
                    {
                        ["databaseNames"] = names
                    });
                }

            case "requestDatabase":
                {
                    string dbName = @params["databaseName"]?.GetValue<string>() ?? "";
                    if (_databases.TryGetValue(dbName, out var db))
                    {
                        var objectStores = new JsonArray();
                        foreach (var storePair in db)
                        {
                            var storeName = storePair.Key;
                            var keyPath = new JsonObject
                            {
                                ["type"] = "string",
                                ["string"] = "key"
                            };

                            var storeObj = new JsonObject
                            {
                                ["name"] = storeName,
                                ["keyPath"] = keyPath,
                                ["autoIncrement"] = false,
                                ["indexes"] = new JsonArray()
                            };
                            objectStores.Add(storeObj);
                        }

                        var databaseWithObjectStores = new JsonObject
                        {
                            ["name"] = dbName,
                            ["version"] = 1.0,
                            ["objectStores"] = objectStores
                        };

                        return Task.FromResult(new JsonObject
                        {
                            ["databaseWithObjectStores"] = databaseWithObjectStores
                        });
                    }
                    else
                    {
                        return Task.FromResult(new JsonObject());
                    }
                }

            case "requestData":
                {
                    string dbName = @params["databaseName"]?.GetValue<string>() ?? "";
                    string storeName = @params["objectStoreName"]?.GetValue<string>() ?? "";

                    var entries = new JsonArray();
                    if (_databases.TryGetValue(dbName, out var db) && db.TryGetValue(storeName, out var store))
                    {
                        foreach (var pair in store)
                        {
                            var entry = new JsonObject
                            {
                                ["key"] = new JsonObject
                                {
                                    ["type"] = "string",
                                    ["value"] = pair.Key
                                },
                                ["primaryKey"] = new JsonObject
                                {
                                    ["type"] = "string",
                                    ["value"] = pair.Key
                                },
                                ["value"] = new JsonObject
                                {
                                    ["type"] = "string",
                                    ["value"] = pair.Value
                                }
                            };
                            entries.Add(entry);
                        }
                    }

                    return Task.FromResult(new JsonObject
                    {
                        ["objectStoreDataEntries"] = entries,
                        ["hasMore"] = false
                    });
                }

            case "clearObjectStore":
                {
                    string dbName = @params["databaseName"]?.GetValue<string>() ?? "";
                    string storeName = @params["objectStoreName"]?.GetValue<string>() ?? "";

                    if (_databases.TryGetValue(dbName, out var db) && db.TryGetValue(storeName, out var store))
                    {
                        store.Clear();
                    }
                    return Task.FromResult(new JsonObject());
                }

            case "deleteDatabase":
                {
                    string dbName = @params["databaseName"]?.GetValue<string>() ?? "";
                    _databases.TryRemove(dbName, out _);
                    return Task.FromResult(new JsonObject());
                }

            default:
                throw new Exception($"Method IndexedDB.{action} is not implemented");
        }
    }
}
