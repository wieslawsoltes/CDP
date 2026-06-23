using CdpInspectorApp.Services;
using CdpInspectorApp.Models;
using Xunit;

namespace Avalonia.Diagnostics.Cdp.Tests;

public class YamlIntelliSenseTests
{
    [Theory]
    [InlineData("tapOn:", true)]
    [InlineData("doubleTapOn:", true)]
    [InlineData("inputText:", true)]
    [InlineData("back", true)]
    [InlineData("selector:", false)] // parameter, not a command
    [InlineData("text:", false)]
    [InlineData("appId:", false)]
    public void TestIsCommand(string suggestion, bool expected)
    {
        Assert.Equal(expected, YamlIntelliSenseProvider.IsCommand(suggestion));
    }

    [Fact]
    public void TestGetProperIndentation_Empty()
    {
        string yaml = "ta";
        int indent = YamlIntelliSenseProvider.GetProperIndentation(yaml, 2);
        Assert.Equal(0, indent);
    }

    [Fact]
    public void TestGetProperIndentation_AfterSequenceItem()
    {
        string yaml = @"- launchApp
  ta";
        int caretOffset = yaml.IndexOf("ta") + 2;
        int indent = YamlIntelliSenseProvider.GetProperIndentation(yaml, caretOffset);
        Assert.Equal(0, indent); // Aligns with root - launchApp which has 0 indent
    }

    [Fact]
    public void TestGetProperIndentation_AfterIndentedSequenceItem()
    {
        string yaml = @"- repeat:
    times: 3
    commands:
      - tapOn: ""#btn""
      ta";
        int caretOffset = yaml.IndexOf("ta") + 2;
        int indent = YamlIntelliSenseProvider.GetProperIndentation(yaml, caretOffset);
        Assert.Equal(6, indent); // Aligns with the previous list item which has 6 spaces indent
    }

    [Fact]
    public void TestGetProperIndentation_AfterCommandsHeader()
    {
        string yaml = @"- repeat:
    times: 3
    commands:
      ta";
        int caretOffset = yaml.IndexOf("ta") + 2;
        int indent = YamlIntelliSenseProvider.GetProperIndentation(yaml, caretOffset);
        Assert.Equal(6, indent); // commands has 4 spaces, so children list items should have 4 + 2 = 6 spaces
    }

    [Fact]
    public void TestGetProperIndentation_AcrossDocumentSeparator()
    {
        string yaml = @"appId: com.test
---
- repeat:
    commands:
      - tapOn: ""#btn""
---
ta";
        int caretOffset = yaml.LastIndexOf("ta") + 2;
        int indent = YamlIntelliSenseProvider.GetProperIndentation(yaml, caretOffset);
        Assert.Equal(0, indent); // Resets to 0 because of --- separator
    }

    [Fact]
    public void TestCatalogCommandsAreSuggested()
    {
        var suggestions = YamlIntelliSenseProvider.GetSuggestions("-", 1, null);

        foreach (var command in FlowCommandCatalog.PublicCommands)
        {
            var expected = command.ValueKind == FlowCommandValueKind.None ? command.Name : $"{command.Name}:";
            Assert.Contains(expected, suggestions);
        }
    }

    [Fact]
    public void TestSelectorMapParametersAreSuggested()
    {
        string yaml = @"- tapOn:
    ";
        var suggestions = YamlIntelliSenseProvider.GetSuggestions(yaml, yaml.Length, null);

        Assert.Contains("text:", suggestions);
        Assert.Contains("id:", suggestions);
        Assert.Contains("point:", suggestions);
        Assert.Contains("enabled:", suggestions);
        Assert.Contains("containsChild:", suggestions);
    }
}
