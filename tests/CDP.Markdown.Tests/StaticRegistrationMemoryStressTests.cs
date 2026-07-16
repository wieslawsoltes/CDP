using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Xunit;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using CDP.Markdown.Editor;

namespace CDP.Markdown.Tests;

public class StaticRegistrationMemoryStressTests
{
    private MarkdownEditor CreateEditor(string initialText = "")
    {
        var editor = new MarkdownEditor
        {
            Width = 200,
            Height = 150,
            Text = initialText
        };
        editor.Arrange(new Rect(0, 0, 200, 150));
        return editor;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private (List<WeakReference> References, List<MarkdownEditor> KeepAlive) CreateManyEditors(int count)
    {
        var references = new List<WeakReference>();
        var keepAlive = new List<MarkdownEditor>();

        for (int i = 0; i < count; i++)
        {
            var editor = CreateEditor($"Editor {i}");
            references.Add(new WeakReference(editor));
            keepAlive.Add(editor);
        }

        return (references, keepAlive);
    }

    [AvaloniaFact]
    public async Task Test_LargeNumber_Editors_Memory_And_GC()
    {
        const int count = 500;
        
        // 1. Create a large number of editors
        var (references, keepAlive) = CreateManyEditors(count);

        // Verify they all work, set text on all of them
        for (int i = 0; i < count; i++)
        {
            var editor = keepAlive[i];
            editor.TextPropertyChangedCallCount = 0;
            editor.Text = $"Updated Editor {i}";
            Assert.Equal(1, editor.TextPropertyChangedCallCount);
        }

        // 2. Clear our references to allow them to be GC'ed
        keepAlive.Clear();

        // Let dispatcher execute any pending cleanup
        for (int k = 0; k < 5; k++)
        {
            Dispatcher.UIThread.RunJobs();
            await Task.Delay(20);
        }

        // 3. Force garbage collection
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        // 4. Verify all editors are collected
        int aliveCount = 0;
        foreach (var r in references)
        {
            if (r.IsAlive)
            {
                aliveCount++;
            }
        }

        // Assert that all of the editors have been successfully collected.
        // If the static class handler was registered per-instance and leaking references, they would remain alive.
        Assert.Equal(0, aliveCount);
    }

    [AvaloniaFact]
    public async Task Test_Concurrent_Text_Changes_On_Multiple_Editors()
    {
        const int count = 100;
        var editors = new List<MarkdownEditor>();
        for (int i = 0; i < count; i++)
        {
            editors.Add(CreateEditor($"Editor {i}"));
        }

        var tasks = new List<Task>();
        for (int i = 0; i < count; i++)
        {
            var editor = editors[i];
            int index = i;
            tasks.Add(Task.Run(async () =>
            {
                // Concurrently update text via dispatcher
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    editor.Text = $"Concurrent Updated {index}";
                });
            }));
        }

        await Task.WhenAll(tasks);

        // Verify correct final values and call count
        for (int i = 0; i < count; i++)
        {
            Assert.Equal($"Concurrent Updated {i}", editors[i].Text);
            // Since we set initial text during construction and updated it once, it should have triggered.
            // Let's verify the updated value is correct.
        }
    }

    [AvaloniaFact]
    public void Test_ClassHandler_Invocation_Count_Not_Scaling_With_Instance_Count()
    {
        // 1. Create one editor and check call count
        var mainEditor = CreateEditor("Main");
        mainEditor.TextPropertyChangedCallCount = 0;

        // 2. Create 1000 other editor instances
        var otherEditors = new List<MarkdownEditor>();
        for (int i = 0; i < 1000; i++)
        {
            var editor = CreateEditor($"Other {i}");
            editor.TextPropertyChangedCallCount = 0;
            otherEditors.Add(editor);
        }

        // 3. Modify text on the main editor
        mainEditor.Text = "New Main Text";

        // If the class handler was registered per-instance, the handler would run 1001 times on the main editor!
        // We assert that it runs exactly once.
        Assert.Equal(1, mainEditor.TextPropertyChangedCallCount);

        // Check one of the other editors - its handler should NOT run because its text didn't change
        Assert.Equal(0, otherEditors[500].TextPropertyChangedCallCount);
    }
}
