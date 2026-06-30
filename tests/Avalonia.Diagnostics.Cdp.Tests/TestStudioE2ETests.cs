using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Threading.Tasks;
using Xunit;
using CdpInspectorApp.Models;
using CdpInspectorApp.ViewModels;
using CdpInspectorApp.Services;
using System.Text.Json.Nodes;

namespace Avalonia.Diagnostics.Cdp.Tests;

public class TestStudioE2ETests
{
    private class DummyCdpService : ICdpService
    {
        public bool IsConnected { get; set; } = true;
        public string ConnectionStatus => "";
        public string ConnectedHost => "";
        public string ConnectedTargetId => "";
        public bool IsPreviewScreencastActive { get; set; } = false;

        public event PropertyChangedEventHandler? PropertyChanged;
        public event EventHandler<CdpEventEventArgs>? EventReceived;

        public Task<List<TargetItem>> GetTargetsAsync(string host) => Task.FromResult(new List<TargetItem>());
        public Task ConnectAsync(string host, TargetItem target) => Task.CompletedTask;
        public Task DisconnectAsync() => Task.CompletedTask;
        public Task<JsonObject> SendCommandAsync(string method, JsonObject? parameters = null)
        {
            if (method == "Runtime.evaluate" && parameters != null)
            {
                var expression = parameters["expression"]?.GetValue<string>();
                if (expression != null && expression.Contains("GetProperties("))
                {
                    return Task.FromResult(new JsonObject
                    {
                        ["result"] = new JsonObject
                        {
                            ["value"] = "[{\"Name\":\"IsEnabled\",\"Type\":\"Boolean\",\"Value\":\"True\"},{\"Name\":\"Text\",\"Type\":\"String\",\"Value\":\"Click Me\"}]"
                        }
                    });
                }

                bool val = expression != null && (expression.Contains("== true") || expression.Contains("IsEnabled"));
                return Task.FromResult(new JsonObject
                {
                    ["result"] = new JsonObject
                    {
                        ["value"] = val
                    }
                });
            }
            return Task.FromResult(new JsonObject());
        }
    }

    [Fact]
    public async Task Test_E2E_DualCdp_Connect_And_Workspace_Flow_Replay()
    {
        var vm = new TestStudioViewModel(new DummyCdpService());
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        try
        {
            var flowFile = Path.Combine(tempDir, "flow1.yaml");
            File.WriteAllText(flowFile, "appId: \"\"\ndescription: \"E2E Flow\"\n---\n- delay: 10\n");

            vm.WorkspaceRootPath = tempDir;
            vm.LoadFlowFile(flowFile);

            await vm.PlayAsync();

            Assert.Single(vm.Steps);
            Assert.Equal(StepStatus.Passed, vm.Steps[0].Status);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task Test_E2E_Suite_Execution_On_Live_App()
    {
        var vm = new TestStudioViewModel(new DummyCdpService());
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        try
        {
            var flowFile = Path.Combine(tempDir, "flow1.yaml");
            File.WriteAllText(flowFile, "appId: \"\"\ndescription: \"E2E Live Flow\"\n---\n- delay: 10\n");

            await vm.RunSuite(tempDir);

            Assert.Equal(1, vm.SuitePassCount);
            Assert.Equal(0, vm.SuiteFailCount);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void Test_E2E_Sidebar_Navigation_And_Execution()
    {
        var vm = new TestStudioViewModel(new DummyCdpService());
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        vm.FolderPickerHandler = () => Task.FromResult<string?>(tempDir);

        vm.ToggleSidebarCommand.Execute(null);
        Assert.True(vm.IsSidebarCollapsed);

        vm.BrowseWorkspaceRootCommand.Execute(null);
        Assert.Equal(tempDir, vm.WorkspaceRootPath);
    }

    [Fact]
    public async Task Test_AddAssertTrue_Prompting_When_Value_Is_Empty()
    {
        var vm = new TestStudioViewModel(new DummyCdpService());
        vm.InputSimText = "";

        // Trigger command
        await vm.AddAssertTrueAsync();

        // Verify prompt is shown
        Assert.True(vm.IsNamePromptVisible);
        Assert.Equal("Assert True Expression", vm.NamePromptTitle);

        // Simulate user submitting prompt
        vm.NamePromptCallback?.Invoke("Window.DataContext.IsDirty == true");

        // Wait for the async step addition to complete
        for (int i = 0; i < 200 && vm.Steps.Count == 0; i++)
        {
            await Task.Delay(10);
        }

        // Verify step is added with expected values
        Assert.Single(vm.Steps);
        Assert.Equal("assertTrue", vm.Steps[0].Action);
        Assert.Equal("Window.DataContext.IsDirty == true", vm.Steps[0].Value);
    }

    [Fact]
    public async Task Test_AddAssertFalse_Direct_When_Value_Is_Provided()
    {
        var vm = new TestStudioViewModel(new DummyCdpService());
        vm.InputSimText = "Window.DataContext.IsDirty == false";

        // Trigger command
        await vm.AddAssertFalseAsync();

        // Verify prompt is NOT shown and step is added directly
        Assert.False(vm.IsNamePromptVisible);
        Assert.Single(vm.Steps);
        Assert.Equal("assertFalse", vm.Steps[0].Action);
        Assert.Equal("Window.DataContext.IsDirty == false", vm.Steps[0].Value);
    }

    [Fact]
    public async Task Test_AssertPropertyPicker_Flow()
    {
        var vm = new TestStudioViewModel(new DummyCdpService());

        // 1. Show property picker
        await vm.ShowAssertPropertyPickerAsync("#btnClickMe");

        // Verify picker state is populated
        Assert.True(vm.IsAssertPickerVisible);
        Assert.Equal("#btnClickMe", vm.AssertPickerSelector);
        Assert.Equal("Assert Property on '#btnClickMe'", vm.AssertPickerTitle);
        Assert.Equal(2, vm.AssertPickerProperties.Count);

        // Verify default selection (IsEnabled should be first because it is default and alphabetically sorted)
        Assert.NotNull(vm.AssertPickerSelectedProperty);
        Assert.Equal("IsEnabled", vm.AssertPickerSelectedProperty.Name);
        Assert.Equal("Boolean", vm.AssertPickerSelectedProperty.Type);
        Assert.Equal("True", vm.AssertPickerSelectedProperty.Value);

        // Verify IsAssertPickerValueInputVisible is false for boolean types by default (Assert True comparison default)
        Assert.Equal(0, vm.AssertPickerComparisonIndex); // Assert True
        Assert.False(vm.IsAssertPickerValueInputVisible);

        // 2. Submit the picker
        await vm.SubmitAssertPickerAsync();

        // Verify picker is closed and step is added
        Assert.False(vm.IsAssertPickerVisible);
        Assert.Single(vm.Steps);
        Assert.Equal("assertTrue", vm.Steps[0].Action);
        Assert.Equal("document.querySelector(\"#btnClickMe\").visual.IsEnabled", vm.Steps[0].Value);
    }

    [Fact]
    public async Task Test_AssertPropertyPicker_ManualInput_Flow()
    {
        var vm = new TestStudioViewModel(new DummyCdpService());

        // 1. Show property picker
        await vm.ShowAssertPropertyPickerAsync("#btnClickMe");

        // 2. Type a custom manual property name
        vm.AssertPickerSelectedPropertyName = "DataContext.CustomFlag";

        // Verify selection is cleared, but typed text is preserved
        Assert.Null(vm.AssertPickerSelectedProperty);
        Assert.Equal("DataContext.CustomFlag", vm.AssertPickerSelectedPropertyName);

        // 3. Set comparison type to Assert True (0)
        vm.AssertPickerComparisonIndex = 0;

        // 4. Submit
        await vm.SubmitAssertPickerAsync();

        // Verify step is added using the custom manual property path
        Assert.False(vm.IsAssertPickerVisible);
        Assert.Single(vm.Steps);
        Assert.Equal("assertTrue", vm.Steps[0].Action);
        Assert.Equal("document.querySelector(\"#btnClickMe\").visual.DataContext.CustomFlag", vm.Steps[0].Value);
    }

    [Fact]
    public async Task Test_AssertPropertyPicker_SelectorWithDoubleQuotes_Escaping_Flow()
    {
        var vm = new TestStudioViewModel(new DummyCdpService());

        // 1. Show property picker with a selector containing double quotes
        await vm.ShowAssertPropertyPickerAsync("[AccessibilityId=\"txtTarget\"]");

        // 2. Select isEnabled property (which is boolean)
        var isEnabledProp = vm.AssertPickerProperties.FirstOrDefault(p => p.Name == "IsEnabled");
        Assert.NotNull(isEnabledProp);
        vm.AssertPickerSelectedProperty = isEnabledProp;

        // 3. Set comparison type to Assert True (0)
        vm.AssertPickerComparisonIndex = 0;

        // 4. Submit
        await vm.SubmitAssertPickerAsync();

        // Verify selector double quotes are escaped properly in the generated value expression
        Assert.False(vm.IsAssertPickerVisible);
        Assert.Single(vm.Steps);
        Assert.Equal("assertTrue", vm.Steps[0].Action);
        Assert.Equal("document.querySelector(\"[AccessibilityId=\\\"txtTarget\\\"]\").visual.IsEnabled", vm.Steps[0].Value);
    }

    [Fact]
    public void Test_ReportOptions_Initialization_And_Persistence()
    {
        var vm = new TestStudioViewModel(new DummyCdpService());

        // Verify default options are true
        Assert.True(vm.ReportIncludeScreenshots);
        Assert.True(vm.ReportIncludeCharts);
        Assert.True(vm.ReportIncludeMetricsTable);
        Assert.True(vm.ReportIncludeNetworkDetails);

        // Modify options
        vm.ReportIncludeScreenshots = false;
        vm.ReportIncludeCharts = false;
        vm.ReportIncludeMetricsTable = true;
        vm.ReportIncludeNetworkDetails = false;

        // Serialize state
        var state = vm.SaveState();
        Assert.NotNull(state);

        // Verify state object values
        var json = state.AsObject();
        Assert.False(json["reportIncludeScreenshots"]?.GetValue<bool>());
        Assert.False(json["reportIncludeCharts"]?.GetValue<bool>());
        Assert.True(json["reportIncludeMetricsTable"]?.GetValue<bool>());
        Assert.False(json["reportIncludeNetworkDetails"]?.GetValue<bool>());

        // Restore state onto a new instance
        var vm2 = new TestStudioViewModel(new DummyCdpService());
        vm2.LoadState(state);

        // Assert values are restored
        Assert.False(vm2.ReportIncludeScreenshots);
        Assert.False(vm2.ReportIncludeCharts);
        Assert.True(vm2.ReportIncludeMetricsTable);
        Assert.False(vm2.ReportIncludeNetworkDetails);
    }

    [Fact]
    public void Test_ReportGenerators_Honors_Options()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        try
        {
            var steps = new List<StepReportItem>
            {
                new StepReportItem
                {
                    Index = 1,
                    Action = "tap",
                    ActionDisplay = "Tap button",
                    Selector = "#btn",
                    Status = "Passed",
                    DurationMs = 250,
                    RelativeStartMs = 10,
                    CpuUsage = 15.5,
                    MemoryJsHeapUsed = 42.1,
                    MemoryJsHeapTotal = 64.0,
                    Fps = 60,
                    NetworkRequestCount = 2,
                    NetworkResponseBytes = 2048,
                    DomNodes = 450,
                    DomDocuments = 2
                }
            };

            var data = new TestRunReportData
            {
                TestName = "Test Run",
                Description = "Report unit test description",
                AppId = "test.app",
                Steps = steps,
                StartTime = DateTime.UtcNow.AddSeconds(-5),
                EndTime = DateTime.UtcNow,
                MetricsTimeline = new List<RunMetricSample>
                {
                    new RunMetricSample { RelativeTimeMs = 0, CpuUsage = 10.0, MemoryJsHeapUsed = 40.0, Fps = 60 },
                    new RunMetricSample { RelativeTimeMs = 260, CpuUsage = 15.5, MemoryJsHeapUsed = 42.1, Fps = 60 }
                }
            };

            var options = new TestStudioReportOptions
            {
                IncludeScreenshots = false,
                IncludeCharts = false,
                IncludeMetricsTable = false,
                IncludeNetworkDetails = false
            };

            // Generate HTML report with all disabled
            Chrome.DevTools.Protocol.TestStudioReportGenerator.GenerateHtmlReport(tempDir, data, options);
            var htmlPath = Path.Combine(tempDir, "index.html");
            Assert.True(File.Exists(htmlPath));

            var htmlContent = File.ReadAllText(htmlPath);
            Assert.DoesNotContain("step_1_screenshot.png", htmlContent);
            Assert.Contains("options = {\"IncludeScreenshots\":false,", htmlContent);

            // Generate PDF report with all disabled
            var pdfPath = Path.Combine(tempDir, "report.pdf");
            Chrome.DevTools.Protocol.TestStudioReportGenerator.GeneratePdfReport(pdfPath, data, options);
            Assert.True(File.Exists(pdfPath));
            
            // Check size to ensure PDF is non-empty
            var fileInfo = new FileInfo(pdfPath);
            Assert.True(fileInfo.Length > 0);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void Test_ReportGenerators_WaterfallChart()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        try
        {
            var steps = new List<StepReportItem>
            {
                new StepReportItem
                {
                    Index = 1,
                    Action = "tap",
                    ActionDisplay = "Tap button",
                    Selector = "#btn",
                    Status = "Passed",
                    DurationMs = 500,
                    RelativeStartMs = 100,
                    NetworkRequestCount = 1
                }
            };

            var networkRequests = new List<NetworkReportItem>
            {
                new NetworkReportItem
                {
                    RequestId = "req-1",
                    Url = "http://example.com/api/test",
                    Method = "GET",
                    Status = "200 OK",
                    RelativeStartMs = 150,
                    DurationMs = 200,
                    EncodedDataLength = 512
                }
            };

            var data = new TestRunReportData
            {
                TestName = "Test Run Waterfall",
                Steps = steps,
                NetworkRequests = networkRequests,
                StartTime = DateTime.UtcNow.AddSeconds(-2),
                EndTime = DateTime.UtcNow
            };

            var options = new TestStudioReportOptions
            {
                IncludeNetworkDetails = true,
                IncludeCharts = false,
                IncludeScreenshots = false,
                IncludeMetricsTable = false
            };

            // Generate HTML report with waterfall enabled
            Chrome.DevTools.Protocol.TestStudioReportGenerator.GenerateHtmlReport(tempDir, data, options);
            var htmlPath = Path.Combine(tempDir, "index.html");
            Assert.True(File.Exists(htmlPath));

            var htmlContent = File.ReadAllText(htmlPath);
            Assert.Contains("step-network-section-0", htmlContent);
            Assert.Contains("renderStepNetworkWaterfall", htmlContent);

            // Generate PDF report with waterfall enabled
            var pdfPath = Path.Combine(tempDir, "report.pdf");
            Chrome.DevTools.Protocol.TestStudioReportGenerator.GeneratePdfReport(pdfPath, data, options);
            Assert.True(File.Exists(pdfPath));
            
            var fileInfo = new FileInfo(pdfPath);
            Assert.True(fileInfo.Length > 0);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }
}
