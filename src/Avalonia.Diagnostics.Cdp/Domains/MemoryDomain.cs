using System;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Avalonia.Threading;
using Avalonia.VisualTree;
using System.Linq;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

namespace Avalonia.Diagnostics.Cdp.Domains;

public static class MemoryDomain
{
    public static void Initialize()
    {
        Avalonia.Controls.Control.LoadedEvent.AddClassHandler<Avalonia.Controls.Control>((element, args) =>
        {
            ControlTracker.Register(element);
        });
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
                    int nodes = await Dispatcher.UIThread.InvokeAsync(() => CountVisuals(session.Window));
                    int jsEventListeners = 0;

                    return new JsonObject
                    {
                        ["documents"] = documents,
                        ["nodes"] = nodes,
                        ["jsEventListeners"] = jsEventListeners
                    };
                }

            case "getLiveControls":
                return await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    var counts = new Dictionary<string, int>();
                    foreach (var win in CdpServer.GetWindows())
                    {
                        CountControlTypes(win.Window, counts);
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

            case "getDetachedControls":
                {
                    var detachedList = await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        var list = new JsonArray();
                        var detachedControls = ControlTracker.GetDetachedControls().ToList();
                        
                        // Reflection Diagnostics
                        try
                        {
                            foreach (var session in CdpServer.Sessions)
                            {
                                var propHandlersField = typeof(CdpSession).GetField("_propertyHandlers", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                                var colHandlersField = typeof(CdpSession).GetField("_collectionHandlers", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                                var classesHandlersField = typeof(CdpSession).GetField("_classesHandlers", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

                                var propHandlers = propHandlersField?.GetValue(session) as System.Collections.IDictionary;
                                var colHandlers = colHandlersField?.GetValue(session) as System.Collections.IDictionary;
                                var classesHandlers = classesHandlersField?.GetValue(session) as System.Collections.IDictionary;

                                Console.WriteLine($"[CDP DIAGNOSTIC] Session NodeMap size: {(session.NodeMap.GetVisual(0) == null ? "N/A" : "OK")}, PropHandlers: {propHandlers?.Count ?? 0}, ColHandlers: {colHandlers?.Count ?? 0}, ClassesHandlers: {classesHandlers?.Count ?? 0}");

                                if (propHandlers != null)
                                {
                                    foreach (var key in propHandlers.Keys)
                                    {
                                        if (key is Avalonia.Controls.Control ctrl && ctrl.Name == "leakButton")
                                        {
                                            Console.WriteLine("[CDP LEAK] leakButton is still in _propertyHandlers!");
                                        }
                                    }
                                }
                                if (colHandlers != null)
                                {
                                    foreach (var key in colHandlers.Keys)
                                    {
                                        if (key is Avalonia.Controls.Control ctrl && ctrl.Name == "leakButton")
                                        {
                                            Console.WriteLine("[CDP LEAK] leakButton is still in _collectionHandlers!");
                                        }
                                    }
                                }
                                if (classesHandlers != null)
                                {
                                    foreach (var key in classesHandlers.Keys)
                                    {
                                        if (key is Avalonia.Controls.Control ctrl && ctrl.Name == "leakButton")
                                        {
                                            Console.WriteLine("[CDP LEAK] leakButton is still in _classesHandlers!");
                                        }
                                    }
                                }
                                // Check if in NodeMap
                                for (int i = 0; i < 10000; i++)
                                {
                                    var v = session.NodeMap.GetVisual(i);
                                    if (v is Avalonia.Controls.Control ctrl && ctrl.Name == "leakButton")
                                    {
                                        Console.WriteLine($"[CDP LEAK] leakButton is still in NodeMap with ID {i}!");
                                    }
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"[CDP DIAGNOSTIC ERROR] {ex}");
                        }

                        foreach (var info in detachedControls)
                        {
                            var visual = info.Visual;
                            bool hasDc = (visual as Avalonia.StyledElement)?.DataContext != null;
                            string dcType = (visual as Avalonia.StyledElement)?.DataContext?.GetType().FullName ?? "";

                            list.Add(new JsonObject
                            {
                                ["id"] = "0x" + visual.GetHashCode().ToString("X8"),
                                ["type"] = visual.GetType().FullName ?? visual.GetType().Name,
                                ["name"] = (visual as Avalonia.StyledElement)?.Name ?? "",
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
                    var snapshotJsonObj = await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        var visitedVisuals = new HashSet<Visual>();
                        var orderedVisuals = new List<Visual>();

                        void Traverse(Visual visual)
                        {
                            if (visual == null || !visitedVisuals.Add(visual)) return;
                            orderedVisuals.Add(visual);
                            foreach (var child in visual.GetVisualChildren())
                            {
                                Traverse(child);
                            }
                        }

                        foreach (var win in CdpServer.GetWindows())
                        {
                            Traverse(win.Window);
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
                            string controlName = (visual as Avalonia.StyledElement)?.Name ?? "";
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

                            var dc = (visual as Avalonia.StyledElement)?.DataContext;
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

                            var dc = (visual as Avalonia.StyledElement)?.DataContext;
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
                            foreach (var child in visual.GetVisualChildren())
                            {
                                if (child != null && nodesMap.TryGetValue(child, out var childNode))
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

            case "collectGarbage":
            case "forciblyPurgeJavaScriptMemory":
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
                return new JsonObject();

            case "setPressureNotificationsSuppressed":
            case "simulatePressureNotification":
            case "prepareForLeakDetection":
                {
                    return new JsonObject();
                }

            default:
                throw new Exception($"Method Memory.{action} is not implemented");
        }
    }

    private static void CountControlTypes(Visual visual, Dictionary<string, int> counts)
    {
        string typeName = visual.GetType().Name;
        counts[typeName] = counts.TryGetValue(typeName, out int c) ? c + 1 : 1;
        
        foreach (var child in visual.GetVisualChildren())
        {
            CountControlTypes(child, counts);
        }
    }

    private static int CountVisuals(Avalonia.Visual visual)
    {
        int count = 1;
        foreach (var child in visual.GetVisualChildren())
        {
            count += CountVisuals(child);
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
    public Visual Visual { get; }
    public TimeSpan Duration { get; }

    public DetachedControlInfo(Visual visual, TimeSpan duration)
    {
        Visual = visual;
        Duration = duration;
    }
}

public static class ControlTracker
{
    private static readonly List<WeakReference<Visual>> _trackedVisuals = new();
    private static readonly object _lock = new();
    private static readonly ConditionalWeakTable<Visual, DetachInfo> _detachTimes = new();

    private class DetachInfo
    {
        public DateTime DetachTime { get; set; } = DateTime.UtcNow;
    }

    public static void Register(Visual visual)
    {
        if (visual == null) return;
        lock (_lock)
        {
            // Avoid duplicate registrations
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
                _trackedVisuals.Add(new WeakReference<Visual>(visual));
            }
        }
    }

    public static IEnumerable<DetachedControlInfo> GetDetachedControls()
    {
        var list = new List<DetachedControlInfo>();
        var now = DateTime.UtcNow;

        lock (_lock)
        {
            // Prune dead references
            _trackedVisuals.RemoveAll(w => !w.TryGetTarget(out var dummy));

            foreach (var weakRef in _trackedVisuals)
            {
                if (weakRef.TryGetTarget(out var visual))
                {
                    if (!visual.IsAttachedToVisualTree())
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

    public static void Clear()
    {
        lock (_lock)
        {
            _trackedVisuals.Clear();
        }
    }
}
