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

    [AvaloniaFact]
    public void TestTextBasedSelectors()
    {
        var panel = new StackPanel();
        var textBlock = new TextBlock { Text = "Welcome to CDP" };
        var button = new Button { Content = "Click Me Now" };
        var textBox = new TextBox { Text = "Search Query" };

        panel.Children.Add(textBlock);
        panel.Children.Add(button);
        panel.Children.Add(textBox);

        // 1. Verify :contains() pseudo-class
        Assert.True(SelectorEngine.Matches(textBlock, "TextBlock:contains(\"Welcome\")"));
        Assert.True(SelectorEngine.Matches(button, "Button:contains('Click Me')"));
        Assert.True(SelectorEngine.Matches(textBox, "TextBox:contains(Search)"));
        Assert.False(SelectorEngine.Matches(button, "Button:contains(\"Welcome\")"));

        // 2. Verify fallback text search on plain string/quoted selector
        Assert.Same(textBlock, SelectorEngine.QuerySelector(panel, "Welcome to CDP"));
        Assert.Same(button, SelectorEngine.QuerySelector(panel, "\"Click Me Now\""));
        Assert.Same(textBox, SelectorEngine.QuerySelector(panel, "'Search Query'"));

        // 3. Verify fallback text search case-insensitivity
        Assert.True(SelectorEngine.Matches(textBlock, "welcome to cdp"));
        Assert.Same(textBlock, SelectorEngine.QuerySelector(panel, "welcome to cdp"));
        
        // 4. Verify multiple contains pseudo-classes
        Assert.True(SelectorEngine.Matches(button, "Button:contains(Click):contains(Me)"));
        Assert.False(SelectorEngine.Matches(button, "Button:contains(Click):contains(Cancel)"));
    }

    [AvaloniaFact]
    public void TestSelectorGeneratorsAndTranslation()
    {
        var panel = new StackPanel();
        var border = new Border { Name = "myBorder" };
        panel.Children.Add(border);

        var button = new Button { Name = "myBtn" };
        button.SetValue(Avalonia.Automation.AutomationProperties.AutomationIdProperty, "myBtnId");
        border.Child = button;

        // 1. Verify Server-Side Generators
        var domGen = SelectorRegistry.GetGenerator("dom");
        var autoGen = SelectorRegistry.GetGenerator("automation");

        Assert.Equal("#myBtn", domGen.GenerateSelector(button));
        Assert.Equal("[AccessibilityId=\"myBtnId\"]", autoGen.GenerateSelector(button));

        // 2. Verify SelectorEngine query using Automation Selector
        Assert.True(SelectorEngine.Matches(button, "[AccessibilityId=\"myBtnId\"]"));
        Assert.Same(button, SelectorEngine.QuerySelector(panel, "[AccessibilityId=\"myBtnId\"]"));

        // 3. Verify Appium C# translation
        var appiumGen = new CdpInspectorApp.Services.AppiumCSharpGenerator();
        var steps = new System.Collections.Generic.List<CdpInspectorApp.Models.RecordedStepModel>
        {
            new CdpInspectorApp.Models.RecordedStepModel { Type = "click", Selector = "[AccessibilityId=\"myBtnId\"]" }
        };
        string generated = appiumGen.Generate(steps, "localhost:9222");
        Assert.Contains("_driver.FindElementByAccessibilityId(\"myBtnId\")", generated);

        // 4. Verify Client-Side Generators
        var clientBtn = new CdpInspectorApp.Models.DomNodeModel(1, "Button");
        clientBtn.AttributesList.Add(new CdpInspectorApp.Models.AttributeModel("AccessibilityId", "myBtnId"));

        var clientBorder = new CdpInspectorApp.Models.DomNodeModel(2, "Border");
        clientBorder.AttributesList.Add(new CdpInspectorApp.Models.AttributeModel("id", "myBorder"));

        clientBtn.Parent = clientBorder;
        clientBorder.Children.Add(clientBtn);

        var clientDomGen = CdpInspectorApp.Services.ClientSelectorRegistry.GetGenerator("dom");
        var clientAutoGen = CdpInspectorApp.Services.ClientSelectorRegistry.GetGenerator("automation");

        Assert.Equal("#myBorder > Button", clientDomGen.GenerateSelector(clientBtn));
        Assert.Equal("[AccessibilityId=\"myBtnId\"]", clientAutoGen.GenerateSelector(clientBtn));
    }
}

