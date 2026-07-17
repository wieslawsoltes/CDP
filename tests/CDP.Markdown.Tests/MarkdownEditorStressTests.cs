using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using SkiaSharp;
using CDP.Markdown.Editor;
using CDP.Markdown.Parser;
using CDP.Markdown.Parser.AST;
using CDP.Markdown.Renderer.Layout;

namespace CDP.Markdown.Tests;

public class MarkdownEditorStressTests
{
    private MarkdownEditor CreateEditor(string initialText = "")
    {
        var editor = new MarkdownEditor
        {
            Width = 400,
            Height = 300,
            Text = initialText
        };
        editor.Arrange(new Rect(0, 0, 400, 300));
        return editor;
    }

    private string GetInternalText(MarkdownEditor editor)
    {
        var field = typeof(MarkdownEditor)
            .GetField("_internalText", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        Assert.NotNull(field);
        return (string)field.GetValue(editor)!;
    }

    private bool IsAutoSaveTimerEnabled(MarkdownEditor editor)
    {
        var field = typeof(MarkdownEditor)
            .GetField("_autoSaveTimer", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        Assert.NotNull(field);
        var timer = (DispatcherTimer)field.GetValue(editor)!;
        return timer.IsEnabled;
    }

    private List<ListItemLayoutBlock> FindAllListItemLayoutBlocks(IEnumerable<ILayoutBlock> blocks)
    {
        var result = new List<ListItemLayoutBlock>();
        foreach (var block in blocks)
        {
            if (block is ListLayoutBlock listBlock)
            {
                result.AddRange(FindAllListItemLayoutBlocks(listBlock.Items));
            }
            else if (block is ListItemLayoutBlock listItemBlock)
            {
                result.Add(listItemBlock);
                result.AddRange(FindAllListItemLayoutBlocks(listItemBlock.InnerBlocks));
            }
        }
        return result;
    }

    [AvaloniaFact]
    public async Task Test_FastTyping_DoesNotSavePrematurely()
    {
        var editor = CreateEditor("Start: ");
        editor.CaretIndex = editor.Text.Length;
        editor.Focus();

        Assert.Equal("Start: ", editor.Text);

        // We will type 5 characters, waiting 150ms between each character.
        // Total elapsed time will be ~750ms, but since each character is typed
        // within the 500ms window, the debounce timer should be reset each time
        // and should NOT tick.
        for (int i = 0; i < 5; i++)
        {
            var textArgs = new TextInputEventArgs
            {
                Text = i.ToString(),
                RoutedEvent = InputElement.TextInputEvent,
                Source = editor
            };
            editor.RaiseEvent(textArgs);

            // Wait 150ms
            await Task.Delay(150);

            // Assert that the public Text property has NOT been updated to include the new typed text
            // because the debounce timer should not have fired yet.
            Assert.Equal("Start: ", editor.Text);
        }

        // Now, wait 600ms (longer than the 500ms debounce interval) without typing.
        await Task.Delay(600);

        // The auto-save should have fired and updated the Text property to include "01234".
        Assert.Contains("Start: 01234", editor.Text);
    }

    [AvaloniaFact]
    public async Task Test_Concurrency_And_StateTransitions()
    {
        var editor = CreateEditor("Initial");
        editor.CaretIndex = editor.Text.Length;
        editor.Focus();
        
        // State 1: Saved state
        Assert.Equal("Initial", editor.Text);
        Assert.Equal("Initial", GetInternalText(editor));
        Assert.False(IsAutoSaveTimerEnabled(editor));

        // Start multiple concurrent tasks to simulate rapid updates
        int taskCount = 10;
        var tasks = new List<Task>();
        for (int i = 0; i < taskCount; i++)
        {
            int index = i;
            tasks.Add(Task.Run(async () =>
            {
                // Simulate some random latency
                await Task.Delay(index * 10);
                
                // Dispatch text update back to the UI thread
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    var textArgs = new TextInputEventArgs
                    {
                        Text = $" {index}",
                        RoutedEvent = InputElement.TextInputEvent,
                        Source = editor
                    };
                    editor.RaiseEvent(textArgs);
                });
            }));
        }

        // Wait for all concurrent tasks to push their input events
        await Task.WhenAll(tasks);

        // State 2: Modified state (timer is running, internal text has changes, but public Text is not updated yet)
        Assert.True(IsAutoSaveTimerEnabled(editor));
        
        var publicText = editor.Text.TrimEnd('\r', '\n');
        var internalText = GetInternalText(editor);

        // State 3: Transition to Saved (Wait for timer to tick and save)
        await Task.Delay(600);

        // State 4: Saved state (timer stopped, public text updated)
        Assert.False(IsAutoSaveTimerEnabled(editor));
        Assert.Equal(GetInternalText(editor), editor.Text);
        Assert.Contains("Initial", editor.Text);
    }

    [AvaloniaFact]
    public void Test_Checklist_Click_Mutation_And_Serialization()
    {
        var originalMarkdown = "- [ ] Task 1\n- [x] Task 2\n";
        var editor = new MarkdownEditor
        {
            Width = 400,
            Height = 300,
            Text = originalMarkdown
        };

        // Attach editor to a window to enable coordinate translation
        var window = new Window
        {
            Width = 400,
            Height = 300,
            Content = editor
        };
        window.Show();
        editor.Arrange(new Rect(0, 0, 400, 300));
        editor.Focus();

        // Get document layout to locate the items
        var layoutField = typeof(MarkdownEditor)
            .GetField("_documentLayout", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        Assert.NotNull(layoutField);
        var documentLayout = (DocumentLayout)layoutField.GetValue(editor)!;

        Assert.NotNull(documentLayout);

        var listItems = FindAllListItemLayoutBlocks(documentLayout.Blocks);
        
        // Let's print out basic info for debugging if it fails again
        Assert.Equal(2, listItems.Count);

        var firstItem = listItems[0];
        var secondItem = listItems[1];

        Assert.NotNull(firstItem.Node.IsChecked);
        Assert.False(firstItem.Node.IsChecked.Value);

        Assert.NotNull(secondItem.Node.IsChecked);
        Assert.True(secondItem.Node.IsChecked.Value);

        // Try direct click simulation on the editor
        float boxX = firstItem.Bounds.Left + 8f;
        float boxY = firstItem.Bounds.Top + 4f;
        float clickX = boxX + 7f;
        float clickY = boxY + 7f;

        var clickFirstArgs = new PointerPressedEventArgs(
            editor,
            new Pointer(0, PointerType.Mouse, true),
            editor,
            new Point(clickX, clickY),
            0,
            new PointerPointProperties(RawInputModifiers.LeftMouseButton, PointerUpdateKind.LeftButtonPressed),
            KeyModifiers.None
        )
        {
            RoutedEvent = InputElement.PointerPressedEvent
        };

        // Call OnPointerPressed directly via reflection
        var onPointerPressedMethod = typeof(MarkdownEditor).GetMethod("OnPointerPressed", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        Assert.NotNull(onPointerPressedMethod);
        onPointerPressedMethod.Invoke(editor, new[] { clickFirstArgs });

        try
        {
            // Retrieve new layout blocks since ParseAndLayout was called
            var updatedListItems = FindAllListItemLayoutBlocks(documentLayout.Blocks);
            Assert.Equal(2, updatedListItems.Count);

            var newFirstItem = updatedListItems[0];
            var newSecondItem = updatedListItems[1];

            // Verify first item is checked now
            Assert.NotNull(newFirstItem.Node.IsChecked);
            Assert.True(newFirstItem.Node.IsChecked.Value);
            
            // Verify serialized Markdown text was updated to checked immediately
            Assert.False(IsAutoSaveTimerEnabled(editor));
            var expectedMarkdownAfterFirstClick = "- [x] Task 1\n- [x] Task 2\n";
            Assert.Equal(expectedMarkdownAfterFirstClick, editor.Text);

            // Now, click the second item (which was checked, so it should become unchecked)
            // Retrieve bounds from the fresh layout block!
            float boxX2 = newSecondItem.Bounds.Left + 8f;
            float boxY2 = newSecondItem.Bounds.Top + 4f;
            float clickX2 = boxX2 + 7f;
            float clickY2 = boxY2 + 7f;

            var clickSecondArgs = new PointerPressedEventArgs(
                editor,
                new Pointer(1, PointerType.Mouse, true),
                editor,
                new Point(clickX2, clickY2),
                0,
                new PointerPointProperties(RawInputModifiers.LeftMouseButton, PointerUpdateKind.LeftButtonPressed),
                KeyModifiers.None
            )
            {
                RoutedEvent = InputElement.PointerPressedEvent
            };

            onPointerPressedMethod.Invoke(editor, new[] { clickSecondArgs });

            // Retrieve final layout blocks to verify the second click
            var finalListItems = FindAllListItemLayoutBlocks(documentLayout.Blocks);
            Assert.Equal(2, finalListItems.Count);

            // Verify it was mutated to unchecked immediately
            Assert.False(finalListItems[1].Node.IsChecked == true);

            // Verify serialization immediately
            Assert.False(IsAutoSaveTimerEnabled(editor));
            var expectedMarkdownAfterSecondClick = "- [x] Task 1\n- [ ] Task 2\n";
            Assert.Equal(expectedMarkdownAfterSecondClick, editor.Text);
        }
        finally
        {
            window.Close();
        }
    }
}
