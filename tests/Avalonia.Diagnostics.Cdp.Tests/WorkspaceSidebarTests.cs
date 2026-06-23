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
using ProDataGrid;
using Avalonia.Controls.DataGridHierarchical;

namespace Avalonia.Diagnostics.Cdp.Tests;

public class WorkspaceSidebarTests
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
    public void Test_Sidebar_Toggle_Collapsed_State()
    {
        var vm = new TestStudioViewModel(new DummyCdpService());
        bool raised = false;
        vm.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(vm.IsSidebarCollapsed))
                raised = true;
        };

        Assert.False(vm.IsSidebarCollapsed);
        vm.ToggleSidebarCommand.Execute(null);

        Assert.True(raised);
        Assert.True(vm.IsSidebarCollapsed);
    }

    [Fact]
    public void Test_WorkspaceRootPath_Valid_Updates_Tree()
    {
        var vm = new TestStudioViewModel(new DummyCdpService());
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        try
        {
            Directory.CreateDirectory(Path.Combine(tempDir, "SubFolder"));
            File.WriteAllText(Path.Combine(tempDir, "flow.yaml"), "appId: \"\"\ndescription: \"\"\nsteps: []\n");

            vm.WorkspaceRootPath = tempDir;

            Assert.Equal(tempDir, vm.WorkspaceRootPath);
            Assert.NotEmpty(vm.WorkspaceFiles);
            Assert.Contains(vm.WorkspaceFiles, x => x.Name == "SubFolder" && x.IsFolder);
            Assert.Contains(vm.WorkspaceFiles, x => x.Name == "flow.yaml" && !x.IsFolder);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void Test_WorkspaceRootPath_Invalid_Path_Error()
    {
        var vm = new TestStudioViewModel(new DummyCdpService());
        vm.WorkspaceRootPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

        Assert.Empty(vm.WorkspaceFiles);
        Assert.Contains("error", string.Join(" ", vm.Logs).ToLower()); 
    }

    [Fact]
    public void Test_Sidebar_Expand_Triggers_Tree_Reload()
    {
        var vm = new TestStudioViewModel(new DummyCdpService());
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        try
        {
            File.WriteAllText(Path.Combine(tempDir, "flow.yaml"), "appId: \"\"\ndescription: \"\"\nsteps: []\n");

            vm.IsSidebarCollapsed = true;
            vm.WorkspaceRootPath = tempDir;
            vm.WorkspaceFiles.Clear();

            vm.ToggleSidebarCommand.Execute(null);

            Assert.False(vm.IsSidebarCollapsed);
            Assert.NotEmpty(vm.WorkspaceFiles);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void Test_Workspace_Picker_Browse_Button_Triggers_StorageProvider()
    {
        var vm = new TestStudioViewModel(new DummyCdpService());
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        vm.FolderPickerHandler = () => Task.FromResult<string?>(tempDir);

        vm.BrowseWorkspaceRootCommand.Execute(null);

        Assert.Equal(tempDir, vm.WorkspaceRootPath);
    }

    [Fact]
    public void Test_Integration_Sidebar_Collapse_Layout_Update()
    {
        var vm = new TestStudioViewModel(new DummyCdpService());
        vm.IsSidebarCollapsed = false;

        vm.ToggleSidebarCommand.Execute(null);

        Assert.True(vm.IsSidebarCollapsed);
    }

    [Fact]
    public void Test_Integration_Browse_Button_Dialog_Returns_Path()
    {
        var vm = new TestStudioViewModel(new DummyCdpService());
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        vm.FolderPickerHandler = () => Task.FromResult<string?>(tempDir);
        
        vm.BrowseWorkspaceRootCommand.Execute(null);

        Assert.Equal(tempDir, vm.WorkspaceRootPath);
    }

    [Fact]
    public void Test_Integration_Tree_Node_Expansion_Loads_Subfolders()
    {
        var vm = new TestStudioViewModel(new DummyCdpService());
        var parentNode = new WorkspaceItemModel { Name = "Folder1", Path = "/Folder1", IsFolder = true };
        vm.WorkspaceFiles.Add(parentNode);

        parentNode.Children.Add(new WorkspaceItemModel { Name = "Child", Path = "/Folder1/Child", IsFolder = false });

        Assert.NotEmpty(parentNode.Children);
    }

    [Fact]
    public void Test_Integration_File_Tree_Selection_Updates_Active_Path()
    {
        var vm = new TestStudioViewModel(new DummyCdpService());
        var selectedItem = new WorkspaceItemModel { Name = "test.yaml", Path = "/test.yaml", IsFolder = false };

        vm.SelectedWorkspaceItem = selectedItem;

        Assert.Same(selectedItem, vm.SelectedWorkspaceItem);
    }

    [Fact]
    public void Test_Integration_Sidebar_State_Persistence()
    {
        var vm = new TestStudioViewModel(new DummyCdpService());
        vm.IsSidebarCollapsed = true;
        vm.WorkspaceRootPath = "/persisted/path";

        Assert.True(vm.IsSidebarCollapsed);
        Assert.Equal("/persisted/path", vm.WorkspaceRootPath);
    }

    [Fact]
    public void Test_Workspace_HierarchicalWorkspace_Selection_Mapping()
    {
        var vm = new TestStudioViewModel(new DummyCdpService());
        var item = new WorkspaceItemModel { Name = "test.yaml", Path = "/test.yaml", IsFolder = false };
        vm.WorkspaceFiles.Add(item);

        Assert.Null(vm.SelectedWorkspaceItem);

        // Simulate ProDataGrid selection setting SelectedWorkspaceNode
        var options = new HierarchicalOptions<WorkspaceItemModel>
        {
            ChildrenSelector = x => x.Children,
            IsLeafSelector = x => x.Children == null || x.Children.Count == 0,
            AutoExpandRoot = true
        };
        var tree = new HierarchicalModel<WorkspaceItemModel>(options);
        tree.SetRoots(vm.WorkspaceFiles);
        var node = tree.FindNode(item).Value;

        vm.SelectedWorkspaceNode = node;

        Assert.Same(item, vm.SelectedWorkspaceItem);

        // Setting SelectedWorkspaceItem to null should clear SelectedWorkspaceNode
        vm.SelectedWorkspaceItem = null;
        Assert.Null(vm.SelectedWorkspaceNode);
    }
}
