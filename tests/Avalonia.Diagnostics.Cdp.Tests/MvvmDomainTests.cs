using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Net.WebSockets;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Diagnostics.Cdp.Domains;
using Avalonia.Headless.XUnit;
using Xunit;

namespace Avalonia.Diagnostics.Cdp.Tests;

public class TestAsyncCommand : System.Windows.Input.ICommand, INotifyPropertyChanged
{
    private bool _isRunning;
    public event PropertyChangedEventHandler? PropertyChanged;
#pragma warning disable 67
    public event EventHandler? CanExecuteChanged;
#pragma warning restore 67

    public bool IsRunning
    {
        get => _isRunning;
        set
        {
            if (_isRunning != value)
            {
                _isRunning = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsRunning)));
            }
        }
    }

    public bool CanExecute(object? parameter) => true;
    public void Execute(object? parameter)
    {
        IsRunning = true;
        IsRunning = false;
    }
}

public class TestViewModel : INotifyPropertyChanged
{
    private string _name = "Initial";
    private int _value = 42;

    public event PropertyChangedEventHandler? PropertyChanged;

    public TestAsyncCommand RunCommand { get; } = new();

    public string Name
    {
        get => _name;
        set
        {
            if (_name != value)
            {
                _name = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Name)));
            }
        }
    }

    public int Value
    {
        get => _value;
        set
        {
            if (_value != value)
            {
                _value = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Value)));
            }
        }
    }
}

public class MvvmDomainTests
{
    [AvaloniaFact]
    public async Task TestMvvmDomainTreeAndPropertyOperations()
    {
        var vm = new TestViewModel();
        var window = new Window
        {
            Title = "MVVM Test Window",
            DataContext = vm,
            Content = new Button { Content = "Target" }
        };
        window.Show();

        using var clientWs = new ClientWebSocket();
        var session = new CdpSession(clientWs, window);

        // 1. Enable MVVM domain
        var enableResult = await MvvmDomain.HandleAsync(session, "enable", new JsonObject());
        Assert.NotNull(enableResult);

        // 2. Get ViewModel tree
        var getTreeResult = await MvvmDomain.HandleAsync(session, "getViewModelTree", new JsonObject());
        Assert.NotNull(getTreeResult);
        Assert.True(getTreeResult.ContainsKey("tree"));

        var tree = getTreeResult["tree"] as JsonArray;
        Assert.NotNull(tree);
        Assert.Single(tree);

        var rootNode = tree[0] as JsonObject;
        Assert.NotNull(rootNode);
        
        string vmId = rootNode["id"]?.GetValue<string>() ?? "";
        Assert.NotEmpty(vmId);
        Assert.Contains("TestViewModel", rootNode["type"]?.GetValue<string>());

        var properties = rootNode["properties"] as JsonArray;
        Assert.NotNull(properties);

        var nameProp = properties.FirstOrDefault(p => p?["name"]?.GetValue<string>() == "Name") as JsonObject;
        Assert.NotNull(nameProp);
        Assert.Equal("Initial", nameProp["value"]?.GetValue<string>());
        Assert.Equal("System.String", nameProp["type"]?.GetValue<string>());

        var valProp = properties.FirstOrDefault(p => p?["name"]?.GetValue<string>() == "Value") as JsonObject;
        Assert.NotNull(valProp);
        Assert.Equal(42, valProp["value"]?.GetValue<int>());

        // 3. Set property value via CDP
        var setParams = new JsonObject
        {
            ["viewModelId"] = vmId,
            ["propertyName"] = "Name",
            ["value"] = JsonValue.Create("Updated via CDP")
        };
        var setRes = await MvvmDomain.HandleAsync(session, "setPropertyValue", setParams);
        Assert.NotNull(setRes);

        // Verify property changed in VM
        Assert.Equal("Updated via CDP", vm.Name);

        // 4. Test property changed event broadcast
        var eventReceived = false;
        string? changedPropName = null;
        string? changedValue = null;

        session.EventSentForTesting += (evt) =>
        {
            if (evt?["method"]?.GetValue<string>() == "Mvvm.propertyChanged")
            {
                var @params = evt["params"] as JsonObject;
                if (@params != null && @params["viewModelId"]?.GetValue<string>() == vmId)
                {
                    eventReceived = true;
                    changedPropName = @params["propertyName"]?.GetValue<string>();
                    changedValue = @params["value"]?.ToString();
                }
            }
        };

        // Trigger change in the ViewModel directly
        vm.Value = 99;

        // Wait up to 100ms for event broadcast
        await Task.Delay(100);

        Assert.True(eventReceived);
        Assert.Equal("Value", changedPropName);
        Assert.Equal("99", changedValue);

        // 5. Test command execution broadcast
        var commandExecutedEventReceived = false;
        string? executedCmdName = null;

        session.EventSentForTesting += (evt) =>
        {
            if (evt?["method"]?.GetValue<string>() == "Mvvm.commandExecuted")
            {
                var @params = evt["params"] as JsonObject;
                if (@params != null && @params["viewModelId"]?.GetValue<string>() == vmId)
                {
                    commandExecutedEventReceived = true;
                    executedCmdName = @params["commandName"]?.GetValue<string>();
                }
            }
        };

        // Trigger command execution on the VM directly
        vm.RunCommand.Execute(null);

        // Wait up to 100ms for event broadcast
        await Task.Delay(100);

        Assert.True(commandExecutedEventReceived);
        Assert.Equal("RunCommand", executedCmdName);

        // Disable MVVM domain
        var disableResult = await MvvmDomain.HandleAsync(session, "disable", new JsonObject());
        Assert.NotNull(disableResult);

        window.Close();
    }
}
