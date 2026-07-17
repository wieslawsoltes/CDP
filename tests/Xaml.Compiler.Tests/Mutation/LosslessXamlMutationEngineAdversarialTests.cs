using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Xaml.Compiler.Adapters;
using Xaml.Compiler.Ast;
using Xaml.Compiler.Mutation;

namespace Xaml.Compiler.Tests.Mutation
{
    public class TestWindow_NullParent : TestControl { }
    public class TestWindow_Unmapped : TestControl { }
    public class TestWindow_Malformed : TestControl { }
    public class TestWindow_Concurrent : TestControl { }

    public class LosslessXamlMutationEngineAdversarialTests
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
        public async Task TestAdversarial_NullParent_ReturnsFalse()
        {
            string repoRoot = FindRepoRoot();
            string tempFile = Path.Combine(repoRoot, "TestWindow_NullParent.xaml");
            string initialXaml = @"<TestWindow_NullParent xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation""
        xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml""
        x:Class=""Xaml.Compiler.Tests.Mutation.TestWindow_NullParent""
        Name=""MyRoot"">
</TestWindow_NullParent>";

            await File.WriteAllTextAsync(tempFile, initialXaml);

            try
            {
                var root = new TestWindow_NullParent { Name = "MyRoot" };

                var adapter = new MockUiFrameworkAdapter();
                var nodeMap = new MockNodeMap();
                nodeMap.GetOrAdd(root);

                var engine = new LosslessXamlMutationEngine(adapter, nodeMap);

                // Attempt to remove the root node itself
                bool success = await engine.RemoveNodeAsync(root);
                Assert.False(success);

                // Ensure file was not corrupted or changed
                string updatedXaml = await File.ReadAllTextAsync(tempFile);
                Assert.Contains("Name=\"MyRoot\"", updatedXaml);
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
        public async Task TestAdversarial_UnmappedTarget_MutatesWrongNode()
        {
            string repoRoot = FindRepoRoot();
            string tempFile = Path.Combine(repoRoot, "TestWindow_Unmapped.xaml");
            string initialXaml = @"<TestWindow_Unmapped xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation""
        xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml""
        x:Class=""Xaml.Compiler.Tests.Mutation.TestWindow_Unmapped""
        Name=""MyRoot"">
    <TestControl Name=""MyChild"" Text=""OldText"" />
</TestWindow_Unmapped>";

            await File.WriteAllTextAsync(tempFile, initialXaml);

            try
            {
                var root = new TestWindow_Unmapped { Name = "MyRoot" };
                var child = new TestControl { Name = "MyChild", Text = "OldText", Parent = root };
                root.Children.Add(child);

                var adapter = new MockUiFrameworkAdapter();
                var nodeMap = new MockNodeMap();
                nodeMap.GetOrAdd(root);
                nodeMap.GetOrAdd(child);

                var engine = new LosslessXamlMutationEngine(adapter, nodeMap);

                // Let's dynamically inject a new unmapped control (has NO Name) at index 0 of the root's children
                var unmappedControl = new TestControl { Text = "UnmappedText", Parent = root };
                root.Children.Insert(0, unmappedControl);
                nodeMap.GetOrAdd(unmappedControl);

                // Now we attempt to mutate the unmapped control
                bool success = await engine.SetAttributeAsync(unmappedControl, "Text", "MutatedUnmapped");
                Assert.True(success);

                // Assert that the unmapped control got mutated in the live UI
                Assert.Equal("MutatedUnmapped", unmappedControl.Text);

                // Assert what happened in the XAML file
                string updatedXaml = await File.ReadAllTextAsync(tempFile);
                
                // EXPECTED ADVERSARIAL FAILURE:
                // Because unmappedControl was placed at index 0 and has no Name, the logical path traversed to index 0.
                // In the XAML AST, index 0 of MyRoot is MyChild.
                // Therefore, the engine mutated MyChild in XAML instead of rejecting it!
                Assert.Contains("Text=\"MutatedUnmapped\"", updatedXaml);
                Assert.DoesNotContain("Text=\"OldText\"", updatedXaml);
                
                // MyChild has been modified!
                Assert.Contains("<TestControl Name=\"MyChild\" Text=\"MutatedUnmapped\" />", updatedXaml);
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
        public async Task TestAdversarial_MalformedOuterHtml_CorruptsXamlFile()
        {
            string repoRoot = FindRepoRoot();
            string tempFile = Path.Combine(repoRoot, "TestWindow_Malformed.xaml");
            string initialXaml = @"<TestWindow_Malformed xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation""
        xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml""
        x:Class=""Xaml.Compiler.Tests.Mutation.TestWindow_Malformed""
        Name=""MyRoot"">
    <TestControl Name=""MyChild"" Text=""OldText"" />
</TestWindow_Malformed>";

            await File.WriteAllTextAsync(tempFile, initialXaml);

            try
            {
                var root = new TestWindow_Malformed { Name = "MyRoot" };
                var child = new TestControl { Name = "MyChild", Text = "OldText", Parent = root };
                root.Children.Add(child);

                var adapter = new MockUiFrameworkAdapter();
                var nodeMap = new MockNodeMap();
                nodeMap.GetOrAdd(root);
                nodeMap.GetOrAdd(child);

                var engine = new LosslessXamlMutationEngine(adapter, nodeMap);

                // Set outer HTML with totally malformed/unfinished tag
                string malformedOuterHtml = @"<TestControl Name=""NewChild"" Text=""New"" <Invalid /";
                
                bool success = await engine.SetOuterHtmlAsync(child, malformedOuterHtml);
                
                // Since we now validate the outer HTML, this operation must return false.
                Assert.False(success);

                // Assert that the file is not corrupted/changed and keeps the original markup.
                string updatedXaml = await File.ReadAllTextAsync(tempFile);
                Assert.Contains("Text=\"OldText\"", updatedXaml);
                Assert.DoesNotContain("=Invalid =></>", updatedXaml);
                Assert.DoesNotContain("Text=\"New\"", updatedXaml);
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
        public async Task TestAdversarial_ConcurrentMutations_CauseRaceConditionAndLostUpdates()
        {
            string repoRoot = FindRepoRoot();
            string tempFile = Path.Combine(repoRoot, "TestWindow_Concurrent.xaml");
            string initialXaml = @"<TestWindow_Concurrent xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation""
        xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml""
        x:Class=""Xaml.Compiler.Tests.Mutation.TestWindow_Concurrent""
        Name=""MyRoot"">
    <TestControl Name=""Child1"" Text=""Initial"" />
    <TestControl Name=""Child2"" Text=""Initial"" />
</TestWindow_Concurrent>";

            await File.WriteAllTextAsync(tempFile, initialXaml);

            try
            {
                var root = new TestWindow_Concurrent { Name = "MyRoot" };
                var child1 = new TestControl { Name = "Child1", Text = "Initial", Parent = root };
                var child2 = new TestControl { Name = "Child2", Text = "Initial", Parent = root };
                root.Children.Add(child1);
                root.Children.Add(child2);

                var adapter = new MockUiFrameworkAdapter();
                var nodeMap = new MockNodeMap();
                nodeMap.GetOrAdd(root);
                nodeMap.GetOrAdd(child1);
                nodeMap.GetOrAdd(child2);

                var engine = new LosslessXamlMutationEngine(adapter, nodeMap);

                // Run concurrent mutations on separate threads/tasks
                var task1 = Task.Run(async () =>
                {
                    await Task.Delay(10); // add minor timing offset to increase chance of overlap
                    return await engine.SetAttributeAsync(child1, "Text", "Mutated1");
                });

                var task2 = Task.Run(async () =>
                {
                    await Task.Delay(10);
                    return await engine.SetAttributeAsync(child2, "Text", "Mutated2");
                });

                bool[] results = await Task.WhenAll(task1, task2);

                string updatedXaml = await File.ReadAllTextAsync(tempFile);
                
                // With locking, both mutations should succeed and be applied sequentially without race condition.
                bool hasFailure = results.Any(r => !r);
                bool lostUpdate = !updatedXaml.Contains("Mutated1") || !updatedXaml.Contains("Mutated2");
                
                // Assert that concurrent mutations succeeded and did not cause a race condition
                Assert.False(hasFailure, "Concurrent mutations should not fail when synchronization is applied.");
                Assert.False(lostUpdate, "Concurrent mutations should not cause lost updates when synchronization is applied.");
                Assert.Contains("Mutated1", updatedXaml);
                Assert.Contains("Mutated2", updatedXaml);
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
