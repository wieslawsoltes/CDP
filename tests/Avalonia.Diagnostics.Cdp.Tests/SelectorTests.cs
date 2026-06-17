using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Xunit;

namespace Avalonia.Diagnostics.Cdp.Tests;

public class SelectorTests
{
    [AvaloniaFact]
    public void TestSelectorMatching()
    {
        var grid = new Grid();
        var border = new Border { Name = "myBorder" };
        border.Classes.Add("primary");
        border.Classes.Add("card");
        
        var button = new Button { Name = "myBtn" };
        button.Classes.Add("btn-click");

        border.Child = button;
        grid.Children.Add(border);

        // Verify Matches
        Assert.True(SelectorEngine.Matches(grid, "*"));
        Assert.True(SelectorEngine.Matches(grid, "Grid"));
        Assert.True(SelectorEngine.Matches(border, "#myBorder"));
        Assert.True(SelectorEngine.Matches(border, ".primary"));
        Assert.True(SelectorEngine.Matches(border, ".card"));
        Assert.True(SelectorEngine.Matches(border, "Border#myBorder.primary.card"));
        
        // Verify Descendant selectors
        Assert.True(SelectorEngine.Matches(button, "Grid Border Button"));
        Assert.True(SelectorEngine.Matches(button, "Border .btn-click"));
        
        // Verify Child selectors (>)
        Assert.True(SelectorEngine.Matches(border, "Grid > Border"));
        Assert.True(SelectorEngine.Matches(button, "Border > .btn-click"));
        Assert.True(SelectorEngine.Matches(button, "Grid > Border > Button"));
        Assert.False(SelectorEngine.Matches(button, "Grid > Button")); // Not a direct child

        // Verify QuerySelector
        var matched = SelectorEngine.QuerySelector(grid, "Grid > Border > .btn-click");
        Assert.Same(button, matched);

        var allMatches = SelectorEngine.QuerySelectorAll(grid, "Grid > Border");
        Assert.Single(allMatches);
        Assert.Same(border, allMatches[0]);
    }
}
