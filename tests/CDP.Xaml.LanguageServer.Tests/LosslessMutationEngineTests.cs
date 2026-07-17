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
using Xaml.Compiler.Registry;
using Xaml.Compiler.TypeSystem;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;
using Xaml.Compiler.Mutation;
using Avalonia.Diagnostics.Cdp.Adapters;

namespace Avalonia.Diagnostics.Cdp.Tests
{
    public class TempLosslessMutationWindow : Window
    {
    }

    public class LosslessMutationEngineTests
    {
        [AvaloniaFact]
        public async Task TestLosslessMutationEngine_Operations()
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
            string tempFile = Path.Combine(repoRoot, "TempLosslessMutationWindow.axaml");

            string initialXaml = @"<Window xmlns=""https://github.com/avaloniaui""
        xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml""
        x:Class=""Avalonia.Diagnostics.Cdp.Tests.TempLosslessMutationWindow"">
    <StackPanel x:Name=""MyStackPanel"">
        <Border x:Name=""MyBorder"">
            <Button x:Name=""MyButton"" Content=""Click Me""/>
        </Border>
    </StackPanel>
</Window>";

            await File.WriteAllTextAsync(tempFile, initialXaml);

            try
            {
                var window = new TempLosslessMutationWindow { Title = "Temp Window" };
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

                Assert.True(engine.CanMutate(button));

                // 1. Set Attribute
                bool setAttrSuccess = await engine.SetAttributeAsync(button, "Content", "New Click Me");
                Assert.True(setAttrSuccess);
                Assert.Equal("New Click Me", button.Content);

                string xamlContent = await File.ReadAllTextAsync(tempFile);
                Assert.Contains("Content=\"New Click Me\"", xamlContent);
                Assert.Contains(@"x:Class=""Avalonia.Diagnostics.Cdp.Tests.TempLosslessMutationWindow"">", xamlContent);

                // 2. Add class attribute
                bool setClassSuccess = await engine.SetAttributeAsync(button, "class", "btn-primary active");
                Assert.True(setClassSuccess);
                Assert.Contains("btn-primary", button.Classes);
                Assert.Contains("active", button.Classes);

                xamlContent = await File.ReadAllTextAsync(tempFile);
                Assert.Contains("class=\"btn-primary active\"", xamlContent);

                // 3. Remove class attribute
                bool removeAttrSuccess = await engine.RemoveAttributeAsync(button, "class");
                Assert.True(removeAttrSuccess);
                Assert.Empty(button.Classes);

                xamlContent = await File.ReadAllTextAsync(tempFile);
                Assert.DoesNotContain("class=\"btn-primary active\"", xamlContent);

                // 4. Set Outer HTML
                int buttonId = session.NodeMap.GetOrAdd(button);
                string outerHtmlReplacement = @"<TextBlock x:Name=""MyTextBlock"" Text=""Replaced Content""/>";
                bool setOuterHtmlSuccess = await engine.SetOuterHtmlAsync(button, outerHtmlReplacement);
                Assert.True(setOuterHtmlSuccess);

                Assert.IsType<TextBlock>(border.Child);
                var replacedText = (TextBlock)border.Child;
                Assert.Equal("MyTextBlock", replacedText.Name);
                Assert.Equal("Replaced Content", replacedText.Text);

                xamlContent = await File.ReadAllTextAsync(tempFile);
                Assert.Contains("<TextBlock x:Name=\"MyTextBlock\" Text=\"Replaced Content\"", xamlContent);
                Assert.DoesNotContain("MyButton", xamlContent);

                // 5. Remove Node
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
        public async Task TestXamlLspDomain_Operations()
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
            string tempFile = Path.Combine(repoRoot, "TempLspWindow.axaml");

            string initialXaml = @"<Window xmlns=""https://github.com/avaloniaui""
        xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml""
        x:Class=""Avalonia.Diagnostics.Cdp.Tests.TempLspWindow"">
    <StackPanel x:Name=""MyStackPanel"">
        <Button x:Name=""MyButton"" Content=""Click Me""/>
    </StackPanel>
</Window>";

            await File.WriteAllTextAsync(tempFile, initialXaml);

            try
            {
                var window = new TempLosslessMutationWindow { Title = "Temp Window" };
                using var clientWs = new ClientWebSocket();
                var session = new CdpSession(clientWs, window);

                // 1. Test getCompletions for element names starting with 'StackPa'
                var completionsParams = new JsonObject
                {
                    ["file"] = tempFile,
                    ["line"] = 4,
                    ["column"] = 12
                };

                var completionsResult = await XamlLspDomain.HandleAsync(session, "getCompletions", completionsParams);
                Assert.NotNull(completionsResult);
                var completionsArray = completionsResult["completions"] as JsonArray;
                Assert.NotNull(completionsArray);
                Assert.NotEmpty(completionsArray);

                var hasStackPanel = completionsArray.Any(c => c?["label"]?.GetValue<string>() == "StackPanel");
                Assert.True(hasStackPanel);

                // 1b. Test getCompletions for element names starting with 'But' on line 5
                var buttonCompletionsParams = new JsonObject
                {
                    ["file"] = tempFile,
                    ["line"] = 5,
                    ["column"] = 12
                };

                var buttonCompletionsResult = await XamlLspDomain.HandleAsync(session, "getCompletions", buttonCompletionsParams);
                Assert.NotNull(buttonCompletionsResult);
                var buttonCompletionsArray = buttonCompletionsResult["completions"] as JsonArray;
                Assert.NotNull(buttonCompletionsArray);
                var hasButton = buttonCompletionsArray.Any(c => c?["label"]?.GetValue<string>() == "Button");
                if (!hasButton)
                {
                    var registryField = typeof(XamlLspDomain).GetField("s_schemaRegistry", BindingFlags.NonPublic | BindingFlags.Static);
                    var registry = registryField?.GetValue(null) as XamlSchemaRegistry;
                    var field = typeof(XamlSchemaRegistry).GetField("_namespaces", BindingFlags.NonPublic | BindingFlags.Instance);
                    var namespaces = field?.GetValue(registry) as ConcurrentDictionary<string, IXamlNamespace>;
                    var nsList = string.Join(", ", namespaces?.Keys ?? Array.Empty<string>());
                    
                    var asmList = new List<string>();
                    var tList = new List<string>();
                    if (namespaces != null)
                    {
                        foreach (var ns in namespaces.Values)
                        {
                            foreach (var asm in ns.TargetAssemblies)
                            {
                                asmList.Add(asm);
                                try
                                {
                                    var assembly = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => string.Equals(a.GetName().Name, asm, StringComparison.OrdinalIgnoreCase));
                                    if (assembly != null)
                                    {
                                        tList.Add($"{asm}: Loaded");
                                        var types = assembly.GetTypes().Where(t => t.IsPublic && !t.IsAbstract && ns.ClrNamespaces.Contains(t.Namespace ?? "")).Select(t => t.Name).Take(5);
                                        tList.Add($"  Types: {string.Join(", ", types)}");
                                    }
                                    else
                                    {
                                        tList.Add($"{asm}: NOT Loaded in AppDomain");
                                    }
                                }
                                catch (Exception ex)
                                {
                                    tList.Add($"{asm}: Error {ex.Message}");
                                }
                            }
                        }
                    }

                    throw new Exception($"Button not found. Registered Namespaces: [{nsList}], Assemblies: [{string.Join(", ", asmList)}], Diagnostics:\n{string.Join("\n", tList)}");
                }
                Assert.True(hasButton);

                // 2. Test getCompletions for standard properties on Button
                var propertyParams = new JsonObject
                {
                    ["file"] = tempFile,
                    ["line"] = 5,
                    ["column"] = 34
                };

                var propResult = await XamlLspDomain.HandleAsync(session, "getCompletions", propertyParams);
                Assert.NotNull(propResult);
                var propArray = propResult["completions"] as JsonArray;
                Assert.NotNull(propArray);
                Assert.NotEmpty(propArray);

                var hasContent = propArray.Any(c => c?["label"]?.GetValue<string>() == "Content");
                Assert.True(hasContent);

                // 3. Test getHover on Button
                var hoverParams = new JsonObject
                {
                    ["file"] = tempFile,
                    ["line"] = 5,
                    ["column"] = 12
                };

                var hoverResult = await XamlLspDomain.HandleAsync(session, "getHover", hoverParams);
                Assert.NotNull(hoverResult);
                string contents = hoverResult["contents"]?.GetValue<string>() ?? "";
                if (!contents.Contains("Button"))
                {
                    throw new Exception($"Hover content did not contain 'Button'. Contents returned: '{contents}'");
                }
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
        public async Task TestXamlLspDomain_CompletionsInSpecialNodes()
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
            string tempFile = Path.Combine(repoRoot, "TempLspSpecialWindow.axaml");

            string initialXaml = @"<Window xmlns=""https://github.com/avaloniaui""
        xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml""
        x:Class=""Avalonia.Diagnostics.Cdp.Tests.TempLspSpecialWindow"">
    <StackPanel>
        <!-- This is a comment -->
        <![CDATA[ This is CDATA ]]>
        Some raw text here
    </StackPanel>
</Window>";

            await File.WriteAllTextAsync(tempFile, initialXaml);

            try
            {
                var window = new TempLosslessMutationWindow { Title = "Temp Window" };
                using var clientWs = new ClientWebSocket();
                var session = new CdpSession(clientWs, window);

                // Line 5: comment
                var commentParams = new JsonObject { ["file"] = tempFile, ["line"] = 5, ["column"] = 15 };
                var commentResult = await XamlLspDomain.HandleAsync(session, "getCompletions", commentParams);
                var commentArray = commentResult["completions"] as JsonArray;
                Assert.Empty(commentArray);

                // Line 6: CDATA
                var cdataParams = new JsonObject { ["file"] = tempFile, ["line"] = 6, ["column"] = 15 };
                var cdataResult = await XamlLspDomain.HandleAsync(session, "getCompletions", cdataParams);
                var cdataArray = cdataResult["completions"] as JsonArray;
                Assert.Empty(cdataArray);

                // Line 7: text content
                var textParams = new JsonObject { ["file"] = tempFile, ["line"] = 7, ["column"] = 15 };
                var textResult = await XamlLspDomain.HandleAsync(session, "getCompletions", textParams);
                var textArray = textResult["completions"] as JsonArray;
                Assert.Empty(textArray);
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
}
