using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Xunit;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using CDP.Markdown.Editor;
using CDP.Markdown.Parser;
using CDP.Markdown.Renderer.Layout;

namespace CDP.Markdown.Tests;

public class MarkdownEditorChallengerTests
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

    private bool IsAutoSaveTimerEnabled(MarkdownEditor editor)
    {
        var field = typeof(MarkdownEditor)
            .GetField("_autoSaveTimer", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        Assert.NotNull(field);
        var timer = (DispatcherTimer)field.GetValue(editor)!;
        return timer.IsEnabled;
    }

    private string GetInternalText(MarkdownEditor editor)
    {
        var field = typeof(MarkdownEditor)
            .GetField("_internalText", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        Assert.NotNull(field);
        return (string)field.GetValue(editor)!;
    }

    [AvaloniaFact]
    public void Test_CaretJump_OnCheckboxToggle_Scenario()
    {
        var originalMarkdown = "- [ ] Task 1\n- [ ] Task 2\n";
        var editor = CreateEditor(originalMarkdown);

        var window = new Window
        {
            Width = 400,
            Height = 300,
            Content = editor
        };
        window.Show();
        editor.Arrange(new Rect(0, 0, 400, 300));
        editor.Focus();

        var layoutField = typeof(MarkdownEditor)
            .GetField("_documentLayout", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        Assert.NotNull(layoutField);
        var documentLayout = (DocumentLayout)layoutField.GetValue(editor)!;

        var listItems = FindAllListItemLayoutBlocks(documentLayout.Blocks);
        Assert.Equal(2, listItems.Count);

        // Caret in Task 2 at index 20 (after 'T')
        editor.CaretIndex = 20;

        var firstItem = listItems[0];
        float boxX = firstItem.Bounds.Left + 8f;
        float boxY = firstItem.Bounds.Top + 4f;
        float clickX = boxX + 7f;
        float clickY = boxY + 7f;

        var clickArgs = new PointerPressedEventArgs(
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

        var onPointerPressedMethod = typeof(MarkdownEditor).GetMethod("OnPointerPressed", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        Assert.NotNull(onPointerPressedMethod);

        onPointerPressedMethod.Invoke(editor, new[] { clickArgs });

        try
        {
            // The caret should remain inside Task 2 at index 20.
            // If the mapping is bugged, it will jump to index 12 (inside Task 1).
            Assert.Equal(20, editor.CaretIndex);
        }
        finally
        {
            window.Close();
            Dispatcher.UIThread.RunJobs();
        }
    }

    [AvaloniaFact]
    public async Task Test_CaretDrift_OnAutoSave_Formatting_Scenario()
    {
        var editor = CreateEditor("This is **bold** text");
        editor.Focus();

        // Place caret after 'd' (index 14).
        editor.CaretIndex = 14;

        // Force user edit to trigger auto save
        var textArgs = new TextInputEventArgs
        {
            Text = " ",
            RoutedEvent = InputElement.TextInputEvent,
            Source = editor
        };
        editor.RaiseEvent(textArgs);

        int caretBeforeSave = editor.CaretIndex;

        // Wait 600ms for auto save
        await Task.Delay(600);

        // Caret should remain at the same logical/physical position (index 15).
        // If the serializer escapes it, caret will jump/drift (e.g. to 17).
        Assert.Equal(15, editor.CaretIndex);
    }

    [AvaloniaFact]
    public void Test_IsReadOnly_Blocks_CheckboxMutation_Scenario()
    {
        var originalMarkdown = "- [ ] Task 1\n- [ ] Task 2\n";
        var editor = CreateEditor(originalMarkdown);
        editor.IsReadOnly = true;

        var window = new Window
        {
            Width = 400,
            Height = 300,
            Content = editor
        };
        window.Show();
        editor.Arrange(new Rect(0, 0, 400, 300));
        editor.Focus();

        var layoutField = typeof(MarkdownEditor)
            .GetField("_documentLayout", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        Assert.NotNull(layoutField);
        var documentLayout = (DocumentLayout)layoutField.GetValue(editor)!;

        var listItems = FindAllListItemLayoutBlocks(documentLayout.Blocks);
        Assert.Equal(2, listItems.Count);

        var firstItem = listItems[0];
        float boxX = firstItem.Bounds.Left + 8f;
        float boxY = firstItem.Bounds.Top + 4f;
        float clickX = boxX + 7f;
        float clickY = boxY + 7f;

        var clickArgs = new PointerPressedEventArgs(
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

        var onPointerPressedMethod = typeof(MarkdownEditor).GetMethod("OnPointerPressed", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        Assert.NotNull(onPointerPressedMethod);
        onPointerPressedMethod.Invoke(editor, new[] { clickArgs });

        try
        {
            var updatedItems = FindAllListItemLayoutBlocks(documentLayout.Blocks);
            // Checkbox should NOT have mutated when IsReadOnly is true
            Assert.False(updatedItems[0].Node.IsChecked);
            Assert.False(IsAutoSaveTimerEnabled(editor));
        }
        finally
        {
            window.Close();
            Dispatcher.UIThread.RunJobs();
        }
    }

    [AvaloniaFact]
    public async Task Test_IsReadOnly_Blocks_Keyboard_Input_Scenario()
    {
        var editor = CreateEditor("Initial Text");
        editor.IsReadOnly = true;
        editor.CaretIndex = 12;
        editor.Focus();

        var textArgs = new TextInputEventArgs
        {
            Text = "Added",
            RoutedEvent = InputElement.TextInputEvent,
            Source = editor
        };
        editor.RaiseEvent(textArgs);

        var backspaceArgs = new KeyEventArgs
        {
            Key = Key.Back,
            RoutedEvent = InputElement.KeyDownEvent,
            Source = editor
        };
        editor.RaiseEvent(backspaceArgs);

        await Task.Delay(600);

        // Text should remain unmodified
        Assert.Equal("Initial Text", editor.Text);
        Assert.False(IsAutoSaveTimerEnabled(editor));
    }

    [AvaloniaFact]
    public async Task Test_FastTyping_StateTransitions_Scenario()
    {
        var editor = CreateEditor("Start: ");
        editor.CaretIndex = editor.Text.Length;
        editor.Focus();

        for (int i = 0; i < 5; i++)
        {
            var textArgs = new TextInputEventArgs
            {
                Text = i.ToString(),
                RoutedEvent = InputElement.TextInputEvent,
                Source = editor
            };
            editor.RaiseEvent(textArgs);
            await Task.Delay(100);
        }

        // Timer is running but public Text is not saved yet
        Assert.Equal("Start: ", editor.Text);

        await Task.Delay(600);

        // Text should be saved after debounce interval
        Assert.Equal("Start: 01234\n", editor.Text);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private WeakReference CreateAndCloseEditor(int index)
    {
        var editor = new MarkdownEditor
        {
            Width = 200,
            Height = 150,
            Text = $"Editor {index}"
        };
        var window = new Window { Content = editor };
        window.Show();
        editor.Arrange(new Rect(0, 0, 200, 150));
        editor.Focus();

        var reference = new WeakReference(editor);

        window.Close();
        return reference;
    }

    [AvaloniaFact]
    public async Task Test_MemoryLeaks_DetachedControls_Scenario()
    {
        var references = new List<WeakReference>();

        for (int i = 0; i < 10; i++)
        {
            references.Add(CreateAndCloseEditor(i));
        }

        // Let the dispatcher finish close jobs completely
        for (int k = 0; k < 5; k++)
        {
            Dispatcher.UIThread.RunJobs();
            await Task.Delay(50);
        }

        // Trigger garbage collection
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        int aliveCount = 0;
        foreach (var r in references)
        {
            if (r.IsAlive)
            {
                aliveCount++;
            }
        }

        // Assert that at least one of the editors has been successfully collected
        // to show memory leak profiling works. If all 10 are alive, then a hard root exists.
        Assert.Equal(0, aliveCount);
    }
}
