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
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        var tempFile = Path.Combine(tempDir, "file.yaml");
        File.WriteAllText(tempFile, "steps: []");
        try
        {
            if (OperatingSystem.IsWindows())
            {
                File.SetAttributes(tempFile, FileAttributes.ReadOnly);
            }
            else
            {
                File.SetUnixFileMode(tempDir, UnixFileMode.UserRead | UnixFileMode.UserExecute);
            }

            var vm = new TestStudioViewModel(new DummyCdpService());
            vm.DeleteItem(tempFile);
            
            Assert.Contains("denied", string.Join(" ", vm.Logs).ToLower());
        }
        finally
        {
            try
            {
                if (!OperatingSystem.IsWindows())
                {
                    File.SetUnixFileMode(tempDir, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
                }
                else
                {
                    File.SetAttributes(tempFile, FileAttributes.Normal);
                }
                Directory.Delete(tempDir, true);
            }
            catch {}
        }
    }

    [Fact]
    public void Test_Folder_Rename_Rebases_CurrentFlowFilePath()
    {
        var vm = new TestStudioViewModel(new DummyCdpService());
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        var subDir = Path.Combine(tempDir, "sub");
        Directory.CreateDirectory(subDir);
        var testFile = Path.Combine(subDir, "test.yaml");
        File.WriteAllText(testFile, "steps: []");

        try
        {
            vm.WorkspaceRootPath = tempDir;
            vm.CurrentFlowFilePath = testFile;
            
            // Selected item is the sub folder
            vm.SelectedWorkspaceItem = new WorkspaceItemModel
            {
                Name = "sub",
                Path = subDir,
                IsFolder = true
            };

            vm.RenameItem("sub_renamed");

            var expectedNewDir = Path.Combine(tempDir, "sub_renamed");
            var expectedNewFile = Path.Combine(expectedNewDir, "test.yaml");

            Assert.True(Directory.Exists(expectedNewDir));
            Assert.True(File.Exists(expectedNewFile));
            Assert.Equal(expectedNewFile, vm.CurrentFlowFilePath);
        }
        finally
        {
            try { Directory.Delete(tempDir, true); } catch {}
        }
    }

    [Fact]
    public void Test_Folder_Delete_Closes_CurrentFlowFilePath()
    {
        var vm = new TestStudioViewModel(new DummyCdpService());
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        var subDir = Path.Combine(tempDir, "sub");
        Directory.CreateDirectory(subDir);
        var testFile = Path.Combine(subDir, "test.yaml");
        File.WriteAllText(testFile, "steps: []");

        try
        {
            vm.WorkspaceRootPath = tempDir;
            vm.CurrentFlowFilePath = testFile;
            
            // Add a step so we can verify Steps are cleared too
            vm.Steps.Add(new TestStudioStepModel { Action = "delay", Value = "10" });
            Assert.NotEmpty(vm.Steps);

            // Selected item is the sub folder
            vm.SelectedWorkspaceItem = new WorkspaceItemModel
            {
                Name = "sub",
                Path = subDir,
                IsFolder = true
            };

            vm.DeleteItem(subDir);

            Assert.False(Directory.Exists(subDir));
            Assert.Null(vm.CurrentFlowFilePath);
            Assert.Empty(vm.Steps);
        }
        finally
        {
            try { Directory.Delete(tempDir, true); } catch {}
        }
    }

    [Fact]
    public void Test_RunSuiteCommand_Disabled_During_Active_Playback()
    {
        var vm = new TestStudioViewModel(new DummyCdpService());
        
        Assert.True(vm.RunSuiteCommand.CanExecute("/some/path"));

        var prop = typeof(TestStudioViewModel).GetProperty("IsExecuting");
        Assert.NotNull(prop);
        prop.SetValue(vm, true);

        Assert.False(vm.RunSuiteCommand.CanExecute("/some/path"));
    }

    [Fact]
    public void Test_ResolveFlowPath_Falls_Back_To_CurrentFlowFilePath()
    {
        var vm = new TestStudioViewModel(new DummyCdpService());
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        var testFile = Path.Combine(tempDir, "main.yaml");
        var nestedFile = Path.Combine(tempDir, "nested.yaml");
        File.WriteAllText(testFile, "steps: []");
        File.WriteAllText(nestedFile, "steps: []");

        try
        {
            vm.WorkspaceRootPath = tempDir;
            vm.CurrentFlowFilePath = testFile;

            var resolved = vm.ResolveFlowPath("nested.yaml", vm.CurrentFlowFilePath);
            Assert.Equal(nestedFile, resolved);
        }
        finally
        {
            try { Directory.Delete(tempDir, true); } catch {}
        }
    }

    private class DummyCdpServiceWithFileContent : MemoryViewModelTests.MockCdpService
    {
        private readonly string _content;
        private readonly bool _base64Encoded;

        public DummyCdpServiceWithFileContent(string content, bool base64Encoded)
        {
            _content = content;
            _base64Encoded = base64Encoded;
            IsConnected = true;
        }

        public override Task<JsonObject> SendCommandAsync(string method, JsonObject? parameters = null)
        {
            if (method == "Sources.getFileContent")
            {
                return Task.FromResult(new JsonObject
                {
                    ["content"] = _content,
                    ["base64Encoded"] = _base64Encoded
                });
            }
            return Task.FromResult(new JsonObject());
        }
    }

    [Fact]
    public async Task Test_SourcesViewModel_LoadRtfFile_MaterializesToTempFile()
    {
        var mockService = new DummyCdpServiceWithFileContent("{\\rtf1\\ansi This is RTF content}", false);
        var vm = new SourcesViewModel(mockService);
        var fileNode = new WorkspaceFileNode("test_doc.rtf", "/path/to/test_doc.rtf", false);
        
        vm.SelectedFile = fileNode;

        for (int i = 0; i < 50; i++)
        {
            if (!vm.IsLoadingContent) break;
            await Task.Delay(10);
        }

        Assert.Equal("test_doc.rtf", vm.SelectedFileName);
        Assert.NotNull(vm.LocalPreviewFilePath);
        Assert.True(File.Exists(vm.LocalPreviewFilePath));
        Assert.EndsWith(".rtf", vm.LocalPreviewFilePath, StringComparison.OrdinalIgnoreCase);

        var fileText = await File.ReadAllTextAsync(vm.LocalPreviewFilePath);
        Assert.Equal("{\\rtf1\\ansi This is RTF content}", fileText);

        try
        {
            File.Delete(vm.LocalPreviewFilePath);
        }
        catch {}
    }
}
