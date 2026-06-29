using Avalonia.Controls;
using Avalonia.Automation;
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
        var appiumGen = new AppiumCSharpGenerator();
        var steps = new System.Collections.Generic.List<CdpInspectorApp.Models.RecordedStepModel>
        {
            new CdpInspectorApp.Models.RecordedStepModel { Type = "click", Selector = "[AccessibilityId=\"myBtnId\"]" }
        };
        string generated = appiumGen.Generate(steps.Select(s => s.ToCoreStep()), "localhost:9222");
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

        Assert.Equal("[AccessibilityId=\"myBtnId\"]", clientDomGen.GenerateSelector(clientBtn));
        Assert.Equal("[AccessibilityId=\"myBtnId\"]", clientAutoGen.GenerateSelector(clientBtn));
    }

    [AvaloniaFact]
    public void TestAttributeSelectorsAreStrictAndAgentFriendly()
    {
        var panel = new StackPanel();
        var namedButton = new Button { Name = "btnClickMe", Content = "Click Me" };
        namedButton.Classes.Add("primary");
        namedButton.SetValue(AutomationProperties.AutomationIdProperty, "btnAutomation");

        var unnamedButton = new Button { Content = "Other" };
        panel.Children.Add(namedButton);
        panel.Children.Add(unnamedButton);

        Assert.Same(namedButton, SelectorEngine.QuerySelector(panel, "#btnClickMe"));
        Assert.Same(namedButton, SelectorEngine.QuerySelector(panel, "[id=\"btnClickMe\"]"));
        Assert.Same(namedButton, SelectorEngine.QuerySelector(panel, "[Id=\"btnClickMe\"]"));
        Assert.Same(namedButton, SelectorEngine.QuerySelector(panel, "[Name=\"btnClickMe\"]"));
        Assert.Same(namedButton, SelectorEngine.QuerySelector(panel, "[AccessibilityId=\"btnAutomation\"]"));
        Assert.Same(namedButton, SelectorEngine.QuerySelector(panel, "[AutomationId=\"btnAutomation\"]"));
        Assert.Same(namedButton, SelectorEngine.QuerySelector(panel, "[AutomationProperties.AutomationId=\"btnAutomation\"]"));
        Assert.Same(namedButton, SelectorEngine.QuerySelector(panel, "[class~=\"primary\"]"));
        Assert.Same(namedButton, SelectorEngine.QuerySelector(panel, "[Text=\"Click Me\"]"));

        var idMatches = SelectorEngine.QuerySelectorAll(panel, "[id]");
        Assert.Single(idMatches);
        Assert.Same(namedButton, idMatches[0]);

        var automationMatches = SelectorEngine.QuerySelectorAll(panel, "[AutomationId]");
        Assert.Single(automationMatches);
        Assert.Same(namedButton, automationMatches[0]);

        Assert.Null(SelectorEngine.QuerySelector(panel, "[AutomationId=\"missing\"]"));
        Assert.Null(SelectorEngine.QuerySelector(panel, "[UnknownAttribute=\"btnAutomation\"]"));
        Assert.Null(SelectorEngine.QuerySelector(panel, "[]"));
        Assert.Null(SelectorEngine.QuerySelector(panel, "[=btnAutomation]"));
    }

    [AvaloniaFact]
    public void TestSelectorFallbackBehavior()
    {
        // Test that server-side generators correctly generate contains-text selectors as fallbacks when name/automation is missing.
        var panel = new StackPanel();
        var label = new Label { Content = "Submit Form" };
        panel.Children.Add(label);

        var domGen = SelectorRegistry.GetGenerator("dom");
        var autoGen = SelectorRegistry.GetGenerator("automation");

        Assert.Equal("StackPanel > Label:contains(\"Submit Form\")", domGen.GenerateSelector(label));
        Assert.Equal("StackPanel > Label:contains(\"Submit Form\")", autoGen.GenerateSelector(label));

        // Test that client-side generators correctly generate contains-text selectors as fallbacks.
        var clientLabel = new CdpInspectorApp.Models.DomNodeModel(3, "Label");
        clientLabel.AttributesList.Add(new CdpInspectorApp.Models.AttributeModel("text", "Submit Form"));
        
        var clientDomGen = CdpInspectorApp.Services.ClientSelectorRegistry.GetGenerator("dom");
        var clientAutoGen = CdpInspectorApp.Services.ClientSelectorRegistry.GetGenerator("automation");

        Assert.Equal("Label", clientDomGen.GenerateSelector(clientLabel));
        Assert.Equal("Label:contains(\"Submit Form\")", clientAutoGen.GenerateSelector(clientLabel));
    }

    [AvaloniaFact]
    public void TestEmptyAttributeMatching()
    {
        var panel = new StackPanel();
        var emptyTextBox = new TextBox { Name = "txtInput", Text = "" };
        var nonEmptyTextBox = new TextBox { Text = "Something" };
        panel.Children.Add(emptyTextBox);
        panel.Children.Add(nonEmptyTextBox);

        // Verify empty text matching
        Assert.True(SelectorEngine.Matches(emptyTextBox, "#txtInput[Text=\"\"]"));
        Assert.Same(emptyTextBox, SelectorEngine.QuerySelector(panel, "#txtInput[Text=\"\"]"));
        Assert.False(SelectorEngine.Matches(nonEmptyTextBox, "TextBox[Text=\"\"]"));
    }

    [AvaloniaFact]
    public void TestTabItemHeaderMatching()
    {
        var tabItem = new TabItem { Name = "tabScroll", Header = "Scroll Test" };
        Assert.True(SelectorEngine.Matches(tabItem, "#tabScroll[Header=\"Scroll Test\"]"));
    }
}
