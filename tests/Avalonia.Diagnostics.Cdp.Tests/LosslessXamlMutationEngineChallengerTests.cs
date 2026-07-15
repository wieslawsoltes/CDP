using System;
using System.IO;
using System.Linq;
using System.Net.WebSockets;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Xunit;
using Avalonia.Diagnostics.Cdp.Domains;
using Chrome.DevTools.Protocol;
using Xaml.Compiler.Mutation;
using Avalonia.Diagnostics.Cdp.Adapters;

namespace Avalonia.Diagnostics.Cdp.Tests
{
    public class ChallengerLosslessMutationWindow : Window
    {
    }

    public class LosslessXamlMutationEngineChallengerTests
    {
        private string FindRepoRoot()
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

        [AvaloniaFact]
        public async Task TestChallenger_RemovingRoot()
        {
            string repoRoot = FindRepoRoot();
            string tempFile = Path.Combine(repoRoot, "ChallengerLosslessMutationWindow.axaml");

            string initialXaml = @"<Window xmlns=""https://github.com/avaloniaui""
        xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml""
        x:Class=""Avalonia.Diagnostics.Cdp.Tests.ChallengerLosslessMutationWindow""
        Title=""Test Window"">
    <StackPanel x:Name=""MyStackPanel"">
        <Button x:Name=""MyButton"" Content=""Click Me""/>
    </StackPanel>
</Window>";

            await File.WriteAllTextAsync(tempFile, initialXaml);

            try
            {
                var window = new ChallengerLosslessMutationWindow { Title = "Test Window" };
                var stackPanel = new StackPanel { Name = "MyStackPanel" };
                var button = new Button { Name = "MyButton", Content = "Click Me" };

                stackPanel.Children.Add(button);
                window.Content = stackPanel;
                window.Show();

                using var clientWs = new ClientWebSocket();
                var session = new CdpSession(clientWs, window);
                var engine = (LosslessXamlMutationEngine)session.MutationEngine!;

                // Attempt to remove root (the window itself)
                bool removeRootSuccess = await engine.RemoveNodeAsync(window);
                Assert.False(removeRootSuccess);

                // Verify XAML is unmodified
                string currentXaml = await File.ReadAllTextAsync(tempFile);
                Assert.Equal(initialXaml, currentXaml);
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
        public async Task TestChallenger_MalformedXaml()
        {
            string repoRoot = FindRepoRoot();
            string tempFile = Path.Combine(repoRoot, "ChallengerLosslessMutationWindow.axaml");

            // Malformed XAML with unclosed tags
            string initialXaml = @"<Window xmlns=""https://github.com/avaloniaui""
        xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml""
        x:Class=""Avalonia.Diagnostics.Cdp.Tests.ChallengerLosslessMutationWindow"">
    <StackPanel x:Name=""MyStackPanel"">
        <Button x:Name=""MyButton"" Content=""Click Me""
    </StackPanel>
</Window>";

            await File.WriteAllTextAsync(tempFile, initialXaml);

            try
            {
                var window = new ChallengerLosslessMutationWindow();
                var stackPanel = new StackPanel { Name = "MyStackPanel" };
                var button = new Button { Name = "MyButton", Content = "Click Me" };

                stackPanel.Children.Add(button);
                window.Content = stackPanel;
                window.Show();

                using var clientWs = new ClientWebSocket();
                var session = new CdpSession(clientWs, window);
                var engine = (LosslessXamlMutationEngine)session.MutationEngine!;

                // Try to set attribute on a control in malformed XAML - must be rejected
                bool success = await engine.SetAttributeAsync(button, "Content", "New Content");
                Assert.False(success);

                // The XAML file on disk should not be mutated
                string mutatedXaml = await File.ReadAllTextAsync(tempFile);
                Assert.Equal(initialXaml, mutatedXaml);
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
        public async Task TestChallenger_NestedNamespaces()
        {
            string repoRoot = FindRepoRoot();
            string tempFile = Path.Combine(repoRoot, "ChallengerLosslessMutationWindow.axaml");

            string initialXaml = @"<Window xmlns=""https://github.com/avaloniaui""
        xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml""
        xmlns:local=""clr-namespace:Avalonia.Diagnostics.Cdp.Tests;assembly=Avalonia.Diagnostics.Cdp.Tests""
        x:Class=""Avalonia.Diagnostics.Cdp.Tests.ChallengerLosslessMutationWindow"">
    <StackPanel x:Name=""MyStackPanel"">
        <Button x:Name=""MyButton"" Content=""Click Me""/>
    </StackPanel>
</Window>";

            await File.WriteAllTextAsync(tempFile, initialXaml);

            try
            {
                var window = new ChallengerLosslessMutationWindow();
                var stackPanel = new StackPanel { Name = "MyStackPanel" };
                var button = new Button { Name = "MyButton", Content = "Click Me" };

                stackPanel.Children.Add(button);
                window.Content = stackPanel;
                window.Show();

                using var clientWs = new ClientWebSocket();
                var session = new CdpSession(clientWs, window);
                var engine = (LosslessXamlMutationEngine)session.MutationEngine!;

                // Verify mutation works when custom namespaces are declared on root
                bool setAttrSuccess = await engine.SetAttributeAsync(button, "Content", "Nested OK");
                Assert.True(setAttrSuccess);

                string xaml = await File.ReadAllTextAsync(tempFile);
                Assert.Contains("xmlns:local=\"clr-namespace:Avalonia.Diagnostics.Cdp.Tests;assembly=Avalonia.Diagnostics.Cdp.Tests\"", xaml);
                Assert.Contains("Content=\"Nested OK\"", xaml);
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
        public async Task TestChallenger_SequentialEdits()
        {
            string repoRoot = FindRepoRoot();
            string tempFile = Path.Combine(repoRoot, "ChallengerLosslessMutationWindow.axaml");

            string initialXaml = @"<Window xmlns=""https://github.com/avaloniaui""
        xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml""
        x:Class=""Avalonia.Diagnostics.Cdp.Tests.ChallengerLosslessMutationWindow"">
    <StackPanel x:Name=""MyStackPanel"">
        <Button x:Name=""MyButton"" Content=""Click Me""/>
    </StackPanel>
</Window>";

            await File.WriteAllTextAsync(tempFile, initialXaml);

            try
            {
                var window = new ChallengerLosslessMutationWindow();
                var stackPanel = new StackPanel { Name = "MyStackPanel" };
                var button = new Button { Name = "MyButton", Content = "Click Me" };

                stackPanel.Children.Add(button);
                window.Content = stackPanel;
                window.Show();

                using var clientWs = new ClientWebSocket();
                var session = new CdpSession(clientWs, window);
                var engine = (LosslessXamlMutationEngine)session.MutationEngine!;

                // 1. Set Content
                bool step1 = await engine.SetAttributeAsync(button, "Content", "Edit 1");
                Assert.True(step1);

                // 2. Set Class
                bool step2 = await engine.SetAttributeAsync(button, "class", "my-class");
                Assert.True(step2);

                // 3. Set another attribute (e.g. Width)
                bool step3 = await engine.SetAttributeAsync(button, "Width", "100");
                Assert.True(step3);

                // 4. Remove Class
                bool step4 = await engine.RemoveAttributeAsync(button, "class");
                Assert.True(step4);

                // 5. Replace with Outer HTML
                session.NodeMap.GetOrAdd(button); // ensure registered
                string outerHtml = @"<TextBlock x:Name=""MyTextBlock"" Text=""Replaced Inline""/>";
                bool step5 = await engine.SetOuterHtmlAsync(button, outerHtml);
                Assert.True(step5);

                // 6. Mutate the replaced element
                var textBlock = (TextBlock)stackPanel.Children.First(c => c is TextBlock);
                bool step6 = await engine.SetAttributeAsync(textBlock, "Text", "Final Edit");
                Assert.True(step6);

                string xaml = await File.ReadAllTextAsync(tempFile);
                Assert.Contains("<TextBlock x:Name=\"MyTextBlock\" Text=\"Final Edit\"", xaml);
                Assert.DoesNotContain("MyButton", xaml);
                Assert.DoesNotContain("Edit 1", xaml);
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
        public async Task TestChallenger_InvalidAttributesAndQuotes()
        {
            string repoRoot = FindRepoRoot();
            string tempFile = Path.Combine(repoRoot, "ChallengerLosslessMutationWindow.axaml");

            string initialXaml = @"<Window xmlns=""https://github.com/avaloniaui""
        xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml""
        x:Class=""Avalonia.Diagnostics.Cdp.Tests.ChallengerLosslessMutationWindow"">
    <StackPanel x:Name=""MyStackPanel"">
        <Button x:Name=""MyButton"" Content=""Click Me""/>
    </StackPanel>
</Window>";

            await File.WriteAllTextAsync(tempFile, initialXaml);

            try
            {
                var window = new ChallengerLosslessMutationWindow();
                var stackPanel = new StackPanel { Name = "MyStackPanel" };
                var button = new Button { Name = "MyButton", Content = "Click Me" };

                stackPanel.Children.Add(button);
                window.Content = stackPanel;
                window.Show();

                using var clientWs = new ClientWebSocket();
                var session = new CdpSession(clientWs, window);
                var engine = (LosslessXamlMutationEngine)session.MutationEngine!;

                // Case 1: Attribute value with double quotes inside
                // Verification Finding: The engine should escape double quotes, creating well-formed XAML
                string valueWithQuotes = "Hello \"World\"";
                bool step1 = await engine.SetAttributeAsync(button, "Content", valueWithQuotes);
                Assert.True(step1);

                // Let's read the raw XAML on disk and verify if it's well-formed or malformed.
                string xamlContent = await File.ReadAllTextAsync(tempFile);
                
                // Let's attempt to parse the resulting XAML using XamlParser
                var doc = Xaml.Compiler.Parser.XamlParser.Parse(xamlContent);
                
                // Check that no diagnostic errors were reported (proving quotes are escaped)
                var errors = doc.Diagnostics.Where(d => d.Severity == Xaml.Compiler.Ast.DiagnosticSeverity.Error).ToList();
                
                Assert.Empty(errors);
                Assert.Contains("Content=\"Hello &quot;World&quot;\"", xamlContent);
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
        public async Task TestChallenger_SetOuterHtmlMalformed()
        {
            string repoRoot = FindRepoRoot();
            string tempFile = Path.Combine(repoRoot, "ChallengerLosslessMutationWindow.axaml");

            string initialXaml = @"<Window xmlns=""https://github.com/avaloniaui""
        xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml""
        x:Class=""Avalonia.Diagnostics.Cdp.Tests.ChallengerLosslessMutationWindow"">
    <StackPanel x:Name=""MyStackPanel"">
        <Button x:Name=""MyButton"" Content=""Click Me""/>
    </StackPanel>
</Window>";

            await File.WriteAllTextAsync(tempFile, initialXaml);

            try
            {
                var window = new ChallengerLosslessMutationWindow();
                var stackPanel = new StackPanel { Name = "MyStackPanel" };
                var button = new Button { Name = "MyButton", Content = "Click Me" };

                stackPanel.Children.Add(button);
                window.Content = stackPanel;
                window.Show();

                using var clientWs = new ClientWebSocket();
                var session = new CdpSession(clientWs, window);
                var engine = (LosslessXamlMutationEngine)session.MutationEngine!;

                // Set outer HTML to a malformed element
                session.NodeMap.GetOrAdd(button);
                bool success = await engine.SetOuterHtmlAsync(button, "<Button x:Name=\"MyBtn\"");
                
                // Verification Finding: The engine returns false (failed live UI update)
                Assert.False(success);

                // The file on disk should NOT be modified because the load failed
                string xamlContent = await File.ReadAllTextAsync(tempFile);
                Assert.DoesNotContain("<Button x:Name=\"MyBtn\"", xamlContent);
                Assert.Equal(initialXaml, xamlContent);
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
        public async Task TestChallenger_WhitespacePreservation()
        {
            string repoRoot = FindRepoRoot();
            string tempFile = Path.Combine(repoRoot, "ChallengerLosslessMutationWindow.axaml");

            // A XAML structure with complex formatting, comments, mixed tabs/spaces/newlines
            string initialXaml = @"<Window xmlns=""https://github.com/avaloniaui""
        xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml""
        x:Class=""Avalonia.Diagnostics.Cdp.Tests.ChallengerLosslessMutationWindow"">
	<!-- Complex formatted section -->
	<StackPanel	x:Name=""MyStackPanel"">
		
		<Border   x:Name=""MyBorder""
			  BorderThickness=""2""
			  BorderBrush=""Red"">
			
			<Button x:Name=""MyButton""
				Content=""Click Me""
				Width=""100"" />
			
		</Border>
		
	</StackPanel>
</Window>
";

            await File.WriteAllTextAsync(tempFile, initialXaml);

            try
            {
                var window = new ChallengerLosslessMutationWindow();
                var stackPanel = new StackPanel { Name = "MyStackPanel" };
                var border = new Border { Name = "MyBorder" };
                var button = new Button { Name = "MyButton", Content = "Click Me" };

                border.Child = button;
                stackPanel.Children.Add(border);
                window.Content = stackPanel;
                window.Show();

                using var clientWs = new ClientWebSocket();
                var session = new CdpSession(clientWs, window);
                var engine = (LosslessXamlMutationEngine)session.MutationEngine!;

                // Mutate the button's Content
                bool step1 = await engine.SetAttributeAsync(button, "Content", "New Content");
                Assert.True(step1);

                string mutatedXaml = await File.ReadAllTextAsync(tempFile);

                // Verify that everything outside the modified <Button /> tag is EXACTLY identical byte-for-byte.
                // To do this, we can replace the mutated button tag in both initial and mutated XAML and check for equality.
                
                string cleanInitial = RemoveButtonTag(initialXaml);
                string cleanMutated = RemoveButtonTag(mutatedXaml);

                Assert.Equal(cleanInitial, cleanMutated);
            }
            finally
            {
                if (File.Exists(tempFile))
                {
                    File.Delete(tempFile);
                }
            }
        }

        private string RemoveButtonTag(string xaml)
        {
            // Find the Button tag and replace it with a marker
            int startIdx = xaml.IndexOf("<Button");
            int endIdx = xaml.IndexOf("/>", startIdx);
            if (startIdx == -1 || endIdx == -1) return xaml;
            
            // Extract before and after
            string before = xaml.Substring(0, startIdx);
            string after = xaml.Substring(endIdx + 2);
            return before + "[BUTTON_MARKER]" + after;
        }
    }
}
