using System.Collections.Generic;
using CdpInspectorApp.Models;
using CdpInspectorApp.Services;
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
                var dict1 = new Dictionary<string, string>();
                foreach (var p in steps[i].Value.Split(','))
                {
                    var kv = p.Split(':');
                    dict1[kv[0].Trim()] = kv[1].Trim();
                }
                var dict2 = new Dictionary<string, string>();
                foreach (var p in stepsSecond[i].Value.Split(','))
                {
                    var kv = p.Split(':');
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
}
