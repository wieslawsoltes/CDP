using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;

namespace Chrome.DevTools.Protocol.Domains;

public class V8ProfileNode
{
    public int Id { get; }
    public string FunctionName { get; }
    public string Url { get; }
    public int LineNumber { get; }
    public int ColumnNumber { get; }
    public int HitCount { get; set; }
    public List<int> Children { get; } = new();

    public V8ProfileNode(int id, string functionName, string url, int lineNumber, int columnNumber)
    {
        Id = id;
        FunctionName = functionName;
        Url = url;
        LineNumber = lineNumber;
        ColumnNumber = columnNumber;
    }

    public JsonObject ToJson()
    {
        var json = new JsonObject
        {
            ["id"] = Id,
            ["callFrame"] = new JsonObject
            {
                ["functionName"] = FunctionName,
                ["scriptId"] = "0",
                ["url"] = Url,
                ["lineNumber"] = LineNumber,
                ["columnNumber"] = ColumnNumber
            },
            ["hitCount"] = HitCount
        };

        if (Children.Count > 0)
        {
            var childrenArray = new JsonArray();
            foreach (var childId in Children)
            {
                childrenArray.Add(childId);
            }
            json["children"] = childrenArray;
        }

        return json;
    }
}

public static class ProfileConverter
{
    public static JsonObject ConvertFirefoxToV8(JsonObject firefoxProfileJson)
    {
        var threads = firefoxProfileJson["threads"]?.AsArray();
        var mainThread = threads?[0]?.AsObject();
        if (mainThread == null) return CreateEmptyProfile();

        var samplesTable = mainThread["samples"]?.AsObject();
        var stacksTable = mainThread["stacks"]?.AsObject();
        var framesTable = mainThread["frames"]?.AsObject();
        var funcTable = mainThread["funcTable"]?.AsObject();
        var stringTable = mainThread["stringTable"]?.AsArray();

        if (samplesTable == null || stacksTable == null || framesTable == null || funcTable == null || stringTable == null)
        {
            return CreateEmptyProfile();
        }

        var sampleStackIndices = samplesTable["stack"]?.AsArray();
        var sampleTimes = samplesTable["time"]?.AsArray();
        var stackPrefixes = stacksTable["prefix"]?.AsArray();
        var stackFrames = stacksTable["frame"]?.AsArray();
        var frameFuncs = framesTable["func"]?.AsArray();
        var funcNames = funcTable["name"]?.AsArray();
        var funcFiles = funcTable["fileName"]?.AsArray();

        if (sampleStackIndices == null || sampleTimes == null || stackPrefixes == null || stackFrames == null || frameFuncs == null || funcNames == null || funcFiles == null)
        {
            return CreateEmptyProfile();
        }

        var nodes = new List<V8ProfileNode>();
        var v8Samples = new List<int>();
        var v8TimeDeltas = new List<int>();

        // Root Node (ID = 1)
        var rootNode = new V8ProfileNode(1, "(root)", "", -1, -1);
        nodes.Add(rootNode);
        int nextNodeId = 2;

        var stackToNodeIdMap = new Dictionary<int, int>();

        double lastTimeMs = 0;
        for (int i = 0; i < sampleStackIndices.Count; i++)
        {
            int stackIndex = sampleStackIndices[i]?.GetValue<int>() ?? -1;
            double timeMs = sampleTimes[i]?.GetValue<double>() ?? 0;

            int deltaUs = (i == 0) ? 0 : (int)Math.Max(0, (timeMs - lastTimeMs) * 1000.0);
            v8TimeDeltas.Add(deltaUs);
            lastTimeMs = timeMs;

            if (stackIndex == -1)
            {
                // Idle sample
                v8Samples.Add(1);
                rootNode.HitCount++;
                continue;
            }

            int leafNodeId = ResolveV8Node(
                stackIndex,
                stackPrefixes,
                stackFrames,
                frameFuncs,
                funcNames,
                funcFiles,
                stringTable,
                nodes,
                ref nextNodeId,
                stackToNodeIdMap
            );

            v8Samples.Add(leafNodeId);
            var leafNode = nodes.First(n => n.Id == leafNodeId);
            leafNode.HitCount++;
        }

        var nodesArray = new JsonArray();
        foreach (var node in nodes)
        {
            nodesArray.Add(node.ToJson());
        }

        double startTimeUs = (sampleTimes.Count > 0) ? (sampleTimes[0]?.GetValue<double>() ?? 0) * 1000.0 : 0;
        double endTimeUs = (sampleTimes.Count > 0) ? (sampleTimes[sampleTimes.Count - 1]?.GetValue<double>() ?? 0) * 1000.0 : 0;

        return new JsonObject
        {
            ["nodes"] = nodesArray,
            ["startTime"] = startTimeUs,
            ["endTime"] = endTimeUs,
            ["samples"] = new JsonArray(v8Samples.Select(s => (JsonNode)s).ToArray()),
            ["timeDeltas"] = new JsonArray(v8TimeDeltas.Select(d => (JsonNode)d).ToArray())
        };
    }

    private static int ResolveV8Node(
        int stackIndex,
        JsonArray prefixes,
        JsonArray frames,
        JsonArray frameFuncs,
        JsonArray funcNames,
        JsonArray funcFiles,
        JsonArray stringTable,
        List<V8ProfileNode> nodes,
        ref int nextNodeId,
        Dictionary<int, int> cache)
    {
        if (cache.TryGetValue(stackIndex, out int existingId))
        {
            return existingId;
        }

        int prefixIndex = prefixes[stackIndex]?.GetValue<int>() ?? -1;
        int parentNodeId = 1; // root
        if (prefixIndex != -1)
        {
            parentNodeId = ResolveV8Node(
                prefixIndex,
                prefixes,
                frames,
                frameFuncs,
                funcNames,
                funcFiles,
                stringTable,
                nodes,
                ref nextNodeId,
                cache
            );
        }

        int frameIndex = frames[stackIndex]?.GetValue<int>() ?? -1;
        int funcIndex = (frameIndex != -1 && frameIndex < frameFuncs.Count) ? (frameFuncs[frameIndex]?.GetValue<int>() ?? -1) : -1;
        
        string funcName = "Unknown";
        if (funcIndex != -1 && funcIndex < funcNames.Count)
        {
            int nameStrIdx = funcNames[funcIndex]?.GetValue<int>() ?? -1;
            if (nameStrIdx != -1 && nameStrIdx < stringTable.Count)
            {
                funcName = stringTable[nameStrIdx]?.GetValue<string>() ?? "Unknown";
            }
        }

        string fileUrl = "";
        if (funcIndex != -1 && funcIndex < funcFiles.Count)
        {
            int fileStrIdx = funcFiles[funcIndex]?.GetValue<int>() ?? -1;
            if (fileStrIdx != -1 && fileStrIdx < stringTable.Count)
            {
                fileUrl = stringTable[fileStrIdx]?.GetValue<string>() ?? "";
            }
        }

        var parentNode = nodes.First(n => n.Id == parentNodeId);
        
        // Check if a child node with the same function name and URL already exists in parent's children
        int existingChildId = -1;
        foreach (var childId in parentNode.Children)
        {
            var childNode = nodes.First(n => n.Id == childId);
            if (childNode.FunctionName == funcName && childNode.Url == fileUrl)
            {
                existingChildId = childId;
                break;
            }
        }

        int nodeId;
        if (existingChildId != -1)
        {
            nodeId = existingChildId;
        }
        else
        {
            nodeId = nextNodeId++;
            var newNode = new V8ProfileNode(nodeId, funcName, fileUrl, 0, 0);
            newNode.HitCount = 0; // set initial to 0, incremented in main loop if it's the leaf
            nodes.Add(newNode);
            parentNode.Children.Add(nodeId);
        }

        cache[stackIndex] = nodeId;
        return nodeId;
    }

    private static JsonObject CreateEmptyProfile()
    {
        var rootNode = new V8ProfileNode(1, "(root)", "", -1, -1);
        rootNode.HitCount = 0;

        return new JsonObject
        {
            ["nodes"] = new JsonArray { rootNode.ToJson() },
            ["startTime"] = 0,
            ["endTime"] = 0,
            ["samples"] = new JsonArray(),
            ["timeDeltas"] = new JsonArray()
        };
    }
}
