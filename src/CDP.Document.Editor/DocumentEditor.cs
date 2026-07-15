using System;
using System.IO;
using System.Threading;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using SkiaSharp;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using DocumentFormat.OpenXml.Presentation;
using CDP.Document.Parser;
using CDP.Document.Parser.AST;
using CDP.Document.Renderer;
using CDP.Document.Renderer.Layout.Presentation;
using CDP.Document.Renderer.Layout.Spreadsheet;

namespace CDP.Document.Editor;

/// <summary>
/// Avalonia control that renders and allows interactive editing of rich documents (DOCX, RTF, PPTX, XLSX).
/// Parses the document from a file path, renders it using SkiaSharp onto a bitmap, and supports
/// pointer-based cursor placement, text selection via drag, and keyboard text input.
/// </summary>
public class DocumentEditor : Avalonia.Controls.Control, ILogicalScrollable
{
    public static readonly StyledProperty<string?> FilePathProperty =
        AvaloniaProperty.Register<DocumentEditor, string?>(nameof(FilePath));

    public string? FilePath
    {
        get => GetValue(FilePathProperty);
        set => SetValue(FilePathProperty, value);
    }

    public static readonly StyledProperty<bool> IsReadOnlyProperty =
        AvaloniaProperty.Register<DocumentEditor, bool>(nameof(IsReadOnly), true);

    public bool IsReadOnly
    {
        get => GetValue(IsReadOnlyProperty);
        set => SetValue(IsReadOnlyProperty, value);
    }

    private DocumentRoot? _document;
    private readonly DocumentRenderer _renderer = new();

    // Cached bitmap buffers for rendering
    private SKBitmap? _cachedBitmap;
    private WriteableBitmap? _cachedWriteableBitmap;

    // Caret and selection state
    private int _caretOffset;
    private int _selectionStart = -1;
    private int _selectionEnd = -1;
    private int _selectionAnchor;
    private bool _isDragging;

    // Caret blink state
    private bool _caretVisible = true;
    private DispatcherTimer? _caretTimer;

    // Auto-save debounce
    private Timer? _saveDebounceTimer;
    private const int SaveDebounceMs = 500;

    // Scroll offset
    private double _scrollOffsetX;
    private double _scrollOffsetY;

    // ILogicalScrollable implementation
    public bool CanHorizontallyScroll { get; set; } = true;
    public bool CanVerticallyScroll { get; set; } = true;
    public bool IsLogicalScrollEnabled => true;
    public Size ScrollSize => new(16, 16);
    public Size PageScrollSize => new(80, 80);
    public event EventHandler? ScrollInvalidated;

    public Size Extent => _renderer.DocumentBounds.IsEmpty ? new Size(0, 0) : new Size(_renderer.DocumentBounds.Width, _renderer.DocumentBounds.Height);
    public Size Viewport => (Parent as Avalonia.Visual)?.Bounds.Size ?? Bounds.Size;

    public Vector Offset
    {
        get => new Vector(_scrollOffsetX, _scrollOffsetY);
        set
        {
            var maxScrollX = Math.Max(0, Extent.Width - Viewport.Width);
            var maxScrollY = Math.Max(0, Extent.Height - Viewport.Height);
            double targetX = Math.Clamp(value.X, 0, maxScrollX);
            double targetY = Math.Clamp(value.Y, 0, maxScrollY);

            bool changed = false;
            if (Math.Abs(_scrollOffsetX - targetX) > 0.001)
            {
                _scrollOffsetX = targetX;
                changed = true;
            }
            if (Math.Abs(_scrollOffsetY - targetY) > 0.001)
            {
                _scrollOffsetY = targetY;
                changed = true;
            }

            if (changed)
            {
                ScrollInvalidated?.Invoke(this, EventArgs.Empty);
                InvalidateVisual();
            }
        }
    }

    public bool BringIntoView(Avalonia.Controls.Control target, Rect targetRect) => false;
    public Avalonia.Controls.Control? GetControlInDirection(NavigationDirection direction, Avalonia.Controls.Control? from) => null;
    public void RaiseScrollInvalidated(EventArgs e) => ScrollInvalidated?.Invoke(this, e);

    // Undo/Redo Stacks
    private readonly Stack<DocumentRoot> _undoStack = new();
    private readonly Stack<DocumentRoot> _redoStack = new();

    // Sheet cell editing
    private GridCellNode? _editingCellNode;

    // Presentation shape drag/resize
    private ShapeNode? _selectedShapeNode;
    private ShapeLayoutBlock? _selectedShapeBlock;
    private bool _isDraggingShape;
    private bool _isResizingShape;
    private int _resizeHandleIndex = -1;
    private Point _lastDragPosition;

    // Interactive panning
    private Point _lastPointerPosition;
    private bool _isPanning;


    static DocumentEditor()
    {
        FilePathProperty.Changed.AddClassHandler<DocumentEditor>((editor, args) =>
        {
            var oldPath = args.GetOldValue<string?>();
            if (editor._saveDebounceTimer != null && !string.IsNullOrEmpty(oldPath))
            {
                editor.FlushToPath(oldPath);
            }
            editor.OnFilePathChanged(args.GetNewValue<string?>());
        });
    }

    public DocumentEditor()
    {
        Focusable = true;
        ClipToBounds = true;

        _caretTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        _caretTimer.Tick += (s, e) =>
        {
            _caretVisible = !_caretVisible;
            InvalidateVisual();
        };
        _caretTimer.Start();
    }

    private void OnFilePathChanged(string? path)
    {
        _undoStack.Clear();
        _redoStack.Clear();
        _editingCellNode = null;
        _selectedShapeNode = null;
        _selectedShapeBlock = null;
        _isDraggingShape = false;
        _isResizingShape = false;
        _resizeHandleIndex = -1;

        if (string.IsNullOrEmpty(path) || !File.Exists(path))
        {
            _document = null;
            _cachedBitmap?.Dispose();
            _cachedBitmap = null;
            InvalidateVisual();
            return;
        }

        try
        {
            string ext = Path.GetExtension(path);
            var parser = DocumentParserFactory.GetParser(ext);
            using var stream = File.OpenRead(path);
            _document = parser.Parse(stream);
            PerformLayout();
            InvalidateVisual();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[DocumentEditor] Failed to parse '{path}': {ex.Message}");
            _document = null;
            InvalidateVisual();
        }
    }

    /// <summary>
    /// Reloads the document from the current file path.
    /// </summary>
    public void Reload()
    {
        OnFilePathChanged(FilePath);
    }

    private void PerformLayout()
    {
        if (_document == null) return;
        _renderer.Layout(_document);
        _cachedBitmap?.Dispose();
        _cachedBitmap = null;
        ScrollInvalidated?.Invoke(this, EventArgs.Empty);
        InvalidateMeasure();
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var docBounds = _renderer.DocumentBounds;
        if (docBounds.IsEmpty) return new Size(400, 300);
        
        double width = double.IsInfinity(availableSize.Width) ? docBounds.Width : availableSize.Width;
        double height = double.IsInfinity(availableSize.Height) ? docBounds.Height : availableSize.Height;
        
        return new Size(width, height);
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        var bounds = Bounds;
        if (bounds.Width <= 0 || bounds.Height <= 0) return;

        if (_document == null)
        {
            // Draw placeholder
            var ft = new FormattedText(
                "No document loaded",
                System.Globalization.CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                new Typeface("Inter", FontStyle.Normal, FontWeight.Normal),
                14,
                Brushes.Gray);
            context.DrawText(ft, new Point(20, 20));
            return;
        }

        double scaling = TopLevel.GetTopLevel(this)?.RenderScaling ?? 1.0;
        float scale = (float)scaling;
        int pixelWidth = (int)Math.Max(1, Math.Round(bounds.Width * scaling));
        int pixelHeight = (int)Math.Max(1, Math.Round(bounds.Height * scaling));

        // Render to SkiaSharp bitmap
        if (_cachedBitmap == null || _cachedBitmap.Width != pixelWidth || _cachedBitmap.Height != pixelHeight)
        {
            _cachedBitmap?.Dispose();
            _cachedBitmap = new SKBitmap(pixelWidth, pixelHeight, SKColorType.Bgra8888, SKAlphaType.Premul);
            _cachedWriteableBitmap?.Dispose();
            _cachedWriteableBitmap = null;
        }

        using (var canvas = new SKCanvas(_cachedBitmap))
        {
            canvas.Clear(new SKColor(40, 40, 40)); // Dark background

            canvas.Save();
            canvas.Scale(scale);
            canvas.Translate(-(float)_scrollOffsetX, -(float)_scrollOffsetY);

            var renderContext = new Renderer.RenderContext
            {
                DrawCaret = _caretVisible && IsFocused && !IsReadOnly,
                CaretOffset = _caretOffset,
                SelectionStart = _selectionStart,
                SelectionEnd = _selectionEnd
            };

            _renderer.Render(canvas, renderContext);

            if (_selectedShapeBlock != null)
            {
                using var borderPaint = new SKPaint { Color = SKColors.Blue, Style = SKPaintStyle.Stroke, StrokeWidth = 1.5f };
                canvas.DrawRect(_selectedShapeBlock.Bounds, borderPaint);

                float handleSize = 6f;
                using var handlePaint = new SKPaint { Color = SKColors.Blue, Style = SKPaintStyle.Fill };
                var b = _selectedShapeBlock.Bounds;
                canvas.DrawRect(new SKRect(b.Left - handleSize/2, b.Top - handleSize/2, b.Left + handleSize/2, b.Top + handleSize/2), handlePaint);
                canvas.DrawRect(new SKRect(b.Right - handleSize/2, b.Top - handleSize/2, b.Right + handleSize/2, b.Top + handleSize/2), handlePaint);
                canvas.DrawRect(new SKRect(b.Right - handleSize/2, b.Bottom - handleSize/2, b.Right + handleSize/2, b.Bottom + handleSize/2), handlePaint);
                canvas.DrawRect(new SKRect(b.Left - handleSize/2, b.Bottom - handleSize/2, b.Left + handleSize/2, b.Bottom + handleSize/2), handlePaint);
            }

            canvas.Restore();
        }

        // Copy SKBitmap to Avalonia WriteableBitmap
        if (_cachedWriteableBitmap == null || _cachedWriteableBitmap.PixelSize.Width != pixelWidth || _cachedWriteableBitmap.PixelSize.Height != pixelHeight)
        {
            _cachedWriteableBitmap?.Dispose();
            _cachedWriteableBitmap = new WriteableBitmap(
                new PixelSize(pixelWidth, pixelHeight),
                new Vector(96 * scaling, 96 * scaling),
                Avalonia.Platform.PixelFormat.Bgra8888,
                AlphaFormat.Premul);
        }

        using (var fb = _cachedWriteableBitmap.Lock())
        {
            unsafe
            {
                var srcPtr = _cachedBitmap.GetPixels();
                var dstPtr = fb.Address;
                var srcRowBytes = _cachedBitmap.RowBytes;
                var dstRowBytes = fb.RowBytes;
                var rowSize = Math.Min(srcRowBytes, dstRowBytes);
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

        double logicalWidth = (double)pixelWidth / scaling;
        double logicalHeight = (double)pixelHeight / scaling;
        context.DrawImage(_cachedWriteableBitmap, new Rect(0, 0, logicalWidth, logicalHeight));
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _caretTimer?.Stop();
        _saveDebounceTimer?.Dispose();
        _saveDebounceTimer = null;
        _cachedBitmap?.Dispose();
        _cachedBitmap = null;
        _cachedWriteableBitmap?.Dispose();
        _cachedWriteableBitmap = null;
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        Focus();

        var pos = e.GetPosition(this);
        var skPoint = new SKPoint((float)(pos.X + _scrollOffsetX), (float)(pos.Y + _scrollOffsetY));

        var properties = e.GetCurrentPoint(this).Properties;
        if (properties.IsMiddleButtonPressed || properties.IsRightButtonPressed)
        {
            _isPanning = true;
            _lastPointerPosition = pos;
            e.Handled = true;
            return;
        }

        if (_document is Parser.AST.PresentationDocument)
        {
            // If a shape is selected, check if we clicked on one of the handles
            if (_selectedShapeBlock != null)
            {
                float handleSize = 8f;
                var bounds = _selectedShapeBlock.Bounds;
                var tl = new SKRect(bounds.Left - handleSize, bounds.Top - handleSize, bounds.Left + handleSize, bounds.Top + handleSize);
                var tr = new SKRect(bounds.Right - handleSize, bounds.Top - handleSize, bounds.Right + handleSize, bounds.Top + handleSize);
                var br = new SKRect(bounds.Right - handleSize, bounds.Bottom - handleSize, bounds.Right + handleSize, bounds.Bottom + handleSize);
                var bl = new SKRect(bounds.Left - handleSize, bounds.Bottom - handleSize, bounds.Left + handleSize, bounds.Bottom + handleSize);

                if (tl.Contains(skPoint)) { _isResizingShape = true; _resizeHandleIndex = 0; }
                else if (tr.Contains(skPoint)) { _isResizingShape = true; _resizeHandleIndex = 1; }
                else if (br.Contains(skPoint)) { _isResizingShape = true; _resizeHandleIndex = 2; }
                else if (bl.Contains(skPoint)) { _isResizingShape = true; _resizeHandleIndex = 3; }

                if (_isResizingShape)
                {
                    PushUndoState();
                    _lastDragPosition = pos;
                    e.Handled = true;
                    return;
                }
            }

            var shapeBlock = FindShapeBlockAt(skPoint);
            if (shapeBlock != null)
            {
                PushUndoState();
                _selectedShapeBlock = shapeBlock;
                _selectedShapeNode = shapeBlock.Node as ShapeNode;
                _isDraggingShape = true;
                _lastDragPosition = pos;
                e.Handled = true;
                InvalidateVisual();
                return;
            }
            else
            {
                _selectedShapeBlock = null;
                _selectedShapeNode = null;
                InvalidateVisual();
            }
        }
        else if (e.ClickCount == 2 && _document is Parser.AST.SpreadsheetDocument)
        {
            var cellBlock = FindCellBlockAt(skPoint);
            if (cellBlock != null)
            {
                _editingCellNode = cellBlock.Node as GridCellNode;
                if (_editingCellNode != null)
                {
                    _caretOffset = _editingCellNode.DisplayText.Length;
                    ResetCaretBlink();
                    InvalidateVisual();
                }
                e.Handled = true;
                return;
            }
        }
        else
        {
            _editingCellNode = null;
        }

        int hitOffset = _renderer.HitTest(skPoint);
        if (hitOffset >= 0)
        {
            _caretOffset = hitOffset;
            _selectionAnchor = hitOffset;
            _selectionStart = -1;
            _selectionEnd = -1;
            _isDragging = true;
            ResetCaretBlink();
            InvalidateVisual();
        }

        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        var pos = e.GetPosition(this);
        var skPoint = new SKPoint((float)(pos.X + _scrollOffsetX), (float)(pos.Y + _scrollOffsetY));

        if (_isPanning)
        {
            double deltaY = pos.Y - _lastPointerPosition.Y;
            _scrollOffsetY -= deltaY;
            _scrollOffsetY = Math.Max(0, _scrollOffsetY);
            double maxScroll = _renderer.DocumentBounds.Height - Bounds.Height;
            if (maxScroll > 0)
            {
                _scrollOffsetY = Math.Min(_scrollOffsetY, maxScroll);
            }
            _lastPointerPosition = pos;
            InvalidateVisual();
            e.Handled = true;
            return;
        }

        if (_isDraggingShape && _selectedShapeNode != null && _selectedShapeBlock != null)
        {
            double dx = pos.X - _lastDragPosition.X;
            double dy = pos.Y - _lastDragPosition.Y;

            _selectedShapeNode.X = (_selectedShapeNode.X ?? 0) + dx;
            _selectedShapeNode.Y = (_selectedShapeNode.Y ?? 0) + dy;
            
            var b = _selectedShapeBlock.Bounds;
            _selectedShapeBlock.Bounds = new SKRect(b.Left + (float)dx, b.Top + (float)dy, b.Right + (float)dx, b.Bottom + (float)dy);

            _lastDragPosition = pos;
            InvalidateVisual();
            ScheduleAutoSave();
            e.Handled = true;
            return;
        }
        else if (_isResizingShape && _selectedShapeNode != null && _selectedShapeBlock != null)
        {
            double dx = pos.X - _lastDragPosition.X;
            double dy = pos.Y - _lastDragPosition.Y;

            double oldX = _selectedShapeNode.X ?? 0;
            double oldY = _selectedShapeNode.Y ?? 0;
            double oldW = _selectedShapeNode.Width ?? 100;
            double oldH = _selectedShapeNode.Height ?? 40;

            if (_resizeHandleIndex == 0) // top-left
            {
                _selectedShapeNode.X = oldX + dx;
                _selectedShapeNode.Y = oldY + dy;
                _selectedShapeNode.Width = Math.Max(10, oldW - dx);
                _selectedShapeNode.Height = Math.Max(10, oldH - dy);
            }
            else if (_resizeHandleIndex == 1) // top-right
            {
                _selectedShapeNode.Y = oldY + dy;
                _selectedShapeNode.Width = Math.Max(10, oldW + dx);
                _selectedShapeNode.Height = Math.Max(10, oldH - dy);
            }
            else if (_resizeHandleIndex == 2) // bottom-right
            {
                _selectedShapeNode.Width = Math.Max(10, oldW + dx);
                _selectedShapeNode.Height = Math.Max(10, oldH + dy);
            }
            else if (_resizeHandleIndex == 3) // bottom-left
            {
                _selectedShapeNode.X = oldX + dx;
                _selectedShapeNode.Width = Math.Max(10, oldW - dx);
                _selectedShapeNode.Height = Math.Max(10, oldH + dy);
            }

            _lastDragPosition = pos;
            PerformLayout();
            InvalidateVisual();
            ScheduleAutoSave();
            e.Handled = true;
            return;
        }

        if (!_isDragging) return;

        int hitOffset = _renderer.HitTest(skPoint);
        if (hitOffset >= 0)
        {
            _caretOffset = hitOffset;
            _selectionStart = Math.Min(_selectionAnchor, hitOffset);
            _selectionEnd = Math.Max(_selectionAnchor, hitOffset);

            if (_selectionStart == _selectionEnd)
            {
                _selectionStart = -1;
                _selectionEnd = -1;
            }

            InvalidateVisual();
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        _isDragging = false;
        _isPanning = false;
        _isDraggingShape = false;
        _isResizingShape = false;
        _resizeHandleIndex = -1;
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

        double maxScroll = _renderer.DocumentBounds.Height - Bounds.Height;
        if (maxScroll > 0)
        {
            _scrollOffsetY -= e.Delta.Y * 40;
            _scrollOffsetY = Math.Clamp(_scrollOffsetY, 0, maxScroll);
            ScrollInvalidated?.Invoke(this, EventArgs.Empty);
            InvalidateVisual();
            e.Handled = true;
        }
    }

    private void UpdateEditingCellText(string newText)
    {
        if (_editingCellNode == null) return;
        _editingCellNode.DisplayText = newText;
        _editingCellNode.Value = newText;
        if (newText.StartsWith("="))
        {
            _editingCellNode.Formula = newText.Substring(1);
        }
        else
        {
            _editingCellNode.Formula = null;
        }
    }

    protected override void OnTextInput(TextInputEventArgs e)
    {
        base.OnTextInput(e);
        if (IsReadOnly || string.IsNullOrEmpty(e.Text)) return;

        if (_editingCellNode != null)
        {
            PushUndoState();
            UpdateEditingCellText(_editingCellNode.DisplayText.Insert(_caretOffset, e.Text));
            _caretOffset += e.Text.Length;
            PerformLayout();
            InvalidateVisual();
            ScheduleAutoSave();
            e.Handled = true;
            return;
        }

        if (_document is not WordDocument wordDoc) return;

        InsertTextAtCaret(wordDoc, e.Text);
        e.Handled = true;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        var modifiers = e.KeyModifiers;
        bool isCtrlCmd = (modifiers & KeyModifiers.Control) != 0 || (modifiers & KeyModifiers.Meta) != 0;

        if (isCtrlCmd && e.Key == Key.Z)
        {
            Undo();
            e.Handled = true;
            return;
        }
        else if (isCtrlCmd && e.Key == Key.Y)
        {
            Redo();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.PageUp)
        {
            _scrollOffsetY -= Bounds.Height;
            _scrollOffsetY = Math.Max(0, _scrollOffsetY);
            InvalidateVisual();
            e.Handled = true;
            return;
        }
        else if (e.Key == Key.PageDown)
        {
            _scrollOffsetY += Bounds.Height;
            double maxScroll = _renderer.DocumentBounds.Height - Bounds.Height;
            if (maxScroll > 0)
            {
                _scrollOffsetY = Math.Min(_scrollOffsetY, maxScroll);
            }
            InvalidateVisual();
            e.Handled = true;
            return;
        }

        if (IsReadOnly) return;

        if (_editingCellNode != null)
        {
            if (e.Key == Key.Back && _caretOffset > 0)
            {
                PushUndoState();
                UpdateEditingCellText(_editingCellNode.DisplayText.Remove(_caretOffset - 1, 1));
                _caretOffset--;
                PerformLayout();
                InvalidateVisual();
                ScheduleAutoSave();
                e.Handled = true;
                return;
            }
            else if (e.Key == Key.Delete && _caretOffset < _editingCellNode.DisplayText.Length)
            {
                PushUndoState();
                UpdateEditingCellText(_editingCellNode.DisplayText.Remove(_caretOffset, 1));
                PerformLayout();
                InvalidateVisual();
                ScheduleAutoSave();
                e.Handled = true;
                return;
            }
            else if (e.Key == Key.Enter || e.Key == Key.Escape)
            {
                _editingCellNode = null;
                InvalidateVisual();
                e.Handled = true;
                return;
            }
            else if (e.Key == Key.Left && _caretOffset > 0)
            {
                _caretOffset--;
                InvalidateVisual();
                e.Handled = true;
                return;
            }
            else if (e.Key == Key.Right && _caretOffset < _editingCellNode.DisplayText.Length)
            {
                _caretOffset++;
                InvalidateVisual();
                e.Handled = true;
                return;
            }
        }

        if (_document is not WordDocument wordDoc) return;

        if (e.Key == Key.Back)
        {
            DeleteCharBeforeCaret(wordDoc);
            e.Handled = true;
        }
        else if (e.Key == Key.Delete)
        {
            DeleteCharAfterCaret(wordDoc);
            e.Handled = true;
        }
        else if (e.Key == Key.Enter)
        {
            InsertTextAtCaret(wordDoc, "\n");
            e.Handled = true;
        }
        else if (e.Key == Key.Left)
        {
            if (_caretOffset > 0) _caretOffset--;
            ClearSelection();
            ResetCaretBlink();
            InvalidateVisual();
            e.Handled = true;
        }
        else if (e.Key == Key.Right)
        {
            _caretOffset++;
            ClearSelection();
            ResetCaretBlink();
            InvalidateVisual();
            e.Handled = true;
        }
    }

    private void InsertTextAtCaret(WordDocument wordDoc, string text)
    {
        PushUndoState();
        // Delete selection first if active
        if (_selectionStart >= 0 && _selectionEnd > _selectionStart)
        {
            DeleteRange(wordDoc, _selectionStart, _selectionEnd);
            _caretOffset = _selectionStart;
            ClearSelection();
        }

        // Find the TextRun at the caret offset and insert
        int globalOffset = 0;
        foreach (var child in wordDoc.Children)
        {
            if (child is ParagraphBlock para)
            {
                foreach (var inline in para.Children)
                {
                    if (inline is TextRun run)
                    {
                        int runEnd = globalOffset + run.Text.Length;
                        if (_caretOffset >= globalOffset && _caretOffset <= runEnd)
                        {
                            int localOffset = _caretOffset - globalOffset;
                            run.Text = run.Text.Insert(localOffset, text);
                            _caretOffset += text.Length;
                            PerformLayout();
                            ResetCaretBlink();
                            InvalidateVisual();
                            ScheduleAutoSave();
                            return;
                        }
                        globalOffset = runEnd;
                    }
                    else if (inline is LineBreakInline)
                    {
                        globalOffset++;
                    }
                }
            }
        }

        // Caret is at the end — add to the last run or create one
        var lastPara = GetLastParagraph(wordDoc);
        if (lastPara != null)
        {
            TextRun? lastRun = null;
            foreach (var c in lastPara.Children)
            {
                if (c is TextRun tr) lastRun = tr;
            }
            if (lastRun != null)
            {
                lastRun.Text += text;
            }
            else
            {
                lastPara.AddChild(new TextRun { Text = text });
            }
            _caretOffset += text.Length;
            PerformLayout();
            ResetCaretBlink();
            InvalidateVisual();
            ScheduleAutoSave();
        }
    }

    private void DeleteCharBeforeCaret(WordDocument wordDoc)
    {
        PushUndoState();
        if (_selectionStart >= 0 && _selectionEnd > _selectionStart)
        {
            DeleteRange(wordDoc, _selectionStart, _selectionEnd);
            _caretOffset = _selectionStart;
            ClearSelection();
            PerformLayout();
            ResetCaretBlink();
            InvalidateVisual();
            ScheduleAutoSave();
            return;
        }

        if (_caretOffset <= 0) return;
        DeleteRange(wordDoc, _caretOffset - 1, _caretOffset);
        _caretOffset--;
        PerformLayout();
        ResetCaretBlink();
        InvalidateVisual();
        ScheduleAutoSave();
    }

    private void DeleteCharAfterCaret(WordDocument wordDoc)
    {
        PushUndoState();
        if (_selectionStart >= 0 && _selectionEnd > _selectionStart)
        {
            DeleteRange(wordDoc, _selectionStart, _selectionEnd);
            _caretOffset = _selectionStart;
            ClearSelection();
        }
        else
        {
            DeleteRange(wordDoc, _caretOffset, _caretOffset + 1);
        }

        PerformLayout();
        ResetCaretBlink();
        InvalidateVisual();
        ScheduleAutoSave();
    }

    private static void DeleteRange(WordDocument wordDoc, int start, int end)
    {
        if (start >= end) return;

        int globalOffset = 0;
        foreach (var child in wordDoc.Children)
        {
            if (child is ParagraphBlock para)
            {
                foreach (var inline in para.Children)
                {
                    if (inline is TextRun run)
                    {
                        int runStart = globalOffset;
                        int runEnd = globalOffset + run.Text.Length;

                        int delStart = Math.Max(start, runStart) - runStart;
                        int delEnd = Math.Min(end, runEnd) - runStart;

                        if (delStart < delEnd && delStart >= 0 && delEnd <= run.Text.Length)
                        {
                            run.Text = run.Text.Remove(delStart, delEnd - delStart);
                        }
                        globalOffset = runEnd;
                    }
                    else if (inline is LineBreakInline)
                    {
                        globalOffset++;
                    }
                }
            }
        }
    }

    private static ParagraphBlock? GetLastParagraph(WordDocument doc)
    {
        ParagraphBlock? last = null;
        foreach (var child in doc.Children)
        {
            if (child is ParagraphBlock p) last = p;
        }
        return last;
    }

    private void ClearSelection()
    {
        _selectionStart = -1;
        _selectionEnd = -1;
    }

    private void ResetCaretBlink()
    {
        _caretVisible = true;
        _caretTimer?.Stop();
        _caretTimer?.Start();
    }

    private void ScheduleAutoSave()
    {
        _saveDebounceTimer?.Dispose();
        _saveDebounceTimer = new Timer(_ =>
        {
            Dispatcher.UIThread.Post(() => SaveDocument());
        }, null, SaveDebounceMs, Timeout.Infinite);
    }

    private void SaveDocumentToPath(string path)
    {
        if (_document == null || string.IsNullOrEmpty(path)) return;
        if (!File.Exists(path)) return;

        try
        {
            string ext = Path.GetExtension(path).ToLowerInvariant().TrimStart('.');
            if (ext == "rtf" && _document is WordDocument rtfDoc)
            {
                string rtf = SerializeToRtf(rtfDoc);
                File.WriteAllText(path, rtf);
            }
            else if (ext == "docx" && _document is WordDocument wordDoc)
            {
                SerializeToDocx(wordDoc, path);
            }
            else if (ext == "xlsx" && _document is Parser.AST.SpreadsheetDocument spreadDoc)
            {
                SerializeToXlsx(spreadDoc, path);
            }
            else if (ext == "pptx" && _document is Parser.AST.PresentationDocument presDoc)
            {
                SerializeToPptx(presDoc, path);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[DocumentEditor] Save to '{path}' failed: {ex.Message}");
        }
    }

    private void SaveDocument()
    {
        SaveDocumentToPath(FilePath ?? "");
    }

    private void FlushToPath(string path)
    {
        _saveDebounceTimer?.Dispose();
        _saveDebounceTimer = null;
        SaveDocumentToPath(path);
    }

    /// <summary>
    /// Flushes any pending save operations.
    /// </summary>
    public void Flush()
    {
        _saveDebounceTimer?.Dispose();
        _saveDebounceTimer = null;
        SaveDocument();
    }

    private static string SerializeToRtf(WordDocument doc)
    {
        var sb = new System.Text.StringBuilder();
        sb.Append(@"{\rtf1\ansi\deff0");
        sb.AppendLine();

        foreach (var child in doc.Children)
        {
            if (child is ParagraphBlock para)
            {
                foreach (var inline in para.Children)
                {
                    if (inline is TextRun run)
                    {
                        if (run.Bold) sb.Append(@"\b ");
                        if (run.Italic) sb.Append(@"\i ");
                        if (run.Underline) sb.Append(@"\ul ");
                        if (run.FontSize.HasValue)
                        {
                            int halfPts = (int)(run.FontSize.Value * 2);
                            sb.Append($@"\fs{halfPts} ");
                        }

                        // Escape RTF special characters
                        foreach (char c in run.Text)
                        {
                            if (c == '\\') sb.Append(@"\\");
                            else if (c == '{') sb.Append(@"\{");
                            else if (c == '}') sb.Append(@"\}");
                            else if (c > 127) sb.Append($@"\u{(int)c}?");
                            else sb.Append(c);
                        }

                        if (run.Bold) sb.Append(@"\b0 ");
                        if (run.Italic) sb.Append(@"\i0 ");
                        if (run.Underline) sb.Append(@"\ulnone ");
                    }
                    else if (inline is LineBreakInline)
                    {
                        sb.Append(@"\line ");
                    }
                }
                sb.Append(@"\par ");
                sb.AppendLine();
            }
        }

        sb.Append('}');
        return sb.ToString();
    }

    private void PushUndoState()
    {
        if (_document == null) return;
        _undoStack.Push(CloneDocument(_document));
        _redoStack.Clear();
    }

    public void Undo()
    {
        if (_undoStack.Count > 0 && _document != null)
        {
            _redoStack.Push(CloneDocument(_document));
            _document = _undoStack.Pop();
            PerformLayout();
            InvalidateVisual();
            ScheduleAutoSave();
        }
    }

    public void Redo()
    {
        if (_redoStack.Count > 0 && _document != null)
        {
            _undoStack.Push(CloneDocument(_document));
            _document = _redoStack.Pop();
            PerformLayout();
            InvalidateVisual();
            ScheduleAutoSave();
        }
    }

    private DocumentRoot CloneDocument(DocumentRoot doc)
    {
        if (doc is WordDocument wordDoc)
        {
            var clone = new WordDocument { Header = wordDoc.Header, Footer = wordDoc.Footer };
            CloneChildren(wordDoc, clone);
            return clone;
        }
        if (doc is Parser.AST.SpreadsheetDocument spreadDoc)
        {
            var clone = new Parser.AST.SpreadsheetDocument();
            CloneChildren(spreadDoc, clone);
            return clone;
        }
        if (doc is Parser.AST.PresentationDocument presDoc)
        {
            var clone = new Parser.AST.PresentationDocument();
            foreach (var master in presDoc.Masters)
            {
                var masterClone = new SlideMasterNode { Name = master.Name, BackgroundColor = master.BackgroundColor };
                CloneChildren(master, masterClone);
                clone.Masters.Add(masterClone);
            }
            CloneChildren(presDoc, clone);
            return clone;
        }
        return doc;
    }

    private void CloneChildren(DocumentNode source, DocumentNode dest)
    {
        foreach (var child in source.Children)
        {
            DocumentNode? childClone = null;
            if (child is ParagraphBlock p)
            {
                childClone = new ParagraphBlock { IsBullet = p.IsBullet, BulletLevel = p.BulletLevel, BulletStyle = p.BulletStyle };
            }
            else if (child is TextRun tr)
            {
                childClone = new TextRun { Text = tr.Text, FontSize = tr.FontSize, Bold = tr.Bold, Italic = tr.Italic, Underline = tr.Underline, Color = tr.Color };
            }
            else if (child is ImageInline img)
            {
                childClone = new ImageInline { Source = img.Source, AltText = img.AltText };
            }
            else if (child is LineBreakInline lb)
            {
                childClone = new LineBreakInline();
            }
            else if (child is SectionBlock sec)
            {
                childClone = new SectionBlock();
            }
            else if (child is TableBlock tb)
            {
                childClone = new TableBlock();
            }
            else if (child is TableRowBlock trb)
            {
                childClone = new TableRowBlock();
            }
            else if (child is TableCellBlock tcb)
            {
                childClone = new TableCellBlock();
            }
            else if (child is WorksheetNode ws)
            {
                var wsClone = new WorksheetNode { Name = ws.Name };
                wsClone.MergedCellRanges.AddRange(ws.MergedCellRanges);
                childClone = wsClone;
            }
            else if (child is GridRowNode gr)
            {
                childClone = new GridRowNode { RowIndex = gr.RowIndex };
            }
            else if (child is GridCellNode gc)
            {
                childClone = new GridCellNode
                {
                    ColumnIndex = gc.ColumnIndex,
                    Formula = gc.Formula,
                    Value = gc.Value,
                    DisplayText = gc.DisplayText,
                    Style = gc.Style,
                    Bold = gc.Bold,
                    Italic = gc.Italic,
                    FontSize = gc.FontSize,
                    Color = gc.Color,
                    RowSpan = gc.RowSpan,
                    ColumnSpan = gc.ColumnSpan,
                    IsMerged = gc.IsMerged
                };
            }
            else if (child is SlideNode sn)
            {
                childClone = new SlideNode { SlideIndex = sn.SlideIndex, Title = sn.Title, MasterName = sn.MasterName };
            }
            else if (child is ShapeNode spn)
            {
                childClone = new ShapeNode { X = spn.X, Y = spn.Y, Width = spn.Width, Height = spn.Height, ShapeType = spn.ShapeType, Text = spn.Text, ImageSource = spn.ImageSource };
            }
            else if (child is GroupNode gn)
            {
                childClone = new GroupNode { X = gn.X, Y = gn.Y, Width = gn.Width, Height = gn.Height };
            }

            if (childClone != null)
            {
                dest.AddChild(childClone);
                CloneChildren(child, childClone);
            }
        }
    }

    private CellLayoutBlock? FindCellBlockAt(SKPoint point)
    {
        if (_renderer.LayoutBlock is SpreadsheetDocumentLayoutBlock spreadBlock)
        {
            foreach (var ws in spreadBlock.Worksheets)
            {
                foreach (var cell in ws.Cells)
                {
                    if (cell.Bounds.Contains(point))
                    {
                        return cell;
                    }
                }
            }
        }
        return null;
    }

    private ShapeLayoutBlock? FindShapeBlockAt(SKPoint point)
    {
        if (_renderer.LayoutBlock is PresentationDocumentLayoutBlock presBlock)
        {
            foreach (var slide in presBlock.Slides)
            {
                foreach (var shape in slide.Shapes)
                {
                    if (shape.Bounds.Contains(point))
                    {
                        return shape;
                    }
                }
            }
        }
        return null;
    }

    private static void SerializeToDocx(WordDocument doc, string filePath)
    {
        using var wordDoc = WordprocessingDocument.Open(filePath, true);
        var body = wordDoc.MainDocumentPart?.Document?.Body;
        if (body == null) return;
        body.RemoveAllChildren();

        foreach (var child in doc.Children)
        {
            if (child is ParagraphBlock para)
            {
                var wPara = new DocumentFormat.OpenXml.Wordprocessing.Paragraph();
                var pPr = new DocumentFormat.OpenXml.Wordprocessing.ParagraphProperties();
                 if (para.IsBullet)
                 {
                     var numPr = new DocumentFormat.OpenXml.Wordprocessing.NumberingProperties(
                         new DocumentFormat.OpenXml.Wordprocessing.NumberingLevelReference { Val = para.BulletLevel },
                         new DocumentFormat.OpenXml.Wordprocessing.NumberingId { Val = int.TryParse(para.BulletStyle, out int nid) ? nid : 1 }
                     );
                     pPr.AppendChild(numPr);
                 }
                 wPara.AppendChild(pPr);

                foreach (var inline in para.Children)
                {
                    if (inline is TextRun run)
                    {
                        var wRun = new DocumentFormat.OpenXml.Wordprocessing.Run();
                        var rPr = new DocumentFormat.OpenXml.Wordprocessing.RunProperties();
                        if (run.Bold) rPr.AppendChild(new DocumentFormat.OpenXml.Wordprocessing.Bold());
                        if (run.Italic) rPr.AppendChild(new DocumentFormat.OpenXml.Wordprocessing.Italic());
                        if (run.Underline) rPr.AppendChild(new DocumentFormat.OpenXml.Wordprocessing.Underline());
                        if (run.FontSize.HasValue) rPr.AppendChild(new DocumentFormat.OpenXml.Wordprocessing.FontSize { Val = (run.FontSize.Value * 2).ToString() });
                        if (!string.IsNullOrEmpty(run.Color)) rPr.AppendChild(new DocumentFormat.OpenXml.Wordprocessing.Color { Val = run.Color });
                        wRun.AppendChild(rPr);
                        wRun.AppendChild(new DocumentFormat.OpenXml.Wordprocessing.Text(run.Text));
                        wPara.AppendChild(wRun);
                    }
                    else if (inline is ImageInline img)
                    {
                        string? relationshipId = null;
                        if (img.Source != null && img.Source.StartsWith("data:image/", StringComparison.OrdinalIgnoreCase))
                        {
                            try
                            {
                                var parts = img.Source.Split(',');
                                if (parts.Length > 1)
                                {
                                    var header = parts[0];
                                    var base64 = parts[1];
                                    var contentType = header.Split(';')[0].Split(':')[1];
                                    
                                    PartTypeInfo type = ImagePartType.Png;
                                    if (contentType.Contains("jpeg") || contentType.Contains("jpg")) type = ImagePartType.Jpeg;
                                    else if (contentType.Contains("gif")) type = ImagePartType.Gif;
                                    else if (contentType.Contains("bmp")) type = ImagePartType.Bmp;
                                    else if (contentType.Contains("tiff")) type = ImagePartType.Tiff;

                                    byte[] bytes = Convert.FromBase64String(base64);
                                    var imagePart = wordDoc.MainDocumentPart.AddImagePart(type);
                                    using (var partStream = imagePart.GetStream())
                                    {
                                        partStream.Write(bytes, 0, bytes.Length);
                                    }
                                    relationshipId = wordDoc.MainDocumentPart.GetIdOfPart(imagePart);
                                }
                            }
                            catch (Exception ex)
                            {
                                System.Diagnostics.Debug.WriteLine($"Failed to write image part: {ex.Message}");
                            }
                        }
                        else if (!string.IsNullOrEmpty(img.Source))
                        {
                            relationshipId = img.Source;
                        }

                        if (!string.IsNullOrEmpty(relationshipId))
                        {
                            var wRun = new DocumentFormat.OpenXml.Wordprocessing.Run();
                            var drawing = CreateDrawing(relationshipId, img.AltText ?? "Image");
                            wRun.AppendChild(drawing);
                            wPara.AppendChild(wRun);
                        }
                    }
                    else if (inline is LineBreakInline)
                    {
                        wPara.AppendChild(new DocumentFormat.OpenXml.Wordprocessing.Run(new DocumentFormat.OpenXml.Wordprocessing.Break()));
                    }
                }
                body.AppendChild(wPara);
            }
            else if (child is TableBlock table)
            {
                var wTable = new DocumentFormat.OpenXml.Wordprocessing.Table();
                foreach (var rowNode in table.Children.OfType<TableRowBlock>())
                {
                    var wRow = new DocumentFormat.OpenXml.Wordprocessing.TableRow();
                    foreach (var cellNode in rowNode.Children.OfType<TableCellBlock>())
                    {
                        var wCell = new DocumentFormat.OpenXml.Wordprocessing.TableCell();
                        foreach (var cellChild in cellNode.Children.OfType<ParagraphBlock>())
                        {
                            var wCellPara = new DocumentFormat.OpenXml.Wordprocessing.Paragraph();
                            foreach (var run in cellChild.Children.OfType<TextRun>())
                            {
                                var wCellRun = new DocumentFormat.OpenXml.Wordprocessing.Run(new DocumentFormat.OpenXml.Wordprocessing.Text(run.Text));
                                wCellPara.AppendChild(wCellRun);
                            }
                            wCell.AppendChild(wCellPara);
                        }
                        if (wCell.ChildElements.Count == 0)
                        {
                            wCell.AppendChild(new DocumentFormat.OpenXml.Wordprocessing.Paragraph());
                        }
                        wRow.AppendChild(wCell);
                    }
                    wTable.AppendChild(wRow);
                }
                body.AppendChild(wTable);
            }
        }
        wordDoc.MainDocumentPart.Document.Save();
    }

    private static DocumentFormat.OpenXml.Wordprocessing.Drawing CreateDrawing(string relationshipId, string altText, long widthCx = 952500, long heightCx = 952500)
    {
        return new DocumentFormat.OpenXml.Wordprocessing.Drawing(
            new DocumentFormat.OpenXml.Drawing.Wordprocessing.Inline(
                new DocumentFormat.OpenXml.Drawing.Wordprocessing.Extent { Cx = widthCx, Cy = heightCx },
                new DocumentFormat.OpenXml.Drawing.Wordprocessing.EffectExtent { LeftEdge = 0L, TopEdge = 0L, RightEdge = 0L, BottomEdge = 0L },
                new DocumentFormat.OpenXml.Drawing.Wordprocessing.DocProperties { Id = (DocumentFormat.OpenXml.UInt32Value)1U, Name = altText ?? "Image" },
                new DocumentFormat.OpenXml.Drawing.Wordprocessing.NonVisualGraphicFrameDrawingProperties(
                    new DocumentFormat.OpenXml.Drawing.GraphicFrameLocks { NoChangeAspect = true }),
                new DocumentFormat.OpenXml.Drawing.Graphic(
                    new DocumentFormat.OpenXml.Drawing.GraphicData(
                        new DocumentFormat.OpenXml.Drawing.Pictures.Picture(
                            new DocumentFormat.OpenXml.Drawing.Pictures.NonVisualPictureProperties(
                                new DocumentFormat.OpenXml.Drawing.Pictures.NonVisualDrawingProperties { Id = (DocumentFormat.OpenXml.UInt32Value)2U, Name = altText ?? "Image" },
                                new DocumentFormat.OpenXml.Drawing.Pictures.NonVisualPictureDrawingProperties()),
                            new DocumentFormat.OpenXml.Drawing.Pictures.BlipFill(
                                new DocumentFormat.OpenXml.Drawing.Blip { Embed = relationshipId, CompressionState = DocumentFormat.OpenXml.Drawing.BlipCompressionValues.Print },
                                new DocumentFormat.OpenXml.Drawing.Stretch(new DocumentFormat.OpenXml.Drawing.FillRectangle())),
                            new DocumentFormat.OpenXml.Drawing.Pictures.ShapeProperties(
                                new DocumentFormat.OpenXml.Drawing.Transform2D(
                                    new DocumentFormat.OpenXml.Drawing.Offset { X = 0L, Y = 0L },
                                    new DocumentFormat.OpenXml.Drawing.Extents { Cx = widthCx, Cy = heightCx }),
                                new DocumentFormat.OpenXml.Drawing.PresetGeometry { Preset = DocumentFormat.OpenXml.Drawing.ShapeTypeValues.Rectangle }))
                    ) { Uri = "http://schemas.openxmlformats.org/drawingml/2006/picture" }
                )
            ) { DistanceFromTop = 0U, DistanceFromBottom = 0U, DistanceFromLeft = 0U, DistanceFromRight = 0U }
        );
    }

    private static void SerializeToXlsx(Parser.AST.SpreadsheetDocument doc, string filePath)
    {
        using var spreadsheetDoc = DocumentFormat.OpenXml.Packaging.SpreadsheetDocument.Open(filePath, true);
        var workbookPart = spreadsheetDoc.WorkbookPart;
        if (workbookPart == null) return;

        foreach (var wsNode in doc.Children.OfType<WorksheetNode>())
        {
            var sheet = workbookPart.Workbook.Sheets?.Cast<Sheet>().FirstOrDefault(s => s.Name == wsNode.Name);
            if (sheet?.Id?.Value == null) continue;
            var worksheetPart = workbookPart.GetPartById(sheet.Id.Value) as WorksheetPart;
            var sheetData = worksheetPart?.Worksheet?.Elements<SheetData>().FirstOrDefault();
            if (sheetData == null) continue;

            foreach (var rowNode in wsNode.Children.OfType<GridRowNode>())
            {
                var wRow = sheetData.Elements<Row>().FirstOrDefault(r => r.RowIndex == (uint)(rowNode.RowIndex + 1));
                if (wRow == null) continue;

                foreach (var cellNode in rowNode.Children.OfType<GridCellNode>())
                {
                    string colLetter = GetColumnLabel(cellNode.ColumnIndex);
                    string cellRef = $"{colLetter}{rowNode.RowIndex + 1}";
                    var wCell = wRow.Elements<Cell>().FirstOrDefault(c => c.CellReference?.Value == cellRef);
                    
                    if (wCell == null)
                    {
                        wCell = new Cell { CellReference = cellRef };
                        wRow.AppendChild(wCell);
                    }

                    if (cellNode.Formula != null)
                    {
                        wCell.CellFormula = new CellFormula(cellNode.Formula);
                    }
                    else
                    {
                        wCell.CellFormula = null;
                    }

                    wCell.DataType = CellValues.InlineString;
                    wCell.InlineString = new InlineString(new DocumentFormat.OpenXml.Spreadsheet.Text(cellNode.DisplayText));
                    wCell.CellValue = null;
                }
            }
            worksheetPart.Worksheet.Save();
        }
        workbookPart.Workbook.Save();
    }

    private static void SerializeToPptx(Parser.AST.PresentationDocument doc, string filePath)
    {
        using var presentationDoc = DocumentFormat.OpenXml.Packaging.PresentationDocument.Open(filePath, true);
        var presentationPart = presentationDoc.PresentationPart;
        if (presentationPart == null) return;

        var slideIdList = presentationPart.Presentation.SlideIdList;
        if (slideIdList == null) return;

        var slideNodes = doc.Children.OfType<SlideNode>().ToList();

        int slideIdx = 0;
        foreach (var slideIdObj in slideIdList.Cast<SlideId>())
        {
            if (slideIdObj.RelationshipId?.Value == null) continue;
            var slidePart = presentationPart.GetPartById(slideIdObj.RelationshipId.Value) as SlidePart;
            if (slidePart?.Slide == null) continue;

            if (slideIdx >= slideNodes.Count) break;
            var slideNode = slideNodes[slideIdx++];

            var shapes = slidePart.Slide.CommonSlideData?.ShapeTree?.Descendants<DocumentFormat.OpenXml.Presentation.Shape>().ToList();
            if (shapes == null) continue;

            int shapeIdx = 0;
            var shapeNodes = slideNode.Children.OfType<ShapeNode>().Where(s => s.ShapeType != "Picture").ToList();

            foreach (var sp in shapes)
            {
                if (shapeIdx >= shapeNodes.Count) break;
                var shapeNode = shapeNodes[shapeIdx++];

                if (shapeNode.Text != null && sp.TextBody != null)
                {
                    var textPara = sp.TextBody.Descendants<DocumentFormat.OpenXml.Drawing.Text>().FirstOrDefault();
                    if (textPara != null)
                    {
                        textPara.Text = shapeNode.Text;
                    }
                }

                var xfrm = sp.ShapeProperties?.Transform2D;
                if (xfrm != null)
                {
                    if (shapeNode.X.HasValue) xfrm.Offset.X = (long)(shapeNode.X.Value * 12700.0);
                    if (shapeNode.Y.HasValue) xfrm.Offset.Y = (long)(shapeNode.Y.Value * 12700.0);
                    if (shapeNode.Width.HasValue) xfrm.Extents.Cx = (long)(shapeNode.Width.Value * 12700.0);
                    if (shapeNode.Height.HasValue) xfrm.Extents.Cy = (long)(shapeNode.Height.Value * 12700.0);
                }
            }
            slidePart.Slide.Save();
        }
        presentationPart.Presentation.Save();
    }

    private static string GetColumnLabel(int index)
    {
        string label = string.Empty;
        int col = index;
        while (col >= 0)
        {
            label = (char)('A' + col % 26) + label;
            col = col / 26 - 1;
        }
        return label;
    }
}
