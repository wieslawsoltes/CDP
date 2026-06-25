using System;
using System.Collections.Concurrent;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace Chrome.DevTools.Protocol.Domains;

public static class DOMStorageDomain
{
    private static readonly ConcurrentDictionary<(string origin, bool isLocal, string targetId), ConcurrentDictionary<string, string>> _stores = new();

    private static ConcurrentDictionary<string, string> GetStore(CdpSession session, JsonObject? storageId)
    {
        bool isLocal = storageId?["isLocalStorage"]?.GetValue<bool>() ?? true;
        string origin = storageId?["securityOrigin"]?.GetValue<string>() ?? "default";
        string targetId = isLocal ? "" : (session.CurrentTargetSession?.TargetId ?? "default");
        return _stores.GetOrAdd((origin, isLocal, targetId), _ => new ConcurrentDictionary<string, string>());
    }

    public static Task<JsonObject> HandleAsync(CdpSession session, string action, JsonObject @params)
    {
        var storageId = @params["storageId"] as JsonObject;

        switch (action)
        {
            case "enable":
            case "disable":
                return Task.FromResult(new JsonObject());

            case "clear":
                {
                    var store = GetStore(session, storageId);
                    store.Clear();
                    return Task.FromResult(new JsonObject());
                }

            case "removeDOMStorageItem":
                {
                    string key = @params["key"]?.GetValue<string>() ?? "";
                    if (!string.IsNullOrEmpty(key))
                    {
                        var store = GetStore(session, storageId);
                        store.TryRemove(key, out _);
                    }
                    return Task.FromResult(new JsonObject());
                }

            case "setDOMStorageItem":
                {
                    string key = @params["key"]?.GetValue<string>() ?? "";
                    string value = @params["value"]?.GetValue<string>() ?? "";
                    if (!string.IsNullOrEmpty(key))
                    {
                        var store = GetStore(session, storageId);
                        store[key] = value;
                    }
                    return Task.FromResult(new JsonObject());
                }

            case "getDOMStorageItems":
                {
                    var store = GetStore(session, storageId);
                    var entries = new JsonArray();
                    foreach (var pair in store)
                    {
                        entries.Add(new JsonArray { pair.Key, pair.Value });
                    }
                    return Task.FromResult(new JsonObject
                    {
                        ["entries"] = entries
                    });
                }

            default:
                throw new Exception($"Method DOMStorage.{action} is not implemented");
        }
    }
}
