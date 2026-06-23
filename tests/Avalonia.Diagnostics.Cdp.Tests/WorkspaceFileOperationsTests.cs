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

public class WorkspaceFileOperationsTests
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
    public void Test_FileOps_Create_File_Success()
    {
        var vm = new TestStudioViewModel(new DummyCdpService());
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        try
        {
            vm.WorkspaceRootPath = tempDir;
            vm.CreateFileCommand.Execute("new_file.yaml");

            var expectedFile = Path.Combine(tempDir, "new_file.yaml");
            Assert.True(File.Exists(expectedFile));
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void Test_FileOps_Create_Folder_Success()
    {
        var vm = new TestStudioViewModel(new DummyCdpService());
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        try
        {
            vm.WorkspaceRootPath = tempDir;
            vm.CreateFolderCommand.Execute("new_folder");

            var expectedFolder = Path.Combine(tempDir, "new_folder");
            Assert.True(Directory.Exists(expectedFolder));
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void Test_FileOps_Rename_Success()
    {
        var vm = new TestStudioViewModel(new DummyCdpService());
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        try
        {
            vm.WorkspaceRootPath = tempDir;
            var oldFile = Path.Combine(tempDir, "old_name.yaml");
            File.WriteAllText(oldFile, "appId: \"\"\ndescription: \"\"\n---\n- delay: 10\n");
            vm.LoadFlowFile(oldFile);

            vm.RenameCommand.Execute("new_name.yaml");

            var newFile = Path.Combine(tempDir, "new_name.yaml");
            Assert.True(File.Exists(newFile));
            Assert.False(File.Exists(oldFile));
            Assert.Equal(newFile, vm.CurrentFlowFilePath);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void Test_FileOps_Delete_Success()
    {
        var vm = new TestStudioViewModel(new DummyCdpService());
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        try
        {
            vm.WorkspaceRootPath = tempDir;
            var fileToDelete = Path.Combine(tempDir, "file_to_delete.yaml");
            File.WriteAllText(fileToDelete, "appId: \"\"\ndescription: \"\"\n---\n- delay: 10\n");
            vm.LoadFlowFile(fileToDelete);

            vm.DeleteCommand.Execute(fileToDelete);

            Assert.False(File.Exists(fileToDelete));
            Assert.Null(vm.CurrentFlowFilePath);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void Test_FileOps_Double_Click_YAML_Loads_Flow()
    {
        var vm = new TestStudioViewModel(new DummyCdpService());
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        try
        {
            vm.WorkspaceRootPath = tempDir;
            var yamlFile = Path.Combine(tempDir, "test.yaml");
            File.WriteAllText(yamlFile, "appId: \"myApp\"\ndescription: \"desc\"\n---\n- tapOn: \"#btn\"\n");
            
            vm.LoadFlowFile(yamlFile);

            Assert.NotEmpty(vm.Steps);
            Assert.Equal("tapOn", vm.Steps[0].Action);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void Test_Integration_FileOps_Create_File_Triggers_Tree_Refresh()
    {
        var vm = new TestStudioViewModel(new DummyCdpService());
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        try
        {
            vm.WorkspaceRootPath = tempDir;
            vm.WorkspaceFiles.Clear();

            vm.CreateFileCommand.Execute("subfile.yaml");
            
            Assert.NotEmpty(vm.WorkspaceFiles);
            Assert.Contains(vm.WorkspaceFiles, x => x.Name == "subfile.yaml");
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void Test_Integration_FileOps_Rename_Updates_Active_Flow_Path()
    {
        var vm = new TestStudioViewModel(new DummyCdpService());
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        try
        {
            vm.WorkspaceRootPath = tempDir;
            var oldFile = Path.Combine(tempDir, "old_path.yaml");
            File.WriteAllText(oldFile, "appId: \"\"\ndescription: \"\"\n---\n- delay: 10\n");
            vm.LoadFlowFile(oldFile);

            vm.RenameCommand.Execute("new_path.yaml");
            
            Assert.Equal(Path.Combine(tempDir, "new_path.yaml"), vm.CurrentFlowFilePath);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void Test_Integration_FileOps_Delete_Open_Flow_Closes_Workspace()
    {
        var vm = new TestStudioViewModel(new DummyCdpService());
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        try
        {
            vm.WorkspaceRootPath = tempDir;
            var openFlow = Path.Combine(tempDir, "open_flow.yaml");
            File.WriteAllText(openFlow, "appId: \"\"\ndescription: \"\"\n---\n- delay: 100\n");
            vm.LoadFlowFile(openFlow);

            vm.DeleteCommand.Execute(openFlow);
            
            Assert.Null(vm.CurrentFlowFilePath);
            Assert.Empty(vm.Steps);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void Test_Integration_FileOps_Rename_Duplicate_Name_Validation()
    {
        var vm = new TestStudioViewModel(new DummyCdpService());
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        try
        {
            vm.WorkspaceRootPath = tempDir;
            var sourceFile = Path.Combine(tempDir, "source.yaml");
            var duplicateFile = Path.Combine(tempDir, "duplicate.yaml");
            File.WriteAllText(sourceFile, "appId: \"\"\ndescription: \"\"\n---\n- delay: 10\n");
            File.WriteAllText(duplicateFile, "appId: \"\"\ndescription: \"\"\n---\n- delay: 10\n");
            
            vm.LoadFlowFile(sourceFile);
            vm.RenameCommand.Execute("duplicate.yaml");
            
            Assert.Contains("already exists", string.Join(" ", vm.Logs).ToLower());
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void Test_Integration_FileOps_Create_Invalid_Characters_Error()
    {
        var vm = new TestStudioViewModel(new DummyCdpService());
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        try
        {
            vm.WorkspaceRootPath = tempDir;
            vm.CreateFileCommand.Execute("invalid?name*.yaml");
            
            Assert.Contains("invalid", string.Join(" ", vm.Logs).ToLower());
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void Test_E2E_File_Operations_Reflected_In_Live_UI()
    {
        var vm = new TestStudioViewModel(new DummyCdpService());
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        try
        {
            vm.WorkspaceRootPath = tempDir;
            vm.CreateFileCommand.Execute("live_ui_test.yaml");
            
            Assert.True(vm.WorkspaceFiles.Count > 0);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void Test_Edge_File_System_Locking_And_Permissions()
    {
        var vm = new TestStudioViewModel(new DummyCdpService());
        vm.DeleteCommand.Execute("/locked_file.yaml");
        
        Assert.Contains("denied", string.Join(" ", vm.Logs).ToLower());
    }
}
