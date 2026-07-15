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

public class WorkspaceCodeGenTests
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
    public void Test_WorkspaceCodeGen_GenerateAllCode_Manual_Success()
    {
        var vm = new TestStudioViewModel(new DummyCdpService());
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        try
        {
            vm.WorkspaceRootPath = tempDir;
            
            // Create a mock YAML scenario file
            var yamlFile = Path.Combine(tempDir, "test_scenario.yaml");
            File.WriteAllText(yamlFile, "appId: \"testApp\"\ndescription: \"desc\"\n---\n- delay: 10\n");

            // Enable code generation formats and paths
            vm.CodeGenPuppeteerEnabled = true;
            vm.CodeGenPuppeteerPath = "codegen/puppeteer";
            vm.CodeGenPuppeteerRelative = true;

            vm.CodeGenPlaywrightEnabled = true;
            vm.CodeGenPlaywrightPath = "codegen/playwright";
            vm.CodeGenPlaywrightRelative = true;

            vm.CodeGenSeleniumEnabled = true;
            vm.CodeGenSeleniumPath = Path.Combine(tempDir, "absolute/selenium");
            vm.CodeGenSeleniumRelative = false;

            // Trigger manual generation
            vm.GenerateAllCodeCommand.Execute(null);

            // Assertions
            var expectedPuppeteerFile = Path.Combine(tempDir, "codegen/puppeteer", "test_scenario.js");
            var expectedPlaywrightFile = Path.Combine(tempDir, "codegen/playwright", "test_scenario.spec.js");
            var expectedSeleniumFile = Path.Combine(tempDir, "absolute/selenium", "test_scenario.Selenium.cs");

            Assert.True(File.Exists(expectedPuppeteerFile));
            Assert.True(File.Exists(expectedPlaywrightFile));
            Assert.True(File.Exists(expectedSeleniumFile));

            // Verify content contains the correct mappings
            var puppeteerContent = File.ReadAllText(expectedPuppeteerFile);
            Assert.Contains("puppeteer", puppeteerContent, StringComparison.OrdinalIgnoreCase);

            var seleniumContent = File.ReadAllText(expectedSeleniumFile);
            Assert.Contains("selenium", seleniumContent, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void Test_WorkspaceCodeGen_AutoGeneration_OnSave_Success()
    {
        var vm = new TestStudioViewModel(new DummyCdpService());
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        try
        {
            vm.WorkspaceRootPath = tempDir;
            
            // Setup target path
            var yamlFile = Path.Combine(tempDir, "on_save_scenario.yaml");
            vm.CurrentFlowFilePath = yamlFile;
            vm.YamlCode = "appId: \"testApp\"\ndescription: \"desc\"\n---\n- delay: 15\n";

            // Configure generation
            vm.CodeGenPuppeteerEnabled = true;
            vm.CodeGenPuppeteerPath = "auto_gen_out";
            vm.CodeGenPuppeteerRelative = true;
            
            // Set auto gen enabled
            vm.IsAutoCodeGenerationEnabled = true;

            // Trigger Save
            vm.SaveYamlCommand.Execute(null);

            var expectedPuppeteerFile = Path.Combine(tempDir, "auto_gen_out", "on_save_scenario.js");
            Assert.True(File.Exists(expectedPuppeteerFile));
            var codeContent = File.ReadAllText(expectedPuppeteerFile);
            Assert.Contains("setTimeout", codeContent);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void Test_WorkspaceCodeGen_State_Serialization_Success()
    {
        var vm = new TestStudioViewModel(new DummyCdpService());
        vm.IsAutoCodeGenerationEnabled = true;
        vm.CodeGenPuppeteerEnabled = true;
        vm.CodeGenPuppeteerPath = "relative/path";
        vm.CodeGenPuppeteerRelative = true;
        vm.CodeGenPlaywrightEnabled = false;
        vm.CodeGenPlaywrightPath = "/abs/path";
        vm.CodeGenPlaywrightRelative = false;

        // Save state
        var savedState = vm.SaveState();
        Assert.NotNull(savedState);

        // Load into new view model
        var vmLoad = new TestStudioViewModel(new DummyCdpService());
        vmLoad.LoadState(savedState);

        // Assertions
        Assert.True(vmLoad.IsAutoCodeGenerationEnabled);
        Assert.True(vmLoad.CodeGenPuppeteerEnabled);
        Assert.Equal("relative/path", vmLoad.CodeGenPuppeteerPath);
        Assert.True(vmLoad.CodeGenPuppeteerRelative);
        Assert.False(vmLoad.CodeGenPlaywrightEnabled);
        Assert.Equal("/abs/path", vmLoad.CodeGenPlaywrightPath);
        Assert.False(vmLoad.CodeGenPlaywrightRelative);
    }

    [Fact]
    public void Test_WorkspaceCodeGen_PreserveRelativeSubfolders_And_OpenLink_Urls()
    {
        var vm = new TestStudioViewModel(new DummyCdpService());
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        try
        {
            vm.WorkspaceRootPath = tempDir;

            // 1. Create duplicate scenarios in different subfolders
            var checkoutDir = Path.Combine(tempDir, "checkout");
            Directory.CreateDirectory(checkoutDir);
            var checkoutYamlFile = Path.Combine(checkoutDir, "login.yaml");
            // Test scalar openLink
            File.WriteAllText(checkoutYamlFile, "appId: \"testApp\"\ndescription: \"desc\"\n---\n- openLink: \"https://example.com/checkout\"\n");

            var adminDir = Path.Combine(tempDir, "admin");
            Directory.CreateDirectory(adminDir);
            var adminYamlFile = Path.Combine(adminDir, "login.yaml");
            // Test map-form openLink
            File.WriteAllText(adminYamlFile, "appId: \"testApp\"\ndescription: \"desc\"\n---\n- openLink:\n    link: \"https://example.com/admin\"\n");

            // Configure code-generation format
            vm.CodeGenPuppeteerEnabled = true;
            vm.CodeGenPuppeteerPath = "codegen/puppeteer";
            vm.CodeGenPuppeteerRelative = true;

            // Trigger generation
            vm.GenerateAllCodeCommand.Execute(null);

            // Assert relative subfolders are preserved
            var expectedCheckoutFile = Path.Combine(tempDir, "codegen/puppeteer/checkout", "login.js");
            var expectedAdminFile = Path.Combine(tempDir, "codegen/puppeteer/admin", "login.js");

            Assert.True(File.Exists(expectedCheckoutFile), $"Checkout file should exist at: {expectedCheckoutFile}");
            Assert.True(File.Exists(expectedAdminFile), $"Admin file should exist at: {expectedAdminFile}");

            // Verify URL values are successfully mapped into code-gen Output
            var checkoutJsContent = File.ReadAllText(expectedCheckoutFile);
            Assert.Contains("https://example.com/checkout", checkoutJsContent);

            var adminJsContent = File.ReadAllText(expectedAdminFile);
            Assert.Contains("https://example.com/admin", adminJsContent);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void Test_WorkspaceCodeGen_EnvironmentInterpolation()
    {
        var vm = new TestStudioViewModel(new DummyCdpService());
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        try
        {
            vm.WorkspaceRootPath = tempDir;

            // Setup active SelectedEnvironment mock variables
            var testEnv = new TestEnvironmentModel { Name = "Staging" };
            testEnv.Variables.Add(new EnvironmentVariableModel { Key = "baseUrl", Value = "https://staging.example.com" });
            testEnv.Variables.Add(new EnvironmentVariableModel { Key = "userId", Value = "user_426" });
            vm.Environments.Add(testEnv);
            vm.SelectedEnvironment = testEnv;

            // Create scenario file using local env override and placeholders
            var yamlFile = Path.Combine(tempDir, "profile.yaml");
            File.WriteAllText(yamlFile, 
@"appId: ""testApp""
description: ""desc""
env:
  suffix: ""_profile""
---
- openLink: ""${baseUrl}/dashboard""
- inputText: ""User ID: ${userId}${suffix}""
  selector: ""#userIdInput""
");

            // Configure code generation format
            vm.CodeGenPuppeteerEnabled = true;
            vm.CodeGenPuppeteerPath = "codegen/puppeteer";
            vm.CodeGenPuppeteerRelative = true;

            // Trigger generation
            vm.GenerateAllCodeCommand.Execute(null);

            var expectedFile = Path.Combine(tempDir, "codegen/puppeteer", "profile.js");
            Assert.True(File.Exists(expectedFile));

            var jsContent = File.ReadAllText(expectedFile);
            // Assert placeholders are correctly resolved before generation
            Assert.Contains("https://staging.example.com/dashboard", jsContent);
            Assert.Contains("User ID: user_426_profile", jsContent);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }
}
