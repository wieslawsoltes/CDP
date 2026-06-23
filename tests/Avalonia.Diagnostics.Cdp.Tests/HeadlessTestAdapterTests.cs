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
        try
        {
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
        finally
        {
            window.Close();
        }
    }

    public class MockWindow2 : Window
    {
        public int ClickCount { get; set; } = 0;

        public MockWindow2()
        {
            Title = "Headless Test Adapter Mock Window 2";
            Width = 400;
            Height = 300;
            var button = new Button
            {
                Name = "btnClick",
                Content = "Click Me"
            };
            button.Click += (s, e) => ClickCount++;
            Content = button;
        }
    }

    [AvaloniaFact]
    public async Task RunTestAsync_ExecutesNewMaestroCommands()
    {
        var window = new MockWindow2();
        try
        {
            var adapter = new HeadlessTestAdapter();

            string yaml = @"appId: ""MockWindow2""
description: ""Integration test for new commands""
---
- assertFalse: ""1 == 2""
- setAirplaneMode: ""on""
- repeat:
    times: 3
    commands:
      - tapOn: ""#btnClick""
- retry:
    maxRetries: 2
    commands:
      - tapOn: ""#btnClick""
";

            await adapter.RunTestAsync(window, yaml, isYamlContent: true);

            // ClickCount should be:
            // - 3 from repeat loop
            // - 1 from retry block (succeeds on first try)
            // Total = 4
            Assert.Equal(4, window.ClickCount);
        }
        finally
        {
            window.Close();
        }
    }
}
