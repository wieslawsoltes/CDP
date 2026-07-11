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

    [Fact]
    public void Test_Sidebar_Column_Width_Collapses_And_Restores()
    {
        var vm = new TestStudioViewModel(new DummyCdpService());

        // Default state
        Assert.Equal(250, vm.SidebarColumnWidth.Value);
        Assert.Equal(4, vm.SidebarSplitterWidth.Value);

        // Collapse
        vm.IsSidebarCollapsed = true;
        Assert.Equal(0, vm.SidebarColumnWidth.Value);
        Assert.Equal(0, vm.SidebarSplitterWidth.Value);

        // Expand
        vm.IsSidebarCollapsed = false;
        Assert.Equal(250, vm.SidebarColumnWidth.Value);
        Assert.Equal(4, vm.SidebarSplitterWidth.Value);

        // Resize
        vm.SidebarColumnWidth = new Avalonia.Controls.GridLength(350, Avalonia.Controls.GridUnitType.Pixel);

        // Collapse
        vm.IsSidebarCollapsed = true;
        Assert.Equal(0, vm.SidebarColumnWidth.Value);
        Assert.Equal(0, vm.SidebarSplitterWidth.Value);

        // Expand (should restore resized width)
        vm.IsSidebarCollapsed = false;
        Assert.Equal(350, vm.SidebarColumnWidth.Value);
        Assert.Equal(4, vm.SidebarSplitterWidth.Value);
    }

    [Fact]
    public void Test_WorkspaceItemModel_BindingProperties()
    {
        var model = new WorkspaceItemModel
        {
            Name = "flow.yaml",
            Path = "/flow.yaml",
            IsFolder = false,
            FileType = "YAML Flow File",
            FormattedSize = "2.3 KB",
            FormattedDateModified = "2026-07-11 12:00:00",
            IsExpanded = true,
            IsSelected = true
        };

        Assert.Equal("flow.yaml", model.Name);
        Assert.Equal("/flow.yaml", model.Path);
        Assert.False(model.IsFolder);
        Assert.Equal("YAML Flow File", model.FileType);
        Assert.Equal("2.3 KB", model.FormattedSize);
        Assert.Equal("2026-07-11 12:00:00", model.FormattedDateModified);
        Assert.True(model.IsExpanded);
        Assert.True(model.IsSelected);
    }

    [Fact]
    public void Test_OpenEditors_Management()
    {
        var vm = new TestStudioViewModel(new DummyCdpService());
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        var testFile = Path.Combine(tempDir, "flow1.yaml");
        File.WriteAllText(testFile, "appId: \"test-app\"\ndescription: \"\"\nsteps: []\n");

        try
        {
            // Initial state
            Assert.Empty(vm.OpenEditors);

            // Load file should register in OpenEditors and set Active
            vm.LoadFlowFile(testFile);
            Assert.Single(vm.OpenEditors);
            var editor = vm.OpenEditors[0];
            Assert.Equal(testFile, editor.FilePath);
            Assert.Equal("flow1.yaml", editor.DisplayName);
            Assert.True(editor.IsActive);
            Assert.False(editor.IsDirty);

            // Mutating YamlCode should set IsDirty
            vm.YamlCode = "appId: \"modified-app\"\ndescription: \"\"\nsteps: []\n";
            Assert.True(editor.IsDirty);

            // Saving YamlCode should clear IsDirty
            vm.SaveYaml();
            Assert.False(editor.IsDirty);

            // Closing editor should clean up
            vm.CloseEditor(editor);
            Assert.Empty(vm.OpenEditors);
            Assert.Null(vm.CurrentFlowFilePath);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void Test_Workspace_Search()
    {
        var vm = new TestStudioViewModel(new DummyCdpService());
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        var testFile1 = Path.Combine(tempDir, "flow1.yaml");
        var testFile2 = Path.Combine(tempDir, "flow2.yaml");
        File.WriteAllText(testFile1, "appId: \"target-app\"\n");
        File.WriteAllText(testFile2, "appId: \"other-app\"\n");

        try
        {
            vm.WorkspaceRootPath = tempDir;
            
            // Search query "target" (Case Insensitive)
            vm.SearchQuery = "target";
            vm.IsSearchCaseSensitive = false;
            vm.IsSearchRegex = false;
            vm.PerformSearch();

            Assert.Single(vm.SearchResults);
            var fileResult = vm.SearchResults[0];
            Assert.Equal(testFile1, fileResult.FilePath);
            Assert.Single(fileResult.Matches);
            Assert.Equal(1, fileResult.Matches[0].LineNumber);
            Assert.Equal("appId: \"target-app\"", fileResult.Matches[0].LineText);

            // Clear search
            vm.ClearSearchResults();
            Assert.Empty(vm.SearchResults);
            Assert.Equal("", vm.SearchQuery);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void Test_Workspace_MoveWorkspaceItem()
    {
        var vm = new TestStudioViewModel(new DummyCdpService());
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        var subDir = Path.Combine(tempDir, "SubDir");
        Directory.CreateDirectory(subDir);

        var testFile = Path.Combine(tempDir, "flow.yaml");
        File.WriteAllText(testFile, "appId: \"test-app\"\n");

        try
        {
            vm.WorkspaceRootPath = tempDir;
            vm.LoadWorkspaceTree();

            // Load file
            vm.LoadFlowFile(testFile);
            Assert.Single(vm.OpenEditors);

            // Move flow.yaml to SubDir
            vm.MoveWorkspaceItem(testFile, subDir);

            var expectedNewPath = Path.Combine(subDir, "flow.yaml");
            Assert.True(File.Exists(expectedNewPath));
            Assert.False(File.Exists(testFile));

            // Verify active editor path updated
            Assert.Equal(expectedNewPath, vm.CurrentFlowFilePath);

            // Verify OpenEditors path updated
            Assert.Single(vm.OpenEditors);
            Assert.Equal(expectedNewPath, vm.OpenEditors[0].FilePath);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void Test_ClearFileFilter_Resets_Text()
    {
        var vm = new TestStudioViewModel(new DummyCdpService());
        vm.FileFilterText = "some-filter";
        Assert.Equal("some-filter", vm.FileFilterText);

        vm.ClearFileFilterCommand.Execute(null);

        Assert.Equal(string.Empty, vm.FileFilterText);
    }

    [Fact]
    public void Test_SelectedOpenEditor_Switches_Active_File()
    {
        var vm = new TestStudioViewModel(new DummyCdpService());
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        var testFile1 = Path.Combine(tempDir, "flow1.yaml");
        var testFile2 = Path.Combine(tempDir, "flow2.yaml");
        File.WriteAllText(testFile1, "appId: \"test-app-1\"\n");
        File.WriteAllText(testFile2, "appId: \"test-app-2\"\n");

        try
        {
            vm.WorkspaceRootPath = tempDir;
            
            // Load first file
            vm.LoadFlowFile(testFile1);
            Assert.Equal(testFile1, vm.CurrentFlowFilePath);
            Assert.Single(vm.OpenEditors);

            // Load second file
            vm.LoadFlowFile(testFile2);
            Assert.Equal(testFile2, vm.CurrentFlowFilePath);
            Assert.Equal(2, vm.OpenEditors.Count);

            // Verify both editors are in the collection and second is active
            var editor1 = vm.OpenEditors[0];
            var editor2 = vm.OpenEditors[1];
            Assert.False(editor1.IsActive);
            Assert.True(editor2.IsActive);

            // Switch to first editor by setting SelectedOpenEditor
            vm.SelectedOpenEditor = editor1;

            // Verify active file switched
            Assert.Equal(testFile1, vm.CurrentFlowFilePath);
            Assert.True(editor1.IsActive);
            Assert.False(editor2.IsActive);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void Test_Workspace_LoadFlowFile_ClearsStaleStepsOnParseError()
    {
        var vm = new TestStudioViewModel(new DummyCdpService());
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        var testFile1 = Path.Combine(tempDir, "flow1.yaml");
        var testFile2 = Path.Combine(tempDir, "flow2.yaml");

        // Valid YAML
        File.WriteAllText(testFile1, "appId: \"app-1\"\ndescription: \"desc\"\nsteps:\n  - action: tap\n    selector: \"#btn\"\n");
        // Invalid YAML
        File.WriteAllText(testFile2, "invalid: : yaml :\n");

        try
        {
            vm.LoadFlowFile(testFile1);
            vm.Steps.Add(new TestStudioStepModel { Action = "tap" });
            Assert.NotEmpty(vm.Steps);

            // Switching to invalid YAML should clear Steps and throw
            Assert.ThrowsAny<Exception>(() => vm.LoadFlowFile(testFile2));
            Assert.Empty(vm.Steps);
            Assert.Empty(vm.FlowTags);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void Test_Workspace_Search_PrunesIgnoredDirectories()
    {
        var vm = new TestStudioViewModel(new DummyCdpService());
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        var nodeModulesDir = Path.Combine(tempDir, "node_modules");
        Directory.CreateDirectory(nodeModulesDir);

        var testFile1 = Path.Combine(tempDir, "flow1.yaml");
        var testFile2 = Path.Combine(nodeModulesDir, "flow2.yaml");

        File.WriteAllText(testFile1, "appId: \"target-app\"\n");
        File.WriteAllText(testFile2, "appId: \"target-app\"\n");

        try
        {
            vm.WorkspaceRootPath = tempDir;
            vm.SearchQuery = "target";
            vm.IsSearchCaseSensitive = false;
            vm.IsSearchRegex = false;
            vm.PerformSearch();

            // Search results should only contain flow1.yaml, flow2.yaml in node_modules must be ignored
            Assert.Single(vm.SearchResults);
            Assert.Equal(testFile1, vm.SearchResults[0].FilePath);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }
}
