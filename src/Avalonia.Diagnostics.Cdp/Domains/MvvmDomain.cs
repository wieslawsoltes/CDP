using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace Avalonia.Diagnostics.Cdp.Domains;

public class ViewModelNode
{
    public string Id { get; set; } = "";
    public string Type { get; set; } = "";
    public string ControlType { get; set; } = "";
    public string ControlName { get; set; } = "";
    public List<JsonObject> Properties { get; set; } = new();
    public List<ViewModelNode> Children { get; set; } = new();

    public JsonObject ToJson()
    {
        var propsArray = new JsonArray();
        foreach (var p in Properties) propsArray.Add(p);

        var childrenArray = new JsonArray();
        foreach (var c in Children) childrenArray.Add(c.ToJson());

        return new JsonObject
        {
            ["id"] = Id,
            ["type"] = Type,
            ["controlType"] = ControlType,
            ["controlName"] = ControlName,
            ["properties"] = propsArray,
            ["children"] = childrenArray
        };
    }
}

public static class ObservableSubscriptionHelper
{
    public static IDisposable? SubscribeDynamic(object observable, Action<object?> callback)
    {
        var type = observable.GetType();
        foreach (var iface in type.GetInterfaces())
        {
            if (iface.IsGenericType && iface.GetGenericTypeDefinition() == typeof(IObservable<>))
            {
                var itemType = iface.GetGenericArguments()[0];
                var helperType = typeof(SubscriptionHelper<>).MakeGenericType(itemType);
                var helper = Activator.CreateInstance(helperType, callback);
                var subscribeMethod = iface.GetMethod("Subscribe", new[] { typeof(IObserver<>).MakeGenericType(itemType) });
                if (subscribeMethod != null)
                {
                    return subscribeMethod.Invoke(observable, new[] { helper }) as IDisposable;
                }
            }
        }
        return null;
    }
}

public class SubscriptionHelper<T> : IObserver<T>
{
    private readonly Action<object?> _callback;
    public SubscriptionHelper(Action<object?> callback) => _callback = callback;
    public void OnCompleted() {}
    public void OnError(Exception error) {}
    public void OnNext(T value) => _callback(value);
}

public class SessionMvvmState : IDisposable
{
    private readonly CdpSession _session;
    private readonly ConditionalWeakTable<object, string> _vmToId = new();
    private readonly ConcurrentDictionary<string, WeakReference<object>> _idToVm = new();
    private readonly ConcurrentDictionary<object, PropertyChangedEventHandler> _subscribedVms = new();
    private readonly ConcurrentDictionary<object, IDisposable> _commandSubscriptions = new();

    public SessionMvvmState(CdpSession session)
    {
        _session = session;
    }

    public string GetOrCreateId(object vm)
    {
        if (!_vmToId.TryGetValue(vm, out var id))
        {
            id = "vm-" + Guid.NewGuid().ToString();
            _vmToId.Add(vm, id);
            _idToVm[id] = new WeakReference<object>(vm);
        }
        return id;
    }

    public object? GetViewModelById(string id)
    {
        if (_idToVm.TryGetValue(id, out var weakRef) && weakRef.TryGetTarget(out var target))
        {
            return target;
        }
        return null;
    }

    public void TrySubscribeToCommand(object command, string vmId, string vmType, string cmdName, IMvvmFrameworkProvider provider)
    {
        if (!_commandSubscriptions.ContainsKey(command))
        {
            var sub = provider.SubscribeToCommand(command, result =>
            {
                _ = _session.SendEventAsync("Mvvm.commandExecuted", new JsonObject
                {
                    ["viewModelId"] = vmId,
                    ["viewModelType"] = vmType,
                    ["commandName"] = cmdName,
                    ["result"] = MvvmDomain.SerializeValue(result),
                    ["timestamp"] = DateTime.UtcNow.ToString("o")
                });
            });

            if (sub != null)
            {
                _commandSubscriptions.TryAdd(command, sub);
            }
        }
    }

    public void UpdateSubscriptions(HashSet<object> activeVms)
    {
        // Unsubscribe from VMs no longer active
        var toRemove = new List<object>();
        foreach (var vm in _subscribedVms.Keys)
        {
            if (!activeVms.Contains(vm))
            {
                toRemove.Add(vm);
            }
        }

        foreach (var vm in toRemove)
        {
            if (_subscribedVms.TryRemove(vm, out var handler) && vm is INotifyPropertyChanged inpc)
            {
                inpc.PropertyChanged -= handler;
            }
        }

        // Subscribe to new active VMs
        foreach (var vm in activeVms)
        {
            if (vm is INotifyPropertyChanged inpc && !_subscribedVms.ContainsKey(vm))
            {
                PropertyChangedEventHandler handler = (sender, e) => OnViewModelPropertyChanged(vm, e.PropertyName);
                if (_subscribedVms.TryAdd(vm, handler))
                {
                    inpc.PropertyChanged += handler;
                }
            }
        }
    }

    private void OnViewModelPropertyChanged(object vm, string? propertyName)
    {
        if (string.IsNullOrEmpty(propertyName)) return;

        if (_vmToId.TryGetValue(vm, out var id))
        {
            var property = vm.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
            if (property != null && property.CanRead)
            {
                try
                {
                    var val = property.GetValue(vm);
                    var serializedVal = MvvmDomain.SerializeValue(val);

                    _ = _session.SendEventAsync("Mvvm.propertyChanged", new JsonObject
                    {
                        ["viewModelId"] = id,
                        ["propertyName"] = propertyName,
                        ["value"] = serializedVal
                    });
                }
                catch
                {
                    // Ignore
                }
            }
        }
    }

    public void Dispose()
    {
        foreach (var kvp in _subscribedVms)
        {
            if (kvp.Key is INotifyPropertyChanged inpc)
            {
                inpc.PropertyChanged -= kvp.Value;
            }
        }
        _subscribedVms.Clear();
        _idToVm.Clear();

        foreach (var sub in _commandSubscriptions.Values)
        {
            sub.Dispose();
        }
        _commandSubscriptions.Clear();
    }
}

public static class MvvmDomain
{
    public static List<IMvvmFrameworkProvider> Providers { get; } = new()
    {
        new ReactiveUiFrameworkProvider(),
        new CommunityToolkitMvvmFrameworkProvider(),
        new VanillaMvvmFrameworkProvider()
    };

    private static readonly ConcurrentDictionary<CdpSession, SessionMvvmState> _sessionStates = new();

    public static IMvvmFrameworkProvider? GetProvider(object dataContext)
    {
        if (dataContext == null) return null;
        foreach (var p in Providers)
        {
            if (p.IsViewModel(dataContext)) return p;
        }
        return null;
    }

    public static void CleanupSession(CdpSession session)
    {
        if (_sessionStates.TryRemove(session, out var state))
        {
            state.Dispose();
        }
    }

    public static async Task<JsonObject> HandleAsync(CdpSession session, string action, JsonObject @params)
    {
        switch (action)
        {
            case "enable":
                {
                    _ = GetOrCreateState(session);
                    return new JsonObject();
                }

            case "disable":
                {
                    CleanupSession(session);
                    return new JsonObject();
                }

            case "getViewModelTree":
                {
                    var state = GetOrCreateState(session);
                    var window = session.Window;
                    if (window == null)
                    {
                        return new JsonObject { ["tree"] = new JsonArray() };
                    }

                    var result = await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        var rootNodes = new List<ViewModelNode>();
                        var activeVms = new HashSet<object>();
                        WalkVisualTree(window, null, rootNodes, state, activeVms);
                        
                        state.UpdateSubscriptions(activeVms);

                        var treeArray = new JsonArray();
                        foreach (var node in rootNodes)
                        {
                            treeArray.Add(node.ToJson());
                        }
                        return treeArray;
                    });

                    return new JsonObject
                    {
                        ["tree"] = result
                    };
                }

            case "setPropertyValue":
                {
                    var state = GetOrCreateState(session);
                    string vmId = @params["viewModelId"]?.GetValue<string>() ?? "";
                    string propertyName = @params["propertyName"]?.GetValue<string>() ?? "";
                    JsonNode? valueNode = @params["value"];

                    var vm = state.GetViewModelById(vmId);
                    if (vm == null)
                    {
                        throw new Exception($"ViewModel with ID '{vmId}' not found or has been garbage collected");
                    }

                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        var property = vm.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
                        if (property == null || !property.CanWrite)
                        {
                            throw new Exception($"Property '{propertyName}' not found or not writable on '{vm.GetType().Name}'");
                        }

                        object? convertedVal = ConvertJsonNodeToType(valueNode, property.PropertyType);
                        property.SetValue(vm, convertedVal);
                    });

                    return new JsonObject();
                }

            default:
                throw new Exception($"Method Mvvm.{action} is not implemented");
        }
    }

    private static SessionMvvmState GetOrCreateState(CdpSession session)
    {
        return _sessionStates.GetOrAdd(session, s => new SessionMvvmState(s));
    }

    public static JsonNode? SerializeValue(object? val)
    {
        if (val == null) return null;
        if (val is string s) return JsonValue.Create(s);
        if (val is bool b) return JsonValue.Create(b);
        if (val is int i) return JsonValue.Create(i);
        if (val is double d) return JsonValue.Create(d);
        if (val is float f) return JsonValue.Create(f);
        if (val is long l) return JsonValue.Create(l);
        if (val is decimal dec) return JsonValue.Create(dec);
        if (val is char c) return JsonValue.Create(c.ToString());
        if (val is Guid g) return JsonValue.Create(g.ToString());
        if (val is DateTime dt) return JsonValue.Create(dt.ToString("o"));
        if (val is DateTimeOffset dto) return JsonValue.Create(dto.ToString("o"));

        var type = val.GetType();
        if (val is System.Windows.Input.ICommand) return JsonValue.Create($"[Command: {type.Name}]");
        if (val is System.Collections.IEnumerable) return JsonValue.Create($"[Collection: {type.Name}]");

        if (type.IsPrimitive || type.IsEnum)
        {
            return JsonValue.Create(val.ToString());
        }

        return JsonValue.Create(val.ToString() ?? $"[{type.Name}]");
    }

    private static object? ConvertJsonNodeToType(JsonNode? node, Type targetType)
    {
        if (node == null)
        {
            if (targetType.IsValueType && Nullable.GetUnderlyingType(targetType) == null)
            {
                throw new Exception($"Cannot assign null to value type {targetType.Name}");
            }
            return null;
        }

        var underlyingType = Nullable.GetUnderlyingType(targetType) ?? targetType;

        if (underlyingType == typeof(string))
        {
            return node.GetValue<string>();
        }
        if (underlyingType == typeof(bool))
        {
            return node.GetValue<bool>();
        }
        if (underlyingType == typeof(int))
        {
            return node.GetValue<int>();
        }
        if (underlyingType == typeof(double))
        {
            return node.GetValue<double>();
        }
        if (underlyingType == typeof(float))
        {
            return node.GetValue<float>();
        }
        if (underlyingType == typeof(long))
        {
            return node.GetValue<long>();
        }
        if (underlyingType == typeof(decimal))
        {
            return node.GetValue<decimal>();
        }
        if (underlyingType == typeof(Guid))
        {
            return Guid.Parse(node.GetValue<string>());
        }
        if (underlyingType == typeof(DateTime))
        {
            return DateTime.Parse(node.GetValue<string>());
        }
        if (underlyingType == typeof(DateTimeOffset))
        {
            return DateTimeOffset.Parse(node.GetValue<string>());
        }
        if (underlyingType.IsEnum)
        {
            return Enum.Parse(underlyingType, node.GetValue<string>());
        }

        try
        {
            return System.Text.Json.JsonSerializer.Deserialize(node.ToJsonString(), underlyingType);
        }
        catch
        {
            return Convert.ChangeType(node.ToString(), underlyingType);
        }
    }

    private static void WalkVisualTree(Visual visual, object? parentDataContext, List<ViewModelNode> nodes, SessionMvvmState state, HashSet<object> activeVms)
    {
        object? currentDataContext = visual.DataContext;
        List<ViewModelNode> currentLevelNodes = nodes;

        if (currentDataContext != null && currentDataContext != parentDataContext)
        {
            var provider = GetProvider(currentDataContext);
            if (provider != null)
            {
                activeVms.Add(currentDataContext);
                string id = state.GetOrCreateId(currentDataContext);

                var node = new ViewModelNode
                {
                    Id = id,
                    Type = currentDataContext.GetType().FullName ?? currentDataContext.GetType().Name,
                    ControlType = visual.GetType().FullName ?? visual.GetType().Name,
                    ControlName = (visual as Avalonia.Controls.Control)?.Name ?? ""
                };

                var properties = provider.GetProperties(currentDataContext);
                foreach (var prop in properties)
                {
                    try
                    {
                        if (prop.GetIndexParameters().Length > 0) continue;

                        var val = prop.GetValue(currentDataContext);
                        node.Properties.Add(new JsonObject
                        {
                            ["name"] = prop.Name,
                            ["type"] = prop.PropertyType.FullName ?? prop.PropertyType.Name,
                            ["value"] = SerializeValue(val),
                            ["isWritable"] = prop.CanWrite
                        });

                        if (val != null && provider.IsCommand(val))
                        {
                            state.TrySubscribeToCommand(val, id, currentDataContext.GetType().Name, prop.Name, provider);
                        }
                    }
                    catch
                    {
                        // Ignore
                    }
                }

                nodes.Add(node);
                currentLevelNodes = node.Children;
                parentDataContext = currentDataContext;
            }
            else
            {
                parentDataContext = currentDataContext;
            }
        }

        foreach (var child in visual.GetVisualChildren())
        {
            WalkVisualTree(child, parentDataContext, currentLevelNodes, state, activeVms);
        }
    }
}
