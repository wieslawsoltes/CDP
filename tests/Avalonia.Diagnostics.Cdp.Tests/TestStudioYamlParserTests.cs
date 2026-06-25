using System.Collections.Generic;
using Xunit;

namespace Avalonia.Diagnostics.Cdp.Tests;

public class TestStudioYamlParserTests
{
    [Fact]
    public void TestParseAndGenerate()
    {
        string yaml = @"appId: ""CdpSampleApp""
description: ""Verify login and dashboard interaction""
---
- launchApp
- tapOn: ""Button#btnLogin""
- tapOn:
    x: 150
    y: 320
- inputText:
    selector: ""TextBox#txtUsername""
    text: ""admin""
- inputText: ""Hello World""
- clearText: ""TextBox#txtInput""
- clearText
- assertVisible: ""TextBlock:contains('Welcome back')""
- assertNotVisible: ""Button#hidden""
- delay: 1000
- scroll:
    direction: ""down""
    amount: 100
- scrollUntilVisible:
    selector: ""Button#btnLoadMore""
    direction: ""down""
    maxScrolls: 5
- back
";

        var steps = TestStudioYamlParser.Parse(yaml, out var appId, out var description);

        Assert.Equal("CdpSampleApp", appId);
        Assert.Equal("Verify login and dashboard interaction", description);

        Assert.Equal(13, steps.Count);

        // 1. launchApp
        Assert.Equal("launchApp", steps[0].Action);
        Assert.Equal("", steps[0].Selector);
        Assert.Equal("", steps[0].Value);

        // 2. tapOn (short form)
        Assert.Equal("tapOn", steps[1].Action);
        Assert.Equal("Button#btnLogin", steps[1].Selector);
        Assert.Equal("", steps[1].Value);

        // 3. tapOn (coordinates form)
        Assert.Equal("tapOn", steps[2].Action);
        Assert.Equal("", steps[2].Selector);
        Assert.Equal("150, 320", steps[2].Value);

        // 4. inputText (long form)
        Assert.Equal("inputText", steps[3].Action);
        Assert.Equal("TextBox#txtUsername", steps[3].Selector);
        Assert.Equal("admin", steps[3].Value);

        // 5. inputText (short form)
        Assert.Equal("inputText", steps[4].Action);
        Assert.Equal("", steps[4].Selector);
        Assert.Equal("Hello World", steps[4].Value);

        // 6. clearText (with selector)
        Assert.Equal("clearText", steps[5].Action);
        Assert.Equal("TextBox#txtInput", steps[5].Selector);
        Assert.Equal("", steps[5].Value);

        // 7. clearText (short form)
        Assert.Equal("clearText", steps[6].Action);
        Assert.Equal("", steps[6].Selector);
        Assert.Equal("", steps[6].Value);

        // 8. assertVisible
        Assert.Equal("assertVisible", steps[7].Action);
        Assert.Equal("TextBlock:contains('Welcome back')", steps[7].Selector);
        Assert.Equal("", steps[7].Value);

        // 9. assertNotVisible
        Assert.Equal("assertNotVisible", steps[8].Action);
        Assert.Equal("Button#hidden", steps[8].Selector);
        Assert.Equal("", steps[8].Value);

        // 10. delay
        Assert.Equal("delay", steps[9].Action);
        Assert.Equal("", steps[9].Selector);
        Assert.Equal("1000", steps[9].Value);

        // 11. scroll (long form)
        Assert.Equal("scroll", steps[10].Action);
        Assert.Equal("", steps[10].Selector);
        Assert.Contains("direction: down", steps[10].Value);
        Assert.Contains("amount: 100", steps[10].Value);

        // 12. scrollUntilVisible
        Assert.Equal("scrollUntilVisible", steps[11].Action);
        Assert.Equal("Button#btnLoadMore", steps[11].Selector);
        Assert.Contains("direction: down", steps[11].Value);
        Assert.Contains("maxScrolls: 5", steps[11].Value);

        // 13. back
        Assert.Equal("back", steps[12].Action);
        Assert.Equal("", steps[12].Selector);
        Assert.Equal("", steps[12].Value);

        // Now test generating back to YAML
        var generatedYaml = TestStudioYamlParser.Generate(steps, appId, description);

        // Parse generated YAML again and assert correctness
        var stepsSecond = TestStudioYamlParser.Parse(generatedYaml, out var appIdSecond, out var descSecond);

        Assert.Equal(appId, appIdSecond);
        Assert.Equal(description, descSecond);
        Assert.Equal(steps.Count, stepsSecond.Count);

        for (int i = 0; i < steps.Count; i++)
        {
            Assert.Equal(steps[i].Action, stepsSecond[i].Action);
            Assert.Equal(steps[i].Selector, stepsSecond[i].Selector);
            // Since order of keys in dictionary generated string might vary or format is parsed back, let's assert key equality
            if (steps[i].Action == "scroll" || steps[i].Action == "scrollUntilVisible")
            {
                var val1 = steps[i].Value ?? "";
                var val2 = stepsSecond[i].Value ?? "";
                var dict1 = new Dictionary<string, string>();
                foreach (var p in val1.Split(','))
                {
                    if (string.IsNullOrEmpty(p)) continue;
                    var kv = p.Split(':');
                    if (kv.Length == 2)
                        dict1[kv[0].Trim()] = kv[1].Trim();
                }
                var dict2 = new Dictionary<string, string>();
                foreach (var p in val2.Split(','))
                {
                    if (string.IsNullOrEmpty(p)) continue;
                    var kv = p.Split(':');
                    if (kv.Length == 2)
                        dict2[kv[0].Trim()] = kv[1].Trim();
                }
                Assert.Equal(dict1.Count, dict2.Count);
                foreach (var kv in dict1)
                {
                    Assert.Equal(kv.Value, dict2[kv.Key]);
                }
            }
            else
            {
                Assert.Equal(steps[i].Value, stepsSecond[i].Value);
            }
        }
    }

    [Fact]
    public void TestNewFlowCommands()
    {
        string yaml = @"appId: ""CdpSampleApp""
description: ""Verify new flow commands""
---
- doubleTapOn: ""#btnDouble""
- longPressOn: ""#btnLong""
- pasteText: ""pasted text""
- eraseText: 5
- swipe:
    direction: ""left""
- stopApp
- killApp: ""myApp""
- clearState: ""myApp""
- setOrientation: ""landscape""
- setLocation:
    latitude: 37.7749
    longitude: -122.4194
- takeScreenshot: ""my_screenshot.png""
- assertTrue: ""1 == 1""
- evalScript: ""console.log('hello')""
- repeat: 3
- retry: 2
- runFlow: ""nested_flow.yaml""
- openLink: ""https://google.com""
- copyTextFrom: ""#myLabel""
";

        var steps = TestStudioYamlParser.Parse(yaml, out var appId, out var description);

        Assert.Equal("CdpSampleApp", appId);
        Assert.Equal("Verify new flow commands", description);
        Assert.Equal(18, steps.Count);

        Assert.Equal("doubleTapOn", steps[0].Action);
        Assert.Equal("#btnDouble", steps[0].Selector);

        Assert.Equal("longPressOn", steps[1].Action);
        Assert.Equal("#btnLong", steps[1].Selector);

        Assert.Equal("pasteText", steps[2].Action);
        Assert.Equal("pasted text", steps[2].Value);

        Assert.Equal("eraseText", steps[3].Action);
        Assert.Equal("5", steps[3].Value);

        Assert.Equal("swipe", steps[4].Action);
        Assert.Contains("direction: left", steps[4].Value);

        Assert.Equal("stopApp", steps[5].Action);

        Assert.Equal("killApp", steps[6].Action);
        Assert.Equal("myApp", steps[6].Value);

        Assert.Equal("clearState", steps[7].Action);
        Assert.Equal("myApp", steps[7].Value);

        Assert.Equal("setOrientation", steps[8].Action);
        Assert.Equal("landscape", steps[8].Value);

        Assert.Equal("setLocation", steps[9].Action);
        Assert.Contains("latitude: 37.7749", steps[9].Value);
        Assert.Contains("longitude: -122.4194", steps[9].Value);

        Assert.Equal("takeScreenshot", steps[10].Action);
        Assert.Equal("my_screenshot.png", steps[10].Value);

        Assert.Equal("assertTrue", steps[11].Action);
        Assert.Equal("1 == 1", steps[11].Value);

        Assert.Equal("evalScript", steps[12].Action);
        Assert.Equal("console.log('hello')", steps[12].Value);

        Assert.Equal("repeat", steps[13].Action);
        Assert.Equal("3", steps[13].Value);

        Assert.Equal("retry", steps[14].Action);
        Assert.Equal("2", steps[14].Value);

        Assert.Equal("runFlow", steps[15].Action);
        Assert.Equal("nested_flow.yaml", steps[15].Value);

        Assert.Equal("openLink", steps[16].Action);
        Assert.Equal("https://google.com", steps[16].Value);

        Assert.Equal("copyTextFrom", steps[17].Action);
        Assert.Equal("#myLabel", steps[17].Selector);

        // Test generation round-trip
        var gen = TestStudioYamlParser.Generate(steps, appId, description);
        var stepsGen = TestStudioYamlParser.Parse(gen, out var appIdGen, out var descGen);

        Assert.Equal(appId, appIdGen);
        Assert.Equal(description, descGen);
        Assert.Equal(steps.Count, stepsGen.Count);

        for (int i = 0; i < steps.Count; i++)
        {
            Assert.Equal(steps[i].Action, stepsGen[i].Action);
            Assert.Equal(steps[i].Selector, stepsGen[i].Selector);
        }
    }

    [Fact]
    public void TestPRCommentFixes()
    {
        // 1. Parsing selectorless inputText and clearText
        string selectorlessYaml = @"
- inputText: ""Hello without selector""
- clearText
";
        var selectorlessSteps = TestStudioYamlParser.Parse(selectorlessYaml, out _, out _);
        Assert.Equal(2, selectorlessSteps.Count);
        Assert.Equal("inputText", selectorlessSteps[0].Action);
        Assert.Equal("", selectorlessSteps[0].Selector);
        Assert.Equal("Hello without selector", selectorlessSteps[0].Value);

        Assert.Equal("clearText", selectorlessSteps[1].Action);
        Assert.Equal("", selectorlessSteps[1].Selector);

        // 2 & 3. Targeted scrolls (selector & properties)
        string scrollWithSelectorYaml = @"
- scroll:
    selector: ""#scrollableContainer""
    direction: ""down""
    amount: 250
";
        var scrollSteps = TestStudioYamlParser.Parse(scrollWithSelectorYaml, out _, out _);
        Assert.Single(scrollSteps);
        Assert.Equal("scroll", scrollSteps[0].Action);
        Assert.Equal("#scrollableContainer", scrollSteps[0].Selector);
        Assert.Contains("direction: down", scrollSteps[0].Value);
        Assert.Contains("amount: 250", scrollSteps[0].Value);

        var generatedScroll = TestStudioYamlParser.Generate(scrollSteps, "", "");
        var parsedScroll = TestStudioYamlParser.Parse(generatedScroll, out _, out _);
        Assert.Single(parsedScroll);
        Assert.Equal("scroll", parsedScroll[0].Action);
        Assert.Equal("#scrollableContainer", parsedScroll[0].Selector);
        Assert.Contains("direction: down", parsedScroll[0].Value);
        Assert.Contains("amount: 250", parsedScroll[0].Value);

        // 4. Invalid YAML throws exception
        string invalidYaml = @"
- unclosed_quote: ""string
";
        Assert.ThrowsAny<Exception>(() => TestStudioYamlParser.Parse(invalidYaml, out _, out _));
    }

    [Fact]
    public void TestAdditionalFlowCommandsAndNesting()
    {
        string yaml = @"appId: ""CdpSampleApp""
description: ""Verify additional commands and nesting""
---
- assertFalse: ""1 == 2""
- setAirplaneMode: ""on""
- repeat:
    times: 5
    while:
      notVisible: ""#hiddenBtn""
    commands:
      - tapOn: ""#btnNext""
      - delay: 500
- retry:
    maxRetries: 3
    commands:
      - tapOn: ""#btnRetry""
";

        var steps = TestStudioYamlParser.Parse(yaml, out var appId, out var description);

        Assert.Equal("CdpSampleApp", appId);
        Assert.Equal("Verify additional commands and nesting", description);
        Assert.Equal(4, steps.Count);

        // 1. assertFalse
        Assert.Equal("assertFalse", steps[0].Action);
        Assert.Equal("1 == 2", steps[0].Value);

        // 2. setAirplaneMode
        Assert.Equal("setAirplaneMode", steps[1].Action);
        Assert.Equal("on", steps[1].Value);

        // 3. repeat
        Assert.Equal("repeat", steps[2].Action);
        Assert.Equal("5", steps[2].Value);
        Assert.Equal("notVisible", steps[2].WhileConditionType);
        Assert.Equal("#hiddenBtn", steps[2].WhileConditionValue);
        Assert.NotNull(steps[2].NestedSteps);
        var repeatNested = steps[2].NestedSteps!;
        Assert.Equal(2, repeatNested.Count);
        Assert.Equal("tapOn", repeatNested[0].Action);
        Assert.Equal("#btnNext", repeatNested[0].Selector);
        Assert.Equal("delay", repeatNested[1].Action);
        Assert.Equal("500", repeatNested[1].Value);

        // 4. retry
        Assert.Equal("retry", steps[3].Action);
        Assert.Equal("3", steps[3].Value);
        Assert.NotNull(steps[3].NestedSteps);
        var retryNested = steps[3].NestedSteps!;
        Assert.Single(retryNested);
        Assert.Equal("tapOn", retryNested[0].Action);
        Assert.Equal("#btnRetry", retryNested[0].Selector);

        // Roundtrip test
        var gen = TestStudioYamlParser.Generate(steps, appId, description);
        var stepsGen = TestStudioYamlParser.Parse(gen, out var appIdGen, out var descGen);

        Assert.Equal(appId, appIdGen);
        Assert.Equal(description, descGen);
        Assert.Equal(steps.Count, stepsGen.Count);

        Assert.Equal("assertFalse", stepsGen[0].Action);
        Assert.Equal("1 == 2", stepsGen[0].Value);

        Assert.Equal("setAirplaneMode", stepsGen[1].Action);
        Assert.Equal("on", stepsGen[1].Value);

        Assert.Equal("repeat", stepsGen[2].Action);
        Assert.Equal("5", stepsGen[2].Value);
        Assert.Equal("notVisible", stepsGen[2].WhileConditionType);
        Assert.Equal("#hiddenBtn", stepsGen[2].WhileConditionValue);
        Assert.NotNull(stepsGen[2].NestedSteps);
        Assert.Equal(2, stepsGen[2].NestedSteps!.Count);

        Assert.Equal("retry", stepsGen[3].Action);
        Assert.Equal("3", stepsGen[3].Value);
        Assert.NotNull(stepsGen[3].NestedSteps);
        Assert.Single(stepsGen[3].NestedSteps!);
    }

    [Fact]
    public void TestFullFlowCommandCatalogParses()
    {
        var yamlLines = new List<string>();
        foreach (var command in FlowCommandCatalog.PublicCommands)
        {
            yamlLines.Add(command.ValueKind == FlowCommandValueKind.None
                ? $"- {command.Name}"
                : $"- {command.Name}: \"sample\"");
        }

        var steps = TestStudioYamlParser.Parse(string.Join("\n", yamlLines), out _, out _);

        Assert.Equal(FlowCommandCatalog.PublicCommands.Count, steps.Count);
        foreach (var command in FlowCommandCatalog.PublicCommands)
        {
            Assert.Contains(steps, step => step.Action == command.Name);
        }
    }

    [Fact]
    public void TestStructuredSelectorMapsRoundTrip()
    {
        string yaml = @"
- tapOn:
    text: ""Log in""
    enabled: true
    below:
      id: ""header""
    repeat: 2
    delay: 100
- assertVisible:
    id: ""submit_button""
    selected: false
    width: 120
    height: 48
    tolerance: 2
- scrollUntilVisible:
    element:
      text: ""Receipt""
      traits: long-text
    direction: DOWN
    timeout: 30000
    visibilityPercentage: 80
    centerElement: true
";

        var steps = TestStudioYamlParser.Parse(yaml, out _, out _);

        Assert.Equal(3, steps.Count);
        Assert.Equal("tapOn", steps[0].Action);
        Assert.True(steps[0].Parameters.ContainsKey("text"));
        Assert.True(steps[0].Parameters.ContainsKey("below"));
        Assert.Contains("text: Log in", steps[0].Selector);
        Assert.Contains("repeat: 2", steps[0].Value);

        Assert.Equal("assertVisible", steps[1].Action);
        Assert.Contains("id: submit_button", steps[1].Selector);
        Assert.Contains("tolerance: 2", steps[1].Selector);

        Assert.Equal("scrollUntilVisible", steps[2].Action);
        Assert.True(steps[2].Parameters.ContainsKey("element"));
        Assert.Contains("element", TestStudioYamlParser.Generate(steps, "", ""));

        var generated = TestStudioYamlParser.Generate(steps, "", "");
        var roundTrip = TestStudioYamlParser.Parse(generated, out _, out _);
        Assert.Equal(steps.Count, roundTrip.Count);
        Assert.True(roundTrip[0].Parameters.ContainsKey("below"));
        Assert.True(roundTrip[2].Parameters.ContainsKey("element"));
    }
}
