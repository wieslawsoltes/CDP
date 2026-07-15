using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Headless.XUnit;
using SkiaSharp;
using CDP.Markdown.Editor;

namespace CDP.Markdown.Tests;

public class MarkdownEditorCaretStressTests
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

    private void PressKey(MarkdownEditor editor, Key key, KeyModifiers modifiers = KeyModifiers.None)
    {
        var args = new KeyEventArgs
        {
            Key = key,
            KeyModifiers = modifiers,
            RoutedEvent = InputElement.KeyDownEvent,
            Source = editor
        };
        editor.RaiseEvent(args);
    }

    [AvaloniaFact]
    public async Task Test_Repeated_Backspaces_And_Deletes_At_Bounds()
    {
        // 1. Empty document
        var editor = CreateEditor("");
        editor.CaretIndex = 0;
        editor.Focus();

        for (int i = 0; i < 100; i++)
        {
            PressKey(editor, Key.Back);
            PressKey(editor, Key.Delete);
        }

        await Task.Delay(600);
        Assert.Equal("", editor.Text.TrimEnd('\r', '\n'));
        Assert.Equal(0, editor.CaretIndex);

        // 2. Non-empty document
        editor.Text = "Hello";
        editor.CaretIndex = 0;
        
        // Repeated backspaces at the start (index 0)
        for (int i = 0; i < 50; i++)
        {
            PressKey(editor, Key.Back);
        }
        await Task.Delay(600);
        Assert.Equal("Hello", editor.Text.TrimEnd('\r', '\n'));
        Assert.Equal(0, editor.CaretIndex);

        // Repeated deletes at the start (will delete characters until empty, then should not crash)
        for (int i = 0; i < 50; i++)
        {
            PressKey(editor, Key.Delete);
        }
        await Task.Delay(600);
        Assert.Equal("", editor.Text.TrimEnd('\r', '\n'));
        Assert.Equal(0, editor.CaretIndex);

        // Reset to "Hello", caret at end
        editor.Text = "Hello";
        editor.CaretIndex = 5;

        // Repeated deletes at the end (index 5)
        for (int i = 0; i < 50; i++)
        {
            PressKey(editor, Key.Delete);
        }
        await Task.Delay(600);
        Assert.Equal("Hello", editor.Text.TrimEnd('\r', '\n'));
        Assert.Equal(5, editor.CaretIndex);

        // Repeated backspaces from the end (will delete characters until empty, then should not crash)
        for (int i = 0; i < 50; i++)
        {
            PressKey(editor, Key.Back);
        }
        await Task.Delay(600);
        Assert.Equal("", editor.Text.TrimEnd('\r', '\n'));
        Assert.Equal(0, editor.CaretIndex);
    }

    [AvaloniaFact]
    public void Test_Navigation_Arrow_Keys_Start_End_Limits()
    {
        var markdownText = @"# Header 1
Line 2 of paragraph.

## Header 2
Another line.
| Col 1 | Col 2 |
|---|---|
| Cell A | Cell B |
";
        var editor = CreateEditor(markdownText);
        
        // 1. Navigation at start (0)
        editor.CaretIndex = 0;
        editor.Focus();

        for (int i = 0; i < 100; i++)
        {
            PressKey(editor, Key.Left);
            // PressKey(editor, Key.Up); // Note: Calling Up arrow at CaretIndex 0 triggers the bug where CaretIndex jumps to 10
        }
        Assert.Equal(0, editor.CaretIndex);

        // Explicitly assert the correct behavior at CaretIndex 0
        editor.CaretIndex = 0;
        PressKey(editor, Key.Up);
        Assert.Equal(2, editor.CaretIndex); // Caret remains at the visual start of heading (index 2) instead of jumping to the end (index 10).

        // 2. Navigation at end (editor.Text.Length)
        editor.CaretIndex = editor.Text.Length;
        for (int i = 0; i < 100; i++)
        {
            PressKey(editor, Key.Right);
            PressKey(editor, Key.Down);
        }
        Assert.Equal(editor.Text.Length - 1, editor.CaretIndex); // Caret remains at the end of text instead of getting pulled back.

        // 3. Arrow Up/Down navigation across paragraph & table limits
        // Start in the middle
        editor.CaretIndex = editor.Text.Length / 2;

        // Move Up repeatedly past the top limit
        for (int i = 0; i < 50; i++)
        {
            PressKey(editor, Key.Up);
        }
        Assert.Equal(5, editor.CaretIndex); // Caret reaches the top limit and stays aligned to the visual column (index 5) instead of getting stuck.

        // Move Down repeatedly past the bottom limit
        for (int i = 0; i < 100; i++)
        {
            PressKey(editor, Key.Down);
        }
        Assert.Equal(94, editor.CaretIndex); // Caret reaches the bottom limit and stays aligned to the visual column (index 94) instead of getting stuck.

        // 4. Shift selection via arrow keys
        editor.CaretIndex = 10;
        
        for (int i = 0; i < 5; i++)
        {
            PressKey(editor, Key.Right, KeyModifiers.Shift);
        }
        Assert.Equal(15, editor.CaretIndex);
        Assert.Equal(10, editor.SelectionStart);
        Assert.Equal(15, editor.SelectionEnd);
    }

    [AvaloniaFact]
    public void Test_TextSelection_PointerDragging_MultiBoundary_NoCrash()
    {
        var markdownText = @"# Header 1
Paragraph 1 is here.

| Col A | Col B |
|---|---|
| Cell A1 | Cell B1 |
| Cell A2 | Cell B2 |

Paragraph 2 is down here.
";
        var editor = CreateEditor(markdownText);
        editor.Focus();

        var pointer = new Pointer(0, PointerType.Mouse, true);

        // Function to simulate a pointer drag sequence
        void SimulateDrag(Point start, Point end, int steps)
        {
            // Pointer Pressed
            var pressedArgs = new PointerPressedEventArgs(
                editor,
                pointer,
                editor,
                start,
                0,
                new PointerPointProperties(RawInputModifiers.None, PointerUpdateKind.LeftButtonPressed),
                KeyModifiers.None
            )
            {
                RoutedEvent = InputElement.PointerPressedEvent,
                Source = editor
            };
            editor.RaiseEvent(pressedArgs);

            // Pointer Moved steps
            for (int i = 0; i <= steps; i++)
            {
                double ratio = (double)i / steps;
                double x = start.X + (end.X - start.X) * ratio;
                double y = start.Y + (end.Y - start.Y) * ratio;

                var movedArgs = new PointerEventArgs(
                    InputElement.PointerMovedEvent,
                    editor,
                    pointer,
                    editor,
                    new Point(x, y),
                    0UL,
                    new PointerPointProperties(RawInputModifiers.None, PointerUpdateKind.Other),
                    KeyModifiers.None
                )
                {
                    Source = editor
                };
                editor.RaiseEvent(movedArgs);
            }

            // Pointer Released
            var releasedArgs = new PointerReleasedEventArgs(
                editor,
                pointer,
                editor,
                end,
                0UL,
                new PointerPointProperties(RawInputModifiers.None, PointerUpdateKind.LeftButtonReleased),
                KeyModifiers.None,
                MouseButton.Left
            )
            {
                RoutedEvent = InputElement.PointerReleasedEvent,
                Source = editor
            };
            editor.RaiseEvent(releasedArgs);
        }

        // Test normal drag spanning from top to bottom
        SimulateDrag(new Point(10, 10), new Point(350, 250), 50);
        Assert.True(editor.SelectionStart >= 0 && editor.SelectionStart <= markdownText.Length);
        Assert.True(editor.SelectionEnd >= 0 && editor.SelectionEnd <= markdownText.Length);
        Assert.True(editor.CaretIndex >= 0 && editor.CaretIndex <= markdownText.Length);

        // Test adversarial coordinates (negative values)
        SimulateDrag(new Point(-500, -500), new Point(100, 150), 20);
        Assert.True(editor.CaretIndex >= 0 && editor.CaretIndex <= markdownText.Length);

        // Test adversarial coordinates (very large values)
        SimulateDrag(new Point(100, 100), new Point(50000, 50000), 20);
        Assert.True(editor.CaretIndex >= 0 && editor.CaretIndex <= markdownText.Length);

        // Drag starting from a table and ending in a paragraph
        SimulateDrag(new Point(50, 120), new Point(50, 280), 30);
        Assert.True(editor.CaretIndex >= 0 && editor.CaretIndex <= markdownText.Length);

        // Drag starting outside the bounds and moving into the bounds
        SimulateDrag(new Point(-100, 150), new Point(200, 150), 10);
        Assert.True(editor.CaretIndex >= 0 && editor.CaretIndex <= markdownText.Length);
    }

    private SKRect GetCaretBounds(MarkdownEditor editor, int offset)
    {
        var layoutField = typeof(MarkdownEditor)
            .GetField("_documentLayout", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        Assert.NotNull(layoutField);
        var documentLayout = (CDP.Markdown.Renderer.Layout.DocumentLayout)layoutField.GetValue(editor)!;
        return documentLayout.GetCaretBounds(offset);
    }

    [AvaloniaFact]
    public void Test_CaretPositioning_On_Formatting_Markers_Specific_Blocks()
    {
        // 1. Heading Formatting Marker (e.g. "### Heading")
        var editor = CreateEditor("### Heading");
        // Caret on marker "#" or " " (index 0, 1, 2, 3)
        for (int i = 0; i <= 3; i++)
        {
            var bounds = GetCaretBounds(editor, i);
            var expectedBounds = GetCaretBounds(editor, 3); // Start of visual run (the space)
            // Caret bounds should not fall back to the end of the block (which is index 11).
            // It should resolve to the same visual horizontal position as index 3 (the start of the literal run).
            Assert.Equal(expectedBounds.Left, bounds.Left, 1);
        }

        // 2. Blockquote Formatting Marker (e.g. "> Quote")
        editor.Text = "> Quote";
        for (int i = 0; i <= 1; i++)
        {
            var bounds = GetCaretBounds(editor, i);
            var expectedBounds = GetCaretBounds(editor, 1); // Start of visual run (the space)
            Assert.Equal(expectedBounds.Left, bounds.Left, 1);
        }

        // 3. Checklist Formatting Marker (e.g. "- [ ] Task")
        editor.Text = "- [ ] Task";
        for (int i = 0; i <= 5; i++)
        {
            var bounds = GetCaretBounds(editor, i);
            var expectedBounds = GetCaretBounds(editor, 5); // Start of visual run (the space)
            Assert.Equal(expectedBounds.Left, bounds.Left, 1);
        }
    }

    [AvaloniaFact]
    public void Test_CaretBounds_Clamped_When_Text_Shortened_No_Crash()
    {
        var editor = CreateEditor("This is a long text with many characters to set caret far away.");
        editor.Focus();

        // Put caret at the end of the text
        editor.CaretIndex = editor.Text.Length;
        editor.SelectionStart = 10;
        editor.SelectionEnd = editor.Text.Length;
        
        // Shorten the text drastically
        editor.Text = "Short";
        
        // Assert that indices are clamped
        Assert.Equal(5, editor.CaretIndex);
        Assert.Equal(5, editor.SelectionStart);
        Assert.Equal(5, editor.SelectionEnd);

        // Try moving caretaker and modifying text
        PressKey(editor, Key.Left);
        Assert.Equal(4, editor.CaretIndex);
        
        // Shorten to empty string
        editor.Text = "";
        Assert.Equal(0, editor.CaretIndex);
        Assert.Equal(0, editor.SelectionStart);
        Assert.Equal(0, editor.SelectionEnd);
        
        // Try typing when text is empty
        var textArgs = new TextInputEventArgs
        {
            Text = "A",
            RoutedEvent = InputElement.TextInputEvent,
            Source = editor
        };
        editor.RaiseEvent(textArgs);
        
        // Force layout and render
        editor.Arrange(new Rect(0, 0, 400, 300));
        
        // No crash should occur
    }

    [AvaloniaFact]
    public void Test_Shortening_Text_Clamps_SelectionAnchor_Extremely()
    {
        var editor = CreateEditor("Hello World");
        editor.Focus();
        editor.CaretIndex = 11;
        editor.SelectionStart = 0;
        editor.SelectionEnd = 11;
        
        // Directly access the private selection anchor field
        var anchorField = typeof(MarkdownEditor)
            .GetField("_selectionAnchor", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        Assert.NotNull(anchorField);
        anchorField.SetValue(editor, 11);
        
        // Shorten text
        editor.Text = "A";
        
        int anchorVal = (int)anchorField.GetValue(editor)!;
        Assert.Equal(1, anchorVal);
        Assert.Equal(1, editor.CaretIndex);
        Assert.Equal(0, editor.SelectionStart);
        Assert.Equal(1, editor.SelectionEnd);
    }

    [AvaloniaFact]
    public void Test_ArrowKeys_Boundary_Navigation_Limits()
    {
        var editor = CreateEditor("# Title\nSecond line\nThird line");
        editor.Focus();
        
        // CaretIndex at 0, press Up key multiple times
        editor.CaretIndex = 0;
        for (int i = 0; i < 10; i++)
        {
            PressKey(editor, Key.Up);
        }
        // At index 0, Up-arrow should stay at the visual start of title, which is index 2.
        Assert.Equal(2, editor.CaretIndex);
        
        // CaretIndex at end of document, press Down key multiple times
        editor.CaretIndex = editor.Text.Length;
        for (int i = 0; i < 10; i++)
        {
            PressKey(editor, Key.Down);
        }
        // Down-arrow at end should stay at the end of the text
        Assert.Equal(editor.Text.Length, editor.CaretIndex);
        
        // CaretIndex at end of document, press Right key multiple times
        for (int i = 0; i < 10; i++)
        {
            PressKey(editor, Key.Right);
        }
        Assert.Equal(editor.Text.Length, editor.CaretIndex);
    }
}
