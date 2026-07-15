using System;
using System.IO;
using System.Linq;
using System.Net.WebSockets;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using System.Xml.Linq;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Xunit;

namespace Avalonia.Diagnostics.Cdp.Tests;

public class TempMutationWindow : Window
{
}

public class MutationEngineTests
{
    [AvaloniaFact]
    public async Task TestMutationEngine_Operations()
    {
        string FindRepoRoot()
        {
            string? current = Directory.GetCurrentDirectory();
            while (current != null)
            {
                if (Directory.EnumerateFiles(current, "*.sln").Any() ||
                    Directory.Exists(Path.Combine(current, ".git")))
                {
                    return current;
                }
                var parent = Directory.GetParent(current);
                if (parent == null || parent.FullName == current) break;
                current = parent.FullName;
            }
            return Directory.GetCurrentDirectory();
        }

        string repoRoot = FindRepoRoot();
        string tempFile = Path.Combine(repoRoot, "TempMutationWindow.axaml");

        string initialXaml = @"<Window xmlns=""https://github.com/avaloniaui""
        xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml""
        x:Class=""Avalonia.Diagnostics.Cdp.Tests.TempMutationWindow"">
    <StackPanel x:Name=""MyStackPanel"">
        <Border x:Name=""MyBorder"">
            <Button x:Name=""MyButton"" Content=""Click Me""/>
        </Border>
    </StackPanel>
</Window>";

        await File.WriteAllTextAsync(tempFile, initialXaml);

        try
        {
            var window = new TempMutationWindow { Title = "Temp Window" };
            var stackPanel = new StackPanel { Name = "MyStackPanel" };
            var border = new Border { Name = "MyBorder" };
            var button = new Button { Name = "MyButton", Content = "Click Me" };

            border.Child = button;
            stackPanel.Children.Add(border);
            window.Content = stackPanel;
            window.Show();

            using var clientWs = new ClientWebSocket();
            var session = new CdpSession(clientWs, window);
            var engine = new AvaloniaXamlMutationEngine(session);

            Assert.True(engine.CanMutate(button));

            bool setAttrSuccess = await engine.SetAttributeAsync(button, "Content", "New Click Me");
            Assert.True(setAttrSuccess);
            Assert.Equal("New Click Me", button.Content);

            string xamlContent = await File.ReadAllTextAsync(tempFile);
            Assert.Contains("Content=\"New Click Me\"", xamlContent);

            bool setClassSuccess = await engine.SetAttributeAsync(button, "class", "btn-primary active");
            Assert.True(setClassSuccess);
            Assert.Contains("btn-primary", button.Classes);
            Assert.Contains("active", button.Classes);

            xamlContent = await File.ReadAllTextAsync(tempFile);
            Assert.Contains("class=\"btn-primary active\"", xamlContent);

            bool removeAttrSuccess = await engine.RemoveAttributeAsync(button, "class");
            Assert.True(removeAttrSuccess);
            Assert.Empty(button.Classes);

            xamlContent = await File.ReadAllTextAsync(tempFile);
            Assert.DoesNotContain("class=\"btn-primary active\"", xamlContent);

            int buttonId = session.NodeMap.GetOrAdd(button);
            string outerHtmlReplacement = @"<TextBlock x:Name=""MyTextBlock"" Text=""Replaced Content""/>";
            bool setOuterHtmlSuccess = await engine.SetOuterHtmlAsync(button, outerHtmlReplacement);
            Assert.True(setOuterHtmlSuccess);

            Assert.IsType<TextBlock>(border.Child);
            var replacedText = (TextBlock)border.Child;
            Assert.Equal("MyTextBlock", replacedText.Name);
            Assert.Equal("Replaced Content", replacedText.Text);

            var mappedNode = session.NodeMap.GetVisual(buttonId);
            Assert.Same(replacedText, mappedNode);

            xamlContent = await File.ReadAllTextAsync(tempFile);
            Assert.Contains("<TextBlock x:Name=\"MyTextBlock\" Text=\"Replaced Content\"", xamlContent);
            Assert.DoesNotContain("MyButton", xamlContent);

            bool removeNodeSuccess = await engine.RemoveNodeAsync(replacedText);
            Assert.True(removeNodeSuccess);
            Assert.Null(border.Child);

            xamlContent = await File.ReadAllTextAsync(tempFile);
            Assert.DoesNotContain("MyTextBlock", xamlContent);
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    [AvaloniaFact]
    public async Task TestCssDomain_StyleMutations()
    {
        string FindRepoRoot()
        {
            string? current = Directory.GetCurrentDirectory();
            while (current != null)
            {
                if (Directory.EnumerateFiles(current, "*.sln").Any() ||
                    Directory.Exists(Path.Combine(current, ".git")))
                {
                    return current;
                }
                var parent = Directory.GetParent(current);
                if (parent == null || parent.FullName == current) break;
                current = parent.FullName;
            }
            return Directory.GetCurrentDirectory();
        }

        string repoRoot = FindRepoRoot();
        string tempFile = Path.Combine(repoRoot, "TempMutationWindow.axaml");

        string initialXaml = @"<Window xmlns=""https://github.com/avaloniaui""
        xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml""
        x:Class=""Avalonia.Diagnostics.Cdp.Tests.TempMutationWindow"">
    <StackPanel x:Name=""MyStackPanel"">
        <Button x:Name=""MyButton"" Content=""Click Me"" Width=""100"" Background=""Blue""/>
    </StackPanel>
</Window>";

        await File.WriteAllTextAsync(tempFile, initialXaml);

        try
        {
            var window = new TempMutationWindow { Title = "Temp Window" };
            var stackPanel = new StackPanel { Name = "MyStackPanel" };
            var button = new Button { Name = "MyButton", Content = "Click Me", Width = 100, Background = Avalonia.Media.Brushes.Blue };

            stackPanel.Children.Add(button);
            window.Content = stackPanel;
            window.Show();

            using var clientWs = new ClientWebSocket();
            var session = new CdpSession(clientWs, window);
            var engine = new AvaloniaXamlMutationEngine(session);
            session.MutationEngine = engine;

            int buttonId = session.NodeMap.GetOrAdd(button);

            // Execute setStyleTexts via CssDomain
            var editParams = new JsonObject
            {
                ["edits"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["styleSheetId"] = buttonId.ToString(),
                        ["text"] = "width: 250px; background-color: Red;"
                    }
                }
            };

            var result = await Domains.CssDomain.HandleAsync(session, "setStyleTexts", editParams);
            Assert.NotNull(result);

            // Assert live values updated
            Assert.Equal(250, button.Width);
            Assert.Equal(Avalonia.Media.Brushes.Red.ToString(), button.Background?.ToString());

            // Assert XAML file updated
            string xamlContent = await File.ReadAllTextAsync(tempFile);
            Assert.Contains("Width=\"250\"", xamlContent);
            Assert.Contains("Background=\"Red\"", xamlContent);
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }
}

