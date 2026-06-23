using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Xunit;
using Avalonia.Threading;
using Avalonia.Headless.XUnit;
using CdpInspectorApp.Models;
using CdpInspectorApp.ViewModels;
using CdpInspectorApp.Services;

namespace Avalonia.Diagnostics.Cdp.Tests;

public class ClientRecorderFallbackTests
{
    public class FakeCdpService : ICdpService
    {
        public bool IsConnected => true;
        public string ConnectionStatus => "Connected";
        public string ConnectedHost => "http://localhost:9222";
        public string ConnectedTargetId => "target123";
        public bool IsPreviewScreencastActive { get; set; }

        public event PropertyChangedEventHandler? PropertyChanged;
        public event EventHandler<CdpEventEventArgs>? EventReceived;

        public Task<List<TargetItem>> GetTargetsAsync(string host) => Task.FromResult(new List<TargetItem>());
        public Task ConnectAsync(string host, TargetItem target) => Task.CompletedTask;
        public Task DisconnectAsync() => Task.CompletedTask;

        public string? NavigationHistoryUrl { get; set; }
        public string? DocumentUrl { get; set; }

        public Task<JsonObject> SendCommandAsync(string method, JsonObject? parameters = null)
        {
            if (method == "Recorder.start")
            {
                // Simulate domain not supported error from Chrome
                throw new Exception("Method Recorder.start not found");
            }
            if (method == "Page.getNavigationHistory" && NavigationHistoryUrl != null)
            {
                var entry = new JsonObject
                {
                    ["id"] = 1,
                    ["url"] = NavigationHistoryUrl,
                    ["userTypedURL"] = NavigationHistoryUrl,
                    ["title"] = "Test Title",
                    ["transitionType"] = "typed"
                };
                var entries = new JsonArray { entry };
                var res = new JsonObject
                {
                    ["currentIndex"] = 0,
                    ["entries"] = entries
                };
                return Task.FromResult(res);
            }
            if (method == "DOM.getDocument" && DocumentUrl != null)
            {
                var root = new JsonObject
                {
                    ["nodeId"] = 1,
                    ["documentURL"] = DocumentUrl,
                    ["baseURL"] = DocumentUrl
                };
                var res = new JsonObject
                {
                    ["root"] = root
                };
                return Task.FromResult(res);
            }
            return Task.FromResult(new JsonObject());
        }
    }

    [AvaloniaFact]
    public async Task TestClientSideRecordingFallback()
    {
        var fakeCdp = new FakeCdpService();
        var recorder = new RecorderViewModel(fakeCdp, () => "localhost:9222");

        Assert.False(recorder.IsRecording);
        Assert.False(recorder.IsClientSideRecording);

        // Toggle record should trigger fallback since Recorder.start throws "not found"
        await recorder.ToggleRecordAsync();

        Assert.True(recorder.IsRecording);
        Assert.True(recorder.IsClientSideRecording);

        // Viewport and navigate steps should have been added automatically
        Assert.Equal(2, recorder.RecordedSteps.Count);
        Assert.Equal("setViewport", recorder.RecordedSteps[0].Type);
        Assert.Equal("navigate", recorder.RecordedSteps[1].Type);

        // Add a click step
        var clickStep = new JsonObject
        {
            ["type"] = "click",
            ["selectors"] = new JsonArray { new JsonArray { "#myBtn" } },
            ["button"] = "left",
            ["clickCount"] = 1
        };
        recorder.AddRecordedStepLocal(clickStep);

        // Let the dispatcher run since AddRecordedStepLocal uses Post
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(3, recorder.RecordedSteps.Count);
        Assert.Equal("click", recorder.RecordedSteps[2].Type);
        Assert.Equal("#myBtn", recorder.RecordedSteps[2].Selector);

        // Add character input steps to verify aggregation
        var inputStep1 = new JsonObject
        {
            ["type"] = "change",
            ["selectors"] = new JsonArray { new JsonArray { "#myInput" } },
            ["value"] = "H"
        };
        recorder.AddRecordedStepLocal(inputStep1);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(4, recorder.RecordedSteps.Count);
        Assert.Equal("change", recorder.RecordedSteps[3].Type);
        Assert.Equal("H", recorder.RecordedSteps[3].Value);

        var inputStep2 = new JsonObject
        {
            ["type"] = "change",
            ["selectors"] = new JsonArray { new JsonArray { "#myInput" } },
            ["value"] = "e"
        };
        recorder.AddRecordedStepLocal(inputStep2);
        Dispatcher.UIThread.RunJobs();

        // Should have been aggregated into the previous step!
        Assert.Equal(4, recorder.RecordedSteps.Count);
        Assert.Equal("He", recorder.RecordedSteps[3].Value);

        // Stop recording
        await recorder.ToggleRecordAsync();

        Assert.False(recorder.IsRecording);
        Assert.False(recorder.IsClientSideRecording);
    }

    [AvaloniaFact]
    public async Task TestClientSideRecordingFallback_WithNavigationHistory()
    {
        var fakeCdp = new FakeCdpService
        {
            NavigationHistoryUrl = "https://example.com/from-history"
        };
        var recorder = new RecorderViewModel(fakeCdp, () => "localhost:9222");

        await recorder.ToggleRecordAsync();

        Assert.Equal(2, recorder.RecordedSteps.Count);
        Assert.Equal("navigate", recorder.RecordedSteps[1].Type);
        Assert.Equal("https://example.com/from-history", recorder.RecordedSteps[1].Url);
    }

    [AvaloniaFact]
    public async Task TestClientSideRecordingFallback_WithDocumentUrl()
    {
        var fakeCdp = new FakeCdpService
        {
            DocumentUrl = "https://example.com/from-document"
        };
        var recorder = new RecorderViewModel(fakeCdp, () => "localhost:9222");

        await recorder.ToggleRecordAsync();

        Assert.Equal(2, recorder.RecordedSteps.Count);
        Assert.Equal("navigate", recorder.RecordedSteps[1].Type);
        Assert.Equal("https://example.com/from-document", recorder.RecordedSteps[1].Url);
    }

    [AvaloniaFact]
    public void TestYamlMetadataUrlParsingAndGenerating()
    {
        // 1. Global URL metadata
        string yaml1 = @"url: ""http://uitestingplayground.com/textinput""
description: ""Test Web Site""
---
- launchApp
";
        var steps1 = TestStudioYamlParser.Parse(yaml1, out var appId1, out var description1);
        Assert.Equal("http://uitestingplayground.com/textinput", appId1);
        Assert.Equal("Test Web Site", description1);
        Assert.Single(steps1);
        Assert.Equal("launchApp", steps1[0].Action);
        Assert.Equal("", steps1[0].Value);
        Assert.Equal("", steps1[0].DetailDisplay);

        var generated1 = TestStudioYamlParser.Generate(steps1, appId1, description1);
        Assert.Contains("url: \"http://uitestingplayground.com/textinput\"", generated1);
        Assert.DoesNotContain("appId:", generated1);

        // 2. Inline URL
        string yaml2 = @"---
- launchApp: ""http://uitestingplayground.com/textinput""
";
        var steps2 = TestStudioYamlParser.Parse(yaml2, out var appId2, out var description2);
        Assert.Equal("", appId2);
        Assert.Single(steps2);
        Assert.Equal("launchApp", steps2[0].Action);
        Assert.Equal("http://uitestingplayground.com/textinput", steps2[0].Value);
        Assert.Equal("Value: \"http://uitestingplayground.com/textinput\"", steps2[0].DetailDisplay);

        var generated2 = TestStudioYamlParser.Generate(steps2, appId2, description2);
        Assert.Contains("- launchApp: \"http://uitestingplayground.com/textinput\"", generated2);
    }
}
