using System;
using System.Collections.Generic;
using Xunit;
using CdpInspectorApp.Services;
using CdpInspectorApp.ViewModels;

namespace Avalonia.Diagnostics.Cdp.Tests;

public class DiffTests
{
    [Fact]
    public void Test_DiffEngine_Unchanged()
    {
        string text = "Line 1\nLine 2\nLine 3";
        var diff = DiffEngine.ComputeDiff(text, text);

        Assert.Equal(3, diff.Count);
        foreach (var line in diff)
        {
            Assert.Equal(DiffType.Unchanged, line.Type);
        }
        Assert.Equal("Line 1", diff[0].Text);
        Assert.Equal("Line 2", diff[1].Text);
        Assert.Equal("Line 3", diff[2].Text);
    }

    [Fact]
    public void Test_DiffEngine_Deleted()
    {
        string left = "Line 1\nLine 2\nLine 3";
        string right = "Line 1\nLine 3";
        var diff = DiffEngine.ComputeDiff(left, right);

        Assert.Equal(3, diff.Count);
        Assert.Equal(DiffType.Unchanged, diff[0].Type);
        Assert.Equal(DiffType.Deleted, diff[1].Type);
        Assert.Equal("Line 2", diff[1].Text);
        Assert.Equal(DiffType.Unchanged, diff[2].Type);
    }

    [Fact]
    public void Test_DiffEngine_Added()
    {
        string left = "Line 1\nLine 3";
        string right = "Line 1\nLine 2\nLine 3";
        var diff = DiffEngine.ComputeDiff(left, right);

        Assert.Equal(3, diff.Count);
        Assert.Equal(DiffType.Unchanged, diff[0].Type);
        Assert.Equal(DiffType.Added, diff[1].Type);
        Assert.Equal("Line 2", diff[1].Text);
        Assert.Equal(DiffType.Unchanged, diff[2].Type);
    }

    [Fact]
    public void Test_DiffViewModel_State_Management()
    {
        var vm = new DiffViewModel();
        Assert.False(vm.IsInlineMode);

        vm.SetCompareTexts("Left Title", "Line 1", "Right Title", "Line 2");
        Assert.Equal("Left Title", vm.LeftTitle);
        Assert.Equal("Right Title", vm.RightTitle);
        Assert.Equal("Line 1", vm.LeftText);
        Assert.Equal("Line 2", vm.RightText);

        Assert.Equal(2, vm.DiffLines.Count);
        Assert.Equal(DiffType.Deleted, vm.DiffLines[0].Type);
        Assert.Equal(DiffType.Added, vm.DiffLines[1].Type);

        vm.ToggleDiffModeCommand.Execute(null);
        Assert.True(vm.IsInlineMode);

        vm.ClearDiffCommand.Execute(null);
        Assert.Equal("", vm.LeftText);
        Assert.Equal("", vm.RightText);
        Assert.Empty(vm.DiffLines);
    }

    [Fact]
    public void Test_DiffEngine_IntraLine_Highlights()
    {
        string left = "\"status\": \"pending\"";
        string right = "\"status\": \"success\"";
        var diff = DiffEngine.ComputeDiff(left, right);

        // Should pair them as 1 deletion and 1 addition with computed offsets
        Assert.Equal(2, diff.Count);
        
        var deletedLine = diff[0];
        var addedLine = diff[1];

        Assert.Equal(DiffType.Deleted, deletedLine.Type);
        Assert.Equal(DiffType.Added, addedLine.Type);

        // Since 'e' is common in "pending" and "success", character-level LCS matches 'e'.
        // This splits changes into two blocks: before 'e' and after 'e'.
        Assert.Equal(2, deletedLine.ChangeRanges.Count);
        Assert.Equal(11, deletedLine.ChangeRanges[0].Offset); // "p"
        Assert.Equal(1, deletedLine.ChangeRanges[0].Length);
        Assert.Equal(13, deletedLine.ChangeRanges[1].Offset); // "nding"
        Assert.Equal(5, deletedLine.ChangeRanges[1].Length);

        Assert.Equal(2, addedLine.ChangeRanges.Count);
        Assert.Equal(11, addedLine.ChangeRanges[0].Offset); // "succ"
        Assert.Equal(4, addedLine.ChangeRanges[0].Length);
        Assert.Equal(16, addedLine.ChangeRanges[1].Offset); // "ss"
        Assert.Equal(2, addedLine.ChangeRanges[1].Length);
    }

    [Fact]
    public void Test_DiffEngine_Performance_Stress()
    {
        // Generate 5000 matching lines prefix, 1 modified line, 5000 matching lines suffix
        var leftLines = new List<string>(10001);
        var rightLines = new List<string>(10001);

        for (int i = 0; i < 5000; i++)
        {
            string line = $"Matching prefix line {i}";
            leftLines.Add(line);
            rightLines.Add(line);
        }

        leftLines.Add("Original line in the middle");
        rightLines.Add("Modified line in the middle");

        for (int i = 0; i < 5000; i++)
        {
            string line = $"Matching suffix line {i}";
            leftLines.Add(line);
            rightLines.Add(line);
        }

        string leftText = string.Join("\n", leftLines);
        string rightText = string.Join("\n", rightLines);

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var diff = DiffEngine.ComputeDiff(leftText, rightText);
        stopwatch.Stop();

        // Ensure it executes extremely fast due to prefix/suffix trimming (typically < 10ms)
        Assert.True(stopwatch.ElapsedMilliseconds < 50, $"Diff engine stress test took too long: {stopwatch.ElapsedMilliseconds}ms");
        Assert.Equal(10002, diff.Count);
        
        Assert.Equal(DiffType.Deleted, diff[5000].Type);
        Assert.Equal(DiffType.Added, diff[5001].Type);
    }
}
