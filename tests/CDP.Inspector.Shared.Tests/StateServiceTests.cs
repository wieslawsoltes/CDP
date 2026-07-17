#nullable enable

using System;
using System.IO;
using System.Text.Json.Nodes;
using Xunit;
using CdpInspectorApp.Services;
using CdpInspectorApp.ViewModels;
using CdpInspectorApp.Models;
using CDP.Editor.Splits.Models;
using Avalonia.Headless.XUnit;

namespace Avalonia.Diagnostics.Cdp.Tests;

public class StateServiceTests
{
    public class MockProvider : IStateProvider
    {
        public string StateKey { get; }
        public JsonNode? StateToReturn { get; set; }
        public JsonNode? LoadedState { get; private set; }
        public bool ThrowOnSave { get; set; }
        public bool ThrowOnLoad { get; set; }

        public MockProvider(string key)
        {
            StateKey = key;
        }

        public JsonNode? SaveState()
        {
            if (ThrowOnSave) throw new InvalidOperationException("Mock save error");
            return StateToReturn;
        }

        public void LoadState(JsonNode? stateNode)
        {
            if (ThrowOnLoad) throw new InvalidOperationException("Mock load error");
            LoadedState = stateNode;
        }
    }

    [Fact]
    public void Test_RegisterAndUnregisterProviders()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            var service = new StateService(tempFile);
            var provider = new MockProvider("test");

            service.RegisterProvider(provider);
            service.UnregisterProvider("test");

            // Save should not write anything for "test"
            service.Save();

            var json = File.ReadAllText(tempFile);
            var root = JsonNode.Parse(json) as JsonObject;
            Assert.NotNull(root);
            Assert.NotNull(root["providers"]);
            Assert.Null(root["providers"]?["test"]);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Fact]
    public void Test_SaveAndRestore_MockState()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            var service = new StateService(tempFile);
            var provider1 = new MockProvider("p1")
            {
                StateToReturn = new JsonObject { ["value"] = "hello" }
            };
            var provider2 = new MockProvider("p2")
            {
                StateToReturn = new JsonObject { ["number"] = 42 }
            };

            service.RegisterProvider(provider1);
            service.RegisterProvider(provider2);

            service.Save();

            // Create a new service and reload
            var service2 = new StateService(tempFile);
            var restored1 = new MockProvider("p1");
            var restored2 = new MockProvider("p2");

            service2.RegisterProvider(restored1);
            service2.RegisterProvider(restored2);
            service2.Load();

            Assert.NotNull(restored1.LoadedState);
            Assert.Equal("hello", restored1.LoadedState?["value"]?.GetValue<string>());

            Assert.NotNull(restored2.LoadedState);
            Assert.Equal(42, restored2.LoadedState?["number"]?.GetValue<int>());
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Fact]
    public void Test_ErrorResilience_DuringSaveAndLoad()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            var service = new StateService(tempFile);
            var provider1 = new MockProvider("p1") { ThrowOnSave = true };
            var provider2 = new MockProvider("p2")
            {
                StateToReturn = new JsonObject { ["working"] = true }
            };

            service.RegisterProvider(provider1);
            service.RegisterProvider(provider2);

            // Save should not throw despite provider1 failing
            service.Save();

            // Load resilience test
            var service2 = new StateService(tempFile);
            var restored1 = new MockProvider("p1") { ThrowOnLoad = true };
            var restored2 = new MockProvider("p2");

            service2.RegisterProvider(restored1);
            service2.RegisterProvider(restored2);

            // Load should not throw despite restored1 failing
            service2.Load();

            Assert.NotNull(restored2.LoadedState);
            Assert.True(restored2.LoadedState?["working"]?.GetValue<bool>());
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Fact]
    public void Test_VersionMismatch_Handling()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            // Write a future version 99 state file
            var root = new JsonObject();
            root["version"] = 99;
            root["providers"] = new JsonObject
            {
                ["p1"] = new JsonObject { ["value"] = "future" }
            };
            File.WriteAllText(tempFile, root.ToJsonString());

            var service = new StateService(tempFile);
            var provider = new MockProvider("p1");
            service.RegisterProvider(provider);

            // Loading should read it but handle the future version gracefully
            service.Load();

            Assert.NotNull(provider.LoadedState);
            Assert.Equal("future", provider.LoadedState?["value"]?.GetValue<string>());
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [AvaloniaFact]
    public void Test_ConnectionViewModel_StatePreservation()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            var service = new StateService(tempFile);
            var connectionVM = new ConnectionViewModel(new MemoryViewModelTests.MockCdpService())
            {
                HostAddress = "ws://127.0.0.1:9999/devtools/page/abc",
                UseAutomationSelectors = true
            };

            service.RegisterProvider(connectionVM);
            service.Save();

            var connectionVM2 = new ConnectionViewModel(new MemoryViewModelTests.MockCdpService())
            {
                HostAddress = "http://127.0.0.1:9222",
                UseAutomationSelectors = false
            };

            var service2 = new StateService(tempFile);
            service2.RegisterProvider(connectionVM2);
            service2.Load();

            Assert.Equal("ws://127.0.0.1:9999/devtools/page/abc", connectionVM2.HostAddress);
            Assert.True(connectionVM2.UseAutomationSelectors);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [AvaloniaFact]
    public void Test_TestStudioViewModel_StatePreservation()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            var service = new StateService(tempFile);
            var testStudioVM = new TestStudioViewModel(new MemoryViewModelTests.MockCdpService())
            {
                WorkspaceRootPath = "/Users/workspace/flow",
                IsAutoLaunchEnabled = true,
                AutoLaunchPath = "CdpSampleApp",
                AutoLaunchArguments = "--port 9222",
                IsRecordVideoEnabled = false,
                IsGenerateReportEnabled = false,
                OutputDirectory = "CustomReports"
            };

            service.RegisterProvider(testStudioVM);
            service.Save();

            var testStudioVM2 = new TestStudioViewModel(new MemoryViewModelTests.MockCdpService())
            {
                WorkspaceRootPath = null,
                IsAutoLaunchEnabled = false,
                AutoLaunchPath = "",
                AutoLaunchArguments = "",
                IsRecordVideoEnabled = true,
                IsGenerateReportEnabled = true,
                OutputDirectory = "TestReports"
            };

            var service2 = new StateService(tempFile);
            service2.RegisterProvider(testStudioVM2);
            service2.Load();

            Assert.Equal("/Users/workspace/flow", testStudioVM2.WorkspaceRootPath);
            Assert.True(testStudioVM2.IsAutoLaunchEnabled);
            Assert.Equal("CdpSampleApp", testStudioVM2.AutoLaunchPath);
            Assert.Equal("--port 9222", testStudioVM2.AutoLaunchArguments);
            Assert.False(testStudioVM2.IsRecordVideoEnabled);
            Assert.False(testStudioVM2.IsGenerateReportEnabled);
            Assert.Equal("CustomReports", testStudioVM2.OutputDirectory);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [AvaloniaFact]
    public void Test_MainWindowViewModel_LayoutStatePreservation()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            var vm = new MainWindowViewModel(new MemoryViewModelTests.MockCdpService());
            // Create a custom layout structure
            var simPane = new BoxNode();
            simPane.AddTab("Sim", "PreviewLinkIcon", "Simulation");
            var elementsPane = new BoxNode();
            elementsPane.AddTab("Elems", "CodeIcon", "Elements");

            vm.LayoutRoot = new SplitContainerNode(Avalonia.Layout.Orientation.Vertical, simPane, elementsPane)
            {
                SplitterRatio = 0.45
            };
            vm.IsPreviewPanelVisible = true;

            // Make elementsPane the selected pane
            elementsPane.IsSelected = true;
            vm.SelectedPane = elementsPane;

            // Create custom state service to register it manually without running automatic Load during VM constructor
            var service = new StateService(tempFile);
            service.RegisterProvider(vm);
            service.Save();

            // Load to a new VM
            var vm2 = new MainWindowViewModel(new MemoryViewModelTests.MockCdpService());
            var service2 = new StateService(tempFile);
            service2.RegisterProvider(vm2);
            service2.Load();

            Assert.True(vm2.IsPreviewPanelVisible);
            Assert.NotNull(vm2.LayoutRoot);
            Assert.IsType<SplitContainerNode>(vm2.LayoutRoot);

            var rootContainer = (SplitContainerNode)vm2.LayoutRoot;
            Assert.Equal(Avalonia.Layout.Orientation.Vertical, rootContainer.Orientation);
            Assert.Equal(0.45, rootContainer.SplitterRatio);

            Assert.IsType<BoxNode>(rootContainer.Child1);
            Assert.IsType<BoxNode>(rootContainer.Child2);

            var box1 = (BoxNode)rootContainer.Child1;
            Assert.Single(box1.Tabs);
            Assert.Equal("Simulation", box1.Tabs[0].SelectedViewName);

            var box2 = (BoxNode)rootContainer.Child2;
            Assert.Single(box2.Tabs);
            Assert.Equal("Elements", box2.Tabs[0].SelectedViewName);

            // Selected pane restoration check
            Assert.NotNull(vm2.SelectedPane);
            Assert.Equal("Elements", vm2.SelectedPane.SelectedViewName);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [AvaloniaFact]
    public void Test_ElementsViewModel_StatePreservation()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            var service = new StateService(tempFile);
            var vm = new ElementsViewModel(new MemoryViewModelTests.MockCdpService())
            {
                ShowVisualTree = true,
                SelectedTreeTabIndex = 2,
                SearchQuery = "testQuery",
                AxSearchQuery = "axQuery",
                PropertySearchText = "prop",
                CssSearchText = "css",
                ComputedSearchText = "comp",
                AttributeSearchText = "attr"
            };

            service.RegisterProvider(vm);
            service.Save();

            var vm2 = new ElementsViewModel(new MemoryViewModelTests.MockCdpService());
            var service2 = new StateService(tempFile);
            service2.RegisterProvider(vm2);
            service2.Load();

            Assert.True(vm2.ShowVisualTree);
            Assert.Equal(2, vm2.SelectedTreeTabIndex);
            Assert.Equal("testQuery", vm2.SearchQuery);
            Assert.Equal("axQuery", vm2.AxSearchQuery);
            Assert.Equal("prop", vm2.PropertySearchText);
            Assert.Equal("css", vm2.CssSearchText);
            Assert.Equal("comp", vm2.ComputedSearchText);
            Assert.Equal("attr", vm2.AttributeSearchText);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [AvaloniaFact]
    public void Test_ConsoleViewModel_StatePreservation()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            var service = new StateService(tempFile);
            var vm = new ConsoleViewModel(new MemoryViewModelTests.MockCdpService(), () => null)
            {
                FilterAll = false,
                FilterError = true,
                FilterWarning = true,
                FilterInfo = false,
                FilterVerbose = false,
                FilterQuery = "consoleQuery",
                ConsoleInputText = "consoleInput"
            };
            vm.PinnedExpressions.Add(new PinnedExpressionViewModel { Expression = "expr1" });
            vm.PinnedExpressions.Add(new PinnedExpressionViewModel { Expression = "expr2" });

            service.RegisterProvider(vm);
            service.Save();

            var vm2 = new ConsoleViewModel(new MemoryViewModelTests.MockCdpService(), () => null);
            var service2 = new StateService(tempFile);
            service2.RegisterProvider(vm2);
            service2.Load();

            Assert.False(vm2.FilterAll);
            Assert.True(vm2.FilterError);
            Assert.True(vm2.FilterWarning);
            Assert.False(vm2.FilterInfo);
            Assert.False(vm2.FilterVerbose);
            Assert.Equal("consoleQuery", vm2.FilterQuery);
            Assert.Equal("consoleInput", vm2.ConsoleInputText);
            Assert.Equal(2, vm2.PinnedExpressions.Count);
            Assert.Equal("expr1", vm2.PinnedExpressions[0].Expression);
            Assert.Equal("expr2", vm2.PinnedExpressions[1].Expression);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [AvaloniaFact]
    public void Test_SourcesViewModel_StatePreservation()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            var service = new StateService(tempFile);
            var vm = new SourcesViewModel(new MemoryViewModelTests.MockCdpService())
            {
                SearchQuery = "sourcesQuery",
                SearchCaseSensitive = true,
                BreakpointCondition = "cond"
            };

            service.RegisterProvider(vm);
            service.Save();

            var vm2 = new SourcesViewModel(new MemoryViewModelTests.MockCdpService());
            var service2 = new StateService(tempFile);
            service2.RegisterProvider(vm2);
            service2.Load();

            Assert.Equal("sourcesQuery", vm2.SearchQuery);
            Assert.True(vm2.SearchCaseSensitive);
            Assert.Equal("cond", vm2.BreakpointCondition);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [AvaloniaFact]
    public void Test_NetworkViewModel_StatePreservation()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            var service = new StateService(tempFile);
            var vm = new NetworkViewModel(new MemoryViewModelTests.MockCdpService())
            {
                ActiveFilter = "Doc"
            };
            vm.BlockedUrls.Add(new BlockedUrlModel { Pattern = "blocked1" });
            vm.MockRules.Add(new MockRuleModel
            {
                UrlPattern = "mock1",
                IsActive = false,
                StatusCode = 404,
                MockBody = "body1",
                ResponseHeaders = "header1"
            });

            service.RegisterProvider(vm);
            service.Save();

            var vm2 = new NetworkViewModel(new MemoryViewModelTests.MockCdpService());
            var service2 = new StateService(tempFile);
            service2.RegisterProvider(vm2);
            service2.Load();

            Assert.Equal("Doc", vm2.ActiveFilter);
            Assert.Single(vm2.BlockedUrls);
            Assert.Equal("blocked1", vm2.BlockedUrls[0].Pattern);
            Assert.Single(vm2.MockRules);
            Assert.Equal("mock1", vm2.MockRules[0].UrlPattern);
            Assert.False(vm2.MockRules[0].IsActive);
            Assert.Equal(404, vm2.MockRules[0].StatusCode);
            Assert.Equal("body1", vm2.MockRules[0].MockBody);
            Assert.Equal("header1", vm2.MockRules[0].ResponseHeaders);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [AvaloniaFact]
    public void Test_MemoryViewModel_StatePreservation()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            var service = new StateService(tempFile);
            var vm = new MemoryViewModel(new MemoryViewModelTests.MockCdpService())
            {
                IsComparisonMode = true
            };

            service.RegisterProvider(vm);
            service.Save();

            var vm2 = new MemoryViewModel(new MemoryViewModelTests.MockCdpService());
            var service2 = new StateService(tempFile);
            service2.RegisterProvider(vm2);
            service2.Load();

            Assert.True(vm2.IsComparisonMode);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [AvaloniaFact]
    public void Test_ApplicationViewModel_StatePreservation()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            var service = new StateService(tempFile);
            var vm = new ApplicationViewModel(new MemoryViewModelTests.MockCdpService())
            {
                CustomSqlQuery = "SELECT * FROM my_table;",
                SelectedBackgroundService = "cacheStorage"
            };

            service.RegisterProvider(vm);
            service.Save();

            var vm2 = new ApplicationViewModel(new MemoryViewModelTests.MockCdpService());
            var service2 = new StateService(tempFile);
            service2.RegisterProvider(vm2);
            service2.Load();

            Assert.Equal("SELECT * FROM my_table;", vm2.CustomSqlQuery);
            Assert.Equal("cacheStorage", vm2.SelectedBackgroundService);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [AvaloniaFact]
    public void Test_SimulationViewModel_StatePreservation()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            var service = new StateService(tempFile);
            var vm = new SimulationViewModel(
                new MemoryViewModelTests.MockCdpService(),
                () => null,
                () => false,
                _ => (null, null),
                () => false,
                _ => null,
                () => false
            )
            {
                WidthText = "1200",
                HeightText = "800",
                ScaleFactorText = "2.0",
                IsMobileActive = true,
                NavigateUrlText = "https://google.com"
            };

            service.RegisterProvider(vm);
            service.Save();

            var vm2 = new SimulationViewModel(
                new MemoryViewModelTests.MockCdpService(),
                () => null,
                () => false,
                _ => (null, null),
                () => false,
                _ => null,
                () => false
            );
            var service2 = new StateService(tempFile);
            service2.RegisterProvider(vm2);
            service2.Load();

            Assert.Equal("1200", vm2.WidthText);
            Assert.Equal("800", vm2.HeightText);
            Assert.Equal("2.0", vm2.ScaleFactorText);
            Assert.True(vm2.IsMobileActive);
            Assert.Equal("https://google.com", vm2.NavigateUrlText);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }
}
