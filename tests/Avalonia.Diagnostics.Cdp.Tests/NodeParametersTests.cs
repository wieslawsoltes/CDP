using System;
using System.Linq;
using Xunit;
using CdpInspectorApp.Models;
using CdpInspectorApp.ViewModels;

namespace Avalonia.Diagnostics.Cdp.Tests;

public class NodeParametersTests
{
    [Fact]
    public void TestStudioNodeViewModel_DynamicVisibilityByCommand()
    {
        var node = new TestStudioNodeViewModel
        {
            Action = "back"
        };

        // 'back' command requires no parameters, value, or selector
        Assert.False(node.ShowSelector);
        Assert.False(node.ShowValue);
        Assert.False(node.ShowCustomParams);

        // 'tapOn' command accepts selector
        node.Action = "tapOn";
        Assert.True(node.ShowSelector);
        Assert.False(node.ShowValue);

        // 'inputText' command accepts string value
        node.Action = "inputText";
        Assert.True(node.ShowValue);
    }

    [Fact]
    public void TestStudioNodeViewModel_CustomParametersPopulatedAndSync()
    {
        var step = new TestStudioStepModel
        {
            Action = "assertScreenshot"
        };

        var node = new TestStudioNodeViewModel
        {
            Step = step
        };

        // 'assertScreenshot' parameters: path, cropOn, thresholdPercentage, label
        Assert.True(node.ShowCustomParams);
        Assert.Equal(4, node.CustomParameters.Count);

        var pathParam = node.CustomParameters.First(p => p.Name == "path");
        Assert.Null(pathParam.Value);

        // Set value on custom parameter editor
        pathParam.Value = "screenshot.png";

        // Should propagate to the step parameters dictionary
        Assert.Equal("screenshot.png", step.Parameters["path"]);

        // Setting directly on step parameters should update custom parameter value
        step.Parameters = new System.Collections.Generic.Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["path"] = "updated_screenshot.png"
        };
        Assert.Equal("updated_screenshot.png", pathParam.Value);
    }
}
