using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Automation;
using Avalonia.Headless.XUnit;
using System.Net.WebSockets;
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
        Assert.Contains("_driver.FindElement(MobileBy.AccessibilityId(\"myBtnId\"))", generated);

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

        Assert.Equal("Label:contains(\"Submit Form\")", clientDomGen.GenerateSelector(clientLabel));
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

    [AvaloniaFact]
    public void TestPopupAndContextMenuSelectorSupport()
    {
        var menuItem = new MenuItem { Header = "Right Click Option 1" };
        var comboItem = new ComboBoxItem { Content = "Popup Option 2" };

        var domGen = SelectorRegistry.GetGenerator("dom");
        Assert.Equal("MenuItem:contains(\"Right Click Option 1\")", domGen.GenerateSelector(menuItem));
        Assert.Equal("ComboBoxItem:contains(\"Popup Option 2\")", domGen.GenerateSelector(comboItem));

        var clientMenu = new CdpInspectorApp.Models.DomNodeModel(10, "MenuItem");
        clientMenu.AttributesList.Add(new CdpInspectorApp.Models.AttributeModel("Header", "Right Click Option 1"));

        var clientCombo = new CdpInspectorApp.Models.DomNodeModel(11, "ComboBoxItem");
        clientCombo.AttributesList.Add(new CdpInspectorApp.Models.AttributeModel("Content", "Popup Option 2"));

        var clientDomGen = CdpInspectorApp.Services.ClientSelectorRegistry.GetGenerator("dom");
        Assert.Equal("MenuItem:contains(\"Right Click Option 1\")", clientDomGen.GenerateSelector(clientMenu));
        Assert.Equal("ComboBoxItem:contains(\"Popup Option 2\")", clientDomGen.GenerateSelector(clientCombo));
    }

    [AvaloniaFact]
    public async Task TestPopupAndContextMenuAccessibilityTreeInspection()
    {
        var window = new Window { Title = "Main App Window" };
        window.Show();
        CdpServer.EnsureInitialized();
        CdpServer.GetOrCreateTarget(window, "test-target-id");

        var rootPanel = new StackPanel();
        var popupBtn = new Button { Name = "btnPopup", Content = "Open Popup" };
        var popup = new Popup { IsOpen = true };
        var popupStack = new StackPanel();
        var menuItem1 = new MenuItem { Header = "Popup MenuItem 1" };
        var menuItem2 = new MenuItem { Header = "Popup MenuItem 2" };
        popupStack.Children.Add(menuItem1);
        popupStack.Children.Add(menuItem2);
        popup.Child = popupStack;

        rootPanel.Children.Add(popupBtn);
        rootPanel.Children.Add(popup);
        window.Content = rootPanel;

        using var fakeWs = new ClientWebSocket();
        var session = new CdpSession(fakeWs, window);

        var axResponse = await Domains.AccessibilityDomain.HandleAsync(session, "getFullAXTree", new System.Text.Json.Nodes.JsonObject());
        Assert.NotNull(axResponse);
        var nodes = axResponse["nodes"] as System.Text.Json.Nodes.JsonArray;
        Assert.NotNull(nodes);

        bool foundMenuItem1 = nodes.Any(n => n?["name"]?["value"]?.GetValue<string>() == "Popup MenuItem 1");
        bool foundMenuItem2 = nodes.Any(n => n?["name"]?["value"]?.GetValue<string>() == "Popup MenuItem 2");
        Assert.True(foundMenuItem1, "Popup MenuItem 1 should be found in getFullAXTree");
        Assert.True(foundMenuItem2, "Popup MenuItem 2 should be found in getFullAXTree");
    }

    [AvaloniaFact]
    public void QuerySelectorAll_LogicalTree_ReturnsTabItemContentOnce()
    {
        var panel = new StackPanel { Name = "tabRowsHost" };
        for (int i = 0; i < 25; i++)
        {
            panel.Children.Add(new TextBlock { Text = "tab row " + i });
        }
        var status = new TextBlock { Name = "tabStatusText", Text = "status" };
        var content = new StackPanel();
        content.Children.Add(status);
        content.Children.Add(panel);
        var tabs = new TabControl();
        tabs.Items.Add(new TabItem { Header = "First", Content = new TextBlock { Text = "first tab" } });
        tabs.Items.Add(new TabItem { Header = "Second", Name = "tabSecond", Content = content });
        var window = new Window { Width = 400, Height = 300, Content = tabs };
        window.Show();
        tabs.SelectedIndex = 1;
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        try
        {
            // TabControl also lists the selected content in its LogicalChildren; the content is owned by the TabItem.
            var tabChildren = CdpVisualTreeHelper.GetChildren(tabs, true).ToList();
            Assert.DoesNotContain(content, tabChildren);
            Assert.Contains(content, CdpVisualTreeHelper.GetChildren(tabs.Items[1] as Visual ?? tabs, true));

            foreach (var useLogicalTree in new[] { true, false })
            {
                var rows = SelectorEngine.QuerySelectorAll(window, "#tabRowsHost TextBlock", useLogicalTree);
                Assert.Equal(25, rows.Count);
                Assert.Equal(25, rows.Distinct().Count());

                var statusMatches = SelectorEngine.QuerySelectorAll(window, "#tabStatusText", useLogicalTree);
                Assert.Same(status, Assert.Single(statusMatches));
            }
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>Lists a control in its LogicalChildren without becoming its LogicalParent.</summary>
    private sealed class ForeignLogicalChildHost : Control
    {
        public void ListLogicalChild(Control child) => LogicalChildren.Add(child);
    }

    [AvaloniaFact]
    public void QuerySelectorAll_LogicalTree_KeepsChildWhoseLogicalParentIsOutsideSubtree()
    {
        var foreign = new TextBlock { Name = "foreignListedChild", Text = "foreign" };
        var owner = new Border { Name = "foreignOwner", Child = foreign };
        var host = new ForeignLogicalChildHost { Name = "foreignHost" };
        host.ListLogicalChild(foreign);
        var root = new StackPanel();
        root.Children.Add(owner);
        root.Children.Add(host);
        var window = new Window { Width = 400, Height = 300, Content = root };
        window.Show();

        try
        {
            Assert.Same(owner, foreign.Parent);
            Assert.Contains(foreign, ((Avalonia.LogicalTree.ILogical)host).LogicalChildren);

            // The real LogicalParent lies outside the host subtree, so the host must still report the child.
            Assert.Contains(foreign, CdpVisualTreeHelper.GetChildren(host, true));
            Assert.Same(foreign, Assert.Single(SelectorEngine.QuerySelectorAll(host, "#foreignListedChild", true)));
        }
        finally
        {
            window.Close();
        }
    }
}
