using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using CdpInspectorApp.Services;
using Xunit;

namespace Avalonia.Diagnostics.Cdp.Tests;

public class HeadlessTestAdapterTests
{
    public class MockWindow : Window
    {
        public MockWindow()
        {
            Title = "Headless Test Adapter Mock Window";
            Width = 400;
            Height = 300;
            Content = new Button
            {
                Name = "btnTest",
                Content = "Hello Headless"
            };
        }
    }

    [AvaloniaFact]
    public async Task RunTestAsync_ExecutesYamlSuccessfully()
    {
        var window = new MockWindow();
        var adapter = new HeadlessTestAdapter();

        string yaml = @"appId: ""MockWindow""
description: ""Headless adapter unit test""
---
- tapOn: ""#btnTest""
- assertVisible: ""#btnTest""
";

        // This should run the test steps successfully on the window using CDP
        await adapter.RunTestAsync(window, yaml, isYamlContent: true);
    }
}
