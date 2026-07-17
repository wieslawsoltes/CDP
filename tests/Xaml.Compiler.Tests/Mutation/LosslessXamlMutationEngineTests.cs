using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Xaml.Compiler.Adapters;
using Xaml.Compiler.Ast;
using Xaml.Compiler.Mutation;
using Chrome.DevTools.Protocol;

namespace Xaml.Compiler.Tests.Mutation
{
    public class TestControl
    {
        public string? Name { get; set; }
        public string? Id { get; set; }
        public TestControl? Parent { get; set; }
        public List<TestControl> Children { get; } = new();
        public string? Text { get; set; }
        public Dictionary<string, string> Attributes { get; } = new(StringComparer.OrdinalIgnoreCase);
    }

    public class TestControlWindow : TestControl
    {
    }

    public class MockNodeMap : INodeMap
    {
        private readonly Dictionary<object, int> _toId = new();
        private readonly Dictionary<int, object> _toNode = new();
        private int _nextId = 1;

        public int GetOrAdd(object node)
        {
            if (_toId.TryGetValue(node, out int id)) return id;
            id = _nextId++;
            _toId[node] = id;
            _toNode[id] = node;
            return id;
        }

        public bool TryGetId(object node, out int id)
        {
            return _toId.TryGetValue(node, out id);
        }

        public void UpdateNodeMapping(int id, object newNode)
        {
            if (_toNode.TryGetValue(id, out var oldNode))
            {
                _toId.Remove(oldNode);
            }
            _toNode[id] = newNode;
            _toId[newNode] = id;
        }
    }

    public class MockUiFrameworkAdapter : IUiFrameworkAdapter
    {
        public IReadOnlyCollection<string> XamlFileExtensions { get; } = new[] { "xaml", "axaml" };

        public string DefaultXmlNamespace => "http://schemas.microsoft.com/winfx/2006/xaml/presentation";

        public bool IsControl(object target)
        {
            return target is TestControl;
        }

        public object? GetParent(object control)
        {
            return (control as TestControl)?.Parent;
        }

        public IReadOnlyCollection<object> GetChildren(object parent)
        {
            return (parent as TestControl)?.Children.Cast<object>().ToList() ?? new List<object>();
        }

        public string GetTypeName(object control)
        {
            return control.GetType().Name;
        }

        public string? GetClassFullName(object control)
        {
            return control.GetType().FullName;
        }

        public object? GetPropertyValue(object control, string propertyName)
        {
            if (control is TestControl tc)
            {
                if (propertyName.Equals("Name", StringComparison.OrdinalIgnoreCase)) return tc.Name;
                if (propertyName.Equals("Id", StringComparison.OrdinalIgnoreCase)) return tc.Id;
                if (propertyName.Equals("Text", StringComparison.OrdinalIgnoreCase)) return tc.Text;
                if (tc.Attributes.TryGetValue(propertyName, out var val)) return val;
            }
            return null;
        }

        public Task ApplyAttributeLiveAsync(object control, string propertyName, string valueString)
        {
            if (control is TestControl tc)
            {
                if (propertyName.Equals("Name", StringComparison.OrdinalIgnoreCase)) tc.Name = valueString;
                else if (propertyName.Equals("Id", StringComparison.OrdinalIgnoreCase)) tc.Id = valueString;
                else if (propertyName.Equals("Text", StringComparison.OrdinalIgnoreCase)) tc.Text = valueString;
                else tc.Attributes[propertyName] = valueString;
            }
            return Task.CompletedTask;
        }

        public Task RemoveAttributeLiveAsync(object control, string propertyName)
        {
            if (control is TestControl tc)
            {
                if (propertyName.Equals("Name", StringComparison.OrdinalIgnoreCase)) tc.Name = null;
                else if (propertyName.Equals("Id", StringComparison.OrdinalIgnoreCase)) tc.Id = null;
                else if (propertyName.Equals("Text", StringComparison.OrdinalIgnoreCase)) tc.Text = null;
                else tc.Attributes.Remove(propertyName);
            }
            return Task.CompletedTask;
        }

        public Task RemoveNodeLiveAsync(object control)
        {
            if (control is TestControl tc && tc.Parent != null)
            {
                tc.Parent.Children.Remove(tc);
                tc.Parent = null;
            }
            return Task.CompletedTask;
        }

          public Task<object> InstantiateXamlFragmentAsync(string xamlFragment, Dictionary<string, string> inheritedNamespaces)
        {
            TestControl CreateControl(Xaml.Compiler.Ast.XamlElementSyntax element)
            {
                var tc = new TestControl
                {
                    Name = element.Attributes.FirstOrDefault(a => a.LocalName == "Name")?.ValueNode.ToFullString().Trim('"', '\''),
                    Text = element.Attributes.FirstOrDefault(a => a.LocalName == "Text")?.ValueNode.ToFullString().Trim('"', '\'')
                };
                foreach (var childNode in element.Children)
                {
                    if (childNode is Xaml.Compiler.Ast.XamlElementSyntax childEl)
                    {
                        var childTc = CreateControl(childEl);
                        childTc.Parent = tc;
                        tc.Children.Add(childTc);
                    }
                }
                return tc;
            }

            var doc = Xaml.Compiler.Parser.XamlParser.Parse(xamlFragment);
            var rootEl = doc.RootElement;
            if (rootEl == null) throw new Exception("Invalid XML");
            return Task.FromResult<object>(CreateControl(rootEl));
        }

        public Task<bool> ReplaceChildLiveAsync(object oldChild, object newChild)
        {
            if (oldChild is TestControl oldTc && newChild is TestControl newTc)
            {
                var parent = oldTc.Parent;
                if (parent != null)
                {
                    int index = parent.Children.IndexOf(oldTc);
                    if (index != -1)
                    {
                        parent.Children[index] = newTc;
                        newTc.Parent = parent;
                        oldTc.Parent = null;
                        return Task.FromResult(true);
                    }
                }
            }
            return Task.FromResult(false);
        }
    }

    public class LosslessXamlMutationEngineTests
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

        [Fact]
        public async Task TestMock_SetAttribute_UpdatesAstAndLiveControl()
        {
            string repoRoot = FindRepoRoot();
            string tempFile = Path.Combine(repoRoot, "TestControlWindow.xaml");
            string initialXaml = @"<TestControlWindow xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation""
        xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml""
        x:Class=""Xaml.Compiler.Tests.Mutation.TestControlWindow""
        Name=""MyRoot"">
    <TestControl Name=""MyChild"" Text=""OldText"" />
</TestControlWindow>";

            await File.WriteAllTextAsync(tempFile, initialXaml);

            try
            {
                var root = new TestControlWindow { Name = "MyRoot" };
                var child = new TestControl { Name = "MyChild", Text = "OldText", Parent = root };
                root.Children.Add(child);

                var adapter = new MockUiFrameworkAdapter();
                var nodeMap = new MockNodeMap();
                nodeMap.GetOrAdd(root);
                nodeMap.GetOrAdd(child);

                var engine = new LosslessXamlMutationEngine(adapter, nodeMap);

                Assert.True(engine.CanMutate(child));

                // Mutate attribute
                bool success = await engine.SetAttributeAsync(child, "Text", "NewText");
                Assert.True(success);

                // Assert live UI update occurred via adapter
                Assert.Equal("NewText", child.Text);

                // Assert XAML file was updated correctly
                string updatedXaml = await File.ReadAllTextAsync(tempFile);
                Assert.Contains("Text=\"NewText\"", updatedXaml);
                Assert.DoesNotContain("Text=\"OldText\"", updatedXaml);

                // Verify patching is lossless
                Assert.Contains("x:Class=\"Xaml.Compiler.Tests.Mutation.TestControlWindow\"", updatedXaml);
            }
            finally
            {
                if (File.Exists(tempFile))
                {
                    File.Delete(tempFile);
                }
            }
        }

        [Fact]
        public async Task TestMock_RemoveAttribute_UpdatesAstAndLiveControl()
        {
            string repoRoot = FindRepoRoot();
            string tempFile = Path.Combine(repoRoot, "TestControlWindow.xaml");
            string initialXaml = @"<TestControlWindow xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation""
        xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml""
        x:Class=""Xaml.Compiler.Tests.Mutation.TestControlWindow""
        Name=""MyRoot"">
    <TestControl Name=""MyChild"" Text=""OldText"" />
</TestControlWindow>";

            await File.WriteAllTextAsync(tempFile, initialXaml);

            try
            {
                var root = new TestControlWindow { Name = "MyRoot" };
                var child = new TestControl { Name = "MyChild", Text = "OldText", Parent = root };
                root.Children.Add(child);

                var adapter = new MockUiFrameworkAdapter();
                var nodeMap = new MockNodeMap();
                nodeMap.GetOrAdd(root);
                nodeMap.GetOrAdd(child);

                var engine = new LosslessXamlMutationEngine(adapter, nodeMap);

                // Remove attribute
                bool success = await engine.RemoveAttributeAsync(child, "Text");
                Assert.True(success);

                // Assert live UI update occurred
                Assert.Null(child.Text);

                // Assert XAML file was updated
                string updatedXaml = await File.ReadAllTextAsync(tempFile);
                Assert.DoesNotContain("Text=\"OldText\"", updatedXaml);
            }
            finally
            {
                if (File.Exists(tempFile))
                {
                    File.Delete(tempFile);
                }
            }
        }

        [Fact]
        public async Task TestMock_RemoveNode_UpdatesAstAndLiveControl()
        {
            string repoRoot = FindRepoRoot();
            string tempFile = Path.Combine(repoRoot, "TestControlWindow.xaml");
            string initialXaml = @"<TestControlWindow xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation""
        xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml""
        x:Class=""Xaml.Compiler.Tests.Mutation.TestControlWindow""
        Name=""MyRoot"">
    <TestControl Name=""MyChild"" />
</TestControlWindow>";

            await File.WriteAllTextAsync(tempFile, initialXaml);

            try
            {
                var root = new TestControlWindow { Name = "MyRoot" };
                var child = new TestControl { Name = "MyChild", Parent = root };
                root.Children.Add(child);

                var adapter = new MockUiFrameworkAdapter();
                var nodeMap = new MockNodeMap();
                nodeMap.GetOrAdd(root);
                nodeMap.GetOrAdd(child);

                var engine = new LosslessXamlMutationEngine(adapter, nodeMap);

                // Remove child control node
                bool success = await engine.RemoveNodeAsync(child);
                Assert.True(success);

                // Assert live UI update occurred
                Assert.Empty(root.Children);

                // Assert XAML file was updated
                string updatedXaml = await File.ReadAllTextAsync(tempFile);
                Assert.DoesNotContain("<TestControl Name=\"MyChild\" />", updatedXaml);
            }
            finally
            {
                if (File.Exists(tempFile))
                {
                    File.Delete(tempFile);
                }
            }
        }

        [Fact]
        public async Task TestMock_SetOuterHtml_ReplacesNodeAstAndLiveControl()
        {
            string repoRoot = FindRepoRoot();
            string tempFile = Path.Combine(repoRoot, "TestControlWindow.xaml");
            string initialXaml = @"<TestControlWindow xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation""
        xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml""
        x:Class=""Xaml.Compiler.Tests.Mutation.TestControlWindow""
        Name=""MyRoot"">
    <TestControl Name=""MyChild"" Text=""Old"" />
</TestControlWindow>";

            await File.WriteAllTextAsync(tempFile, initialXaml);

            try
            {
                var root = new TestControlWindow { Name = "MyRoot" };
                var child = new TestControl { Name = "MyChild", Text = "Old", Parent = root };
                root.Children.Add(child);

                var adapter = new MockUiFrameworkAdapter();
                var nodeMap = new MockNodeMap();
                nodeMap.GetOrAdd(root);
                int childId = nodeMap.GetOrAdd(child);

                var engine = new LosslessXamlMutationEngine(adapter, nodeMap);

                // Replace child control outer HTML
                bool success = await engine.SetOuterHtmlAsync(child, @"<TestControl Name=""NewChild"" Text=""New"" />");
                Assert.True(success);

                // Assert live UI update occurred
                Assert.Single(root.Children);
                var replacedChild = root.Children[0];
                Assert.Equal("NewChild", replacedChild.Name);
                Assert.Equal("New", replacedChild.Text);

                // Assert node map was updated
                bool foundNewId = nodeMap.TryGetId(replacedChild, out int newChildId);
                Assert.True(foundNewId);
                Assert.Equal(childId, newChildId);

                // Assert XAML file was updated
                string updatedXaml = await File.ReadAllTextAsync(tempFile);
                Assert.Contains("Name=\"NewChild\"", updatedXaml);
                Assert.Contains("Text=\"New\"", updatedXaml);
                Assert.DoesNotContain("Name=\"MyChild\"", updatedXaml);
            }
            finally
            {
                if (File.Exists(tempFile))
                {
                    File.Delete(tempFile);
                }
            }
        }

        [Fact]
        public async Task TestMock_RemoveDynamicallyAddedNode()
        {
            string repoRoot = FindRepoRoot();
            string tempFile = Path.Combine(repoRoot, "TestControlWindow.xaml");
            string initialXaml = @"<TestControlWindow xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation""
        xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml""
        x:Class=""Xaml.Compiler.Tests.Mutation.TestControlWindow""
        Name=""MyRoot"">
    <TestControl Name=""MyContainer"">
        <TestControl Name=""MyChild"" />
    </TestControl>
</TestControlWindow>";

            await File.WriteAllTextAsync(tempFile, initialXaml);

            try
            {
                var root = new TestControlWindow { Name = "MyRoot" };
                var container = new TestControl { Name = "MyContainer", Parent = root };
                var child = new TestControl { Name = "MyChild", Parent = container };
                container.Children.Add(child);
                root.Children.Add(container);

                var adapter = new MockUiFrameworkAdapter();
                var nodeMap = new MockNodeMap();
                nodeMap.GetOrAdd(root);
                nodeMap.GetOrAdd(container);
                nodeMap.GetOrAdd(child);

                var engine = new LosslessXamlMutationEngine(adapter, nodeMap);

                // 1. Drop a new node inside MyContainer (simulating setOuterHtml on container)
                bool success = await engine.SetOuterHtmlAsync(container, @"<TestControl Name=""MyContainer""><TestControl Name=""MyChild"" /><TestControl Name=""NewChild"" /></TestControl>");
                Assert.True(success);

                // Assert live UI update occurred
                Assert.Single(root.Children);
                var newContainer = root.Children[0] as TestControl;
                Assert.NotNull(newContainer);
                Assert.Equal(2, newContainer.Children.Count);
                var newChild = newContainer.Children[1];
                Assert.Equal("NewChild", newChild.Name);

                // 2. Try to remove the new child
                bool removeSuccess = await engine.RemoveNodeAsync(newChild);
                Assert.True(removeSuccess);

                // Assert it was removed from live UI
                Assert.Single(newContainer.Children);
                Assert.Equal("MyChild", newContainer.Children[0].Name);

                // Assert XAML file was updated
                string updatedXaml = await File.ReadAllTextAsync(tempFile);
                Assert.DoesNotContain("Name=\"NewChild\"", updatedXaml);
            }
            finally
            {
                if (File.Exists(tempFile))
                {
                    File.Delete(tempFile);
                }
            }
        }

        [Fact]
        public void TestParseMainWindowXaml()
        {
            string repoRoot = FindRepoRoot();
            var path = Path.Combine(repoRoot, "samples", "CdpSampleApp", "MainWindow.axaml");
            Assert.True(File.Exists(path), $"File not found at: {path}");
            var text = File.ReadAllText(path);
            var doc = Xaml.Compiler.Parser.XamlParser.Parse(text);
            Assert.NotNull(doc);
            if (doc.Diagnostics != null && doc.Diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error))
            {
                var errors = string.Join("\n", doc.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).Select(d => $"{d.Message} at line {d.Span.Start.Line}, col {d.Span.Start.Column}"));
                Assert.Fail($"XAML parse errors:\n{errors}");
            }
        }
    }
}
