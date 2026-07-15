using System;
using System.Collections.Generic;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using Avalonia.Interactivity;
using SkiaSharp;
using CDP.Markdown.Parser;
using CDP.Markdown.Parser.AST;
using CDP.Markdown.Renderer.Layout;
using CDP.Markdown.Renderer.Rendering;

namespace CDP.Markdown.Editor;

public class MarkdownEditor : Control, ILogicalScrollable
{
    public static readonly StyledProperty<string> TextProperty =
        AvaloniaProperty.Register<MarkdownEditor, string>(nameof(Text), string.Empty, defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    public string Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public static readonly StyledProperty<bool> IsReadOnlyProperty =
        AvaloniaProperty.Register<MarkdownEditor, bool>(nameof(IsReadOnly), false);

    public bool IsReadOnly
    {
        get => GetValue(IsReadOnlyProperty);
        set => SetValue(IsReadOnlyProperty, value);
    }

    public static readonly StyledProperty<bool> IsDarkThemeProperty =
        AvaloniaProperty.Register<MarkdownEditor, bool>(nameof(IsDarkTheme), true);

    public bool IsDarkTheme
    {
        get => GetValue(IsDarkThemeProperty);
        set => SetValue(IsDarkThemeProperty, value);
    }

    private double _scrollOffsetY;

    // ILogicalScrollable implementation
    public bool CanHorizontallyScroll { get; set; }
    public bool CanVerticallyScroll { get; set; }
    public bool IsLogicalScrollEnabled => true;
    public Size ScrollSize => new(16, 16);
    public Size PageScrollSize => new(80, 80);
    public event EventHandler? ScrollInvalidated;

    public Size Extent => _documentLayout.Bounds.Height == 0 ? new Size(0, 0) : new Size(Bounds.Width, _documentLayout.Bounds.Height);
    public Size Viewport => Bounds.Size;

    public Vector Offset
    {
        get => new Vector(0, _scrollOffsetY);
        set
        {
            var maxScrollY = Math.Max(0, Extent.Height - Viewport.Height);
            double targetY = Math.Clamp(value.Y, 0, maxScrollY);
            if (Math.Abs(_scrollOffsetY - targetY) > 0.001)
            {
                _scrollOffsetY = targetY;
                ScrollInvalidated?.Invoke(this, EventArgs.Empty);
                InvalidateVisual();
            }
        }
    }

    public bool BringIntoView(Control target, Rect targetRect) => false;
    public Control? GetControlInDirection(NavigationDirection direction, Control? from) => null;
    public void RaiseScrollInvalidated(EventArgs e) => ScrollInvalidated?.Invoke(this, e);

    private string _internalText = string.Empty;
    private MarkdownDocument? _document;
    private readonly DocumentLayout _documentLayout = new();
    private readonly RenderResources _resources = new();
    private readonly SkiaTextMeasurer _measurer;

    // Cached bitmap buffers for rendering
    private SKBitmap? _cachedBitmap;
    private WriteableBitmap? _cachedWriteableBitmap;

    // Caret and selection state
    private int _caretIndex;
    private int _selectionStart;
    private int _selectionEnd;
    private int _selectionAnchor;
    private bool _isDragging;

    private class UndoState
    {
        public string Text { get; }
        public int CaretIndex { get; }
        public int SelectionStart { get; }
        public int SelectionEnd { get; }

        public UndoState(string text, int caretIndex, int selectionStart, int selectionEnd)
        {
            Text = text;
            CaretIndex = caretIndex;
            SelectionStart = selectionStart;
            SelectionEnd = selectionEnd;
        }
    }

    private readonly Stack<UndoState> _undoStack = new();
    private readonly Stack<UndoState> _redoStack = new();

    public int CaretIndex
    {
        get => _caretIndex;
        set
        {
            _caretIndex = Math.Clamp(value, 0, _internalText.Length);
            _selectionStart = _selectionEnd = _caretIndex;
            ResetCaretBlink();
            InvalidateVisual();
        }
    }

    public int SelectionStart
    {
        get => _selectionStart;
        set
        {
            _selectionStart = Math.Clamp(value, 0, _internalText.Length);
            InvalidateVisual();
        }
    }

    public int SelectionEnd
    {
        get => _selectionEnd;
        set
        {
            _selectionEnd = Math.Clamp(value, 0, _internalText.Length);
            InvalidateVisual();
        }
    }
    
    // Blinking cursor
    private readonly DispatcherTimer _caretTimer;
    private bool _showCaret = true;

    // Auto-save debouncing
    private readonly DispatcherTimer _autoSaveTimer;

    static MarkdownEditor()
    {
        FocusableProperty.OverrideDefaultValue<MarkdownEditor>(true);
        AffectsMeasure<MarkdownEditor>(TextProperty);
        AffectsRender<MarkdownEditor>(IsDarkThemeProperty);
    }

    public MarkdownEditor()
    {
        _measurer = new SkiaTextMeasurer(_resources);

        // Blinking caret timer
        _caretTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(500)
        };
        _caretTimer.Tick += (s, e) =>
        {
            if (IsFocused)
            {
                _showCaret = !_showCaret;
                InvalidateVisual();
            }
        };

        // Auto-save debounce timer (500ms)
        _autoSaveTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(500)
        };
        _autoSaveTimer.Tick += (s, e) =>
        {
            _autoSaveTimer.Stop();
            if (_document != null)
            {
                var serialized = MarkdownSerializer.Serialize(_document);
                if (serialized != _internalText)
                {
                    int oldCaret = _caretIndex;
                    int oldSelStart = _selectionStart;
                    int oldSelEnd = _selectionEnd;
                    int oldSelAnchor = _selectionAnchor;

                    int newCaret = MapCaretIndex(_internalText, serialized, oldCaret);
                    int newSelStart = MapCaretIndex(_internalText, serialized, oldSelStart);
                    int newSelEnd = MapCaretIndex(_internalText, serialized, oldSelEnd);
                    int newSelAnchor = MapCaretIndex(_internalText, serialized, oldSelAnchor);

                    _internalText = serialized;
                    _caretIndex = newCaret;
                    _selectionStart = newSelStart;
                    _selectionEnd = newSelEnd;
                    _selectionAnchor = newSelAnchor;

                    ParseAndLayout();
                }

                if (serialized != Text)
                {
                    SetCurrentValue(TextProperty, serialized);
                }
            }
        };

        GotFocus += (s, e) => ResetCaretBlink();
        LostFocus += (s, e) =>
        {
            Flush();
            _caretTimer.Stop();
            _showCaret = false;
            InvalidateVisual();
        };

        TextProperty.Changed.AddClassHandler<MarkdownEditor>((control, args) => control.OnTextPropertyChanged(args));
    }

    public void Flush()
    {
        if (_autoSaveTimer.IsEnabled)
        {
            _autoSaveTimer.Stop();
            if (_document != null)
            {
                var serialized = MarkdownSerializer.Serialize(_document);
                if (serialized != _internalText)
                {
                    int oldCaret = _caretIndex;
                    int oldSelStart = _selectionStart;
                    int oldSelEnd = _selectionEnd;
                    int oldSelAnchor = _selectionAnchor;

                    int newCaret = MapCaretIndex(_internalText, serialized, oldCaret);
                    int newSelStart = MapCaretIndex(_internalText, serialized, oldSelStart);
                    int newSelEnd = MapCaretIndex(_internalText, serialized, oldSelEnd);
                    int newSelAnchor = MapCaretIndex(_internalText, serialized, oldSelAnchor);

                    _internalText = serialized;
                    _caretIndex = newCaret;
                    _selectionStart = newSelStart;
                    _selectionEnd = newSelEnd;
                    _selectionAnchor = newSelAnchor;

                    ParseAndLayout();
                }

                if (serialized != Text)
                {
                    SetCurrentValue(TextProperty, serialized);
                }
            }
        }
    }

    private void OnTextPropertyChanged(AvaloniaPropertyChangedEventArgs args)
    {
        var newText = args.GetNewValue<string>() ?? string.Empty;
        if (newText != _internalText)
        {
            _internalText = newText;
            _caretIndex = Math.Clamp(_caretIndex, 0, _internalText.Length);
            _selectionStart = Math.Clamp(_selectionStart, 0, _internalText.Length);
            _selectionEnd = Math.Clamp(_selectionEnd, 0, _internalText.Length);
            _selectionAnchor = Math.Clamp(_selectionAnchor, 0, _internalText.Length);
            
            ParseAndLayout();
        }
    }

    private void ParseAndLayout(double? width = null)
    {
        _caretIndex = Math.Clamp(_caretIndex, 0, _internalText.Length);
        _selectionStart = Math.Clamp(_selectionStart, 0, _internalText.Length);
        _selectionEnd = Math.Clamp(_selectionEnd, 0, _internalText.Length);
        _selectionAnchor = Math.Clamp(_selectionAnchor, 0, _internalText.Length);

        _document = MarkdownParser.Parse(_internalText);
        _documentLayout.LoadDocument(_document);

        var actualWidth = (float)(width ?? Bounds.Width);
        if (actualWidth <= 0) actualWidth = 800f; // fallback default width

        var layoutContext = new LayoutContext(
            maxWidth: actualWidth,
            measurer: _measurer,
            resources: _resources,
            startY: 0f,
            markdownText: _internalText
        );
        _documentLayout.Layout(layoutContext);
        ScrollInvalidated?.Invoke(this, EventArgs.Empty);
        InvalidateMeasure();
        InvalidateVisual();
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        double width = double.IsInfinity(availableSize.Width) ? 800 : availableSize.Width;
        ParseAndLayout(width);
        double height = double.IsInfinity(availableSize.Height) ? _documentLayout.Bounds.Height : availableSize.Height;
        return new Size(width, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var size = base.ArrangeOverride(finalSize);
        ParseAndLayout(size.Width);
        return size;
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _autoSaveTimer.Stop();
        _caretTimer.Stop();
        _cachedBitmap?.Dispose();
        _cachedBitmap = null;
        _cachedWriteableBitmap?.Dispose();
        _cachedWriteableBitmap = null;
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        var point = e.GetCurrentPoint(this);
        if (point.Properties.IsLeftButtonPressed)
        {
            e.Pointer.Capture(this);
            _isDragging = true;
            Focus();

            var localPoint = new SKPoint((float)point.Position.X, (float)(point.Position.Y + _scrollOffsetY));
            
            // Check checkbox clicks first
            bool checkboxToggled = false;
            if (!IsReadOnly)
            {
                foreach (var block in _documentLayout.Blocks)
                {
                    if (CheckCheckboxClick(block, localPoint))
                    {
                        checkboxToggled = true;
                        break;
                    }
                }
            }

            if (checkboxToggled && _document != null)
            {
                SaveStateForUndo();
                var newMarkdownText = MarkdownSerializer.Serialize(_document);
                if (newMarkdownText != _internalText)
                {
                    int oldCaret = _caretIndex;
                    int oldSelStart = _selectionStart;
                    int oldSelEnd = _selectionEnd;
                    int oldSelAnchor = _selectionAnchor;

                    int newCaret = MapCaretIndex(_internalText, newMarkdownText, oldCaret);
                    int newSelStart = MapCaretIndex(_internalText, newMarkdownText, oldSelStart);
                    int newSelEnd = MapCaretIndex(_internalText, newMarkdownText, oldSelEnd);
                    int newSelAnchor = MapCaretIndex(_internalText, newMarkdownText, oldSelAnchor);

                    _internalText = newMarkdownText;
                    _caretIndex = newCaret;
                    _selectionStart = newSelStart;
                    _selectionEnd = newSelEnd;
                    _selectionAnchor = newSelAnchor;

                    ParseAndLayout();
                }
                
                _autoSaveTimer.Stop();
                if (newMarkdownText != Text)
                {
                    SetCurrentValue(TextProperty, newMarkdownText);
                }
                
                e.Handled = true;
                return;
            }

            // Normal caret placement
            int clickedIndex = _documentLayout.HitTest(localPoint);
            _caretIndex = clickedIndex;

            if (e.ClickCount == 2)
            {
                GetWordAtCaret(out int wordStart, out int wordEnd);
                _selectionStart = wordStart;
                _selectionEnd = wordEnd;
                _caretIndex = wordEnd;
                _selectionAnchor = wordStart;
            }
            else if (e.ClickCount >= 3)
            {
                GetLineAtCaret(out int lineStart, out int lineEnd);
                _selectionStart = lineStart;
                _selectionEnd = lineEnd;
                _caretIndex = lineEnd;
                _selectionAnchor = lineStart;
            }
            else
            {
                _selectionAnchor = clickedIndex;
                _selectionStart = clickedIndex;
                _selectionEnd = clickedIndex;
            }

            ResetCaretBlink();
            InvalidateVisual();
            e.Handled = true;
        }
    }

    private bool CheckCheckboxClick(ILayoutBlock block, SKPoint point)
    {
        if (block is ListLayoutBlock listBlock)
        {
            foreach (var item in listBlock.Items)
            {
                if (CheckCheckboxClick(item, point))
                    return true;
            }
        }
        else if (block is ListItemLayoutBlock listItemBlock)
        {
            if (listItemBlock.Node.IsChecked.HasValue)
            {
                float boxSize = 14f;
                float boxX = listItemBlock.Bounds.Left + 8f;
                float boxY = listItemBlock.Bounds.Top + 4f;
                var checkboxRect = new SKRect(boxX, boxY, boxX + boxSize, boxY + boxSize);

                if (checkboxRect.Contains(point))
                {
                    listItemBlock.Node.IsChecked = !listItemBlock.Node.IsChecked.Value;
                    return true;
                }
            }

            foreach (var childBlock in listItemBlock.InnerBlocks)
            {
                if (CheckCheckboxClick(childBlock, new SKPoint(point.X - 28f, point.Y)))
                    return true;
            }
        }
        return false;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (_isDragging)
        {
            var point = e.GetCurrentPoint(this);
            var localPoint = new SKPoint((float)point.Position.X, (float)(point.Position.Y + _scrollOffsetY));
            
            int currentIndex = _documentLayout.HitTest(localPoint);
            _caretIndex = currentIndex;
            _selectionStart = Math.Min(_selectionAnchor, currentIndex);
            _selectionEnd = Math.Max(_selectionAnchor, currentIndex);

            ResetCaretBlink();
            InvalidateVisual();
            e.Handled = true;
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (_isDragging)
        {
            e.Pointer.Capture(null);
            _isDragging = false;
            e.Handled = true;
        }
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        _isDragging = false;
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        
        if (Parent is Visual && VisualRoot != null)
        {
            var parent = Parent;
            while (parent != null)
            {
                if (parent is ScrollViewer)
                {
                    return; // Let it bubble
                }
                parent = parent.Parent;
            }
        }

        double maxScroll = _documentLayout.Bounds.Height - Bounds.Height;
        if (maxScroll > 0)
        {
            _scrollOffsetY -= e.Delta.Y * 40;
            _scrollOffsetY = Math.Clamp(_scrollOffsetY, 0, maxScroll);
            ScrollInvalidated?.Invoke(this, EventArgs.Empty);
            InvalidateVisual();
            e.Handled = true;
        }
    }

    protected override void OnTextInput(TextInputEventArgs e)
    {
        if (IsReadOnly) return;
        if (string.IsNullOrEmpty(e.Text)) return;

        SaveStateForUndo();

        int start = Math.Min(_selectionStart, _selectionEnd);
        int length = Math.Abs(_selectionStart - _selectionEnd);
        if (length > 0)
        {
            _internalText = _internalText.Remove(start, length);
        }
        _internalText = _internalText.Insert(start, e.Text);
        _caretIndex = start + e.Text.Length;
        _selectionStart = _selectionEnd = _caretIndex;

        ParseAndLayout();
        TriggerUserEdit();

        e.Handled = true;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        bool shift = e.KeyModifiers.HasFlag(KeyModifiers.Shift);
        bool ctrl = e.KeyModifiers.HasFlag(KeyModifiers.Control) || e.KeyModifiers.HasFlag(KeyModifiers.Meta);

        switch (e.Key)
        {
            case Key.Left:
                MoveCaretLeft(shift);
                e.Handled = true;
                break;
            case Key.Right:
                MoveCaretRight(shift);
                e.Handled = true;
                break;
            case Key.Up:
                MoveCaretUpDown(false, shift);
                e.Handled = true;
                break;
            case Key.Down:
                MoveCaretUpDown(true, shift);
                e.Handled = true;
                break;
            case Key.Back:
                if (!IsReadOnly) HandleBackspace();
                e.Handled = true;
                break;
            case Key.Delete:
                if (!IsReadOnly) HandleDelete();
                e.Handled = true;
                break;
            case Key.Enter:
                if (!IsReadOnly) HandleEnter();
                e.Handled = true;
                break;
            case Key.A:
                if (ctrl)
                {
                    SelectAll();
                    e.Handled = true;
                }
                break;
            case Key.B:
                if (ctrl)
                {
                    ToggleBold();
                    e.Handled = true;
                }
                break;
            case Key.I:
                if (ctrl)
                {
                    ToggleItalic();
                    e.Handled = true;
                }
                break;
            case Key.K:
                if (ctrl)
                {
                    ToggleLink();
                    e.Handled = true;
                }
                break;
            case Key.OemTilde:
                if (ctrl)
                {
                    ToggleCode();
                    e.Handled = true;
                }
                break;
            case Key.Z:
                if (ctrl)
                {
                    if (shift) Redo();
                    else Undo();
                    e.Handled = true;
                }
                break;
            case Key.Y:
                if (ctrl)
                {
                    Redo();
                    e.Handled = true;
                }
                break;
            case Key.C:
                if (ctrl)
                {
                    _ = CopyToClipboardAsync();
                    e.Handled = true;
                }
                break;
            case Key.V:
                if (ctrl)
                {
                    _ = PasteFromClipboardAsync();
                    e.Handled = true;
                }
                break;
        }

        base.OnKeyDown(e);
    }

    private void MoveCaretLeft(bool shift)
    {
        int nextIndex = Math.Max(0, _caretIndex - 1);
        UpdateCaretAndSelection(nextIndex, shift);
    }

    private void MoveCaretRight(bool shift)
    {
        int nextIndex = Math.Min(_internalText.Length, _caretIndex + 1);
        UpdateCaretAndSelection(nextIndex, shift);
    }

    private void MoveCaretUpDown(bool down, bool shift)
    {
        var rect = _documentLayout.GetCaretBounds(_caretIndex);
        float lineHeight = rect.Height > 0 ? rect.Height : 20f;
        float targetY = down ? (rect.MidY + lineHeight + 2f) : (rect.MidY - lineHeight - 2f);
        
        int nextIndex = _documentLayout.HitTest(new SKPoint(rect.Left, targetY));
        UpdateCaretAndSelection(nextIndex, shift);
    }

    private void UpdateCaretAndSelection(int nextIndex, bool shift)
    {
        if (shift)
        {
            if (_selectionStart == _selectionEnd)
            {
                _selectionAnchor = _caretIndex;
            }
            _caretIndex = nextIndex;
            _selectionStart = Math.Min(_selectionAnchor, nextIndex);
            _selectionEnd = Math.Max(_selectionAnchor, nextIndex);
        }
        else
        {
            _caretIndex = nextIndex;
            _selectionStart = _selectionEnd = _caretIndex;
        }
        ResetCaretBlink();
        InvalidateVisual();
    }

    private void HandleBackspace()
    {
        if (_selectionStart != _selectionEnd)
        {
            DeleteSelection();
            return;
        }

        if (_caretIndex > 0)
        {
            SaveStateForUndo();
            _internalText = _internalText.Remove(_caretIndex - 1, 1);
            _caretIndex--;
            _selectionStart = _selectionEnd = _caretIndex;
            
            ParseAndLayout();
            TriggerUserEdit();
        }
    }

    private void HandleDelete()
    {
        if (_selectionStart != _selectionEnd)
        {
            DeleteSelection();
            return;
        }

        if (_caretIndex < _internalText.Length)
        {
            SaveStateForUndo();
            _internalText = _internalText.Remove(_caretIndex, 1);
            _selectionStart = _selectionEnd = _caretIndex;
            
            ParseAndLayout();
            TriggerUserEdit();
        }
    }

    private void HandleEnter()
    {
        if (_selectionStart != _selectionEnd)
        {
            DeleteSelection();
        }
        InsertTextAtCaret("\n");
    }

    private void SelectAll()
    {
        _selectionStart = 0;
        _selectionEnd = _internalText.Length;
        _caretIndex = _internalText.Length;
        ResetCaretBlink();
        InvalidateVisual();
    }

    private void DeleteSelection()
    {
        SaveStateForUndo();
        int start = Math.Min(_selectionStart, _selectionEnd);
        int length = Math.Abs(_selectionStart - _selectionEnd);
        
        _internalText = _internalText.Remove(start, length);
        _caretIndex = start;
        _selectionStart = _selectionEnd = _caretIndex;
        
        ParseAndLayout();
        TriggerUserEdit();
    }

    private void InsertTextAtCaret(string text)
    {
        SaveStateForUndo();
        _internalText = _internalText.Insert(_caretIndex, text);
        _caretIndex += text.Length;
        _selectionStart = _selectionEnd = _caretIndex;

        ParseAndLayout();
        TriggerUserEdit();
    }

    private void SaveStateForUndo()
    {
        if (_undoStack.Count > 0 && _undoStack.Peek().Text == _internalText)
        {
            return;
        }
        _undoStack.Push(new UndoState(_internalText, _caretIndex, _selectionStart, _selectionEnd));
        _redoStack.Clear();
    }

    public void Undo()
    {
        if (IsReadOnly || _undoStack.Count == 0) return;

        var currentState = new UndoState(_internalText, _caretIndex, _selectionStart, _selectionEnd);
        _redoStack.Push(currentState);

        var previousState = _undoStack.Pop();
        _internalText = previousState.Text;
        _caretIndex = previousState.CaretIndex;
        _selectionStart = previousState.SelectionStart;
        _selectionEnd = previousState.SelectionEnd;

        ParseAndLayout();
        TriggerUserEdit();
    }

    public void Redo()
    {
        if (IsReadOnly || _redoStack.Count == 0) return;

        var currentState = new UndoState(_internalText, _caretIndex, _selectionStart, _selectionEnd);
        _undoStack.Push(currentState);

        var nextState = _redoStack.Pop();
        _internalText = nextState.Text;
        _caretIndex = nextState.CaretIndex;
        _selectionStart = nextState.SelectionStart;
        _selectionEnd = nextState.SelectionEnd;

        ParseAndLayout();
        TriggerUserEdit();
    }

    private void GetWordAtCaret(out int wordStart, out int wordEnd)
    {
        wordStart = _caretIndex;
        wordEnd = _caretIndex;
        if (string.IsNullOrEmpty(_internalText)) return;

        while (wordStart > 0 && (char.IsLetterOrDigit(_internalText[wordStart - 1]) || _internalText[wordStart - 1] == '_'))
        {
            wordStart--;
        }
        while (wordEnd < _internalText.Length && (char.IsLetterOrDigit(_internalText[wordEnd]) || _internalText[wordEnd] == '_'))
        {
            wordEnd++;
        }
    }

    private void GetLineAtCaret(out int lineStart, out int lineEnd)
    {
        lineStart = _caretIndex;
        lineEnd = _caretIndex;
        if (string.IsNullOrEmpty(_internalText)) return;

        while (lineStart > 0 && _internalText[lineStart - 1] != '\n')
        {
            lineStart--;
        }
        while (lineEnd < _internalText.Length && _internalText[lineEnd] != '\n')
        {
            lineEnd++;
        }
    }

    private void ReplaceTextRange(int start, int length, string newText, int newCaretIndex, int newSelectionStart, int newSelectionEnd)
    {
        SaveStateForUndo();
        _internalText = _internalText.Remove(start, length).Insert(start, newText);
        _caretIndex = newCaretIndex;
        _selectionStart = newSelectionStart;
        _selectionEnd = newSelectionEnd;
        
        ParseAndLayout();
        TriggerUserEdit();
    }

    private void ToggleBold()
    {
        if (IsReadOnly) return;

        int start = _selectionStart;
        int end = _selectionEnd;
        if (start > end) (start, end) = (end, start);

        bool hasSelection = (start != end);
        if (!hasSelection)
        {
            GetWordAtCaret(out int wordStart, out int wordEnd);
            if (wordStart != wordEnd)
            {
                start = wordStart;
                end = wordEnd;
                hasSelection = true;
            }
        }

        if (hasSelection)
        {
            string selectedText = _internalText.Substring(start, end - start);
            string newText;
            int newSelectionStart;
            int newSelectionEnd;

            if (selectedText.StartsWith("**") && selectedText.EndsWith("**") && selectedText.Length >= 4)
            {
                newText = selectedText.Substring(2, selectedText.Length - 4);
                newSelectionStart = start;
                newSelectionEnd = start + newText.Length;
            }
            else
            {
                newText = "**" + selectedText + "**";
                newSelectionStart = start;
                newSelectionEnd = end + 4;
            }

            ReplaceTextRange(start, end - start, newText, newSelectionEnd, newSelectionStart, newSelectionEnd);
        }
        else
        {
            ReplaceTextRange(_caretIndex, 0, "****", _caretIndex + 2, _caretIndex + 2, _caretIndex + 2);
        }
    }

    private void ToggleItalic()
    {
        if (IsReadOnly) return;

        int start = _selectionStart;
        int end = _selectionEnd;
        if (start > end) (start, end) = (end, start);

        bool hasSelection = (start != end);
        if (!hasSelection)
        {
            GetWordAtCaret(out int wordStart, out int wordEnd);
            if (wordStart != wordEnd)
            {
                start = wordStart;
                end = wordEnd;
                hasSelection = true;
            }
        }

        if (hasSelection)
        {
            string selectedText = _internalText.Substring(start, end - start);
            string newText;
            int newSelectionStart;
            int newSelectionEnd;

            if (selectedText.StartsWith("*") && selectedText.EndsWith("*") && !selectedText.StartsWith("**") && selectedText.Length >= 2)
            {
                newText = selectedText.Substring(1, selectedText.Length - 2);
                newSelectionStart = start;
                newSelectionEnd = start + newText.Length;
            }
            else
            {
                newText = "*" + selectedText + "*";
                newSelectionStart = start;
                newSelectionEnd = end + 2;
            }

            ReplaceTextRange(start, end - start, newText, newSelectionEnd, newSelectionStart, newSelectionEnd);
        }
        else
        {
            ReplaceTextRange(_caretIndex, 0, "**", _caretIndex + 1, _caretIndex + 1, _caretIndex + 1);
        }
    }

    private void ToggleLink()
    {
        if (IsReadOnly) return;

        int start = _selectionStart;
        int end = _selectionEnd;
        if (start > end) (start, end) = (end, start);

        bool hasSelection = (start != end);
        if (!hasSelection)
        {
            GetWordAtCaret(out int wordStart, out int wordEnd);
            if (wordStart != wordEnd)
            {
                start = wordStart;
                end = wordEnd;
                hasSelection = true;
            }
        }

        if (hasSelection)
        {
            string selectedText = _internalText.Substring(start, end - start);
            string newText;
            int newSelectionStart;
            int newSelectionEnd;

            if (selectedText.StartsWith("[") && selectedText.Contains("](") && selectedText.EndsWith(")"))
            {
                int urlStart = selectedText.IndexOf("](");
                newText = selectedText.Substring(1, urlStart - 1);
                newSelectionStart = start;
                newSelectionEnd = start + newText.Length;
            }
            else
            {
                newText = $"[{selectedText}](url)";
                newSelectionStart = start;
                newSelectionEnd = start + newText.Length;
            }

            ReplaceTextRange(start, end - start, newText, newSelectionEnd, newSelectionStart, newSelectionEnd);
        }
        else
        {
            ReplaceTextRange(_caretIndex, 0, "[text](url)", _caretIndex + 5, _caretIndex + 1, _caretIndex + 5);
        }
    }

    private void ToggleCode()
    {
        if (IsReadOnly) return;

        int start = _selectionStart;
        int end = _selectionEnd;
        if (start > end) (start, end) = (end, start);

        bool hasSelection = (start != end);
        if (!hasSelection)
        {
            GetWordAtCaret(out int wordStart, out int wordEnd);
            if (wordStart != wordEnd)
            {
                start = wordStart;
                end = wordEnd;
                hasSelection = true;
            }
        }

        if (hasSelection)
        {
            string selectedText = _internalText.Substring(start, end - start);
            string newText;
            int newSelectionStart;
            int newSelectionEnd;

            if (selectedText.Contains("\n"))
            {
                if (selectedText.StartsWith("```\n") && selectedText.EndsWith("\n```"))
                {
                    newText = selectedText.Substring(4, selectedText.Length - 8);
                    newSelectionStart = start;
                    newSelectionEnd = start + newText.Length;
                }
                else if (selectedText.StartsWith("```") && selectedText.EndsWith("```") && selectedText.Length >= 6)
                {
                    newText = selectedText.Substring(3, selectedText.Length - 6);
                    newSelectionStart = start;
                    newSelectionEnd = start + newText.Length;
                }
                else
                {
                    newText = "```\n" + selectedText + "\n```";
                    newSelectionStart = start;
                    newSelectionEnd = start + newText.Length;
                }
            }
            else
            {
                if (selectedText.StartsWith("`") && selectedText.EndsWith("`") && selectedText.Length >= 2)
                {
                    newText = selectedText.Substring(1, selectedText.Length - 2);
                    newSelectionStart = start;
                    newSelectionEnd = start + newText.Length;
                }
                else
                {
                    newText = "`" + selectedText + "`";
                    newSelectionStart = start;
                    newSelectionEnd = start + newText.Length;
                }
            }

            ReplaceTextRange(start, end - start, newText, newSelectionEnd, newSelectionStart, newSelectionEnd);
        }
        else
        {
            ReplaceTextRange(_caretIndex, 0, "``", _caretIndex + 1, _caretIndex + 1, _caretIndex + 1);
        }
    }

    private async System.Threading.Tasks.Task CopyToClipboardAsync()
    {
        if (_selectionStart == _selectionEnd) return;
        int start = Math.Min(_selectionStart, _selectionEnd);
        int length = Math.Abs(_selectionStart - _selectionEnd);
        string selectedText = _internalText.Substring(start, length);
        
        var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
        if (clipboard != null)
        {
            await Avalonia.Input.Platform.ClipboardExtensions.SetTextAsync(clipboard, selectedText);
        }
    }

    private async System.Threading.Tasks.Task PasteFromClipboardAsync()
    {
        if (IsReadOnly) return;
        var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
        if (clipboard != null)
        {
            string text = await Avalonia.Input.Platform.ClipboardExtensions.TryGetTextAsync(clipboard) ?? "";
            if (!string.IsNullOrEmpty(text))
            {
                Dispatcher.UIThread.Post(() =>
                {
                    SaveStateForUndo();
                    
                    int start = Math.Min(_selectionStart, _selectionEnd);
                    int length = Math.Abs(_selectionStart - _selectionEnd);
                    if (length > 0)
                    {
                        _internalText = _internalText.Remove(start, length);
                    }
                    _internalText = _internalText.Insert(start, text);
                    _caretIndex = start + text.Length;
                    _selectionStart = _selectionEnd = _caretIndex;
                    
                    ParseAndLayout();
                    TriggerUserEdit();
                });
            }
        }
    }

    private void TriggerUserEdit()
    {
        ResetCaretBlink();
        _autoSaveTimer.Stop();
        _autoSaveTimer.Start();
    }

    private void ResetCaretBlink()
    {
        _showCaret = true;
        _caretTimer.Stop();
        if (IsFocused)
        {
            _caretTimer.Start();
        }
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        _resources.UpdateTheme(IsDarkTheme);
        var bounds = Bounds;
        int width = (int)Math.Max(1, bounds.Width);
        int height = (int)Math.Max(1, bounds.Height);

        double scaling = TopLevel.GetTopLevel(this)?.RenderScaling ?? 1.0;
        float scale = (float)scaling;
        int pixelWidth = (int)Math.Max(1, Math.Round(bounds.Width * scaling));
        int pixelHeight = (int)Math.Max(1, Math.Round(bounds.Height * scaling));

        if (_cachedBitmap == null || _cachedBitmap.Width != pixelWidth || _cachedBitmap.Height != pixelHeight)
        {
            _cachedBitmap?.Dispose();
            _cachedBitmap = new SKBitmap(pixelWidth, pixelHeight);
        }

        if (_cachedWriteableBitmap == null || _cachedWriteableBitmap.PixelSize.Width != pixelWidth || _cachedWriteableBitmap.PixelSize.Height != pixelHeight)
        {
            _cachedWriteableBitmap?.Dispose();
            _cachedWriteableBitmap = new WriteableBitmap(
                new PixelSize(pixelWidth, pixelHeight),
                new Vector(96 * scaling, 96 * scaling),
                PixelFormat.Bgra8888,
                AlphaFormat.Premul);
        }

        // 1. Render to SKBitmap using SKCanvas
        using (var canvas = new SKCanvas(_cachedBitmap))
        {
            canvas.Clear(SKColors.Transparent);

            canvas.Save();
            canvas.Scale(scale);
            canvas.Translate(0, -(float)_scrollOffsetY);

            // Render Markdown document
            var renderContext = new RenderContext(_resources, new SKRect(0, 0, width, height))
            {
                OnImageLoaded = () => Dispatcher.UIThread.Post(InvalidateVisual)
            };
            _documentLayout.Render(canvas, renderContext);

            // Render selection highlights
            if (_selectionStart != _selectionEnd)
            {
                var selectionBounds = _documentLayout.GetSelectionBounds(
                    Math.Min(_selectionStart, _selectionEnd),
                    Math.Max(_selectionStart, _selectionEnd));
                using var selectionPaint = new SKPaint
                {
                    Color = new SKColor(38, 79, 120, 64),
                    Style = SKPaintStyle.Fill
                };
                foreach (var rect in selectionBounds)
                {
                    canvas.DrawRect(rect, selectionPaint);
                }
            }

            // Render caret vertical line
            if (_showCaret && IsFocused)
            {
                var caretRect = _documentLayout.GetCaretBounds(_caretIndex);
                using var caretPaint = new SKPaint
                {
                    Color = SKColors.Red,
                    Style = SKPaintStyle.Stroke,
                    StrokeWidth = 1.5f
                };
                canvas.DrawLine(caretRect.Left, caretRect.Top, caretRect.Left, caretRect.Bottom, caretPaint);
            }

            canvas.Restore();
        }

        // 2. Copy pixels to WriteableBitmap
        using (var locked = _cachedWriteableBitmap.Lock())
        {
            var srcPtr = _cachedBitmap.GetPixels();
            var dstPtr = locked.Address;
            var srcRowBytes = _cachedBitmap.RowBytes;
            var dstRowBytes = locked.RowBytes;
            var rowSize = Math.Min(srcRowBytes, dstRowBytes);
            unsafe
            {
                for (int y = 0; y < pixelHeight; y++)
                {
                    Buffer.MemoryCopy(
                        (void*)(srcPtr + y * srcRowBytes),
                        (void*)(dstPtr + y * dstRowBytes),
                        rowSize,
                        rowSize);
                }
            }
        }

        // 3. Draw WriteableBitmap to DrawingContext
        context.DrawImage(_cachedWriteableBitmap, new Rect(0, 0, width, height));
    }

    private class SkiaTextMeasurer : ITextMeasurer
    {
        private readonly RenderResources _resources;

        public SkiaTextMeasurer(RenderResources resources)
        {
            _resources = resources;
        }

        public float MeasureText(string text, TextStyle style)
        {
            if (string.IsNullOrEmpty(text)) return 0f;
            var font = TextLayoutEngine.ResolveFont(style, _resources);
            using var paint = new SKPaint { Typeface = font.Typeface, TextSize = font.Size };
            return paint.MeasureText(text);
        }

        public float[] GetCharacterWidths(string text, TextStyle style)
        {
            if (string.IsNullOrEmpty(text)) return Array.Empty<float>();
            var font = TextLayoutEngine.ResolveFont(style, _resources);
            using var paint = new SKPaint { Typeface = font.Typeface, TextSize = font.Size };
            var widths = new float[text.Length];
            for (int i = 0; i < text.Length; i++)
            {
                widths[i] = paint.MeasureText(text[i].ToString());
            }
            return widths;
        }

        public float GetLineHeight(TextStyle style)
        {
            var font = TextLayoutEngine.ResolveFont(style, _resources);
            return font.Spacing;
        }
    }

    private static int MapCaretIndex(string oldText, string newText, int oldCaretIndex)
    {
        if (oldCaretIndex <= 0) return 0;
        if (oldCaretIndex >= oldText.Length) return newText.Length;

        // Find the longest common prefix length
        int prefixLen = 0;
        int maxPrefix = Math.Min(oldText.Length, newText.Length);
        while (prefixLen < maxPrefix && oldText[prefixLen] == newText[prefixLen])
        {
            prefixLen++;
        }

        // If caret is within the unchanged prefix, keep it
        if (oldCaretIndex <= prefixLen)
        {
            return oldCaretIndex;
        }

        // Find the longest common suffix length (measured from the end)
        int suffixLen = 0;
        int maxSuffix = Math.Min(oldText.Length, newText.Length) - prefixLen;
        while (suffixLen < maxSuffix &&
               oldText[oldText.Length - 1 - suffixLen] == newText[newText.Length - 1 - suffixLen])
        {
            suffixLen++;
        }

        // If caret is within the unchanged suffix, map it relative to the end
        int oldSuffixStart = oldText.Length - suffixLen;
        if (oldCaretIndex >= oldSuffixStart)
        {
            int offsetFromEnd = oldText.Length - oldCaretIndex;
            return Math.Clamp(newText.Length - offsetFromEnd, 0, newText.Length);
        }

        // Caret is in the changed region — map proportionally
        int oldChangedLen = oldText.Length - prefixLen - suffixLen;
        int newChangedLen = newText.Length - prefixLen - suffixLen;
        if (oldChangedLen <= 0)
        {
            return prefixLen;
        }

        int caretInOldChanged = oldCaretIndex - prefixLen;
        int caretInNewChanged = (int)Math.Round((double)caretInOldChanged / oldChangedLen * newChangedLen);
        return Math.Clamp(prefixLen + caretInNewChanged, 0, newText.Length);
    }
}
