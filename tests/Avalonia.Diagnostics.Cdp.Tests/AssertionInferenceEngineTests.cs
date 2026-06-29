using System;
using System.Collections.Generic;
using Xunit;
using CdpInspectorApp.Services;
using CdpInspectorApp.Services.AssertionRules;
using CdpInspectorApp.Models;

namespace Avalonia.Diagnostics.Cdp.Tests;

public class AssertionInferenceEngineTests
{
    [Fact]
    public void TestToggleAssertionRule()
    {
        var engine = new AssertionInferenceEngine();
        
        // Case 1: CheckBox that is checked
        var propsChecked = new Dictionary<string, string>
        {
            { "IsChecked", "True" }
        };
        var assertionsChecked = engine.InferAssertions("CheckBox", "#chkToggle", propsChecked);
        Assert.Single(assertionsChecked);
        Assert.Equal("assertVisible", assertionsChecked[0].Action);
        Assert.Equal("#chkToggle[IsChecked='true']", assertionsChecked[0].Selector);
        Assert.Equal("", assertionsChecked[0].Value);

        // Case 2: CheckBox that is unchecked
        var propsUnchecked = new Dictionary<string, string>
        {
            { "IsChecked", "False" }
        };
        var assertionsUnchecked = engine.InferAssertions("CheckBox", "#chkToggle", propsUnchecked);
        Assert.Single(assertionsUnchecked);
        Assert.Equal("assertVisible", assertionsUnchecked[0].Action);
        Assert.Equal("#chkToggle[IsChecked='false']", assertionsUnchecked[0].Selector);
        Assert.Equal("", assertionsUnchecked[0].Value);
    }

    [Fact]
    public void TestTextBoxAssertionRule()
    {
        var engine = new AssertionInferenceEngine();
        
        var props = new Dictionary<string, string>
        {
            { "Text", "Hello World" }
        };
        var assertions = engine.InferAssertions("TextBox", "#txtInput", props);
        Assert.Single(assertions);
        Assert.Equal("assertVisible", assertions[0].Action);
        Assert.Equal("#txtInput[Text='Hello World']", assertions[0].Selector);
        Assert.Equal("", assertions[0].Value);
    }

    [Fact]
    public void TestSliderAssertionRule()
    {
        var engine = new AssertionInferenceEngine();
        
        var props = new Dictionary<string, string>
        {
            { "Value", "42" }
        };
        var assertions = engine.InferAssertions("Slider", "#slider", props);
        Assert.Single(assertions);
        Assert.Equal("assertVisible", assertions[0].Action);
        Assert.Equal("#slider[Value='42']", assertions[0].Selector);
        Assert.Equal("", assertions[0].Value);
    }

    [Fact]
    public void TestSelectionAssertionRule()
    {
        var engine = new AssertionInferenceEngine();
        
        var props = new Dictionary<string, string>
        {
            { "IsSelected", "True" }
        };
        var assertions = engine.InferAssertions("TabItem", "#tabHome", props);
        Assert.Single(assertions);
        Assert.Equal("assertVisible", assertions[0].Action);
        Assert.Equal("#tabHome[IsSelected='true']", assertions[0].Selector);
        Assert.Equal("", assertions[0].Value);
    }

    [Fact]
    public void TestIndexAssertionRule()
    {
        var engine = new AssertionInferenceEngine();
        
        var props = new Dictionary<string, string>
        {
            { "SelectedIndex", "2" }
        };
        var assertions = engine.InferAssertions("ComboBox", "#cbTargets", props);
        Assert.Single(assertions);
        Assert.Equal("assertVisible", assertions[0].Action);
        Assert.Equal("#cbTargets[SelectedIndex='2']", assertions[0].Selector);
        Assert.Equal("", assertions[0].Value);
    }

    [Fact]
    public void TestNumericUpDownAssertionRule()
    {
        var engine = new AssertionInferenceEngine();
        
        var props = new Dictionary<string, string>
        {
            { "Value", "10.5" }
        };
        var assertions = engine.InferAssertions("NumericUpDown", "#numValue", props);
        Assert.Single(assertions);
        Assert.Equal("assertVisible", assertions[0].Action);
        Assert.Equal("#numValue[Value='10.5']", assertions[0].Selector);
        Assert.Equal("", assertions[0].Value);
    }

    [Fact]
    public void TestExpanderAssertionRule()
    {
        var engine = new AssertionInferenceEngine();
        
        var props = new Dictionary<string, string>
        {
            { "IsExpanded", "True" }
        };
        var assertions = engine.InferAssertions("Expander", "#expMain", props);
        Assert.Single(assertions);
        Assert.Equal("assertVisible", assertions[0].Action);
        Assert.Equal("#expMain[IsExpanded='true']", assertions[0].Selector);
        Assert.Equal("", assertions[0].Value);
    }

    [Fact]
    public void TestTreeViewItemAssertion()
    {
        var engine = new AssertionInferenceEngine();
        
        var props = new Dictionary<string, string>
        {
            { "IsExpanded", "True" },
            { "IsSelected", "False" }
        };
        var assertions = engine.InferAssertions("TreeViewItem", "#treeItem", props);
        Assert.Equal(2, assertions.Count);
        
        // Order of registration: SelectionAssertionRule (IsSelected) first, then ExpanderAssertionRule (IsExpanded)
        Assert.Equal("assertVisible", assertions[0].Action);
        Assert.Equal("#treeItem[IsSelected='false']", assertions[0].Selector);
        Assert.Equal("", assertions[0].Value);
        Assert.Equal("assertVisible", assertions[1].Action);
        Assert.Equal("#treeItem[IsExpanded='true']", assertions[1].Selector);
        Assert.Equal("", assertions[1].Value);
    }

    [Fact]
    public void TestListBoxAssertion()
    {
        var engine = new AssertionInferenceEngine();
        
        var props = new Dictionary<string, string>
        {
            { "SelectedIndex", "3" }
        };
        var assertions = engine.InferAssertions("ListBox", "#lstItems", props);
        Assert.Single(assertions);
        Assert.Equal("assertVisible", assertions[0].Action);
        Assert.Equal("#lstItems[SelectedIndex='3']", assertions[0].Selector);
        Assert.Equal("", assertions[0].Value);
    }

    [Fact]
    public void TestDateTimeAssertionRule()
    {
        var engine = new AssertionInferenceEngine();
        
        var props = new Dictionary<string, string>
        {
            { "SelectedDate", "2026-06-28" },
            { "SelectedTime", "12:34:56" }
        };
        var assertions = engine.InferAssertions("DatePicker", "#datePicker", props);
        Assert.Equal(2, assertions.Count);
        Assert.Equal("assertVisible", assertions[0].Action);
        Assert.Equal("#datePicker[SelectedDate='2026-06-28']", assertions[0].Selector);
        Assert.Equal("", assertions[0].Value);
        Assert.Equal("assertVisible", assertions[1].Action);
        Assert.Equal("#datePicker[SelectedTime='12:34:56']", assertions[1].Selector);
        Assert.Equal("", assertions[1].Value);
    }

    [Fact]
    public void TestFocusAssertionRule()
    {
        var engine = new AssertionInferenceEngine();
        
        var props = new Dictionary<string, string>
        {
            { "IsFocused", "True" }
        };
        var assertions = engine.InferAssertions("TextBox", "#txtInput", props);
        
        var focusAssert = assertions.Find(a => a.Selector.Contains("IsFocused"));
        Assert.NotNull(focusAssert);
        Assert.Equal("assertVisible", focusAssert.Action);
        Assert.Equal("#txtInput[IsFocused='true']", focusAssert.Selector);
        Assert.Equal("", focusAssert.Value);
    }

    [Fact]
    public void TestEnabledDisabledAssertionRule()
    {
        var engine = new AssertionInferenceEngine();
        
        var props = new Dictionary<string, string>
        {
            { "IsEnabled", "False" }
        };
        var assertions = engine.InferAssertions("Button", "#btnClick", props);
        
        Assert.Equal("assertVisible", assertions[0].Action);
        Assert.Equal("#btnClick[IsEnabled='false']", assertions[0].Selector);
        Assert.Equal("", assertions[0].Value);
    }

    [Fact]
    public void TestContentAssertionRule()
    {
        var engine = new AssertionInferenceEngine();
        
        var props = new Dictionary<string, string>
        {
            { "Content", "Click Me" }
        };
        var assertions = engine.InferAssertions("Button", "#btnAction", props);
        
        Assert.Equal("assertVisible", assertions[0].Action);
        Assert.Equal("#btnAction[Content='Click Me']", assertions[0].Selector);
        Assert.Equal("", assertions[0].Value);
    }

    [Fact]
    public void TestHeaderAssertionRule()
    {
        var engine = new AssertionInferenceEngine();
        
        var props = new Dictionary<string, string>
        {
            { "Header", "Tab 1" }
        };
        var assertions = engine.InferAssertions("TabItem", "#tabFirst", props);
        
        Assert.Equal("assertVisible", assertions[0].Action);
        Assert.Equal("#tabFirst[Header='Tab 1']", assertions[0].Selector);
        Assert.Equal("", assertions[0].Value);
    }

    [Fact]
    public void TestPlaceholderAssertionRule()
    {
        var engine = new AssertionInferenceEngine();
        
        var props = new Dictionary<string, string>
        {
            { "PlaceholderText", "Search..." }
        };
        var assertions = engine.InferAssertions("TextBox", "#txtSearch", props);
        
        Assert.Equal("assertVisible", assertions[0].Action);
        Assert.Equal("#txtSearch[PlaceholderText='Search...']", assertions[0].Selector);
        Assert.Equal("", assertions[0].Value);
    }
}
