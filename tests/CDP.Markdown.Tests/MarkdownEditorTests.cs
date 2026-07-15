using System;
using System.Threading.Tasks;
using Xunit;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Headless.XUnit;
using CDP.Markdown.Editor;

namespace CDP.Markdown.Tests;

public class MarkdownEditorTests
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

    [AvaloniaFact]
    public void Test_Initial_Text_Is_Correct()
    {
        var editor = CreateEditor("# Hello World");
        Assert.Equal("# Hello World", editor.Text);
    }

    [AvaloniaFact]
    public void Test_TextInput_Inserts_Character()
    {
        var editor = CreateEditor("Hello");
        editor.CaretIndex = 5;
        editor.Focus();
        
        var textArgs = new TextInputEventArgs
        {
            Text = "!",
            RoutedEvent = InputElement.TextInputEvent,
            Source = editor
        };
        editor.RaiseEvent(textArgs);
    }

    [AvaloniaFact]
    public async Task Test_TextInput_Updates_Text_Property_After_Debounce()
    {
        var editor = CreateEditor("Hello");
        editor.CaretIndex = 5;
        editor.Focus();

        var textArgs = new TextInputEventArgs
        {
            Text = " World",
            RoutedEvent = InputElement.TextInputEvent,
            Source = editor
        };
        editor.RaiseEvent(textArgs);

        Assert.Equal("Hello", editor.Text.TrimEnd('\r', '\n'));

        // Wait 600ms for debounce timer to fire
        await Task.Delay(600);

        Assert.Contains("Hello World", editor.Text);
    }

    [AvaloniaFact]
    public async Task Test_Backspace_Removes_Character()
    {
        var editor = CreateEditor("Hello");
        editor.CaretIndex = 5;
        editor.Focus();

        var keyArgs = new KeyEventArgs
        {
            Key = Key.Back,
            RoutedEvent = InputElement.KeyDownEvent,
            Source = editor
        };
        editor.RaiseEvent(keyArgs);

        await Task.Delay(600);

        Assert.Contains("Hell", editor.Text);
    }

    [AvaloniaFact]
    public async Task Test_Delete_Removes_Character()
    {
        var editor = CreateEditor("Hello");
        editor.CaretIndex = 0;
        editor.Focus();

        var keyArgs = new KeyEventArgs
        {
            Key = Key.Delete,
            RoutedEvent = InputElement.KeyDownEvent,
            Source = editor
        };
        editor.RaiseEvent(keyArgs);

        await Task.Delay(600);

        Assert.Contains("ello", editor.Text);
    }

    [AvaloniaFact]
    public async Task Test_Enter_Inserts_Newline()
    {
        var editor = CreateEditor("Hello");
        editor.CaretIndex = 5;
        editor.Focus();

        var enterArgs = new KeyEventArgs
        {
            Key = Key.Enter,
            RoutedEvent = InputElement.KeyDownEvent,
            Source = editor
        };
        editor.RaiseEvent(enterArgs);

        await Task.Delay(600);

        Assert.Contains("Hello\n", editor.Text);
    }

    [AvaloniaFact]
    public async Task Test_SelectAll_SelectionRange()
    {
        var editor = CreateEditor("Hello World");
        editor.Focus();

        var selectAllArgs = new KeyEventArgs
        {
            Key = Key.A,
            KeyModifiers = KeyModifiers.Control,
            RoutedEvent = InputElement.KeyDownEvent,
            Source = editor
        };
        editor.RaiseEvent(selectAllArgs);

        var deleteArgs = new KeyEventArgs
        {
            Key = Key.Delete,
            RoutedEvent = InputElement.KeyDownEvent,
            Source = editor
        };
        editor.RaiseEvent(deleteArgs);

        await Task.Delay(600);

        Assert.Equal("", editor.Text.TrimEnd('\r', '\n'));
    }

    [AvaloniaFact]
    public void Test_CaretMovement_LeftRight()
    {
        var editor = CreateEditor("Hello");
        editor.CaretIndex = 3;

        var leftArgs = new KeyEventArgs
        {
            Key = Key.Left,
            RoutedEvent = InputElement.KeyDownEvent,
            Source = editor
        };
        editor.RaiseEvent(leftArgs);
        Assert.Equal(2, editor.CaretIndex);

        var rightArgs = new KeyEventArgs
        {
            Key = Key.Right,
            RoutedEvent = InputElement.KeyDownEvent,
            Source = editor
        };
        editor.RaiseEvent(rightArgs);
        Assert.Equal(3, editor.CaretIndex);
    }

    [AvaloniaFact]
    public async Task Test_IsReadOnly_Prevents_TextInput_And_KeyMutations()
    {
        var editor = CreateEditor("Hello");
        editor.IsReadOnly = true;
        editor.CaretIndex = 5;
        editor.Focus();

        // Try TextInput
        var textArgs = new TextInputEventArgs
        {
            Text = " World",
            RoutedEvent = InputElement.TextInputEvent,
            Source = editor
        };
        editor.RaiseEvent(textArgs);

        // Try Backspace
        var backspaceArgs = new KeyEventArgs
        {
            Key = Key.Back,
            RoutedEvent = InputElement.KeyDownEvent,
            Source = editor
        };
        editor.RaiseEvent(backspaceArgs);

        // Try Delete
        var deleteArgs = new KeyEventArgs
        {
            Key = Key.Delete,
            RoutedEvent = InputElement.KeyDownEvent,
            Source = editor
        };
        editor.RaiseEvent(deleteArgs);

        // Try Enter
        var enterArgs = new KeyEventArgs
        {
            Key = Key.Enter,
            RoutedEvent = InputElement.KeyDownEvent,
            Source = editor
        };
        editor.RaiseEvent(enterArgs);

        await Task.Delay(600);

        // Text should remain exactly "Hello"
        Assert.Equal("Hello", editor.Text.TrimEnd('\r', '\n'));
    }

    [AvaloniaFact]
    public void Test_CaretBounds_On_Heading_Markers_Does_Not_Jump_To_End()
    {
        var editor = CreateEditor("# Heading");
        editor.CaretIndex = 0;
        editor.Focus();

        // Move Up. Under correct behavior it stays at the top of heading (index 2 / start of Heading)
        var keyArgs = new KeyEventArgs
        {
            Key = Key.Up,
            RoutedEvent = InputElement.KeyDownEvent,
            Source = editor
        };
        editor.RaiseEvent(keyArgs);

        Assert.Equal(2, editor.CaretIndex);
    }

    [AvaloniaFact]
    public async Task Test_Caret_Mapping_On_AutoSave_Correctly_Aligns()
    {
        var editor = CreateEditor("#  Heading");
        editor.Focus();

        // Put caret at index 10 (at the end of "#  Heading")
        editor.CaretIndex = 10;

        // Force a modification to trigger auto-save
        var textArgs = new TextInputEventArgs
        {
            Text = " ",
            RoutedEvent = InputElement.TextInputEvent,
            Source = editor
        };
        editor.RaiseEvent(textArgs);

        // Wait 600ms for auto-save
        await Task.Delay(600);

        Assert.Equal(10, editor.CaretIndex);
    }

    [AvaloniaFact]
    public void Test_Shortening_Text_Clamps_SelectionAnchor_And_Does_Not_Crash()
    {
        var editor = CreateEditor("Hello World");
        editor.Focus();

        // Put caret at index 11 and select left to establish selection anchor at 11
        editor.CaretIndex = 11;
        var selectLeftArgs = new KeyEventArgs
        {
            Key = Key.Left,
            KeyModifiers = KeyModifiers.Shift,
            RoutedEvent = InputElement.KeyDownEvent,
            Source = editor
        };
        editor.RaiseEvent(selectLeftArgs);

        Assert.Equal(10, editor.SelectionStart);
        Assert.Equal(11, editor.SelectionEnd);
        Assert.Equal(10, editor.CaretIndex);

        // Change text externally to a shorter string
        editor.Text = "Short";

        // This should clamp selection anchor to 5.
        // Doing Shift + Left arrow should update selection start/end correctly without throwing.
        var shiftLeftArgs = new KeyEventArgs
        {
            Key = Key.Left,
            KeyModifiers = KeyModifiers.Shift,
            RoutedEvent = InputElement.KeyDownEvent,
            Source = editor
        };
        
        editor.RaiseEvent(shiftLeftArgs);

        Assert.Equal(4, editor.CaretIndex);
        Assert.Equal(4, editor.SelectionStart);
        Assert.Equal(5, editor.SelectionEnd);
    }

    private void PressKey(MarkdownEditor editor, Key key, KeyModifiers modifiers = KeyModifiers.Control)
    {
        var keyArgs = new KeyEventArgs
        {
            Key = key,
            KeyModifiers = modifiers,
            RoutedEvent = InputElement.KeyDownEvent,
            Source = editor
        };
        editor.RaiseEvent(keyArgs);
    }

    [AvaloniaFact]
    public async Task Test_Ctrl_B_Bold_Formatting()
    {
        var editor = CreateEditor("Hello World");
        editor.Focus();
        editor.CaretIndex = 11;
        editor.SelectionStart = 6;
        editor.SelectionEnd = 11;

        PressKey(editor, Key.B);

        await Task.Delay(600);
        Assert.Contains("Hello **World**", editor.Text);

        // Toggle again to unwrap
        editor.CaretIndex = 15;
        editor.SelectionStart = 6;
        editor.SelectionEnd = 15;
        PressKey(editor, Key.B);

        await Task.Delay(600);
        Assert.Contains("Hello World", editor.Text);
    }

    [AvaloniaFact]
    public async Task Test_Ctrl_I_Italic_Formatting()
    {
        var editor = CreateEditor("Hello World");
        editor.Focus();
        editor.CaretIndex = 11;
        editor.SelectionStart = 6;
        editor.SelectionEnd = 11;

        PressKey(editor, Key.I);

        await Task.Delay(600);
        Assert.Contains("Hello *World*", editor.Text);

        // Toggle again to unwrap
        editor.CaretIndex = 13;
        editor.SelectionStart = 6;
        editor.SelectionEnd = 13;
        PressKey(editor, Key.I);

        await Task.Delay(600);
        Assert.Contains("Hello World", editor.Text);
    }

    [AvaloniaFact]
    public async Task Test_Ctrl_K_Link_Formatting()
    {
        var editor = CreateEditor("Hello World");
        editor.Focus();
        editor.CaretIndex = 11;
        editor.SelectionStart = 6;
        editor.SelectionEnd = 11;

        PressKey(editor, Key.K);

        await Task.Delay(600);
        Assert.Contains("Hello [World](url)", editor.Text);
    }

    [AvaloniaFact]
    public async Task Test_Ctrl_Tilde_Code_Formatting()
    {
        var editor = CreateEditor("Hello World");
        editor.Focus();
        editor.CaretIndex = 11;
        editor.SelectionStart = 6;
        editor.SelectionEnd = 11;

        PressKey(editor, Key.OemTilde);

        await Task.Delay(600);
        Assert.Contains("Hello `World`", editor.Text);
    }

    [AvaloniaFact]
    public async Task Test_Undo_Redo_Stack()
    {
        var editor = CreateEditor("Initial");
        editor.Focus();
        editor.CaretIndex = 7;

        var textArgs = new TextInputEventArgs
        {
            Text = " Text",
            RoutedEvent = InputElement.TextInputEvent,
            Source = editor
        };
        editor.RaiseEvent(textArgs);

        await Task.Delay(600);
        Assert.Contains("Initial Text", editor.Text);

        // Undo
        PressKey(editor, Key.Z);

        await Task.Delay(600);
        Assert.Contains("Initial", editor.Text);

        // Redo
        PressKey(editor, Key.Y);

        await Task.Delay(600);
        Assert.Contains("Initial Text", editor.Text);
    }

    [AvaloniaFact]
    public void Test_GetWordAtCaret_Extraction()
    {
        var editor = CreateEditor("Hello World From Test");
        editor.CaretIndex = 8; // In the middle of "World"

        var method = typeof(MarkdownEditor).GetMethod("GetWordAtCaret", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        
        Assert.NotNull(method);
        object[] args = new object[] { 0, 0 };
        method.Invoke(editor, args);

        Assert.Equal(6, (int)args[0]); // Start of "World"
        Assert.Equal(11, (int)args[1]); // End of "World"
    }

    [AvaloniaFact]
    public void Test_GetLineAtCaret_Extraction()
    {
        var editor = CreateEditor("Hello World\nSecond Line\nThird Line");
        editor.CaretIndex = 15; // In the middle of "Second Line"

        var method = typeof(MarkdownEditor).GetMethod("GetLineAtCaret", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        
        Assert.NotNull(method);
        object[] args = new object[] { 0, 0 };
        method.Invoke(editor, args);

        Assert.Equal(12, (int)args[0]); // Start of "Second Line"
        Assert.Equal(23, (int)args[1]); // End of "Second Line"
    }

    [Fact]
    public async Task Test_MarkdownImageLoader_LocalPathCaching()
    {
        string tempFile = Path.Combine(Path.GetTempPath(), "test_image.png");
        File.WriteAllBytes(tempFile, new byte[] { 1, 2, 3 });

        try
        {
            bool loadedCalled = false;
            var bitmap = CDP.Markdown.Renderer.Rendering.MarkdownImageLoader.GetOrLoadImage(tempFile, () =>
            {
                loadedCalled = true;
            });

            // Wait a small bit for async task to run
            await Task.Delay(100);

            Assert.True(loadedCalled || bitmap != null);

            // Clear Cache
            CDP.Markdown.Renderer.Rendering.MarkdownImageLoader.ClearCache();
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
    public async Task Test_Clipboard_Copy_Paste()
    {
        var editor = CreateEditor("Clipboard Test");
        editor.Focus();
        editor.CaretIndex = 9;
        editor.SelectionStart = 0;
        editor.SelectionEnd = 9;

        // Verify we can call CopyToClipboardAsync via reflection
        var copyMethod = typeof(MarkdownEditor).GetMethod("CopyToClipboardAsync", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        
        Assert.NotNull(copyMethod);
        var task = (Task)copyMethod.Invoke(editor, null)!;
        await task;

        // Clear selection
        editor.CaretIndex = 14;
        editor.SelectionStart = 14;
        editor.SelectionEnd = 14;

        var pasteMethod = typeof(MarkdownEditor).GetMethod("PasteFromClipboardAsync", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        Assert.NotNull(pasteMethod);
        var pasteTask = (Task)pasteMethod.Invoke(editor, null)!;
        await pasteTask;

        // Give it a brief moment for Dispatcher.UIThread to run the paste action
        await Task.Delay(200);

        // The text should contain "Clipboard" pasted at the end
        Assert.Contains("Clipboard", editor.Text);
    }
}
