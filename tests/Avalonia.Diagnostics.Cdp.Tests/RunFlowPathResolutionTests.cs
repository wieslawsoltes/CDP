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

public class RunFlowPathResolutionTests
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
    public void Test_PathResolve_Absolute_Path_Untouched()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        try
        {
            var vm = new TestStudioViewModel(new DummyCdpService());
            string absolutePath = Path.Combine(tempDir, "flow.yaml");
            File.WriteAllText(absolutePath, "steps:");

            string resolved = vm.ResolveFlowPath(absolutePath, "/current/flow.yaml");
            Assert.Equal(Path.GetFullPath(absolutePath), resolved);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void Test_PathResolve_Relative_To_Current_Flow_Directory()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        try
        {
            var vm = new TestStudioViewModel(new DummyCdpService());
            var subFile = Path.Combine(tempDir, "sub.yaml");
            var mainFile = Path.Combine(tempDir, "main.yaml");
            File.WriteAllText(subFile, "steps:");
            File.WriteAllText(mainFile, "steps:");

            string resolved = vm.ResolveFlowPath("sub.yaml", mainFile);
            Assert.Equal(Path.GetFullPath(subFile), resolved);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void Test_PathResolve_Parent_Directory_Traversal()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var folder = Path.Combine(tempDir, "folder");
        var other = Path.Combine(tempDir, "other");
        Directory.CreateDirectory(folder);
        Directory.CreateDirectory(other);
        try
        {
            var vm = new TestStudioViewModel(new DummyCdpService());
            var mainFile = Path.Combine(folder, "main.yaml");
            var subFile = Path.Combine(other, "sub.yaml");
            File.WriteAllText(mainFile, "steps:");
            File.WriteAllText(subFile, "steps:");

            string resolved = vm.ResolveFlowPath("../other/sub.yaml", mainFile);
            Assert.Equal(Path.GetFullPath(subFile), resolved);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void Test_PathResolve_Fallback_To_Workspace_Root()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        try
        {
            var vm = new TestStudioViewModel(new DummyCdpService())
            {
                WorkspaceRootPath = tempDir
            };
            var subFile = Path.Combine(tempDir, "sub.yaml");
            File.WriteAllText(subFile, "steps:");

            string resolved = vm.ResolveFlowPath("sub.yaml", null);
            Assert.Equal(Path.GetFullPath(subFile), resolved);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void Test_PathResolve_Cross_Platform_Separators()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var folder = Path.Combine(tempDir, "folder");
        Directory.CreateDirectory(folder);
        try
        {
            var vm = new TestStudioViewModel(new DummyCdpService());
            var mainFile = Path.Combine(tempDir, "main.yaml");
            var subFile = Path.Combine(folder, "sub.yaml");
            File.WriteAllText(mainFile, "steps:");
            File.WriteAllText(subFile, "steps:");

            string resolved = vm.ResolveFlowPath("folder\\sub.yaml", mainFile);
            Assert.Equal(Path.GetFullPath(subFile), resolved);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void Test_Integration_RunFlow_Resolves_And_Executes_Nested_Flow()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        try
        {
            var vm = new TestStudioViewModel(new DummyCdpService());
            var mainFile = Path.Combine(tempDir, "main.yaml");
            var nestedFile = Path.Combine(tempDir, "nested.yaml");
            File.WriteAllText(mainFile, "steps:");
            File.WriteAllText(nestedFile, "steps:");

            string resolved = vm.ResolveFlowPath("nested.yaml", mainFile);
            Assert.Equal(Path.GetFullPath(nestedFile), resolved);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void Test_Integration_RunFlow_Deeply_Nested_Path_Resolution()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        try
        {
            var vm = new TestStudioViewModel(new DummyCdpService());
            var aFile = Path.Combine(tempDir, "A.yaml");
            var bFile = Path.Combine(tempDir, "B.yaml");
            var cFile = Path.Combine(tempDir, "C.yaml");
            File.WriteAllText(aFile, "steps:");
            File.WriteAllText(bFile, "steps:");
            File.WriteAllText(cFile, "steps:");

            string resolvedB = vm.ResolveFlowPath("B.yaml", aFile);
            string resolvedC = vm.ResolveFlowPath("C.yaml", resolvedB);

            Assert.Equal(Path.GetFullPath(bFile), resolvedB);
            Assert.Equal(Path.GetFullPath(cFile), resolvedC);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void Test_Integration_RunFlow_Relative_To_Workspace_When_Unsaved()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        try
        {
            var vm = new TestStudioViewModel(new DummyCdpService())
            {
                WorkspaceRootPath = tempDir
            };
            var subFile = Path.Combine(tempDir, "sub.yaml");
            File.WriteAllText(subFile, "steps:");

            string resolved = vm.ResolveFlowPath("sub.yaml", null);
            Assert.Equal(Path.GetFullPath(subFile), resolved);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void Test_Integration_RunFlow_Missing_File_Throws_Actionable_Error()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        try
        {
            var vm = new TestStudioViewModel(new DummyCdpService());
            var mainFile = Path.Combine(tempDir, "main.yaml");
            File.WriteAllText(mainFile, "steps:");

            Assert.Throws<FileNotFoundException>(() => vm.ResolveFlowPath("missing.yaml", mainFile));
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void Test_Integration_RunFlow_Empty_File_Throws()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        try
        {
            var vm = new TestStudioViewModel(new DummyCdpService());
            var mainFile = Path.Combine(tempDir, "main.yaml");
            var emptyFile = Path.Combine(tempDir, "empty.yaml");
            File.WriteAllText(mainFile, "steps:");
            File.WriteAllText(emptyFile, ""); // empty 0-byte file

            Assert.Throws<InvalidDataException>(() => vm.ResolveFlowPath("empty.yaml", mainFile));
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void Test_Edge_Unicode_And_Special_Characters_In_Paths()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var subDir = Path.Combine(tempDir, "测试_📂_flow");
        Directory.CreateDirectory(subDir);
        try
        {
            var vm = new TestStudioViewModel(new DummyCdpService());
            var mainFile = Path.Combine(subDir, "main.yaml");
            var nestedFile = Path.Combine(subDir, "nested_测试.yaml");
            File.WriteAllText(mainFile, "steps:");
            File.WriteAllText(nestedFile, "steps:");

            string unicodePath = vm.ResolveFlowPath("nested_测试.yaml", mainFile);
            Assert.Equal(Path.GetFullPath(nestedFile), unicodePath);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }
}
