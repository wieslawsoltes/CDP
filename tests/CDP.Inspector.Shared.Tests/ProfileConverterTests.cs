using System;
using System.Linq;
using System.Text.Json.Nodes;
using Xunit;
using Chrome.DevTools.Protocol.Domains;

namespace Avalonia.Diagnostics.Cdp.Tests;

public class ProfileConverterTests
{
    [Fact]
    public void ConvertFirefoxToV8_CorrectlyMapsCallStackTrieAndSampleTimes()
    {
        // 1. Arrange - Construct mock Firefox Profiler JSON representation
        var stringTable = new JsonArray { "(root)", "AppLoop", "LayoutPass", "Measure", "app://loop", "app://layout" };

        var funcTable = new JsonObject
        {
            ["name"] = new JsonArray { 1, 2, 3 }, // indices into stringTable: AppLoop, LayoutPass, Measure
            ["fileName"] = new JsonArray { 4, 5, 5 } // indices into stringTable: app://loop, app://layout, app://layout
        };

        var framesTable = new JsonObject
        {
            ["func"] = new JsonArray { 0, 1, 2 } // indices into funcTable
        };

        var stacksTable = new JsonObject
        {
            ["prefix"] = new JsonArray { -1, 0, 1 }, // prefix stack indices
            ["frame"] = new JsonArray { 0, 1, 2 }  // frame indices
        };

        var samplesTable = new JsonObject
        {
            ["stack"] = new JsonArray { 0, 1, 2, 1, -1 }, // stack index for each sample
            ["time"] = new JsonArray { 100.0, 101.0, 102.0, 103.0, 104.0 } // time in ms
        };

        var thread = new JsonObject
        {
            ["stringTable"] = stringTable,
            ["funcTable"] = funcTable,
            ["frames"] = framesTable,
            ["stacks"] = stacksTable,
            ["samples"] = samplesTable
        };

        var firefoxProfile = new JsonObject
        {
            ["threads"] = new JsonArray { thread }
        };

        // 2. Act - Perform format conversion
        var v8Profile = ProfileConverter.ConvertFirefoxToV8(firefoxProfile);

        // 3. Assert
        Assert.NotNull(v8Profile);

        // Start & End Timestamps
        double startTime = v8Profile["startTime"]?.GetValue<double>() ?? 0;
        double endTime = v8Profile["endTime"]?.GetValue<double>() ?? 0;
        Assert.Equal(100000.0, startTime);
        Assert.Equal(104000.0, endTime);

        // Time deltas (in microseconds)
        var deltas = v8Profile["timeDeltas"]?.AsArray();
        Assert.NotNull(deltas);
        Assert.Equal(5, deltas.Count);
        Assert.Equal(0, deltas[0]?.GetValue<int>());
        Assert.Equal(1000, deltas[1]?.GetValue<int>());
        Assert.Equal(1000, deltas[2]?.GetValue<int>());
        Assert.Equal(1000, deltas[3]?.GetValue<int>());
        Assert.Equal(1000, deltas[4]?.GetValue<int>());

        // Samples sequence
        var samples = v8Profile["samples"]?.AsArray();
        Assert.NotNull(samples);
        Assert.Equal(5, samples.Count);

        // Nodes list and attributes mapping
        var nodes = v8Profile["nodes"]?.AsArray();
        Assert.NotNull(nodes);
        Assert.Equal(4, nodes.Count);

        foreach (var node in nodes)
        {
            int id = node?["id"]?.GetValue<int>() ?? 0;
            string functionName = node?["callFrame"]?["functionName"]?.GetValue<string>() ?? "";
            int hitCount = node?["hitCount"]?.GetValue<int>() ?? 0;
            var children = node?["children"]?.AsArray();

            if (id == 1) // root
            {
                Assert.Equal("(root)", functionName);
                Assert.Equal(1, hitCount); // 1 idle sample (-1 index)
                Assert.NotNull(children);
                Assert.Single(children);
                Assert.Equal(2, children[0]?.GetValue<int>());
            }
            else if (id == 2) // AppLoop
            {
                Assert.Equal("AppLoop", functionName);
                Assert.Equal(1, hitCount); // 1 active sample
                Assert.NotNull(children);
                Assert.Single(children);
                Assert.Equal(3, children[0]?.GetValue<int>());
            }
            else if (id == 3) // LayoutPass
            {
                Assert.Equal("LayoutPass", functionName);
                Assert.Equal(2, hitCount); // 2 active samples
                Assert.NotNull(children);
                Assert.Single(children);
                Assert.Equal(4, children[0]?.GetValue<int>());
            }
            else if (id == 4) // Measure
            {
                Assert.Equal("Measure", functionName);
                Assert.Equal(1, hitCount); // 1 active sample
                Assert.Null(children);
            }
        }
    }
}
