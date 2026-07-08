using System;
using System.Reflection;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using System.Linq;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

namespace WinUI.Diagnostics.Cdp.Domains;

public static class MemoryDomain
{
    public static void Initialize()
    {
    }

    public static void Shutdown()
    {
        ControlTracker.Clear();
    }

    public static async Task<JsonObject> HandleAsync(CdpSession session, string action, JsonObject @params)
    {
        switch (action)
        {
            case "getDOMCounters":
                {
                    int documents = CdpServer.GetActiveTargets().Count;
                    int nodes = 0;
                    if (session.Window?.Content != null)
                    {
                        nodes = await session.Window.DispatcherQueue.InvokeAsync(() => CountVisuals(session.Window.Content));
                    }
                    int jsEventListeners = 0;

                    return new JsonObject
                    {
                        ["documents"] = documents,
                        ["nodes"] = nodes,
                        ["jsEventListeners"] = jsEventListeners
                    };
                }

            case "getLiveControls":
                {
                    if (session.Window?.Content == null) return new JsonObject { ["controls"] = new JsonArray() };
                    return await session.Window.DispatcherQueue.InvokeAsync(() =>
                    {
                        var counts = new Dictionary<string, int>();
                        foreach (var win in CdpServer.GetWindows())
                        {
                            if (win.Window.Content != null)
                            {
                                CountControlTypes(win.Window.Content, counts);
                            }
                        }
                        
                        var array = new JsonArray();
                        foreach (var pair in counts)
                        {
                            array.Add(new JsonObject
                            {
                                ["type"] = pair.Key,
                                ["count"] = pair.Value
                            });
                        }
                        return new JsonObject { ["controls"] = array };
                    });
                }

            case "getDetachedControls":
                {
                    if (session.Window == null) return new JsonObject { ["detachedControls"] = new JsonArray() };
                    var detachedList = await session.Window.DispatcherQueue.InvokeAsync(() =>
                    {
                        var list = new JsonArray();
                        var detachedControls = ControlTracker.GetDetachedControls().ToList();
                        
                        foreach (var info in detachedControls)
                        {
                            var visual = info.Visual;
                            bool hasDc = (visual is FrameworkElement fe) && fe.DataContext != null;
                            string dcType = (visual is FrameworkElement fe2) && fe2.DataContext != null ? fe2.DataContext.GetType().FullName ?? "" : "";

                            list.Add(new JsonObject
                            {
                                ["id"] = "0x" + visual.GetHashCode().ToString("X8"),
                                ["type"] = visual.GetType().FullName ?? visual.GetType().Name,
                                ["name"] = (visual is FrameworkElement fe3) ? fe3.Name : "",
                                ["hashCode"] = visual.GetHashCode(),
                                ["detachedDurationMs"] = (long)info.Duration.TotalMilliseconds,
                                ["hasDataContext"] = hasDc,
                                ["dataContextType"] = dcType
                            });
                        }
                        return list;
                    });

                    return new JsonObject { ["detachedControls"] = detachedList };
                }

            case "getHeapInfo":
                {
                    var mc = GC.GetGCMemoryInfo();
                    long totalAllocated = GC.GetTotalAllocatedBytes();
                    long committed = mc.TotalCommittedBytes;
                    long fragmented = mc.FragmentedBytes;
                    int gen0 = GC.CollectionCount(0);
                    int gen1 = GC.CollectionCount(1);
                    int gen2 = GC.CollectionCount(2);

                    return new JsonObject
                    {
                        ["totalAllocatedBytes"] = totalAllocated,
                        ["committedHeapBytes"] = committed,
                        ["fragmentedBytes"] = fragmented,
                        ["gen0Collections"] = gen0,
                        ["gen1Collections"] = gen1,
                        ["gen2Collections"] = gen2
                    };
                }

            case "takeHeapSnapshot":
                {
                    if (session.Window == null) return new JsonObject();
                    var snapshotJsonObj = await session.Window.DispatcherQueue.InvokeAsync(() =>
                    {
                        var visitedVisuals = new HashSet<UIElement>();
                        var orderedVisuals = new List<UIElement>();

                        void Traverse(UIElement visualObj)
                        {
                            if (visualObj == null || !visitedVisuals.Add(visualObj)) return;
                            orderedVisuals.Add(visualObj);
                            
                            int count = VisualTreeHelper.GetChildrenCount(visualObj);
                            for (int i = 0; i < count; i++)
                            {
                                if (VisualTreeHelper.GetChild(visualObj, i) is UIElement child)
                                {
                                    Traverse(child);
                                }
                            }
                        }

                        foreach (var win in CdpServer.GetWindows())
                        {
                            if (win.Window.Content != null)
                            {
                                Traverse(win.Window.Content);
                            }
                        }

                        foreach (var info in ControlTracker.GetDetachedControls())
                        {
                            Traverse(info.Visual);
                        }

                        var nodesMap = new Dictionary<object, HeapNode>();
                        var allNodes = new List<HeapNode>();
                        var stringsList = new List<string>();
                        var stringsMap = new Dictionary<string, int>();

                        int GetStringIndex(string str)
                        {
                            if (str == null) str = "";
                            if (stringsMap.TryGetValue(str, out int idx)) return idx;
                            idx = stringsList.Count;
                            stringsList.Add(str);
                            stringsMap[str] = idx;
                            return idx;
                        }

                        ulong nextNodeId = 1;

                        foreach (var visual in orderedVisuals)
                        {
                            var type = visual.GetType();
                            string controlName = (visual is FrameworkElement fe) ? fe.Name : "";
                            string displayName = string.IsNullOrEmpty(controlName) ? type.Name : $"{type.Name}#{controlName}";

                            var visualNode = new HeapNode
                            {
                                Index = allNodes.Count,
                                TypeIndex = 3, // object
                                NameIndex = GetStringIndex(displayName),
                                Id = nextNodeId++,
                                SelfSize = 96,
                            };
                            nodesMap[visual] = visualNode;
                            allNodes.Add(visualNode);

                            var dc = (visual is FrameworkElement fe2) ? fe2.DataContext : null;
                            if (dc != null && !nodesMap.ContainsKey(dc))
                            {
                                var dcNode = new HeapNode
                                {
                                    Index = allNodes.Count,
                                    TypeIndex = 3, // object
                                    NameIndex = GetStringIndex(dc.GetType().FullName ?? dc.GetType().Name),
                                    Id = nextNodeId++,
                                    SelfSize = 64,
                                };
                                nodesMap[dc] = dcNode;
                                allNodes.Add(dcNode);
                            }
                        }

                        foreach (var visual in orderedVisuals)
                        {
                            var visualNode = nodesMap[visual];

                            var dc = (visual is FrameworkElement fe2) ? fe2.DataContext : null;
                            if (dc != null && nodesMap.TryGetValue(dc, out var dcNode))
                            {
                                visualNode.Edges.Add(new HeapEdge
                                {
                                    TypeIndex = 2, // property
                                    NameOrIndex = GetStringIndex("DataContext"),
                                    ToNode = dcNode
                                });
                            }

                            int childIndex = 0;
                            int count = VisualTreeHelper.GetChildrenCount(visual);
                            for (int i = 0; i < count; i++)
                            {
                                if (VisualTreeHelper.GetChild(visual, i) is UIElement child && nodesMap.TryGetValue(child, out var childNode))
                                {
                                    visualNode.Edges.Add(new HeapEdge
                                    {
                                        TypeIndex = 1, // element
                                        NameOrIndex = childIndex++,
                                        ToNode = childNode
                                    });
                                }
                            }
                        }

                        var nodesArray = new JsonArray();
                        var edgesArray = new JsonArray();

                        int totalEdgeCount = 0;
                        foreach (var node in allNodes)
                        {
                            nodesArray.Add(node.TypeIndex);
                            nodesArray.Add(node.NameIndex);
                            nodesArray.Add((long)node.Id);
                            nodesArray.Add(node.SelfSize);
                            nodesArray.Add(node.Edges.Count);
                            nodesArray.Add(0); // trace_node_id

                            totalEdgeCount += node.Edges.Count;

                            foreach (var edge in node.Edges)
                            {
                                edgesArray.Add(edge.TypeIndex);
                                edgesArray.Add(edge.NameOrIndex);
                                edgesArray.Add(edge.ToNode.Index * 6);
                            }
                        }

                        var stringsArray = new JsonArray();
                        foreach (var s in stringsList)
                        {
                            stringsArray.Add(s);
                        }

                        var snapshotMeta = new JsonObject
                        {
                            ["meta"] = new JsonObject
                            {
                                ["node_fields"] = new JsonArray { "type", "name", "id", "self_size", "edge_count", "trace_node_id" },
                                ["node_types"] = new JsonArray
                                {
                                    new JsonArray { "hidden", "array", "string", "object", "code", "closure", "regexp", "number", "native", "synthetic", "concatenated string", "sliced string" },
                                    "string",
                                    "number",
                                    "number",
                                    "number",
                                    "number"
                                },
                                ["edge_fields"] = new JsonArray { "type", "name_or_index", "to_node" },
                                ["edge_types"] = new JsonArray
                                {
                                    new JsonArray { "context", "element", "property", "internal", "hidden", "shortcut" },
                                    "string_or_number",
                                    "number"
                                }
                            },
                            ["node_count"] = allNodes.Count,
                            ["edge_count"] = totalEdgeCount
                        };

                        return new JsonObject
                        {
                            ["snapshot"] = snapshotMeta,
                            ["nodes"] = nodesArray,
                            ["edges"] = edgesArray,
                            ["strings"] = stringsArray
                        };
                    });

                    return snapshotJsonObj;
                }

            case "getRetainers":
                {
                    if (session.Window == null) return new JsonObject();
                    int hashCode = @params["hashCode"]?.GetValue<int>() ?? 0;
                    var tree = await session.Window.DispatcherQueue.InvokeAsync(() =>
                    {
                        var target = ControlTracker.GetControlByHashCode(hashCode);
                        if (target == null)
                        {
                            return new JsonObject { ["error"] = "Control not found or already garbage collected" };
                        }
                        return ReferenceCrawler.GetRetainerTree(target);
                    });
                    return tree;
                }

            case "collectGarbage":
            case "forciblyPurgeJavaScriptMemory":
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
                return new JsonObject();

            case "setPressureNotificationsSuppressed":
            case "simulatePressureNotification":
            case "prepareForLeakDetection":
                return new JsonObject();

            default:
                throw new Exception($"Method Memory.{action} is not implemented");
        }
    }

    private static void CountControlTypes(UIElement visual, Dictionary<string, int> counts)
    {
        string typeName = visual.GetType().Name;
        counts[typeName] = counts.TryGetValue(typeName, out int c) ? c + 1 : 1;
        
        int count = VisualTreeHelper.GetChildrenCount(visual);
        for (int i = 0; i < count; i++)
        {
            if (VisualTreeHelper.GetChild(visual, i) is UIElement child)
            {
                CountControlTypes(child, counts);
            }
        }
    }

    private static int CountVisuals(UIElement visual)
    {
        int count = 1;
        int childrenCount = VisualTreeHelper.GetChildrenCount(visual);
        for (int i = 0; i < childrenCount; i++)
        {
            if (VisualTreeHelper.GetChild(visual, i) is UIElement child)
            {
                count += CountVisuals(child);
            }
        }
        return count;
    }

    private class HeapNode
    {
        public int Index { get; set; }
        public int TypeIndex { get; set; }
        public int NameIndex { get; set; }
        public ulong Id { get; set; }
        public int SelfSize { get; set; }
        public List<HeapEdge> Edges { get; } = new();
    }

    private class HeapEdge
    {
        public int TypeIndex { get; set; }
        public int NameOrIndex { get; set; }
        public HeapNode ToNode { get; set; } = null!;
    }
}

public class DetachedControlInfo
{
    public UIElement Visual { get; }
    public TimeSpan Duration { get; }

    public DetachedControlInfo(UIElement visual, TimeSpan duration)
    {
        Visual = visual;
        Duration = duration;
    }
}

public static class ControlTracker
{
    private static readonly List<WeakReference<UIElement>> _trackedVisuals = new();
    private static readonly object _lock = new();
    private static readonly ConditionalWeakTable<UIElement, DetachInfo> _detachTimes = new();

    private class DetachInfo
    {
        public DateTime DetachTime { get; set; } = DateTime.UtcNow;
    }

    public static void Register(UIElement visual)
    {
        if (visual == null) return;
        lock (_lock)
        {
            bool alreadyRegistered = false;
            foreach (var weakRef in _trackedVisuals)
            {
                if (weakRef.TryGetTarget(out var target) && target == visual)
                {
                    alreadyRegistered = true;
                    break;
                }
            }

            if (!alreadyRegistered)
            {
                _trackedVisuals.Add(new WeakReference<UIElement>(visual));
            }
        }
    }

    public static IEnumerable<DetachedControlInfo> GetDetachedControls()
    {
        var list = new List<DetachedControlInfo>();
        var now = DateTime.UtcNow;

        lock (_lock)
        {
            _trackedVisuals.RemoveAll(w => !w.TryGetTarget(out var dummy));

            foreach (var weakRef in _trackedVisuals)
            {
                if (weakRef.TryGetTarget(out var visual))
                {
                    // In WinUI, if visual tree element has no visual parent or is detached from window content, it is detached
                    if (VisualTreeHelper.GetParent(visual) == null && !CdpServer.GetWindows().Any(w => w.Window.Content == visual))
                    {
                        var info = _detachTimes.GetValue(visual, v => new DetachInfo { DetachTime = now });
                        var duration = now - info.DetachTime;
                        list.Add(new DetachedControlInfo(visual, duration));
                    }
                    else
                    {
                        _detachTimes.Remove(visual);
                    }
                }
            }
        }
        return list;
    }

    public static UIElement? GetControlByHashCode(int hashCode)
    {
        lock (_lock)
        {
            foreach (var weakRef in _trackedVisuals)
            {
                if (weakRef.TryGetTarget(out var visual) && visual.GetHashCode() == hashCode)
                {
                    return visual;
                }
            }
        }
        return null;
    }

    public static void Clear()
    {
        lock (_lock)
        {
            _trackedVisuals.Clear();
        }
    }
}

public static class ReferenceCrawler
{
    public class ReferenceEdge
    {
        public object Source { get; }
        public string Description { get; }

        public ReferenceEdge(object source, string description)
        {
            Source = source;
            Description = description;
        }
    }

    public static JsonObject GetRetainerTree(UIElement targetControl)
    {
        var queue = new Queue<object>();
        var visited = new HashSet<object>(ReferenceEqualityComparer.Instance);
        var incomingEdges = new Dictionary<object, List<ReferenceEdge>>(ReferenceEqualityComparer.Instance);
        var roots = new HashSet<object>(ReferenceEqualityComparer.Instance);

        foreach (var win in CdpServer.GetWindows())
        {
            var w = win.Window;
            if (w != null && visited.Add(w))
            {
                roots.Add(w);
                queue.Enqueue(w);
            }
        }

        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            var name = assembly.GetName().Name;
            if (name == null) continue;
            if (name.StartsWith("System.") || name.StartsWith("Microsoft.") || name == "mscorlib" || name == "netstandard")
            {
                continue;
            }

            Type[] types;
            try
            {
                types = assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                types = ex.Types.Where(t => t != null).ToArray()!;
            }
            catch
            {
                continue;
            }

            foreach (var type in types)
            {
                if (type == null) continue;

                FieldInfo[] fields;
                try
                {
                    fields = type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                }
                catch
                {
                    continue;
                }

                foreach (var field in fields)
                {
                    try
                    {
                        var val = field.GetValue(null);
                        if (val != null && !val.GetType().IsValueType)
                        {
                            if (visited.Add(val))
                            {
                                roots.Add(val);
                                queue.Enqueue(val);
                            }
                        }
                    }
                    catch
                    {
                    }
                }
            }
        }

        int visitedCount = 0;
        const int maxObjects = 50000;
        bool targetFound = false;

        while (queue.Count > 0 && visitedCount < maxObjects)
        {
            var parentObj = queue.Dequeue();
            visitedCount++;

            var children = GetChildren(parentObj);
            foreach (var (childObj, desc) in children)
            {
                if (childObj == null) continue;

                if (ReferenceEquals(childObj, targetControl))
                {
                    targetFound = true;
                }

                if (!incomingEdges.TryGetValue(childObj, out var edges))
                {
                    edges = new List<ReferenceEdge>();
                    incomingEdges[childObj] = edges;
                }

                if (!edges.Any(e => ReferenceEquals(e.Source, parentObj)))
                {
                    edges.Add(new ReferenceEdge(parentObj, desc));
                }

                if (visited.Add(childObj))
                {
                    queue.Enqueue(childObj);
                }
            }

            if (targetFound)
            {
                break;
            }
        }

        var pathVisited = new HashSet<object>(ReferenceEqualityComparer.Instance);
        pathVisited.Add(targetControl);
        return BuildRetainerTree(targetControl, "Target Control", incomingEdges, pathVisited, roots, 0);
    }

    private static List<(object Obj, string Description)> GetChildren(object obj)
    {
        var list = new List<(object, string)>();
        if (obj == null) return list;

        var type = obj.GetType();

        if (obj is MemberInfo || obj is Assembly || obj is Module || obj is Type)
        {
            return list;
        }

        if (obj is Delegate del)
        {
            try
            {
                var targets = del.GetInvocationList();
                for (int i = 0; i < targets.Length; i++)
                {
                    var t = targets[i];
                    if (t.Target != null)
                    {
                        list.Add((t.Target, $"Delegate target [{i}] ({t.Method.Name})"));
                    }
                }
            }
            catch { }
            return list;
        }

        if (obj is Array arr)
        {
            for (int i = 0; i < arr.Length; i++)
            {
                try
                {
                    var val = arr.GetValue(i);
                    if (val != null && !val.GetType().IsValueType)
                    {
                        list.Add((val, $"Array index [{i}]"));
                    }
                }
                catch { }
            }
            return list;
        }

        var currentType = type;
        while (currentType != null)
        {
            if (currentType.FullName != null && (currentType.FullName.StartsWith("System.Reflection") || currentType.FullName.StartsWith("System.RuntimeType")))
            {
                break;
            }

            FieldInfo[] fields;
            try
            {
                fields = currentType.GetFields(BindingFlags.Public | 
                                               BindingFlags.NonPublic | 
                                               BindingFlags.Instance | 
                                               BindingFlags.DeclaredOnly);
            }
            catch
            {
                break;
            }

            foreach (var field in fields)
            {
                if (field.FieldType.IsValueType && !HasReferenceFields(field.FieldType))
                {
                    continue;
                }

                try
                {
                    var val = field.GetValue(obj);
                    if (val != null)
                    {
                        list.Add((val, $"Field '{field.Name}'"));
                    }
                }
                catch
                {
                }
            }

            currentType = currentType.BaseType;
        }

        return list;
    }

    private static readonly ConcurrentDictionary<Type, bool> _hasReferenceFieldsCache = new();
    private static bool HasReferenceFields(Type type)
    {
        if (type.IsPrimitive || type.IsEnum) return false;
        return _hasReferenceFieldsCache.GetOrAdd(type, t =>
        {
            var fields = t.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            foreach (var field in fields)
            {
                if (!field.FieldType.IsValueType) return true;
                if (field.FieldType != t && HasReferenceFields(field.FieldType)) return true;
            }
            return false;
        });
    }

    private static JsonObject BuildRetainerTree(
        object currentObj, 
        string edgeDescription, 
        Dictionary<object, List<ReferenceEdge>> incomingEdges, 
        HashSet<object> pathVisited, 
        HashSet<object> roots,
        int depth)
    {
        var type = currentObj.GetType();
        var node = new JsonObject
        {
            ["name"] = edgeDescription,
            ["type"] = type.FullName ?? type.Name,
            ["hashCode"] = currentObj.GetHashCode()
        };

        if (depth >= 10)
        {
            return node;
        }

        if (roots.Contains(currentObj))
        {
            var rootArray = new JsonArray();
            rootArray.Add(new JsonObject
            {
                ["name"] = "GC Root",
                ["type"] = "Root",
                ["hashCode"] = 0
            });
            node["retainers"] = rootArray;
            return node;
        }

        if (incomingEdges.TryGetValue(currentObj, out var edges))
        {
            var retainersArray = new JsonArray();
            foreach (var edge in edges)
            {
                if (pathVisited.Add(edge.Source))
                {
                    retainersArray.Add(BuildRetainerTree(edge.Source, edge.Description, incomingEdges, pathVisited, roots, depth + 1));
                    pathVisited.Remove(edge.Source);
                }
            }
            node["retainers"] = retainersArray;
        }

        return node;
    }
}
