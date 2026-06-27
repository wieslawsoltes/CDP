using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Xunit;
using CdpInspectorApp.Models;
using CdpInspectorApp.ViewModels;
using CdpInspectorApp.Services;

namespace Avalonia.Diagnostics.Cdp.Tests;

public class ElementsViewModelTests
{
    public class SpyCdpService : ICdpService
    {
        public bool IsConnected { get; set; } = true;
        public string ConnectionStatus => "";
        public string ConnectedHost => "";
        public string ConnectedTargetId => "";
        public bool IsPreviewScreencastActive { get; set; } = false;

        public event PropertyChangedEventHandler? PropertyChanged;
        public event EventHandler<CdpEventEventArgs>? EventReceived;

        public List<(string Method, JsonObject? Parameters)> SentCommands { get; } = new();

        public Task<List<TargetItem>> GetTargetsAsync(string host) => Task.FromResult(new List<TargetItem>());
        public Task ConnectAsync(string host, TargetItem target) => Task.CompletedTask;
        public Task DisconnectAsync() => Task.CompletedTask;

        public Task<JsonObject> SendCommandAsync(string method, JsonObject? parameters = null)
        {
            SentCommands.Add((method, parameters));

            var response = new JsonObject();
            if (method == "DOM.resolveNode")
            {
                response["object"] = new JsonObject { ["objectId"] = "obj123" };
            }
            else if (method == "Runtime.getProperties")
            {
                response["result"] = new JsonArray();
            }
            else if (method == "DOMDebugger.getEventListeners")
            {
                response["listeners"] = new JsonArray();
            }
            else if (method == "CSS.getMatchedStylesForNode")
            {
                response["inlineStyle"] = new JsonObject { ["cssProperties"] = new JsonArray() };
            }
            else if (method == "CSS.getComputedStyleForNode")
            {
                response["computedStyle"] = new JsonArray();
            }
            else if (method == "Accessibility.getAXNode")
            {
                response["nodes"] = new JsonArray();
            }
            else if (method == "DOM.getBoxModel")
            {
                response["model"] = new JsonObject
                {
                    ["margin"] = new JsonArray { 10.0, 10.0, 110.0, 10.0, 110.0, 110.0, 10.0, 110.0 },
                    ["border"] = new JsonArray { 20.0, 20.0, 100.0, 20.0, 100.0, 100.0, 20.0, 100.0 },
                    ["padding"] = new JsonArray { 25.0, 25.0, 95.0, 25.0, 95.0, 95.0, 25.0, 95.0 },
                    ["content"] = new JsonArray { 30.0, 30.0, 90.0, 30.0, 90.0, 90.0, 30.0, 90.0 },
                    ["width"] = 80.0,
                    ["height"] = 80.0
                };
            }
            return Task.FromResult(response);
        }
    }

    [Fact]
    public async Task TestUpdateAttributeAsync()
    {
        var spy = new SpyCdpService();
        var vm = new ElementsViewModel(spy);
        var node = new DomNodeModel(42, "Button");
        vm.SelectedNode = node;
        
        spy.SentCommands.Clear();

        var attr = new AttributeModel("width", "100");
        await vm.UpdateAttributeAsync(attr);

        var setAttrCmd = spy.SentCommands.FirstOrDefault(c => c.Method == "DOM.setAttributeValue");
        Assert.NotNull(setAttrCmd.Parameters);
        Assert.Equal(42, setAttrCmd.Parameters["nodeId"]?.GetValue<int>());
        Assert.Equal("width", setAttrCmd.Parameters["name"]?.GetValue<string>());
        Assert.Equal("100", setAttrCmd.Parameters["value"]?.GetValue<string>());
    }

    [Fact]
    public async Task TestUpdatePropertyAsync()
    {
        var spy = new SpyCdpService();
        var vm = new ElementsViewModel(spy);
        var node = new DomNodeModel(42, "Button");
        vm.SelectedNode = node;
        
        spy.SentCommands.Clear();

        var prop = new PropertyModel("IsVisible", "false", "boolean");
        await vm.UpdatePropertyAsync(prop);

        var callFuncCmd = spy.SentCommands.FirstOrDefault(c => c.Method == "Runtime.callFunctionOn");
        Assert.NotNull(callFuncCmd.Parameters);
        Assert.Equal("obj123", callFuncCmd.Parameters["objectId"]?.GetValue<string>());
        Assert.Equal("function(val) { this.IsVisible = val; }", callFuncCmd.Parameters["functionDeclaration"]?.GetValue<string>());
        
        var args = callFuncCmd.Parameters["arguments"] as JsonArray;
        Assert.NotNull(args);
        var argVal = args[0] as JsonObject;
        Assert.NotNull(argVal);
        Assert.False(argVal["value"]?.GetValue<bool>());
    }

    [Fact]
    public async Task TestUpdateCssPropertyAsync()
    {
        var spy = new SpyCdpService();
        var vm = new ElementsViewModel(spy);
        var node = new DomNodeModel(42, "Button");
        vm.SelectedNode = node;
        
        vm.CssProperties.Add(new CssPropertyModel("background", "red"));
        vm.CssProperties.Add(new CssPropertyModel("color", "blue"));

        spy.SentCommands.Clear();

        var updatedProp = vm.CssProperties[0];
        updatedProp.Value = "green";
        await vm.UpdateCssPropertyAsync(updatedProp);

        var setStylesCmd = spy.SentCommands.FirstOrDefault(c => c.Method == "CSS.setStyleTexts");
        Assert.NotNull(setStylesCmd.Parameters);
        var edits = setStylesCmd.Parameters["edits"] as JsonArray;
        Assert.NotNull(edits);
        var edit = edits[0] as JsonObject;
        Assert.NotNull(edit);
        Assert.Equal("42", edit["styleSheetId"]?.GetValue<string>());
        Assert.Equal("background: green; color: blue;", edit["text"]?.GetValue<string>());
    }

    [Fact]
    public async Task TestDeleteAttributeAsyncFallback()
    {
        var spy = new SpyCdpService();
        var vm = new ElementsViewModel(spy);
        var node = new DomNodeModel(42, "Button");
        node.AttributesList.Add(new AttributeModel("class", "btn"));
        vm.SelectedNode = node;
        
        vm.SelectedAttribute = node.AttributesList[0];
        vm.AttributeNameInputText = ""; // clear textbox input to test fallback

        spy.SentCommands.Clear();

        await vm.DeleteAttributeAsync();

        var removeAttrCmd = spy.SentCommands.FirstOrDefault(c => c.Method == "DOM.removeAttribute");
        Assert.NotNull(removeAttrCmd.Parameters);
        Assert.Equal(42, removeAttrCmd.Parameters["nodeId"]?.GetValue<int>());
        Assert.Equal("class", removeAttrCmd.Parameters["name"]?.GetValue<string>());
    }

    [Fact]
    public async Task TestCommitBoxModelEdit()
    {
        var spy = new SpyCdpService();
        var vm = new ElementsViewModel(spy);
        var node = new DomNodeModel(42, "Button");
        vm.SelectedNode = node;
        
        vm.BoxMarginTop = "15";
        spy.SentCommands.Clear();
        
        await vm.CommitEditAsync("MarginTop");
        
        var setStylesCmd = spy.SentCommands.FirstOrDefault(c => c.Method == "CSS.setStyleTexts");
        Assert.NotNull(setStylesCmd.Parameters);
        var edits = setStylesCmd.Parameters["edits"] as JsonArray;
        Assert.NotNull(edits);
        var edit = edits[0] as JsonObject;
        Assert.NotNull(edit);
        Assert.Equal("42", edit["styleSheetId"]?.GetValue<string>());
        Assert.Equal("margin-top: 15px;", edit["text"]?.GetValue<string>());
    }

    [Fact]
    public async Task TestGetBoxModelOnSelection()
    {
        var spy = new SpyCdpService();
        var vm = new ElementsViewModel(spy);
        var node = new DomNodeModel(42, "Button");
        
        vm.SelectedNode = node;
        
        // Wait a short bit to let selection tasks run
        await Task.Delay(150);
        
        Assert.Equal("10", vm.BoxMarginTop);
        Assert.Equal("10", vm.BoxMarginRight);
        Assert.Equal("10", vm.BoxMarginBottom);
        Assert.Equal("10", vm.BoxMarginLeft);

        Assert.Equal("5", vm.BoxBorderTop);
        Assert.Equal("5", vm.BoxBorderRight);
        Assert.Equal("5", vm.BoxBorderBottom);
        Assert.Equal("5", vm.BoxBorderLeft);

        Assert.Equal("5", vm.BoxPaddingTop);
        Assert.Equal("5", vm.BoxPaddingRight);
        Assert.Equal("5", vm.BoxPaddingBottom);
        Assert.Equal("5", vm.BoxPaddingLeft);

        Assert.Equal("80", vm.BoxWidth);
        Assert.Equal("80", vm.BoxHeight);
    }
}
