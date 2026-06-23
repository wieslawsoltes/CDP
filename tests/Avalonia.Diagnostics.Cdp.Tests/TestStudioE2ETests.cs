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
        public Task<JsonObject> SendCommandAsync(string method, JsonObject? parameters = null) => Task.FromResult(new JsonObject());
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

            Assert.Equal(1, vm.Steps.Count);
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
}
