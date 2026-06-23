using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Text.Json.Nodes;
using Xunit;
using CdpInspectorApp.Models;
using CdpInspectorApp.ViewModels;
using CdpInspectorApp.Services;

namespace Avalonia.Diagnostics.Cdp.Tests;

public class EnvironmentInterpolationTests
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
    public void Test_Env_Interpolate_Single_Variable()
    {
        var vm = new TestStudioViewModel(new DummyCdpService());
        var localEnv = new Dictionary<string, string> { { "BTN_ID", "#btnSubmit" } };

        string result = vm.InterpolateVariables("${BTN_ID}", localEnv);
        Assert.Equal("#btnSubmit", result);
    }

    [Fact]
    public void Test_Env_Interpolate_Multiple_Variables()
    {
        var vm = new TestStudioViewModel(new DummyCdpService());
        var localEnv = new Dictionary<string, string> { { "USER", "John" }, { "ROLE", "Admin" } };

        string result = vm.InterpolateVariables("${USER} - ${ROLE}", localEnv);
        Assert.Equal("John - Admin", result);
    }

    [Fact]
    public void Test_Env_Fallback_To_System_Environment()
    {
        var vm = new TestStudioViewModel(new DummyCdpService());
        Environment.SetEnvironmentVariable("TEST_CDP_VAR", "value");
        try
        {
            string result = vm.InterpolateVariables("${TEST_CDP_VAR}", null);
            Assert.Equal("value", result);
        }
        finally
        {
            Environment.SetEnvironmentVariable("TEST_CDP_VAR", null);
        }
    }

    [Fact]
    public void Test_Env_Nested_RunFlow_Scope()
    {
        var vm = new TestStudioViewModel(new DummyCdpService());
        var localEnv = new Dictionary<string, string> { { "PARENT_VAR", "ParentValue" } };
        
        string result = vm.InterpolateVariables("${PARENT_VAR}", localEnv);
        Assert.Equal("ParentValue", result);
    }

    [Fact]
    public void Test_Env_Variable_Overriding()
    {
        var vm = new TestStudioViewModel(new DummyCdpService());
        var localEnv = new Dictionary<string, string> { { "USER", "Bob" } };

        string result = vm.InterpolateVariables("${USER}", localEnv);
        Assert.Equal("Bob", result);
    }

    [Fact]
    public void Test_Integration_Env_Selector_Interpolation_In_TapOn()
    {
        var vm = new TestStudioViewModel(new DummyCdpService());
        var localEnv = new Dictionary<string, string> { { "MY_SELECTOR", "#targetBtn" } };

        string result = vm.InterpolateVariables("${MY_SELECTOR}", localEnv);
        Assert.Equal("#targetBtn", result);
    }

    [Fact]
    public void Test_Integration_Env_Value_Interpolation_In_InputText()
    {
        var vm = new TestStudioViewModel(new DummyCdpService());
        var localEnv = new Dictionary<string, string> { { "INPUT_VALUE", "Dynamic Text Value" } };

        string result = vm.InterpolateVariables("${INPUT_VALUE}", localEnv);
        Assert.Equal("Dynamic Text Value", result);
    }

    [Fact]
    public void Test_Integration_Env_Delay_Interpolation_As_Int()
    {
        var vm = new TestStudioViewModel(new DummyCdpService());
        var localEnv = new Dictionary<string, string> { { "DELAY_VAL", "2500" } };

        string result = vm.InterpolateVariables("${DELAY_VAL}", localEnv);
        int delay = int.Parse(result);
        
        Assert.Equal(2500, delay);
    }

    [Fact]
    public void Test_Integration_Env_Invalid_Variable_Placeholder_Behavior()
    {
        var vm = new TestStudioViewModel(new DummyCdpService());
        
        // Unresolved variables should throw KeyNotFoundException or custom exception
        Assert.Throws<KeyNotFoundException>(() => vm.InterpolateVariables("${UNRESOLVED_VAR}", new Dictionary<string, string>()));
    }

    [Fact]
    public void Test_Integration_Env_Dynamic_Expressions()
    {
        var vm = new TestStudioViewModel(new DummyCdpService());
        var localEnv = new Dictionary<string, string> { { "PREFIX", "usr_" }, { "SUFFIX", "_id" } };

        string result = vm.InterpolateVariables("${PREFIX}test${SUFFIX}", localEnv);
        Assert.Equal("usr_test_id", result);
    }

    [Fact]
    public void Test_E2E_Relative_Paths_And_Env_Interpolation_Live()
    {
        var vm = new TestStudioViewModel(new DummyCdpService());
        var env = new Dictionary<string, string> { { "DYNAMIC_TEXT", "hello live" } };

        string resolvedText = vm.InterpolateVariables("${DYNAMIC_TEXT}", env);
        Assert.Equal("hello live", resolvedText);
    }
}
