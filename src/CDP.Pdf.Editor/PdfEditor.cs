using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using Avalonia.VisualTree;
using SkiaSharp;
using CDP.Pdf.Editor.Model;

namespace CDP.Pdf.Editor;

public enum PdfEditMode
{
    Select,
    DirectSelect,
    Pan,
    AddText,
    AddShape,
    Highlight,
    Underline,
    StickyNote,
    Pencil,
    AddFormField
}

public enum FitMode
{
    None,
    Width,
    Page
}

public class PdfEditor : Avalonia.Controls.Control, ILogicalScrollable
{
    public static readonly StyledProperty<string?> FilePathProperty =
        AvaloniaProperty.Register<PdfEditor, string?>(nameof(FilePath));

    public string? FilePath
    {
        get => GetValue(FilePathProperty);
        set => SetValue(FilePathProperty, value);
    }

    public static readonly StyledProperty<bool> IsReadOnlyProperty =
        AvaloniaProperty.Register<PdfEditor, bool>(nameof(IsReadOnly), false);

    public bool IsReadOnly
    {
        get => GetValue(IsReadOnlyProperty);
        set => SetValue(IsReadOnlyProperty, value);
    }

    public static readonly StyledProperty<PdfEditMode> EditModeProperty =
        AvaloniaProperty.Register<PdfEditor, PdfEditMode>(nameof(EditMode), PdfEditMode.Select);

    public PdfEditMode EditMode
    {
        get => GetValue(EditModeProperty);
        set => SetValue(EditModeProperty, value);
    }

    public static readonly StyledProperty<double> ZoomScaleProperty =
        AvaloniaProperty.Register<PdfEditor, double>(nameof(ZoomScale), 1.0);

    public double ZoomScale
    {
        get => GetValue(ZoomScaleProperty);
        set => SetValue(ZoomScaleProperty, value);
    }

    public static readonly StyledProperty<string?> SelectedCommentTextProperty =
        AvaloniaProperty.Register<PdfEditor, string?>(nameof(SelectedCommentText));

    public string? SelectedCommentText
    {
        get => GetValue(SelectedCommentTextProperty);
        set => SetValue(SelectedCommentTextProperty, value);
    }

    private bool _isLoading;
    public static readonly DirectProperty<PdfEditor, bool> IsLoadingProperty =
        AvaloniaProperty.RegisterDirect<PdfEditor, bool>(nameof(IsLoading), o => o.IsLoading);

    public bool IsLoading
    {
        get => _isLoading;
        private set => SetAndRaise(IsLoadingProperty, ref _isLoading, value);
    }

    private string _loadingStatus = "";
    public static readonly DirectProperty<PdfEditor, string> LoadingStatusProperty =
        AvaloniaProperty.RegisterDirect<PdfEditor, string>(nameof(LoadingStatus), o => o.LoadingStatus);

    public string LoadingStatus
    {
        get => _loadingStatus;
        private set => SetAndRaise(LoadingStatusProperty, ref _loadingStatus, value);
    }

    public PdfDocumentModel Document => _document;
    private PdfDocumentModel _document = new();
    private PdfElementModel? _selectedElement;
    private int _selectedPageIndex = -1;

    // Caching and Background Rendering
    private PageImageCache? _pageCache;
    private readonly ConcurrentDictionary<(int pageNum, float scale), Task<SKBitmap?>> _renderingTasks = new();

    // Scrolling implementation
    private double _scrollOffsetX;
    private double _scrollOffsetY;
    public bool CanHorizontallyScroll { get; set; } = true;
    public bool CanVerticallyScroll { get; set; } = true;
    public bool IsLogicalScrollEnabled => true;
    public Size ScrollSize => new(16, 16);
    public Size PageScrollSize => new(80, 80);
    public event EventHandler? ScrollInvalidated;
    public string LayoutDebugInfo => $"Bounds={Bounds}, Viewport={Viewport}, Extent={Extent}, ScrollOffset={Offset}";

    private const float PageGap = 20f;
    private const float MarginLeft = 20f;

    // Rendering Cache
    private SKBitmap? _cachedBitmap;
    private WriteableBitmap? _cachedWriteableBitmap;

    // Interactive state
    private bool _isDragging;
    private bool _isResizing;
    private int _resizeHandleIndex = -1; // 0: TopLeft, 1: TopRight, 2: BottomRight, 3: BottomLeft
    private Point _lastPointerPosition;
    private int _caretOffset;
    private bool _caretVisible = true;
    private DispatcherTimer? _caretTimer;
    private PdfElementModel? _hoveredElement;
    private FitMode _fitMode = FitMode.None;
    private Size _lastViewportSize;
    private bool _isFitting;
    private bool _isFitPending;
    private DispatcherTimer? _zoomDebounceTimer;
    private Avalonia.Visual? _subscribedParent;
    private int _loadRequestId;

    // Range Selection & Panning state
    private bool _isPanning;
    private bool _isSelectingRange;
    private Point _rangeSelectStart;
    private Point _rangeSelectEnd;
    private List<PdfTextElementModel> _selectedTextElements = new();

    private ScrollViewer? FindScrollViewer()
    {
        var parent = this.GetVisualParent();
        while (parent != null)
        {
            if (parent is ScrollViewer sv) return sv;
            parent = parent.GetVisualParent();
        }
        return null;
    }

    public Size Viewport
    {
        get
        {
            var visualParent = this.GetVisualParent() as Avalonia.Visual;
            if (visualParent != null) return visualParent.Bounds.Size;
            return (Parent as Avalonia.Visual)?.Bounds.Size ?? Bounds.Size;
        }
    }

    private Size GetViewportSize() => Viewport;

    public Size Extent
    {
        get
        {
            if (_document.Pages.Count == 0) return new Size(0, 0);
            double scale = ZoomScale;
            double maxWidth = _document.Pages.Max(p => (p.Rotation % 180 != 0 ? p.Height : p.Width) * scale) + MarginLeft * 2;
            double totalHeight = _document.Pages.Sum(p => (p.Rotation % 180 != 0 ? p.Width : p.Height) * scale) + (_document.Pages.Count + 1) * PageGap;
            return new Size(maxWidth, totalHeight);
        }
    }

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

    static PdfEditor()
    {
        FilePathProperty.Changed.AddClassHandler<PdfEditor>((editor, args) =>
        {
            _ = editor.LoadDocumentAsync(args.GetNewValue<string?>());
        });

        ZoomScaleProperty.Changed.AddClassHandler<PdfEditor>((editor, args) =>
        {
            if (!editor._isFitting)
            {
                editor._fitMode = FitMode.None;
            }

            editor.ScrollInvalidated?.Invoke(editor, EventArgs.Empty);
            editor.InvalidateMeasure();

            if (editor._zoomDebounceTimer == null)
            {
                editor._zoomDebounceTimer = new DispatcherTimer(DispatcherPriority.Background)
                {
                    Interval = TimeSpan.FromMilliseconds(250)
                };
                editor._zoomDebounceTimer.Tick += (s, e) =>
                {
                    editor._zoomDebounceTimer.Stop();
                    editor.InvalidateVisual();
                };
            }
            editor._zoomDebounceTimer.Stop();
            editor._zoomDebounceTimer.Start();

            editor.InvalidateVisual();
        });

        SelectedCommentTextProperty.Changed.AddClassHandler<PdfEditor>((editor, args) =>
        {
            if (editor._selectedElement is PdfStickyNoteElementModel note)
            {
                var newVal = args.GetNewValue<string?>();
                if (note.CommentText != newVal)
                {
                    note.CommentText = newVal ?? "";
                    note.IsModified = true;
                    editor.InvalidateVisual();
                }
            }
        });
        
        FocusableProperty.OverrideDefaultValue<PdfEditor>(true);
    }

    public PdfEditor()
    {
        ClipToBounds = true;
        _caretTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(500)
        };
        _caretTimer.Tick += (s, e) =>
        {
            _caretVisible = !_caretVisible;
            if (_selectedElement is PdfTextElementModel)
            {
                InvalidateVisual();
            }
        };
        _caretTimer.Start();
    }

    public async Task LoadDocumentAsync(string? path)
    {
        int currentId = ++_loadRequestId;

        _selectedElement = null;
        _selectedPageIndex = -1;
        _selectedTextElements.Clear();
        UpdateSelectedCommentText();
        
        _renderingTasks.Clear();
        _pageCache?.Dispose();
        _pageCache = null;

        _scrollOffsetX = 0;
        _scrollOffsetY = 0;

        PdfDocumentModel tempDoc;
        if (string.IsNullOrEmpty(path))
        {
            tempDoc = new PdfDocumentModel();
        }
        else
        {
            tempDoc = new PdfDocumentModel();
            
            IsLoading = true;
            LoadingStatus = "Loading PDF...";

            try
            {
                await Task.Run(() =>
                {
                    tempDoc.Load(path);
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading PDF: {ex.Message}");
            }
            finally
            {
                if (currentId == _loadRequestId)
                {
                    IsLoading = false;
                    LoadingStatus = "";
                }
            }
        }
        
        if (currentId == _loadRequestId)
        {
            _document = tempDoc;
            ScrollInvalidated?.Invoke(this, EventArgs.Empty);
            InvalidateMeasure();
            InvalidateVisual();
            ApplyActiveFitMode();
        }
    }

    public void RotateCurrentPage(int degrees)
    {
        if (_selectedPageIndex >= 0)
        {
            _pageCache?.Clear();
            _document.RotatePage(_selectedPageIndex, degrees);
            InvalidateMeasure();
            InvalidateVisual();
            ScrollInvalidated?.Invoke(this, EventArgs.Empty);
            ApplyActiveFitMode();
        }
    }

    public void InsertPageAfterCurrent()
    {
        int targetIndex = _selectedPageIndex >= 0 ? _selectedPageIndex + 1 : _document.Pages.Count;
        _pageCache?.Clear();
        _document.InsertPage(targetIndex);
        _selectedPageIndex = targetIndex;
        InvalidateMeasure();
        InvalidateVisual();
        ScrollInvalidated?.Invoke(this, EventArgs.Empty);
        ApplyActiveFitMode();
    }

    public void DeleteCurrentPage()
    {
        if (_selectedPageIndex >= 0)
        {
            _pageCache?.Clear();
            _document.DeletePage(_selectedPageIndex);
            if (_selectedPageIndex >= _document.Pages.Count)
                _selectedPageIndex = _document.Pages.Count - 1;
            _selectedElement = null;
            _selectedTextElements.Clear();
            UpdateSelectedCommentText();
            InvalidateMeasure();
            InvalidateVisual();
            ScrollInvalidated?.Invoke(this, EventArgs.Empty);
            ApplyActiveFitMode();
        }
    }

    private void ApplyActiveFitMode()
    {
        if (_fitMode == FitMode.Width)
        {
            FitToWidth();
        }
        else if (_fitMode == FitMode.Page)
        {
            FitToPage();
        }
    }

    public void FitToWidth()
    {
        if (_document.Pages.Count == 0) return;
        _fitMode = FitMode.Width;
        var viewport = Viewport;
        if (viewport.Width <= 0) return;

        int activeIdx = Math.Max(0, _selectedPageIndex);
        var page = _document.Pages[activeIdx];
        double pageW = page.Rotation % 180 != 0 ? page.Height : page.Width;
        if (pageW <= 0) return;

        // Calculate target scale assuming no vertical scrollbar is needed
        double availableW = viewport.Width - MarginLeft * 2;
        double targetScale = availableW / pageW;

        // Check if total document height at this scale exceeds the viewport height
        double totalHeight = _document.Pages.Sum(p => (p.Rotation % 180 != 0 ? p.Width : p.Height) * targetScale) + (_document.Pages.Count + 1) * PageGap;
        if (totalHeight > viewport.Height)
        {
            // A vertical scrollbar is required. Deduct scrollbar width (typically 16px in Avalonia)
            double scrollbarWidth = 16.0;
            availableW = Math.Max(0, viewport.Width - scrollbarWidth - MarginLeft * 2);
            targetScale = availableW / pageW;
        }

        targetScale = Math.Clamp(targetScale, 0.5, 4.0);
        if (Math.Abs(ZoomScale - targetScale) > 0.001)
        {
            _isFitting = true;
            try
            {
                ZoomScale = targetScale;
            }
            finally
            {
                _isFitting = false;
            }
        }
    }

    public void FitToPage()
    {
        if (_document.Pages.Count == 0) return;
        _fitMode = FitMode.Page;
        var viewport = Viewport;
        if (viewport.Width <= 0 || viewport.Height <= 0) return;

        int activeIdx = Math.Max(0, _selectedPageIndex);
        var page = _document.Pages[activeIdx];
        double pageW = page.Rotation % 180 != 0 ? page.Height : page.Width;
        double pageH = page.Rotation % 180 != 0 ? page.Width : page.Height;
        if (pageW <= 0 || pageH <= 0) return;

        // Calculate target scale assuming no vertical scrollbar is needed
        double availableW = viewport.Width - MarginLeft * 2;
        double availableH = viewport.Height - PageGap * 2;
        double scaleW = availableW / pageW;
        double scaleH = availableH / pageH;
        double targetScale = Math.Min(scaleW, scaleH);

        // Check if total document height at this scale exceeds the viewport height
        double totalHeight = _document.Pages.Sum(p => (p.Rotation % 180 != 0 ? p.Width : p.Height) * targetScale) + (_document.Pages.Count + 1) * PageGap;
        if (totalHeight > viewport.Height)
        {
            // A vertical scrollbar is required. Deduct scrollbar width
            double scrollbarWidth = 16.0;
            availableW = Math.Max(0, viewport.Width - scrollbarWidth - MarginLeft * 2);
            scaleW = availableW / pageW;
            targetScale = Math.Min(scaleW, scaleH);
        }

        targetScale = Math.Clamp(targetScale, 0.5, 4.0);
        if (Math.Abs(ZoomScale - targetScale) > 0.001)
        {
            _isFitting = true;
            try
            {
                ZoomScale = targetScale;
            }
            finally
            {
                _isFitting = false;
            }
        }
    }

    public void SaveDocument(string path)
    {
        try
        {
            _document.Save(path);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error saving PDF: {ex.Message}");
        }
    }

    /// <summary>
    /// Returns the width to use for centering pages during rendering and hit-testing.
    /// This is the visible viewport width, which may differ from Bounds.Width when
    /// the control implements ILogicalScrollable (the layout system may size us
    /// differently from the visible viewport).
    /// </summary>
    private double GetRenderWidth()
    {
        var viewport = Viewport;
        double vw = viewport.Width;
        // Use viewport if available; fall back to Bounds.Width.
        if (vw > 0) return vw;
        return Bounds.Width;
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        if (_document.Pages.Count == 0) return new Size(100, 100);
        
        var viewport = Viewport;
        double width = viewport.Width > 0 ? viewport.Width : Extent.Width;
        double height = viewport.Height > 0 ? viewport.Height : Extent.Height;
        
        return new Size(width, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        return finalSize;
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        
        var viewport = Viewport;
        if (viewport.Width <= 0 || viewport.Height <= 0) return;
        
        if (Math.Abs(_lastViewportSize.Width - viewport.Width) < 0.01 && 
            Math.Abs(_lastViewportSize.Height - viewport.Height) < 0.01)
        {
            return; // Viewport size did not change, ignore child size change
        }
        
        _lastViewportSize = viewport;

        if (_fitMode == FitMode.Width)
        {
            if (!_isFitPending)
            {
                _isFitPending = true;
                Dispatcher.UIThread.Post(() => {
                    _isFitPending = false;
                    FitToWidth();
                }, DispatcherPriority.Background);
            }
        }
        else if (_fitMode == FitMode.Page)
        {
            if (!_isFitPending)
            {
                _isFitPending = true;
                Dispatcher.UIThread.Post(() => {
                    _isFitPending = false;
                    FitToPage();
                }, DispatcherPriority.Background);
            }
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _subscribedParent = this.GetVisualParent() as Avalonia.Visual;
        if (_subscribedParent != null)
        {
            _subscribedParent.PropertyChanged += OnVisualParentPropertyChanged;
        }
    }

    private void OnVisualParentPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == Visual.BoundsProperty)
        {
            InvalidateMeasure();
        }
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        var bounds = Bounds;
        if (bounds.Width <= 0 || bounds.Height <= 0) return;

        // Use the viewport size for rendering, not Bounds which may differ
        // from the visible area with ILogicalScrollable.
        double renderWidth = GetRenderWidth();
        double renderHeight = Viewport.Height > 0 ? Viewport.Height : bounds.Height;

        double scaling = TopLevel.GetTopLevel(this)?.RenderScaling ?? 1.0;
        int pixelWidth = (int)Math.Max(1, Math.Round(renderWidth * scaling));
        int pixelHeight = (int)Math.Max(1, Math.Round(renderHeight * scaling));

        System.Console.WriteLine($"RENDER_DEBUG: renderWidth={renderWidth}, renderHeight={renderHeight}, scaling={scaling}, pixelWidth={pixelWidth}, pixelHeight={pixelHeight}, ZoomScale={ZoomScale}");

        if (_cachedBitmap == null || _cachedBitmap.Width != pixelWidth || _cachedBitmap.Height != pixelHeight)
        {
            _cachedBitmap?.Dispose();
            _cachedBitmap = new SKBitmap(pixelWidth, pixelHeight, SKColorType.Bgra8888, SKAlphaType.Premul);
            _cachedWriteableBitmap?.Dispose();
            _cachedWriteableBitmap = null;
        }

        using (var canvas = new SKCanvas(_cachedBitmap))
        {
            canvas.Clear(new SKColor(43, 45, 48)); // Dark grey

            canvas.Save();
            canvas.Scale((float)scaling);
            canvas.Translate(-(float)_scrollOffsetX, -(float)_scrollOffsetY);

            // Render PDF Pages
            float currentY = PageGap;
            var viewport = new SKRect((float)_scrollOffsetX, (float)_scrollOffsetY, (float)(_scrollOffsetX + renderWidth), (float)(_scrollOffsetY + renderHeight));
            float scale = (float)ZoomScale;
            double docWidth = _document.Pages.Count > 0 ? _document.Pages.Max(p => (p.Rotation % 180 != 0 ? p.Height : p.Width) * scale) : 0;
            double centerAxis = (renderWidth > docWidth + MarginLeft * 2) ? (renderWidth / 2) : (MarginLeft + docWidth / 2);

            for (int i = 0; i < _document.Pages.Count; i++)
            {
                var page = _document.Pages[i];
                float originalW = (float)page.Width;
                float originalH = (float)page.Height;
                float pageW = (page.Rotation % 180 != 0 ? originalH : originalW) * scale;
                float pageH = (page.Rotation % 180 != 0 ? originalW : originalH) * scale;

                // Check if page falls within viewport
                bool isVisible = (currentY + pageH >= viewport.Top) && (currentY <= viewport.Bottom);

                if (isVisible)
                {
                    float pageX = (float)(centerAxis - pageW / 2);
                    System.Console.WriteLine($"RENDER_DEBUG: Page {i+1}: originalW={originalW}, scale={scale}, pageW={pageW}, pageX={pageX}, currentY={currentY}");
                    canvas.Save();
                    canvas.Translate(pageX, currentY);

                    // 1. Draw Page shadow
                    using var shadowPaint = new SKPaint { Color = new SKColor(20, 20, 20, 100), Style = SKPaintStyle.Fill };
                    canvas.DrawRect(new SKRect(2, 2, pageW + 4, pageH + 4), shadowPaint);

                    // 2. Draw page background bitmap or placeholder
                    var bgBitmap = GetPageBitmapCached(i + 1, scale * (float)scaling);
                    if (bgBitmap != null)
                    {
                        canvas.Save();
                        if (page.Rotation != 0)
                        {
                            canvas.Translate(pageW / 2, pageH / 2);
                            canvas.RotateDegrees(page.Rotation);
                            canvas.Translate(-originalW * scale / 2, -originalH * scale / 2);
                        }
                        canvas.DrawBitmap(bgBitmap, new SKRect(0, 0, originalW * scale, originalH * scale));
                        canvas.Restore();
                    }
                    else
                    {
                        using var pageBackgroundPaint = new SKPaint { Color = SKColors.White, Style = SKPaintStyle.Fill };
                        canvas.DrawRect(new SKRect(0, 0, pageW, pageH), pageBackgroundPaint);
                    }

                    // 3. Draw elements on top (taking rotation and scale into account)
                    canvas.Save();
                    if (page.Rotation != 0)
                    {
                        canvas.Translate(pageW / 2, pageH / 2);
                        canvas.RotateDegrees(page.Rotation);
                        canvas.Translate(-originalW * scale / 2, -originalH * scale / 2);
                    }
                    canvas.Scale(scale);

                    foreach (var element in page.Elements)
                    {
                        if (EditMode == PdfEditMode.DirectSelect)
                        {
                            if (element is PdfTextElementModel t && !t.IsGranular) continue;
                        }
                        else
                        {
                            if (element is PdfTextElementModel t && t.IsGranular) continue;
                        }

                        if (element.IsOriginal && (element.IsModified || element.IsDeleted))
                        {
                            // Mask out original elements that were modified or deleted
                            using var maskPaint = new SKPaint { Color = SKColors.White, Style = SKPaintStyle.Fill };
                            canvas.DrawRect(element.OriginalBounds, maskPaint);
                        }

                        if (!element.IsDeleted && (!element.IsOriginal || element.IsModified))
                        {
                            element.Render(canvas, originalH);
                        }
                    }

                    // Draw range selection highlights
                    if (i == _selectedPageIndex && _selectedTextElements.Count > 0)
                    {
                        using var highlightPaint = new SKPaint { Color = new SKColor(52, 152, 219, 70), Style = SKPaintStyle.Fill };
                        foreach (var textEl in _selectedTextElements)
                        {
                            canvas.DrawRect(textEl.Bounds, highlightPaint);
                        }
                    }

                    // Draw hovered element highlight
                    if (i == _selectedPageIndex && _hoveredElement != null && _hoveredElement != _selectedElement && page.Elements.Contains(_hoveredElement))
                    {
                        using var hoverPaint = new SKPaint
                        {
                            Color = new SKColor(52, 152, 219, 140),
                            Style = SKPaintStyle.Stroke,
                            StrokeWidth = 1f
                        };
                        canvas.DrawRect(_hoveredElement.Bounds, hoverPaint);
                    }

                    // Selection Border and Handles
                    if (i == _selectedPageIndex && _selectedElement != null)
                    {
                        DrawSelectionBox(canvas, _selectedElement, originalH);
                    }

                    canvas.Restore(); // Restore elements transform
                    canvas.Restore(); // Restore translation
                }

                currentY += pageH + PageGap;
            }

            canvas.Restore();
        }

        if (_cachedWriteableBitmap == null)
        {
            _cachedWriteableBitmap = new WriteableBitmap(
                new PixelSize(pixelWidth, pixelHeight),
                new Vector(96 * scaling, 96 * scaling),
                PixelFormat.Bgra8888,
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
                    System.Buffer.MemoryCopy(
                        (void*)(srcPtr + y * srcRowBytes),
                        (void*)(dstPtr + y * dstRowBytes),
                        rowSize,
                        rowSize);
                }
            }
        }

        double logicalWidth = renderWidth;
        double logicalHeight = renderHeight;
        context.DrawImage(_cachedWriteableBitmap, new Rect(0, 0, logicalWidth, logicalHeight));
    }

    private SKBitmap? GetPageBitmapCached(int pageNum, float scale)
    {
        if (_pageCache == null)
        {
            _pageCache = new PageImageCache(_document, maxSize: 16);
        }

        float roundedScale = (float)Math.Round(scale, 2);
        var bitmap = _pageCache.GetPageBitmap(pageNum, roundedScale);
        if (bitmap != null)
        {
            return bitmap;
        }

        var placeholder = _pageCache.GetBestAvailablePageBitmap(pageNum, out _);
        
        if (_zoomDebounceTimer == null || !_zoomDebounceTimer.IsEnabled)
        {
            TriggerAsyncRender(pageNum, roundedScale);
        }

        return placeholder;
    }

    private void TriggerAsyncRender(int pageNum, float scale)
    {
        var key = (pageNum, scale);
        if (_renderingTasks.ContainsKey(key)) return;

        var task = Task.Run(() => 
        {
            return _pageCache?.GetPageBitmap(pageNum, scale);
        });

        if (_renderingTasks.TryAdd(key, task))
        {
            task.ContinueWith(t => 
            {
                _renderingTasks.TryRemove(key, out _);
                Avalonia.Threading.Dispatcher.UIThread.Post(InvalidateVisual);
            });
        }
    }

    private void DrawSelectionBox(SKCanvas canvas, PdfElementModel element, float pageHeight)
    {
        using var paint = new SKPaint
        {
            Color = new SKColor(52, 152, 219),
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 1.5f
        };
        canvas.DrawRect(element.Bounds, paint);

        float handleSize = 6f;
        using var handlePaint = new SKPaint
        {
            Color = new SKColor(52, 152, 219),
            Style = SKPaintStyle.Fill
        };

        SKRect bounds = element.Bounds;
        canvas.DrawRect(new SKRect(bounds.Left - handleSize / 2, bounds.Top - handleSize / 2, bounds.Left + handleSize / 2, bounds.Top + handleSize / 2), handlePaint);
        canvas.DrawRect(new SKRect(bounds.Right - handleSize / 2, bounds.Top - handleSize / 2, bounds.Right + handleSize / 2, bounds.Top + handleSize / 2), handlePaint);
        canvas.DrawRect(new SKRect(bounds.Right - handleSize / 2, bounds.Bottom - handleSize / 2, bounds.Right + handleSize / 2, bounds.Bottom + handleSize / 2), handlePaint);
        canvas.DrawRect(new SKRect(bounds.Left - handleSize / 2, bounds.Bottom - handleSize / 2, bounds.Left + handleSize / 2, bounds.Bottom + handleSize / 2), handlePaint);
    }

    public (int pageIndex, Point pageCoords) GetPageAtPoint(Point pt)
    {
        double docY = pt.Y + _scrollOffsetY;
        double docX = pt.X + _scrollOffsetX;
        double scale = ZoomScale;
        double docWidth = _document.Pages.Count > 0 ? _document.Pages.Max(p => (p.Rotation % 180 != 0 ? p.Height : p.Width) * scale) : 0;
        double centerAxis = (Bounds.Width > docWidth + MarginLeft * 2) ? (Bounds.Width / 2) : (MarginLeft + docWidth / 2);

        float currentY = PageGap;
        for (int i = 0; i < _document.Pages.Count; i++)
        {
            var page = _document.Pages[i];
            float originalW = (float)page.Width;
            float originalH = (float)page.Height;
            float ph = (page.Rotation % 180 != 0 ? originalW : originalH) * (float)scale;
            float pw = (page.Rotation % 180 != 0 ? originalH : originalW) * (float)scale;
            if (docY >= currentY && docY <= currentY + ph)
            {
                double pageXOffset = centerAxis - pw / 2;
                double pageX = docX - pageXOffset;
                double pageY = docY - currentY;
                
                var pt2 = new SKPoint((float)pageX, (float)pageY);
                if (page.Rotation != 0)
                {
                    var matrix = SKMatrix.CreateRotationDegrees(-page.Rotation, pw / 2, ph / 2);
                    float transX = (pw - originalW * (float)scale) / 2;
                    float transY = (ph - originalH * (float)scale) / 2;
                    matrix = matrix.PostConcat(SKMatrix.CreateTranslation(-transX, -transY));
                    pt2 = matrix.MapPoint(pt2);
                }
                
                return (i, new Point(pt2.X / scale, pt2.Y / scale));
            }
            currentY += ph + PageGap;
        }

        return (-1, default);
    }

    private PdfElementModel? HitTestElements(Point pageCoords, PdfPageModel page)
    {
        float tolerance = 4f;
        var pt = new SKPoint((float)pageCoords.X, (float)pageCoords.Y);
        var candidates = new List<PdfElementModel>();

        foreach (var el in page.Elements)
        {
            if (el.IsDeleted) continue;
            
            if (EditMode == PdfEditMode.DirectSelect)
            {
                if (el is PdfTextElementModel t && !t.IsGranular) continue;
            }
            else
            {
                if (el is PdfTextElementModel t && t.IsGranular) continue;
            }
            
            var inflatedBounds = el.Bounds;
            inflatedBounds.Inflate(tolerance, tolerance);
            if (inflatedBounds.Contains(pt))
            {
                candidates.Add(el);
            }
        }

        if (candidates.Count == 0) return null;

        return candidates
            .OrderBy(el => el is PdfTextElementModel ? 0 : 1)
            .ThenBy(el => el.Bounds.Width * el.Bounds.Height)
            .First();
    }

    private int HitTestHandles(Point pt, PdfElementModel element)
    {
        float handleSize = 8f;
        SKRect bounds = element.Bounds;
        
        if (SKRect.Create((float)pt.X - handleSize/2, (float)pt.Y - handleSize/2, handleSize, handleSize).IntersectsWith(SKRect.Create(bounds.Left - handleSize/2, bounds.Top - handleSize/2, handleSize, handleSize))) return 0;
        if (SKRect.Create((float)pt.X - handleSize/2, (float)pt.Y - handleSize/2, handleSize, handleSize).IntersectsWith(SKRect.Create(bounds.Right - handleSize/2, bounds.Top - handleSize/2, handleSize, handleSize))) return 1;
        if (SKRect.Create((float)pt.X - handleSize/2, (float)pt.Y - handleSize/2, handleSize, handleSize).IntersectsWith(SKRect.Create(bounds.Right - handleSize/2, bounds.Bottom - handleSize/2, handleSize, handleSize))) return 2;
        if (SKRect.Create((float)pt.X - handleSize/2, (float)pt.Y - handleSize/2, handleSize, handleSize).IntersectsWith(SKRect.Create(bounds.Left - handleSize/2, bounds.Bottom - handleSize/2, handleSize, handleSize))) return 3;

        return -1;
    }

    private void UpdateSelectedCommentText()
    {
        if (_selectedElement is PdfStickyNoteElementModel note)
        {
            SelectedCommentText = note.CommentText;
        }
        else
        {
            SelectedCommentText = null;
        }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        Focus();

        if (IsReadOnly && EditMode != PdfEditMode.Pan && EditMode != PdfEditMode.Select && EditMode != PdfEditMode.DirectSelect)
        {
            return;
        }

        var pt = e.GetCurrentPoint(this).Position;

        if (EditMode == PdfEditMode.Pan)
        {
            _isPanning = true;
            _lastPointerPosition = pt;
            Cursor = new Cursor(StandardCursorType.Hand);
            e.Handled = true;
            return;
        }

        var (pageIdx, pageCoords) = GetPageAtPoint(pt);

        if (pageIdx == -1)
        {
            _selectedElement = null;
            _selectedPageIndex = -1;
            _selectedTextElements.Clear();
            UpdateSelectedCommentText();
            InvalidateVisual();
            return;
        }

        var page = _document.Pages[pageIdx];

        if (EditMode == PdfEditMode.Highlight || EditMode == PdfEditMode.Underline)
        {
            _isSelectingRange = true;
            _rangeSelectStart = pageCoords;
            _rangeSelectEnd = pageCoords;
            _selectedTextElements.Clear();
            _selectedPageIndex = pageIdx;
            _selectedElement = null;
            UpdateSelectedCommentText();
            e.Handled = true;
            return;
        }

        if (EditMode == PdfEditMode.Select || EditMode == PdfEditMode.DirectSelect)
        {
            if (_selectedElement != null && pageIdx == _selectedPageIndex)
            {
                int handleIdx = HitTestHandles(pageCoords, _selectedElement);
                if (handleIdx != -1)
                {
                    _isResizing = !IsReadOnly;
                    _resizeHandleIndex = handleIdx;
                    _lastPointerPosition = pt;
                    e.Handled = true;
                    return;
                }
            }

            PdfElementModel? hitElement = HitTestElements(pageCoords, page);

            if (hitElement != null)
            {
                _selectedElement = hitElement;
                _selectedPageIndex = pageIdx;
                _isDragging = !IsReadOnly;
                _lastPointerPosition = pt;
                _selectedTextElements.Clear();
                UpdateSelectedCommentText();

                if (hitElement is PdfTextElementModel textEl)
                {
                    using var textPaint = new SKPaint { TextSize = textEl.FontSize, Typeface = SKTypeface.FromFamilyName(textEl.FontName) };
                    float localX = (float)pageCoords.X - textEl.Bounds.Left;
                    _caretOffset = 0;
                    float minDiff = float.MaxValue;
                    for (int charIdx = 0; charIdx <= textEl.Text.Length; charIdx++)
                    {
                        float width = textPaint.MeasureText(textEl.Text.Substring(0, charIdx));
                        float diff = Math.Abs(width - localX);
                        if (diff < minDiff)
                        {
                            minDiff = diff;
                            _caretOffset = charIdx;
                        }
                    }
                }

                InvalidateVisual();
                e.Handled = true;
            }
            else
            {
                _isSelectingRange = true;
                _rangeSelectStart = pageCoords;
                _rangeSelectEnd = pageCoords;
                _selectedTextElements.Clear();
                _selectedElement = null;
                _selectedPageIndex = pageIdx;
                UpdateSelectedCommentText();
                InvalidateVisual();
            }
        }
        else if (EditMode == PdfEditMode.AddText)
        {
            var newText = new PdfTextElementModel
            {
                Text = "New Text Block",
                FontSize = 14f,
                Color = SKColors.Black,
                Bounds = SKRect.Create((float)pageCoords.X, (float)pageCoords.Y, 150, 20)
            };
            page.Elements.Add(newText);
            _selectedElement = newText;
            _selectedPageIndex = pageIdx;
            _caretOffset = newText.Text.Length;
            UpdateSelectedCommentText();
            EditMode = PdfEditMode.Select;
            InvalidateVisual();
            e.Handled = true;
        }
        else if (EditMode == PdfEditMode.AddShape)
        {
            var newShape = new PdfShapeElementModel
            {
                Bounds = SKRect.Create((float)pageCoords.X, (float)pageCoords.Y, 100, 100),
                FillColor = SKColors.LightGray,
                StrokeColor = SKColors.Black,
                StrokeWidth = 1.5f,
                IsFilled = true
            };
            page.Elements.Add(newShape);
            _selectedElement = newShape;
            _selectedPageIndex = pageIdx;
            UpdateSelectedCommentText();
            EditMode = PdfEditMode.Select;
            InvalidateVisual();
            e.Handled = true;
        }
        else if (EditMode == PdfEditMode.Highlight)
        {
            var newHighlight = new PdfHighlightElementModel
            {
                Bounds = SKRect.Create((float)pageCoords.X, (float)pageCoords.Y, 100, 20),
                Color = new SKColor(255, 255, 0, 100)
            };
            page.Elements.Add(newHighlight);
            _selectedElement = newHighlight;
            _selectedPageIndex = pageIdx;
            UpdateSelectedCommentText();
            EditMode = PdfEditMode.Select;
            InvalidateVisual();
            e.Handled = true;
        }
        else if (EditMode == PdfEditMode.Underline)
        {
            var newUnderline = new PdfUnderlineElementModel
            {
                Bounds = SKRect.Create((float)pageCoords.X, (float)pageCoords.Y, 100, 20),
                Color = SKColors.Red
            };
            page.Elements.Add(newUnderline);
            _selectedElement = newUnderline;
            _selectedPageIndex = pageIdx;
            UpdateSelectedCommentText();
            EditMode = PdfEditMode.Select;
            InvalidateVisual();
            e.Handled = true;
        }
        else if (EditMode == PdfEditMode.StickyNote)
        {
            var newNote = new PdfStickyNoteElementModel
            {
                Bounds = SKRect.Create((float)pageCoords.X, (float)pageCoords.Y, 20, 20)
            };
            page.Elements.Add(newNote);
            _selectedElement = newNote;
            _selectedPageIndex = pageIdx;
            UpdateSelectedCommentText();
            EditMode = PdfEditMode.Select;
            InvalidateVisual();
            e.Handled = true;
        }
        else if (EditMode == PdfEditMode.Pencil)
        {
            var newPencil = new PdfPencilElementModel
            {
                Color = SKColors.Black,
                Bounds = SKRect.Create((float)pageCoords.X, (float)pageCoords.Y, 1, 1)
            };
            newPencil.Points.Add(new SKPoint((float)pageCoords.X, (float)pageCoords.Y));
            page.Elements.Add(newPencil);
            _selectedElement = newPencil;
            _selectedPageIndex = pageIdx;
            _isDragging = true;
            UpdateSelectedCommentText();
            InvalidateVisual();
            e.Handled = true;
        }
        else if (EditMode == PdfEditMode.AddFormField)
        {
            var newForm = new PdfFormFieldElementModel
            {
                Bounds = SKRect.Create((float)pageCoords.X, (float)pageCoords.Y, 100, 20),
                FieldType = "Text",
                Value = "Field"
            };
            page.Elements.Add(newForm);
            _selectedElement = newForm;
            _selectedPageIndex = pageIdx;
            UpdateSelectedCommentText();
            EditMode = PdfEditMode.Select;
            InvalidateVisual();
            e.Handled = true;
        }
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        var pt = e.GetCurrentPoint(this).Position;

        if (_isPanning)
        {
            var delta = pt - _lastPointerPosition;
            Offset = new Vector(Offset.X - delta.X, Offset.Y - delta.Y);
            _lastPointerPosition = pt;
            e.Handled = true;
            return;
        }

        if (_isSelectingRange)
        {
            var (pageIdx, pageCoords) = GetPageAtPoint(pt);
            if (pageIdx == _selectedPageIndex)
            {
                _rangeSelectEnd = pageCoords;
                
                float x1 = (float)Math.Min(_rangeSelectStart.X, _rangeSelectEnd.X);
                float y1 = (float)Math.Min(_rangeSelectStart.Y, _rangeSelectEnd.Y);
                float x2 = (float)Math.Max(_rangeSelectStart.X, _rangeSelectEnd.X);
                float y2 = (float)Math.Max(_rangeSelectStart.Y, _rangeSelectEnd.Y);
                var selRect = new SKRect(x1, y1, x2, y2);
                
                var page = _document.Pages[_selectedPageIndex];
                _selectedTextElements = page.Elements
                    .OfType<PdfTextElementModel>()
                    .Where(el => !el.IsDeleted && el.Bounds.IntersectsWith(selRect))
                    .OrderBy(el => el.Bounds.Top)
                    .ThenBy(el => el.Bounds.Left)
                    .ToList();
                
                InvalidateVisual();
            }
            e.Handled = true;
            return;
        }

        if (_isDragging && EditMode == PdfEditMode.Pencil && _selectedElement is PdfPencilElementModel pencilEl && !IsReadOnly)
        {
            var (pageIdx, pageCoords) = GetPageAtPoint(pt);
            if (pageIdx == _selectedPageIndex)
            {
                pencilEl.Points.Add(new SKPoint((float)pageCoords.X, (float)pageCoords.Y));
                
                float minX = pencilEl.Points[0].X;
                float minY = pencilEl.Points[0].Y;
                float maxX = pencilEl.Points[0].X;
                float maxY = pencilEl.Points[0].Y;
                foreach (var p in pencilEl.Points)
                {
                    if (p.X < minX) minX = p.X;
                    if (p.Y < minY) minY = p.Y;
                    if (p.X > maxX) maxX = p.X;
                    if (p.Y > maxY) maxY = p.Y;
                }
                pencilEl.Bounds = new SKRect(minX, minY, maxX, maxY);
                InvalidateVisual();
            }
        }
        else if (_isDragging && _selectedElement != null && !IsReadOnly)
        {
            var delta = pt - _lastPointerPosition;
            double scale = ZoomScale;
            float dx = (float)(delta.X / scale);
            float dy = (float)(delta.Y / scale);

            var currentBounds = _selectedElement.Bounds;
            _selectedElement.Bounds = SKRect.Create(
                currentBounds.Left + dx,
                currentBounds.Top + dy,
                currentBounds.Width,
                currentBounds.Height
            );
            
            if (_selectedElement.IsOriginal && !_selectedElement.IsModified)
            {
                _selectedElement.IsModified = true;
            }

            _lastPointerPosition = pt;
            InvalidateVisual();
        }
        else if (_isResizing && _selectedElement != null && !IsReadOnly)
        {
            var delta = pt - _lastPointerPosition;
            double scale = ZoomScale;
            float dx = (float)(delta.X / scale);
            float dy = (float)(delta.Y / scale);

            var currentBounds = _selectedElement.Bounds;
            float newLeft = currentBounds.Left;
            float newTop = currentBounds.Top;
            float newWidth = currentBounds.Width;
            float newHeight = currentBounds.Height;

            switch (_resizeHandleIndex)
            {
                case 0: // TopLeft
                    newLeft += dx;
                    newTop += dy;
                    newWidth -= dx;
                    newHeight -= dy;
                    break;
                case 1: // TopRight
                    newTop += dy;
                    newWidth += dx;
                    newHeight -= dy;
                    break;
                case 2: // BottomRight
                    newWidth += dx;
                    newHeight += dy;
                    break;
                case 3: // BottomLeft
                    newLeft += dx;
                    newWidth -= dx;
                    newHeight += dy;
                    break;
            }

            if (newWidth > 10 && newHeight > 10)
            {
                _selectedElement.Bounds = new SKRect(newLeft, newTop, newLeft + newWidth, newTop + newHeight);
                if (_selectedElement.IsOriginal && !_selectedElement.IsModified)
                {
                    _selectedElement.IsModified = true;
                }
            }
            _lastPointerPosition = pt;
            InvalidateVisual();
        }

        // Hover cursor check
        if ((EditMode == PdfEditMode.Select || EditMode == PdfEditMode.DirectSelect) && !_isDragging && !_isResizing && !_isSelectingRange && !_isPanning)
        {
            var (pageIdx, pageCoords) = GetPageAtPoint(pt);
            if (pageIdx != -1)
            {
                var page = _document.Pages[pageIdx];
                var hit = HitTestElements(pageCoords, page);
                
                if (hit != _hoveredElement)
                {
                    _hoveredElement = hit;
                    InvalidateVisual();
                }
                
                if (hit is PdfTextElementModel)
                {
                    Cursor = new Cursor(StandardCursorType.Ibeam);
                }
                else
                {
                    Cursor = null;
                }
            }
            else
            {
                if (_hoveredElement != null)
                {
                    _hoveredElement = null;
                    InvalidateVisual();
                }
                Cursor = null;
            }
        }
        else
        {
            if (_hoveredElement != null)
            {
                _hoveredElement = null;
                InvalidateVisual();
            }
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        
        // Snap highlights/underlines on release
        if (_isSelectingRange && (EditMode == PdfEditMode.Highlight || EditMode == PdfEditMode.Underline))
        {
            var page = _document.Pages[_selectedPageIndex];
            foreach (var textEl in _selectedTextElements)
            {
                if (EditMode == PdfEditMode.Highlight)
                {
                    page.Elements.Add(new PdfHighlightElementModel
                    {
                        Bounds = textEl.Bounds,
                        Color = new SKColor(255, 255, 0, 100)
                    });
                }
                else
                {
                    page.Elements.Add(new PdfUnderlineElementModel
                    {
                        Bounds = textEl.Bounds,
                        Color = SKColors.Red
                    });
                }
            }
            _selectedTextElements.Clear();
            _isSelectingRange = false;
            InvalidateVisual();
            e.Handled = true;
            return;
        }

        _isDragging = false;
        _isResizing = false;
        _isPanning = false;
        _isSelectingRange = false;
        Cursor = null;
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        if (e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            double delta = e.Delta.Y > 0 ? 0.1 : -0.1;
            ZoomScale = Math.Clamp(ZoomScale + delta, 0.5, 4.0);
            InvalidateVisual();
            ScrollInvalidated?.Invoke(this, EventArgs.Empty);
            e.Handled = true;
        }
    }

    protected override void OnTextInput(TextInputEventArgs e)
    {
        base.OnTextInput(e);
        if (IsReadOnly || _selectedElement is not PdfTextElementModel textEl || string.IsNullOrEmpty(e.Text)) return;

        textEl.IsModified = true;
        textEl.Text = textEl.Text.Insert(_caretOffset, e.Text);
        _caretOffset += e.Text.Length;

        using var paint = new SKPaint { TextSize = textEl.FontSize, Typeface = SKTypeface.FromFamilyName(textEl.FontName) };
        float reqWidth = paint.MeasureText(textEl.Text) + 10;
        if (reqWidth > textEl.Bounds.Width)
        {
            textEl.Bounds = new SKRect(textEl.Bounds.Left, textEl.Bounds.Top, textEl.Bounds.Left + reqWidth, textEl.Bounds.Bottom);
        }

        InvalidateVisual();
        e.Handled = true;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        // Check for Copy Command (Ctrl+C / Cmd+C)
        if (e.Key == Key.C && (e.KeyModifiers.HasFlag(KeyModifiers.Control) || e.KeyModifiers.HasFlag(KeyModifiers.Meta)))
        {
            if (_selectedTextElements.Count > 0)
            {
                string text = string.Join(" ", _selectedTextElements.Select(el => el.Text));
                TopLevel.GetTopLevel(this)?.Clipboard?.SetTextAsync(text);
                e.Handled = true;
                return;
            }
            else if (_selectedElement is PdfTextElementModel singleText)
            {
                TopLevel.GetTopLevel(this)?.Clipboard?.SetTextAsync(singleText.Text);
                e.Handled = true;
                return;
            }
        }

        if (IsReadOnly || _selectedElement is not PdfTextElementModel textEl) return;

        if (e.Key == Key.Left)
        {
            if (_caretOffset > 0)
            {
                _caretOffset--;
                _caretVisible = true;
                InvalidateVisual();
            }
            e.Handled = true;
        }
        else if (e.Key == Key.Right)
        {
            if (_caretOffset < textEl.Text.Length)
            {
                _caretOffset++;
                _caretVisible = true;
                InvalidateVisual();
            }
            e.Handled = true;
        }
        else if (e.Key == Key.Back)
        {
            if (_caretOffset > 0)
            {
                textEl.IsModified = true;
                textEl.Text = textEl.Text.Remove(_caretOffset - 1, 1);
                _caretOffset--;
                _caretVisible = true;
                InvalidateVisual();
            }
            e.Handled = true;
        }
        else if (e.Key == Key.Delete)
        {
            if (_caretOffset < textEl.Text.Length)
            {
                textEl.IsModified = true;
                textEl.Text = textEl.Text.Remove(_caretOffset, 1);
                _caretVisible = true;
                InvalidateVisual();
            }
            e.Handled = true;
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        if (_subscribedParent != null)
        {
            _subscribedParent.PropertyChanged -= OnVisualParentPropertyChanged;
            _subscribedParent = null;
        }
        base.OnDetachedFromVisualTree(e);
        _pageCache?.Dispose();
        _pageCache = null;
    }
}
