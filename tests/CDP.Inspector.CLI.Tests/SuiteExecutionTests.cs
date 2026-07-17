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

public class SuiteExecutionTests
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
            return Task.FromResult(new JsonObject());
        }
    }

    [Fact]
    public async Task Test_Suite_Sequentially_Executes_All_Flows()
    {
        var vm = new TestStudioViewModel(new DummyCdpService());
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        try
        {
            var flow1 = Path.Combine(tempDir, "flow1.yaml");
            var flow2 = Path.Combine(tempDir, "flow2.yaml");
            File.WriteAllText(flow1, "appId: \"\"\ndescription: \"Flow 1\"\n---\n- delay: 10\n");
            File.WriteAllText(flow2, "appId: \"\"\ndescription: \"Flow 2\"\n---\n- delay: 10\n");

            await vm.RunSuite(tempDir);

            Assert.Equal(2, vm.SuitePassCount);
            Assert.Equal(0, vm.SuiteFailCount);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task Test_Suite_Tracks_Pass_Fail_Stats()
    {
        var vm = new TestStudioViewModel(new DummyCdpService());
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        try
        {
            var flow1 = Path.Combine(tempDir, "flow1.yaml");
            var flow2 = Path.Combine(tempDir, "flow2.yaml");
            File.WriteAllText(flow1, "appId: \"\"\ndescription: \"Flow 1\"\n---\n- delay: 10\n");
            File.WriteAllText(flow2, "appId: \"\"\ndescription: \"Flow 2\"\n---\n- invalidAction\n");

            await vm.RunSuite(tempDir);

            Assert.Equal(1, vm.SuitePassCount);
            Assert.Equal(1, vm.SuiteFailCount);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task Test_Suite_Logging_Output()
    {
        var vm = new TestStudioViewModel(new DummyCdpService());
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        try
        {
            var flow1 = Path.Combine(tempDir, "flow1.yaml");
            File.WriteAllText(flow1, "appId: \"\"\ndescription: \"Flow 1\"\n---\n- delay: 10\n");

            await vm.RunSuite(tempDir);

            Assert.NotEmpty(vm.Logs);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task Test_Suite_Cancel_Stop_Mid_Suite()
    {
        var vm = new TestStudioViewModel(new DummyCdpService());
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        try
        {
            var flow1 = Path.Combine(tempDir, "flow1.yaml");
            File.WriteAllText(flow1, "appId: \"\"\ndescription: \"Flow 1\"\n---\n- delay: 5000\n");

            var suiteTask = vm.RunSuite(tempDir);
            await Task.Delay(100);
            vm.StopCommand.Execute(null);

            await suiteTask;

            Assert.False(vm.IsSuiteExecuting);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task Test_Suite_Skip_Non_Yaml_Files()
    {
        var vm = new TestStudioViewModel(new DummyCdpService());
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        try
        {
            var flow1 = Path.Combine(tempDir, "flow1.txt");
            File.WriteAllText(flow1, "some text");

            await vm.RunSuite(tempDir);

            Assert.Equal(0, vm.SuitePassCount);
            Assert.Equal(0, vm.SuiteFailCount);
            Assert.False(vm.IsSuiteExecuting);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task Test_Integration_Suite_Continues_On_Failure_Option()
    {
        var vm = new TestStudioViewModel(new DummyCdpService());
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        try
        {
            var flow1 = Path.Combine(tempDir, "flow1.yaml");
            var flow2 = Path.Combine(tempDir, "flow2.yaml");
            File.WriteAllText(flow1, "appId: \"\"\ndescription: \"Flow 1\"\n---\n- invalidAction\n");
            File.WriteAllText(flow2, "appId: \"\"\ndescription: \"Flow 2\"\n---\n- delay: 10\n");

            await vm.RunSuite(tempDir);

            Assert.Equal(1, vm.SuitePassCount);
            Assert.Equal(1, vm.SuiteFailCount);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task Test_Integration_Suite_HTML_Report_Generation()
    {
        var vm = new TestStudioViewModel(new DummyCdpService());
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        var reportsDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        vm.OutputDirectory = reportsDir;
        vm.IsGenerateReportEnabled = true;
        try
        {
            var flow1 = Path.Combine(tempDir, "flow1.yaml");
            File.WriteAllText(flow1, "appId: \"\"\ndescription: \"Flow 1\"\n---\n- delay: 10\n");

            await vm.RunSuite(tempDir);

            Assert.NotEmpty(vm.LastReportPath);
            Assert.True(File.Exists(vm.LastReportPath));
        }
        finally
        {
            Directory.Delete(tempDir, true);
            if (Directory.Exists(reportsDir)) Directory.Delete(reportsDir, true);
        }
    }

    [Fact]
    public async Task Test_Integration_Suite_PDF_Report_Generation()
    {
        var vm = new TestStudioViewModel(new DummyCdpService());
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        var reportsDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        vm.OutputDirectory = reportsDir;
        vm.IsGenerateReportEnabled = true;
        try
        {
            var flow1 = Path.Combine(tempDir, "flow1.yaml");
            File.WriteAllText(flow1, "appId: \"\"\ndescription: \"Flow 1\"\n---\n- delay: 10\n");

            await vm.RunSuite(tempDir);

            Assert.NotEmpty(vm.LastPdfReportPath);
            Assert.True(File.Exists(vm.LastPdfReportPath));
        }
        finally
        {
            Directory.Delete(tempDir, true);
            if (Directory.Exists(reportsDir)) Directory.Delete(reportsDir, true);
        }
    }

    [Fact]
    public async Task Test_Integration_Suite_Captures_Screenshots_For_Failures()
    {
        var vm = new TestStudioViewModel(new DummyCdpService());
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        try
        {
            var flow1 = Path.Combine(tempDir, "flow1.yaml");
            File.WriteAllText(flow1, "appId: \"\"\ndescription: \"Flow 1\"\n---\n- invalidAction\n");

            await vm.RunSuite(tempDir);

            Assert.Contains("screenshot", string.Join(" ", vm.Logs).ToLower());
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void Test_Integration_Suite_Parallel_Locking()
    {
        var vm = new TestStudioViewModel(new DummyCdpService());
        vm.Steps.Add(new TestStudioStepModel { Action = "delay", Value = "10" });
        
        vm.IsSuiteExecuting = true;

        Assert.False(vm.PlayCommand.CanExecute(null));
        Assert.False(vm.ClearCommand.CanExecute(null));
    }

    [Fact]
    public async Task Test_Edge_Circular_RunFlow_Dependency_Detection()
    {
        var vm = new TestStudioViewModel(new DummyCdpService());
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        try
        {
            vm.WorkspaceRootPath = tempDir;
            var flow1 = Path.Combine(tempDir, "flow1.yaml");
            var flow2 = Path.Combine(tempDir, "flow2.yaml");

            File.WriteAllText(flow1, $"appId: \"\"\ndescription: \"Flow 1\"\n---\n- runFlow: \"{flow2.Replace("\\", "/")}\"\n");
            File.WriteAllText(flow2, $"appId: \"\"\ndescription: \"Flow 2\"\n---\n- runFlow: \"{flow1.Replace("\\", "/")}\"\n");

            vm.CurrentFlowFilePath = flow1;
            vm.LoadFlowFile(flow1);

            await vm.PlayAsync();

            Assert.Contains("circular", string.Join(" ", vm.Logs).ToLower());
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task Test_Edge_Large_Suite_Performance_And_Memory()
    {
        var vm = new TestStudioViewModel(new DummyCdpService());
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        try
        {
            for (int i = 0; i < 20; i++)
            {
                var flow = Path.Combine(tempDir, $"flow_{i:D3}.yaml");
                File.WriteAllText(flow, "appId: \"\"\ndescription: \"Flow\"\n---\n- delay: 1\n");
            }

            await vm.RunSuite(tempDir);

            Assert.False(vm.IsSuiteExecuting);
            Assert.Equal(20, vm.SuitePassCount);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task Test_Edge_Broken_Yaml_Syntax_Handling_In_Suite()
    {
        var vm = new TestStudioViewModel(new DummyCdpService());
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        try
        {
            var brokenFlow = Path.Combine(tempDir, "broken.yaml");
            File.WriteAllText(brokenFlow, "invalid yaml syntax: {");

            await vm.RunSuite(tempDir);

            Assert.Equal(1, vm.SuiteFailCount);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task Test_Suite_Tag_Filtering_Includes_And_Excludes()
    {
        var vm = new TestStudioViewModel(new DummyCdpService());
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        try
        {
            var flow1 = Path.Combine(tempDir, "flow1.yaml");
            File.WriteAllText(flow1, "appId: \"\"\ndescription: \"Smoke Test\"\ntags:\n  - smoke\n---\n- delay: 10\n");

            var flow2 = Path.Combine(tempDir, "flow2.yaml");
            File.WriteAllText(flow2, "appId: \"\"\ndescription: \"Perf Test\"\ntags:\n  - performance\n---\n- delay: 10\n");

            var flow3 = Path.Combine(tempDir, "flow3.yaml");
            File.WriteAllText(flow3, "appId: \"\"\ndescription: \"Flaky Test\"\ntags:\n  - smoke\n  - flaky\n---\n- delay: 10\n");

            var env = new TestEnvironmentModel
            {
                Name = "TestEnv",
                IncludedTags = "smoke",
                ExcludedTags = "flaky"
            };
            vm.Environments.Add(env);
            vm.SelectedEnvironment = env;

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
    public async Task Test_RunSelectedItemCommand_ForFolder()
    {
        var vm = new TestStudioViewModel(new DummyCdpService());
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        try
        {
            var flow1 = Path.Combine(tempDir, "flow1.yaml");
            File.WriteAllText(flow1, "appId: \"\"\ndescription: \"Flow 1\"\n---\n- delay: 10\n");

            var folderItem = new WorkspaceItemModel { Path = tempDir, IsFolder = true, Name = "testFolder" };
            vm.SelectedWorkspaceItem = folderItem;

            Assert.True(vm.IsSelectedWorkspaceItemFolder);
            Assert.False(vm.IsSelectedWorkspaceItemYaml);
            Assert.True(vm.RunSelectedItemCommand.CanExecute(null));

            await vm.RunSelectedItemAsync();

            Assert.Equal(1, vm.SuitePassCount);
            Assert.Equal(0, vm.SuiteFailCount);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task Test_RunSelectedItemCommand_ForYaml()
    {
        var vm = new TestStudioViewModel(new DummyCdpService());
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        try
        {
            var flow1 = Path.Combine(tempDir, "flow1.yaml");
            File.WriteAllText(flow1, "appId: \"\"\ndescription: \"Flow 1\"\n---\n- delay: 10\n");

            var fileItem = new WorkspaceItemModel { Path = flow1, IsFolder = false, Name = "flow1.yaml" };
            vm.SelectedWorkspaceItem = fileItem;

            Assert.False(vm.IsSelectedWorkspaceItemFolder);
            Assert.True(vm.IsSelectedWorkspaceItemYaml);
            Assert.True(vm.RunSelectedItemCommand.CanExecute(null));

            await vm.RunSelectedItemAsync();

            Assert.Equal(0, vm.SuitePassCount); // single flow doesn't count towards suite pass count
            Assert.Single(vm.Steps); // check loaded steps
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void Test_StopRunCommand_CancelsExecution()
    {
        var vm = new TestStudioViewModel(new DummyCdpService());
        Assert.False(vm.StopRunCommand.CanExecute(null));

        vm.IsSuiteExecuting = true;
        Assert.True(vm.StopRunCommand.CanExecute(null));

        vm.StopRunCommand.Execute(null);
        Assert.False(vm.IsSuiteExecuting);
    }
}
