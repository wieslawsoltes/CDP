using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using AvaloniaEdit;
using AvaloniaEdit.Document;
using AvaloniaEdit.Folding;
using AvaloniaEdit.Rendering;

namespace XamlPlayground.Editor.Minimap;

public class TextEditorMinimap : Control
{
    private const double MinimapGutterWidth = 8;
    private const double MinimapWidthPadding = 2;

    public static readonly StyledProperty<IBrush?> BackgroundProperty =
        AvaloniaProperty.Register<TextEditorMinimap, IBrush?>(nameof(Background), Brushes.Transparent);

    public static readonly StyledProperty<TextEditor?> EditorProperty =
        AvaloniaProperty.Register<TextEditorMinimap, TextEditor?>(nameof(Editor));

    public static readonly StyledProperty<bool> MinimapEnabledProperty =
        AvaloniaProperty.Register<TextEditorMinimap, bool>(nameof(MinimapEnabled), true);

    public static readonly StyledProperty<TextMinimapAutoHide> AutoHideProperty =
        AvaloniaProperty.Register<TextEditorMinimap, TextMinimapAutoHide>(nameof(AutoHide));

    public static readonly StyledProperty<TextMinimapSize> SizeModeProperty =
        AvaloniaProperty.Register<TextEditorMinimap, TextMinimapSize>(nameof(SizeMode));

    public static readonly StyledProperty<TextMinimapSliderVisibility> ShowSliderProperty =
        AvaloniaProperty.Register<TextEditorMinimap, TextMinimapSliderVisibility>(
            nameof(ShowSlider),
            TextMinimapSliderVisibility.MouseOver);

    public static readonly StyledProperty<bool> RenderCharactersProperty =
        AvaloniaProperty.Register<TextEditorMinimap, bool>(nameof(RenderCharacters), true);

    public static readonly StyledProperty<int> MaxColumnProperty =
        AvaloniaProperty.Register<TextEditorMinimap, int>(nameof(MaxColumn), 120);

    public static readonly StyledProperty<int> ScaleProperty =
        AvaloniaProperty.Register<TextEditorMinimap, int>(nameof(Scale), 1);

    public static readonly StyledProperty<bool> ShowRegionSectionHeadersProperty =
        AvaloniaProperty.Register<TextEditorMinimap, bool>(nameof(ShowRegionSectionHeaders), true);

    public static readonly StyledProperty<bool> ShowMarkSectionHeadersProperty =
        AvaloniaProperty.Register<TextEditorMinimap, bool>(nameof(ShowMarkSectionHeaders), true);

    public static readonly StyledProperty<string> MarkSectionHeaderRegexProperty =
        AvaloniaProperty.Register<TextEditorMinimap, string>(
            nameof(MarkSectionHeaderRegex),
            @"\bMARK:\s*(?<separator>-?)\s*(?<label>.*)$");

    public static readonly StyledProperty<double> SectionHeaderFontSizeProperty =
        AvaloniaProperty.Register<TextEditorMinimap, double>(nameof(SectionHeaderFontSize), 9);

    public static readonly StyledProperty<double> SectionHeaderLetterSpacingProperty =
        AvaloniaProperty.Register<TextEditorMinimap, double>(nameof(SectionHeaderLetterSpacing), 1);

    public static readonly StyledProperty<string?> LanguageProperty =
        AvaloniaProperty.Register<TextEditorMinimap, string?>(nameof(Language));

    public static readonly StyledProperty<IBrush?> TextBrushProperty =
        AvaloniaProperty.Register<TextEditorMinimap, IBrush?>(nameof(TextBrush));

    public static readonly StyledProperty<IBrush?> CommentBrushProperty =
        AvaloniaProperty.Register<TextEditorMinimap, IBrush?>(nameof(CommentBrush));

    public static readonly StyledProperty<IBrush?> KeywordBrushProperty =
        AvaloniaProperty.Register<TextEditorMinimap, IBrush?>(nameof(KeywordBrush));

    public static readonly StyledProperty<IBrush?> StringBrushProperty =
        AvaloniaProperty.Register<TextEditorMinimap, IBrush?>(nameof(StringBrush));

    public static readonly StyledProperty<IBrush?> TagBrushProperty =
        AvaloniaProperty.Register<TextEditorMinimap, IBrush?>(nameof(TagBrush));

    public static readonly StyledProperty<IBrush?> SliderBrushProperty =
        AvaloniaProperty.Register<TextEditorMinimap, IBrush?>(nameof(SliderBrush));

    public static readonly StyledProperty<IBrush?> SliderPointerOverBrushProperty =
        AvaloniaProperty.Register<TextEditorMinimap, IBrush?>(nameof(SliderPointerOverBrush));

    public static readonly StyledProperty<IBrush?> SliderActiveBrushProperty =
        AvaloniaProperty.Register<TextEditorMinimap, IBrush?>(nameof(SliderActiveBrush));

    public static readonly StyledProperty<IBrush?> SectionHeaderForegroundProperty =
        AvaloniaProperty.Register<TextEditorMinimap, IBrush?>(nameof(SectionHeaderForeground));

    public static readonly StyledProperty<IBrush?> SectionHeaderSeparatorBrushProperty =
        AvaloniaProperty.Register<TextEditorMinimap, IBrush?>(nameof(SectionHeaderSeparatorBrush));

    private static readonly SolidColorBrush s_defaultTextBrush = new(Color.FromRgb(120, 120, 120));
    private static readonly SolidColorBrush s_defaultCommentBrush = new(Color.FromRgb(83, 128, 65));
    private static readonly SolidColorBrush s_defaultKeywordBrush = new(Color.FromRgb(49, 120, 198));
    private static readonly SolidColorBrush s_defaultStringBrush = new(Color.FromRgb(171, 91, 48));
    private static readonly SolidColorBrush s_defaultTagBrush = new(Color.FromRgb(128, 80, 160));
    private static readonly SolidColorBrush s_defaultSliderBrush = new(Color.FromArgb(70, 128, 128, 128));
    private static readonly SolidColorBrush s_defaultSliderPointerOverBrush = new(Color.FromArgb(120, 128, 128, 128));
    private static readonly SolidColorBrush s_defaultSliderActiveBrush = new(Color.FromArgb(160, 128, 128, 128));
    private static readonly SolidColorBrush s_defaultSectionHeaderBrush = new(Color.FromRgb(16, 95, 191));

    private readonly DispatcherTimer _scrollRevealTimer;
    private Regex? _markSectionHeaderRegex;
    private string? _markSectionHeaderRegexPattern;
    private TextEditor? _subscribedEditor;
    private TextDocument? _document;
    private bool _isPointerOver;
    private bool _isDragging;
    private bool _isScrollRevealed;

    static TextEditorMinimap()
    {
        AffectsMeasure<TextEditorMinimap>(
            MinimapEnabledProperty,
            MaxColumnProperty,
            ScaleProperty,
            RenderCharactersProperty);
        AffectsRender<TextEditorMinimap>(
            BackgroundProperty,
            MinimapEnabledProperty,
            AutoHideProperty,
            SizeModeProperty,
            ShowSliderProperty,
            RenderCharactersProperty,
            MaxColumnProperty,
            ScaleProperty,
            ShowRegionSectionHeadersProperty,
            ShowMarkSectionHeadersProperty,
            MarkSectionHeaderRegexProperty,
            SectionHeaderFontSizeProperty,
            SectionHeaderLetterSpacingProperty,
            LanguageProperty,
            TextBrushProperty,
            CommentBrushProperty,
            KeywordBrushProperty,
            StringBrushProperty,
            TagBrushProperty,
            SliderBrushProperty,
            SliderPointerOverBrushProperty,
            SliderActiveBrushProperty,
            SectionHeaderForegroundProperty,
            SectionHeaderSeparatorBrushProperty);
    }

    public TextEditorMinimap()
    {
        Decorations.CollectionChanged += DecorationsOnCollectionChanged;

        _scrollRevealTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(900) };
        _scrollRevealTimer.Tick += ScrollRevealTimerOnTick;
    }

    public IBrush? Background
    {
        get => GetValue(BackgroundProperty);
        set => SetValue(BackgroundProperty, value);
    }

    public TextEditor? Editor
    {
        get => GetValue(EditorProperty);
        set => SetValue(EditorProperty, value);
    }

    public bool MinimapEnabled
    {
        get => GetValue(MinimapEnabledProperty);
        set => SetValue(MinimapEnabledProperty, value);
    }

    public TextMinimapAutoHide AutoHide
    {
        get => GetValue(AutoHideProperty);
        set => SetValue(AutoHideProperty, value);
    }

    public TextMinimapSize SizeMode
    {
        get => GetValue(SizeModeProperty);
        set => SetValue(SizeModeProperty, value);
    }

    public TextMinimapSliderVisibility ShowSlider
    {
        get => GetValue(ShowSliderProperty);
        set => SetValue(ShowSliderProperty, value);
    }

    public bool RenderCharacters
    {
        get => GetValue(RenderCharactersProperty);
        set => SetValue(RenderCharactersProperty, value);
    }

    public int MaxColumn
    {
        get => GetValue(MaxColumnProperty);
        set => SetValue(MaxColumnProperty, value);
    }

    public int Scale
    {
        get => GetValue(ScaleProperty);
        set => SetValue(ScaleProperty, value);
    }

    public bool ShowRegionSectionHeaders
    {
        get => GetValue(ShowRegionSectionHeadersProperty);
        set => SetValue(ShowRegionSectionHeadersProperty, value);
    }

    public bool ShowMarkSectionHeaders
    {
        get => GetValue(ShowMarkSectionHeadersProperty);
        set => SetValue(ShowMarkSectionHeadersProperty, value);
    }

    public string MarkSectionHeaderRegex
    {
        get => GetValue(MarkSectionHeaderRegexProperty);
        set => SetValue(MarkSectionHeaderRegexProperty, value);
    }

    public double SectionHeaderFontSize
    {
        get => GetValue(SectionHeaderFontSizeProperty);
        set => SetValue(SectionHeaderFontSizeProperty, value);
    }

    public double SectionHeaderLetterSpacing
    {
        get => GetValue(SectionHeaderLetterSpacingProperty);
        set => SetValue(SectionHeaderLetterSpacingProperty, value);
    }

    public string? Language
    {
        get => GetValue(LanguageProperty);
        set => SetValue(LanguageProperty, value);
    }

    public IBrush? TextBrush
    {
        get => GetValue(TextBrushProperty);
        set => SetValue(TextBrushProperty, value);
    }

    public IBrush? CommentBrush
    {
        get => GetValue(CommentBrushProperty);
        set => SetValue(CommentBrushProperty, value);
    }

    public IBrush? KeywordBrush
    {
        get => GetValue(KeywordBrushProperty);
        set => SetValue(KeywordBrushProperty, value);
    }

    public IBrush? StringBrush
    {
        get => GetValue(StringBrushProperty);
        set => SetValue(StringBrushProperty, value);
    }

    public IBrush? TagBrush
    {
        get => GetValue(TagBrushProperty);
        set => SetValue(TagBrushProperty, value);
    }

    public IBrush? SliderBrush
    {
        get => GetValue(SliderBrushProperty);
        set => SetValue(SliderBrushProperty, value);
    }

    public IBrush? SliderPointerOverBrush
    {
        get => GetValue(SliderPointerOverBrushProperty);
        set => SetValue(SliderPointerOverBrushProperty, value);
    }

    public IBrush? SliderActiveBrush
    {
        get => GetValue(SliderActiveBrushProperty);
        set => SetValue(SliderActiveBrushProperty, value);
    }

    public IBrush? SectionHeaderForeground
    {
        get => GetValue(SectionHeaderForegroundProperty);
        set => SetValue(SectionHeaderForegroundProperty, value);
    }

    public IBrush? SectionHeaderSeparatorBrush
    {
        get => GetValue(SectionHeaderSeparatorBrushProperty);
        set => SetValue(SectionHeaderSeparatorBrushProperty, value);
    }

    public ObservableCollection<TextMinimapDecoration> Decorations { get; } = [];

    protected override Size MeasureOverride(Size availableSize)
    {
        return new Size(GetPreferredWidth(availableSize), 0);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == EditorProperty)
        {
            SubscribeToEditor(change.NewValue as TextEditor);
        }
        else if (change.Property == MarkSectionHeaderRegexProperty)
        {
            _markSectionHeaderRegex = null;
            _markSectionHeaderRegexPattern = null;
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _scrollRevealTimer.Stop();
        SubscribeToEditor(null);
        base.OnDetachedFromVisualTree(e);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        SubscribeToEditor(Editor);
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        var bounds = new Rect(Bounds.Size);
        if (Background is not null)
        {
            context.FillRectangle(Background, bounds);
        }

        if (!MinimapEnabled || !ShouldDrawContent())
        {
            return;
        }

        var editor = Editor;
        var document = _document;
        if (editor?.TextArea.TextView is null || document is null || document.LineCount == 0)
        {
            return;
        }

        var layout = CreateLayout(editor, document, Bounds.Size);
        if (layout.LineHeight <= 0 || Bounds.Width <= 0 || Bounds.Height <= 0)
        {
            return;
        }

        using (context.PushClip(bounds))
        {
            DrawDocument(context, document, layout);
            DrawDecorations(context, layout);
            DrawSlider(context, layout);
        }
    }

    protected override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e);
        _isPointerOver = true;
        InvalidateVisual();
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        _isPointerOver = false;
        InvalidateVisual();
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);

        if (!MinimapEnabled || Editor?.Document is null)
        {
            return;
        }

        _isDragging = true;
        e.Pointer.Capture(this);
        ScrollToPoint(e.GetPosition(this));
        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);

        if (_isDragging)
        {
            ScrollToPoint(e.GetPosition(this));
            e.Handled = true;
        }
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);

        if (!MinimapEnabled || Editor?.TextArea.TextView is not { } textView)
        {
            return;
        }

        var scrollable = (IScrollable)textView;
        var maxScroll = Math.Max(0, scrollable.Extent.Height - scrollable.Viewport.Height);
        if (maxScroll <= 0)
        {
            return;
        }

        var lineScroll = Math.Max(1, textView.DefaultLineHeight) * 3;
        var targetY = Math.Clamp(scrollable.Offset.Y - e.Delta.Y * lineScroll, 0, maxScroll);
        if (!targetY.Equals(scrollable.Offset.Y))
        {
            scrollable.Offset = new Vector(scrollable.Offset.X, targetY);
        }

        e.Handled = true;
        InvalidateVisual();
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);

        if (!_isDragging)
        {
            return;
        }

        _isDragging = false;
        e.Pointer.Capture(null);
        e.Handled = true;
        InvalidateVisual();
    }

    private void SubscribeToEditor(TextEditor? editor)
    {
        if (ReferenceEquals(_subscribedEditor, editor))
        {
            return;
        }

        if (_subscribedEditor is not null)
        {
            _subscribedEditor.DocumentChanged -= EditorOnDocumentChanged;
            _subscribedEditor.TextChanged -= EditorOnTextChanged;
            _subscribedEditor.PropertyChanged -= EditorOnPropertyChanged;
            _subscribedEditor.TextArea.TextView.ScrollOffsetChanged -= EditorOnScrollOffsetChanged;
            _subscribedEditor.TextArea.TextView.VisualLinesChanged -= EditorOnVisualLinesChanged;
            _subscribedEditor.TextArea.Caret.PositionChanged -= EditorOnCaretPositionChanged;
        }

        _subscribedEditor = editor;
        SubscribeToDocument(null);

        if (_subscribedEditor is not null)
        {
            _subscribedEditor.DocumentChanged += EditorOnDocumentChanged;
            _subscribedEditor.TextChanged += EditorOnTextChanged;
            _subscribedEditor.PropertyChanged += EditorOnPropertyChanged;
            _subscribedEditor.TextArea.TextView.ScrollOffsetChanged += EditorOnScrollOffsetChanged;
            _subscribedEditor.TextArea.TextView.VisualLinesChanged += EditorOnVisualLinesChanged;
            _subscribedEditor.TextArea.Caret.PositionChanged += EditorOnCaretPositionChanged;
            SubscribeToDocument(_subscribedEditor.Document);
        }

        InvalidateMeasure();
        InvalidateVisual();
    }

    private void SubscribeToDocument(TextDocument? document)
    {
        if (ReferenceEquals(_document, document))
        {
            return;
        }

        if (_document is not null)
        {
            _document.Changed -= DocumentOnChanged;
        }

        _document = document;

        if (_document is not null)
        {
            _document.Changed += DocumentOnChanged;
        }
    }

    private void EditorOnDocumentChanged(object? sender, DocumentChangedEventArgs e)
    {
        SubscribeToDocument(e.NewDocument);
        InvalidateMeasure();
        InvalidateVisual();
    }

    private void EditorOnTextChanged(object? sender, EventArgs e)
    {
        InvalidateMeasure();
        InvalidateVisual();
    }

    private void DocumentOnChanged(object? sender, DocumentChangeEventArgs e)
    {
        InvalidateMeasure();
        InvalidateVisual();
    }

    private void EditorOnPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property.Name is nameof(Bounds) or
            nameof(TextEditor.FontFamily) or
            nameof(TextEditor.FontSize) or
            nameof(TextEditor.FontWeight))
        {
            InvalidateMeasure();
            InvalidateVisual();
        }
    }

    private void EditorOnScrollOffsetChanged(object? sender, EventArgs e)
    {
        RevealForScroll();
        InvalidateVisual();
    }

    private void EditorOnVisualLinesChanged(object? sender, EventArgs e)
    {
        InvalidateVisual();
    }

    private void EditorOnCaretPositionChanged(object? sender, EventArgs e)
    {
        InvalidateVisual();
    }

    private void DecorationsOnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        InvalidateVisual();
    }

    private void RevealForScroll()
    {
        _isScrollRevealed = true;
        _scrollRevealTimer.Stop();
        _scrollRevealTimer.Start();
    }

    private void ScrollRevealTimerOnTick(object? sender, EventArgs e)
    {
        _scrollRevealTimer.Stop();
        _isScrollRevealed = false;
        InvalidateVisual();
    }

    private bool ShouldDrawContent()
    {
        return AutoHide switch
        {
            TextMinimapAutoHide.None => true,
            TextMinimapAutoHide.MouseOver => _isPointerOver || _isDragging,
            TextMinimapAutoHide.Scroll => _isScrollRevealed || _isPointerOver || _isDragging,
            _ => true
        };
    }

    private bool ShouldDrawSlider()
    {
        return ShowSlider == TextMinimapSliderVisibility.Always ||
               _isPointerOver ||
               _isDragging;
    }

    private double GetPreferredWidth(Size availableSize)
    {
        if (!MinimapEnabled)
        {
            return 0;
        }

        var scale = Math.Clamp(Scale, 1, 3);
        var maxColumn = Math.Clamp(MaxColumn, 1, 10000);
        var minimapCharWidth = (double)scale;
        var minimapMaxWidth = Math.Floor(maxColumn * minimapCharWidth);
        var editorWidth = GetAvailableEditorWidth(availableSize);
        var textCharWidth = GetEditorCharacterWidth();
        var scrollbarWidth = GetVerticalScrollbarWidth();
        var proportionalWidth = Math.Floor(
            Math.Max(0, (editorWidth - scrollbarWidth - MinimapWidthPadding) * minimapCharWidth) /
            (textCharWidth + minimapCharWidth));
        var width = Math.Min(minimapMaxWidth, Math.Max(0, proportionalWidth) + MinimapGutterWidth);

        return Math.Clamp(width, Math.Min(MinimapGutterWidth, minimapMaxWidth), minimapMaxWidth);
    }

    private double GetAvailableEditorWidth(Size availableSize)
    {
        if (double.IsFinite(availableSize.Width) && availableSize.Width > 0)
        {
            return availableSize.Width;
        }

        if (Editor?.Bounds.Width is > 0 and var editorWidth)
        {
            return editorWidth;
        }

        if (Parent is Visual parent && parent.Bounds.Width > 0)
        {
            return parent.Bounds.Width;
        }

        return 800;
    }

    private double GetEditorCharacterWidth()
    {
        if (Editor?.TextArea.TextView is { } textView)
        {
            return Math.Max(1, textView.WideSpaceWidth);
        }

        return 8;
    }

    private double GetVerticalScrollbarWidth()
    {
        if (Editor is not { } editor || editor.ExtentHeight <= editor.ViewportHeight)
        {
            return 0;
        }

        return 12;
    }

    private MinimapLayout CreateLayout(TextEditor editor, TextDocument document, Size size)
    {
        var metrics = CreateMetrics(editor, document, size);
        return metrics.HeightIsEditorHeight
            ? CreateContainedLayout(metrics)
            : CreateProportionalLayout(metrics);
    }

    private MinimapMetrics CreateMetrics(TextEditor editor, TextDocument document, Size size)
    {
        var textView = editor.TextArea.TextView;
        var scrollable = (IScrollable)textView;
        var editorLineHeight = Math.Max(1, textView.DefaultLineHeight);
        var minimapHeight = Math.Max(1, size.Height);
        var viewportHeight = Math.Max(1, scrollable.Viewport.Height);
        var scrollHeight = Math.Max(viewportHeight, scrollable.Extent.Height);
        var scrollTop = Math.Clamp(scrollable.Offset.Y, 0, Math.Max(0, scrollHeight - viewportHeight));
        var lineCount = Math.Max(1, document.LineCount);
        var foldedLineMap = CreateFoldedLineMap(editor, document);
        var visibleLineCount = foldedLineMap.VisibleLineCount;
        var documentHeight = Math.Max(editorLineHeight, textView.DocumentHeight);
        var viewportStartLine = GetDocumentLineByVisualTop(textView, scrollTop, lineCount);
        var viewportEndLine = GetDocumentLineByVisualTop(textView, scrollTop + viewportHeight, lineCount);
        var viewportStartTop = GetVisualTopByDocumentLine(textView, viewportStartLine);
        var extraLinesAtBottom = Math.Max(0, (scrollHeight - documentHeight) / editorLineHeight);
        var lineHeight = GetMinimapLineHeight(visibleLineCount, minimapHeight, editorLineHeight, extraLinesAtBottom, out var heightIsEditorHeight);

        return new MinimapMetrics(
            LineCount: lineCount,
            VisibleLineCount: visibleLineCount,
            FoldedLineMap: foldedLineMap,
            EditorLineHeight: editorLineHeight,
            MinimapLineHeight: lineHeight,
            MinimapHeight: minimapHeight,
            ViewportHeight: viewportHeight,
            ScrollHeight: scrollHeight,
            ScrollTop: scrollTop,
            ViewportStartLineNumber: viewportStartLine,
            ViewportEndLineNumber: viewportEndLine,
            ViewportStartVisibleLineIndex: foldedLineMap.GetVisibleIndexOrNearest(viewportStartLine),
            ViewportEndVisibleLineIndex: foldedLineMap.GetVisibleIndexOrNearest(viewportEndLine),
            ViewportStartLineTop: viewportStartTop,
            ExtraLinesAtBottom: extraLinesAtBottom,
            HeightIsEditorHeight: heightIsEditorHeight);
    }

    private double GetMinimapLineHeight(
        int lineCount,
        double minimapHeight,
        double editorLineHeight,
        double extraLinesAtBottom,
        out bool heightIsEditorHeight)
    {
        var scale = Math.Clamp(Scale, 1, 3);
        var naturalLineHeight = (RenderCharacters ? 2.0 : 3.0) * scale;
        heightIsEditorHeight = false;

        if (SizeMode == TextMinimapSize.Proportional)
        {
            return naturalLineHeight;
        }

        var containedLineCount = lineCount + extraLinesAtBottom;
        var desiredRatio = containedLineCount / minimapHeight;
        var minimapLineCount = desiredRatio <= 0 ? lineCount : Math.Floor(lineCount / desiredRatio);
        var needsSampling = minimapLineCount > 0 && lineCount / minimapLineCount > 1;
        if (needsSampling)
        {
            heightIsEditorHeight = true;
            return 1;
        }

        var effectiveHeight = containedLineCount * naturalLineHeight;
        var fitBecomesFill = SizeMode == TextMinimapSize.Fit && effectiveHeight > minimapHeight;
        if (SizeMode == TextMinimapSize.Fill || fitBecomesFill)
        {
            heightIsEditorHeight = true;
            return Math.Min(editorLineHeight, Math.Max(1, Math.Floor(1 / Math.Max(desiredRatio, 0.0001))));
        }

        return naturalLineHeight;
    }

    private MinimapLayout CreateContainedLayout(MinimapMetrics metrics)
    {
        var sliderHeight = Math.Clamp(
            Math.Floor(metrics.ViewportHeight * metrics.ViewportHeight / metrics.ScrollHeight),
            1,
            metrics.MinimapHeight);
        var maxSliderTop = Math.Max(0, metrics.MinimapHeight - sliderHeight);
        var sliderRatio = GetSliderRatio(maxSliderTop, metrics);
        var sliderTop = Math.Clamp(metrics.ScrollTop * sliderRatio, 0, maxSliderTop);
        var maxLinesFitting = Math.Max(1, (int)Math.Floor(metrics.MinimapHeight / metrics.MinimapLineHeight));
        var isSampling = maxLinesFitting < metrics.VisibleLineCount;
        var endVisibleLineIndex = isSampling
            ? metrics.VisibleLineCount - 1
            : Math.Min(metrics.VisibleLineCount - 1, maxLinesFitting - 1);

        return new MinimapLayout(
            metrics.MinimapLineHeight,
            metrics.FoldedLineMap.GetLineNumberAtVisibleIndex(0),
            metrics.FoldedLineMap.GetLineNumberAtVisibleIndex(endVisibleLineIndex),
            metrics.FoldedLineMap,
            0,
            endVisibleLineIndex,
            0,
            maxSliderTop > 0,
            sliderTop,
            sliderHeight,
            sliderRatio,
            isSampling,
            isSampling ? maxLinesFitting : 0);
    }

    private MinimapLayout CreateProportionalLayout(MinimapMetrics metrics)
    {
        var expectedViewportLineCount = metrics.ViewportHeight / metrics.EditorLineHeight;
        var sliderHeight = Math.Clamp(
            Math.Floor(expectedViewportLineCount * metrics.MinimapLineHeight),
            1,
            metrics.MinimapHeight);
        var maxSliderTop = metrics.ExtraLinesAtBottom > 0
            ? (metrics.VisibleLineCount + metrics.ExtraLinesAtBottom - expectedViewportLineCount - 1) * metrics.MinimapLineHeight
            : Math.Max(0, metrics.VisibleLineCount * metrics.MinimapLineHeight - sliderHeight);

        maxSliderTop = Math.Clamp(maxSliderTop, 0, Math.Max(0, metrics.MinimapHeight - sliderHeight));
        var sliderRatio = GetSliderRatio(maxSliderTop, metrics);
        var sliderTop = Math.Clamp(metrics.ScrollTop * sliderRatio, 0, maxSliderTop);
        var minimapLinesFitting = Math.Max(1, (int)Math.Floor(metrics.MinimapHeight / metrics.MinimapLineHeight));

        if (minimapLinesFitting >= metrics.VisibleLineCount + metrics.ExtraLinesAtBottom)
        {
            return new MinimapLayout(
                metrics.MinimapLineHeight,
                metrics.FoldedLineMap.GetLineNumberAtVisibleIndex(0),
                metrics.FoldedLineMap.GetLineNumberAtVisibleIndex(metrics.VisibleLineCount - 1),
                metrics.FoldedLineMap,
                0,
                metrics.VisibleLineCount - 1,
                0,
                maxSliderTop > 0,
                sliderTop,
                sliderHeight,
                sliderRatio,
                false,
                0);
        }

        var consideringStartVisibleLineIndex = metrics.ViewportStartVisibleLineIndex >= 0
            ? metrics.ViewportStartVisibleLineIndex
            : Math.Max(0, metrics.ScrollTop / metrics.EditorLineHeight);
        var startVisibleLineIndex = Math.Clamp(
            (int)Math.Floor(consideringStartVisibleLineIndex - sliderTop / metrics.MinimapLineHeight),
            0,
            metrics.VisibleLineCount - 1);
        var endVisibleLineIndex = Math.Min(metrics.VisibleLineCount - 1, startVisibleLineIndex + minimapLinesFitting - 1);
        var partialLine = (metrics.ScrollTop - metrics.ViewportStartLineTop) / metrics.EditorLineHeight;
        var sliderTopAligned = Math.Clamp(
            (metrics.ViewportStartVisibleLineIndex - startVisibleLineIndex + partialLine) * metrics.MinimapLineHeight,
            0,
            Math.Max(0, metrics.MinimapHeight - sliderHeight));

        return new MinimapLayout(
            metrics.MinimapLineHeight,
            metrics.FoldedLineMap.GetLineNumberAtVisibleIndex(startVisibleLineIndex),
            metrics.FoldedLineMap.GetLineNumberAtVisibleIndex(endVisibleLineIndex),
            metrics.FoldedLineMap,
            startVisibleLineIndex,
            endVisibleLineIndex,
            0,
            true,
            sliderTopAligned,
            sliderHeight,
            sliderRatio,
            false,
            0);
    }

    private static double GetSliderRatio(double maxSliderTop, MinimapMetrics metrics)
    {
        var maxEditorScroll = metrics.ScrollHeight - metrics.ViewportHeight;
        return maxEditorScroll <= 0 ? 0 : maxSliderTop / maxEditorScroll;
    }

    private static int GetDocumentLineByVisualTop(TextView textView, double visualTop, int lineCount)
    {
        try
        {
            return Math.Clamp(textView.GetDocumentLineByVisualTop(Math.Max(0, visualTop)).LineNumber, 1, lineCount);
        }
        catch
        {
            var lineHeight = Math.Max(1, textView.DefaultLineHeight);
            return Math.Clamp((int)Math.Floor(visualTop / lineHeight) + 1, 1, lineCount);
        }
    }

    private static double GetVisualTopByDocumentLine(TextView textView, int lineNumber)
    {
        try
        {
            return textView.GetVisualTopByDocumentLine(lineNumber);
        }
        catch
        {
            return Math.Max(0, lineNumber - 1) * Math.Max(1, textView.DefaultLineHeight);
        }
    }

    private static FoldedLineMap CreateFoldedLineMap(TextEditor editor, TextDocument document)
    {
        var hiddenRanges = GetHiddenLineRanges(editor, document);
        return CreateFoldedLineMap(document.LineCount, hiddenRanges);
    }

    private static FoldedLineMap CreateFoldedLineMap(int lineCount, IEnumerable<HiddenLineRange> hiddenRanges)
    {
        var mergedRanges = MergeHiddenLineRanges(hiddenRanges, lineCount);
        var visibleLineNumbers = new List<int>(lineCount);
        var lineToVisibleIndex = Enumerable.Repeat(-1, lineCount + 1).ToArray();
        var rangeIndex = 0;

        for (var lineNumber = 1; lineNumber <= lineCount; lineNumber++)
        {
            while (rangeIndex < mergedRanges.Count && lineNumber > mergedRanges[rangeIndex].EndLineNumber)
            {
                rangeIndex++;
            }

            var isHidden = rangeIndex < mergedRanges.Count &&
                           lineNumber >= mergedRanges[rangeIndex].StartLineNumber &&
                           lineNumber <= mergedRanges[rangeIndex].EndLineNumber;
            if (isHidden)
            {
                continue;
            }

            lineToVisibleIndex[lineNumber] = visibleLineNumbers.Count;
            visibleLineNumbers.Add(lineNumber);
        }

        if (visibleLineNumbers.Count == 0)
        {
            visibleLineNumbers.Add(1);
            lineToVisibleIndex[1] = 0;
        }

        return new FoldedLineMap(visibleLineNumbers.ToArray(), lineToVisibleIndex);
    }

    private static IReadOnlyList<HiddenLineRange> GetHiddenLineRanges(TextEditor editor, TextDocument document)
    {
        var foldingManager = GetFoldingManager(editor);
        if (foldingManager is null)
        {
            return [];
        }

        var ranges = new List<HiddenLineRange>();
        foreach (var folding in foldingManager.AllFoldings)
        {
            if (!folding.IsFolded)
            {
                continue;
            }

            var startLine = document.GetLineByOffset(folding.StartOffset).LineNumber;
            var endOffset = Math.Max(folding.StartOffset, folding.EndOffset - 1);
            var endLine = document.GetLineByOffset(endOffset).LineNumber;
            var hiddenStartLine = Math.Min(document.LineCount, startLine + 1);
            var hiddenEndLine = Math.Min(document.LineCount, Math.Max(hiddenStartLine - 1, endLine));
            if (hiddenStartLine <= hiddenEndLine)
            {
                ranges.Add(new HiddenLineRange(hiddenStartLine, hiddenEndLine));
            }
        }

        return ranges;
    }

    private static FoldingManager? GetFoldingManager(TextEditor editor)
    {
        return editor.TextArea.TextView.ElementGenerators
            .OfType<FoldingElementGenerator>()
            .Select(static generator => generator.FoldingManager)
            .FirstOrDefault(manager => manager is not null);
    }

    private static List<HiddenLineRange> MergeHiddenLineRanges(IEnumerable<HiddenLineRange> ranges, int lineCount)
    {
        var orderedRanges = ranges
            .Select(range => new HiddenLineRange(
                Math.Clamp(range.StartLineNumber, 1, lineCount),
                Math.Clamp(range.EndLineNumber, 1, lineCount)))
            .Where(static range => range.StartLineNumber <= range.EndLineNumber)
            .OrderBy(static range => range.StartLineNumber)
            .ToList();
        if (orderedRanges.Count <= 1)
        {
            return orderedRanges;
        }

        var mergedRanges = new List<HiddenLineRange>();
        var current = orderedRanges[0];
        for (var i = 1; i < orderedRanges.Count; i++)
        {
            var next = orderedRanges[i];
            if (next.StartLineNumber <= current.EndLineNumber + 1)
            {
                current = new HiddenLineRange(current.StartLineNumber, Math.Max(current.EndLineNumber, next.EndLineNumber));
                continue;
            }

            mergedRanges.Add(current);
            current = next;
        }

        mergedRanges.Add(current);
        return mergedRanges;
    }

    private void DrawDocument(DrawingContext context, TextDocument document, MinimapLayout layout)
    {
        var width = Math.Max(0, Bounds.Width - 3);
        var maxColumn = Math.Clamp(MaxColumn, 1, 10000);
        if (layout.IsSampling)
        {
            DrawSampledDocument(context, document, layout, width, maxColumn);
            return;
        }

        var renderedLines = 0;

        for (var visibleLineIndex = layout.StartVisibleLineIndex; visibleLineIndex <= layout.EndVisibleLineIndex; visibleLineIndex++)
        {
            var lineNumber = layout.GetLineNumberAtVisibleIndex(visibleLineIndex);
            var y = layout.GetYForVisibleLineIndex(visibleLineIndex);
            if (y > Bounds.Height)
            {
                break;
            }

            if (y + layout.LineHeight < 0)
            {
                continue;
            }

            var line = document.GetLineByNumber(lineNumber);
            var text = document.GetText(line.Offset, Math.Min(line.Length, maxColumn));

            if (RenderCharacters && layout.LineHeight >= 1.5)
            {
                DrawLineText(context, text, y, width, layout.LineHeight);
            }
            else
            {
                DrawLineBlocks(context, text, y, width, layout.LineHeight);
            }

            if (TryGetSectionHeader(text, out var header))
            {
                DrawSectionHeader(context, header, y, width);
            }

            renderedLines++;
            if (renderedLines > 20000)
            {
                break;
            }
        }
    }

    private void DrawSampledDocument(
        DrawingContext context,
        TextDocument document,
        MinimapLayout layout,
        double width,
        int maxColumn)
    {
        var rows = Math.Min(layout.SampleRowCount, (int)Math.Ceiling(Bounds.Height / layout.LineHeight));
        for (var row = 0; row < rows; row++)
        {
            var lineNumber = layout.GetLineNumberForSampleRow(row);
            var y = row * layout.LineHeight;
            var line = document.GetLineByNumber(lineNumber);
            var text = document.GetText(line.Offset, Math.Min(line.Length, maxColumn));

            DrawLineBlocks(context, text, y, width, layout.LineHeight);

            if (TryGetSectionHeader(text, out var header))
            {
                DrawSectionHeader(context, header, y, width);
            }
        }
    }

    private void DrawLineText(DrawingContext context, string text, double y, double width, double lineHeight)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        var fontSize = Math.Clamp(lineHeight * 1.25, 1.5, 9);
        var brush = GetTextBrush(text);
        var formattedText = new FormattedText(
            text,
            CultureInfo.CurrentUICulture,
            FlowDirection.LeftToRight,
            new Typeface(FontFamily.Default, FontStyle.Normal, FontWeight.Normal),
            fontSize,
            brush)
        {
            MaxTextWidth = width,
            MaxLineCount = 1,
            Trimming = TextTrimming.None
        };

        context.DrawText(formattedText, new Point(2, y - Math.Max(0, (fontSize - lineHeight) / 2)));
    }

    private void DrawLineBlocks(DrawingContext context, string text, double y, double width, double lineHeight)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        var brush = GetTextBrush(text);
        var charWidth = Math.Max(0.5, width / Math.Max(1, Math.Clamp(MaxColumn, 1, 10000)));
        var blockHeight = Math.Max(1, Math.Min(lineHeight, 2.5));
        var index = 0;

        while (index < text.Length)
        {
            while (index < text.Length && char.IsWhiteSpace(text[index]))
            {
                index++;
            }

            var start = index;
            while (index < text.Length && !char.IsWhiteSpace(text[index]))
            {
                index++;
            }

            if (index <= start)
            {
                break;
            }

            var x = 2 + start * charWidth;
            var blockWidth = Math.Max(charWidth, (index - start) * charWidth);
            if (x < width)
            {
                context.FillRectangle(brush, new Rect(x, y, Math.Min(blockWidth, width - x), blockHeight));
            }
        }
    }

    private void DrawDecorations(DrawingContext context, MinimapLayout layout)
    {
        if (Decorations.Count == 0)
        {
            return;
        }

        var width = Bounds.Width;
        foreach (var decoration in Decorations)
        {
            if (decoration.LineNumber <= 0 || decoration.Brush is null)
            {
                continue;
            }

            var y = layout.GetY(decoration.LineNumber);
            if (double.IsNaN(y) || y > Bounds.Height || y + decoration.Thickness < 0)
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(decoration.SectionHeaderText))
            {
                DrawSectionHeader(
                    context,
                    new SectionHeader(decoration.SectionHeaderText, decoration.SectionHeaderStyle),
                    y,
                    width);
            }

            var rect = decoration.Placement == TextMinimapDecorationPlacement.Gutter
                ? new Rect(width - 3, y, 3, Math.Max(1, decoration.Thickness))
                : new Rect(0, y, width, Math.Max(1, decoration.Thickness));

            context.FillRectangle(decoration.Brush, rect);
        }
    }

    private void DrawSlider(DrawingContext context, MinimapLayout layout)
    {
        if (!ShouldDrawSlider() || !layout.SliderNeeded)
        {
            return;
        }

        var brush = GetSliderBrush();

        context.FillRectangle(brush, new Rect(0, layout.SliderTop, Bounds.Width, layout.SliderHeight));
    }

    private IBrush GetSliderBrush()
    {
        if (_isDragging)
        {
            return SliderActiveBrush ??
                   SliderPointerOverBrush ??
                   s_defaultSliderActiveBrush;
        }

        if (_isPointerOver)
        {
            return SliderPointerOverBrush ??
                   s_defaultSliderPointerOverBrush;
        }

        return SliderBrush ?? s_defaultSliderBrush;
    }

    private void DrawSectionHeader(DrawingContext context, SectionHeader header, double y, double width)
    {
        var text = header.Text.Trim();
        if (text.Length == 0)
        {
            return;
        }

        var brush = SectionHeaderForeground ?? s_defaultSectionHeaderBrush;
        var separatorBrush = SectionHeaderSeparatorBrush ?? brush;
        var fontSize = Math.Clamp(SectionHeaderFontSize, 4, 32);
        var letterSpacing = Math.Clamp(SectionHeaderLetterSpacing, 0, 5);
        var x = 2.0;

        foreach (var character in text)
        {
            var formattedText = new FormattedText(
                character.ToString(),
                CultureInfo.CurrentUICulture,
                FlowDirection.LeftToRight,
                new Typeface(FontFamily.Default, FontStyle.Normal, FontWeight.SemiBold),
                fontSize,
                brush);

            if (x >= width)
            {
                break;
            }

            context.DrawText(formattedText, new Point(x, y));
            x += formattedText.Width + letterSpacing;
        }

        if (header.Style == TextMinimapSectionHeaderStyle.Underlined)
        {
            var lineY = y + fontSize + 1;
            context.FillRectangle(separatorBrush, new Rect(2, lineY, Math.Max(0, width - 4), 1));
        }
    }

    private bool TryGetSectionHeader(string text, out SectionHeader header)
    {
        if (ShowRegionSectionHeaders && TryGetRegionSectionHeader(text, out header))
        {
            return true;
        }

        if (ShowMarkSectionHeaders && TryGetMarkSectionHeader(text, out header))
        {
            return true;
        }

        header = default;
        return false;
    }

    private bool TryGetRegionSectionHeader(string text, out SectionHeader header)
    {
        var trimmed = text.Trim();
        if (IsXmlLikeLanguage())
        {
            var match = Regex.Match(
                trimmed,
                @"^<!--\s*#?region\s+(?<label>.*?)\s*-->$",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            if (match.Success)
            {
                header = new SectionHeader(match.Groups["label"].Value, TextMinimapSectionHeaderStyle.Normal);
                return true;
            }
        }

        var region = Regex.Match(
            trimmed,
            @"^#\s*region\s+(?<label>.+)$",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (region.Success)
        {
            header = new SectionHeader(region.Groups["label"].Value, TextMinimapSectionHeaderStyle.Normal);
            return true;
        }

        header = default;
        return false;
    }

    private bool TryGetMarkSectionHeader(string text, out SectionHeader header)
    {
        var regex = GetMarkRegex();
        if (regex is null)
        {
            header = default;
            return false;
        }

        var normalized = IsXmlLikeLanguage()
            ? text.Trim().Replace("<!--", string.Empty, StringComparison.Ordinal).Replace("-->", string.Empty, StringComparison.Ordinal)
            : text;
        var match = regex.Match(normalized);
        if (!match.Success || match.Groups["label"] is not { Success: true } label)
        {
            header = default;
            return false;
        }

        var style = match.Groups["separator"] is { Success: true } separator &&
                    separator.Value.Contains('-', StringComparison.Ordinal)
            ? TextMinimapSectionHeaderStyle.Underlined
            : TextMinimapSectionHeaderStyle.Normal;

        header = new SectionHeader(label.Value, style);
        return true;
    }

    private Regex? GetMarkRegex()
    {
        var pattern = MarkSectionHeaderRegex;
        if (string.Equals(pattern, _markSectionHeaderRegexPattern, StringComparison.Ordinal))
        {
            return _markSectionHeaderRegex;
        }

        _markSectionHeaderRegexPattern = pattern;
        try
        {
            _markSectionHeaderRegex = new Regex(pattern, RegexOptions.CultureInvariant);
        }
        catch
        {
            _markSectionHeaderRegex = null;
        }

        return _markSectionHeaderRegex;
    }

    private void ScrollToPoint(Point point)
    {
        if (Editor is not { } editor || _document is null)
        {
            return;
        }

        var scrollable = (IScrollable)editor.TextArea.TextView;
        var maxScroll = Math.Max(0, scrollable.Extent.Height - scrollable.Viewport.Height);
        if (maxScroll <= 0)
        {
            return;
        }

        var layout = CreateLayout(editor, _document, Bounds.Size);
        var targetY = layout.SliderRatio <= 0
            ? 0
            : Math.Clamp((point.Y - layout.SliderHeight / 2) / layout.SliderRatio, 0, maxScroll);

        scrollable.Offset = new Vector(scrollable.Offset.X, targetY);

        InvalidateVisual();
    }

    private IBrush GetTextBrush(string text)
    {
        var trimmed = text.TrimStart();
        if (trimmed.Length == 0)
        {
            return TextBrush ?? s_defaultTextBrush;
        }

        if (IsComment(trimmed))
        {
            return CommentBrush ?? s_defaultCommentBrush;
        }

        if (IsXmlLikeLanguage() && trimmed.StartsWith('<'))
        {
            return TagBrush ?? s_defaultTagBrush;
        }

        if (IsStringDominated(trimmed))
        {
            return StringBrush ?? s_defaultStringBrush;
        }

        if (IsKeywordDominated(trimmed))
        {
            return KeywordBrush ?? s_defaultKeywordBrush;
        }

        return TextBrush ?? s_defaultTextBrush;
    }

    private bool IsComment(string trimmed)
    {
        return trimmed.StartsWith("//", StringComparison.Ordinal) ||
               trimmed.StartsWith("/*", StringComparison.Ordinal) ||
               trimmed.StartsWith("*", StringComparison.Ordinal) ||
               trimmed.StartsWith("<!--", StringComparison.Ordinal);
    }

    private bool IsStringDominated(string trimmed)
    {
        var firstQuote = trimmed.IndexOf('"');
        var firstApostrophe = trimmed.IndexOf('\'');
        var first = firstQuote < 0 ? firstApostrophe : firstApostrophe < 0 ? firstQuote : Math.Min(firstQuote, firstApostrophe);
        return first >= 0 && first <= 4;
    }

    private bool IsKeywordDominated(string trimmed)
    {
        if (IsXmlLikeLanguage())
        {
            return false;
        }

        var firstWord = new string(trimmed.TakeWhile(static character => char.IsLetter(character) || character == '_').ToArray());
        return firstWord is "using" or "namespace" or "public" or "private" or "protected" or "internal" or
            "class" or "interface" or "record" or "struct" or "enum" or "static" or "void" or
            "return" or "if" or "else" or "for" or "foreach" or "while" or "switch" or "case" or
            "try" or "catch" or "finally" or "new" or "var" or "const" or "readonly";
    }

    private bool IsXmlLikeLanguage()
    {
        var language = Language;
        return string.Equals(language, "xaml", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(language, "xml", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(language, ".xaml", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(language, ".axaml", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(language, ".xml", StringComparison.OrdinalIgnoreCase);
    }

    private readonly record struct MinimapMetrics(
        int LineCount,
        int VisibleLineCount,
        FoldedLineMap FoldedLineMap,
        double EditorLineHeight,
        double MinimapLineHeight,
        double MinimapHeight,
        double ViewportHeight,
        double ScrollHeight,
        double ScrollTop,
        int ViewportStartLineNumber,
        int ViewportEndLineNumber,
        int ViewportStartVisibleLineIndex,
        int ViewportEndVisibleLineIndex,
        double ViewportStartLineTop,
        double ExtraLinesAtBottom,
        bool HeightIsEditorHeight);

    private readonly record struct MinimapLayout(
        double LineHeight,
        int StartLineNumber,
        int EndLineNumber,
        FoldedLineMap FoldedLineMap,
        int StartVisibleLineIndex,
        int EndVisibleLineIndex,
        int TopPaddingLineCount,
        bool SliderNeeded,
        double SliderTop,
        double SliderHeight,
        double SliderRatio,
        bool IsSampling,
        int SampleRowCount)
    {
        public double GetY(int lineNumber)
        {
            var visibleLineIndex = FoldedLineMap.GetVisibleIndex(lineNumber);
            if (visibleLineIndex < 0)
            {
                return double.NaN;
            }

            return GetYForVisibleLineIndex(visibleLineIndex);
        }

        public double GetYForVisibleLineIndex(int visibleLineIndex)
        {
            if (IsSampling)
            {
                var rowCount = Math.Max(1, SampleRowCount);
                var visibleLineCount = Math.Max(1, EndVisibleLineIndex - StartVisibleLineIndex + 1);
                return Math.Floor((visibleLineIndex - StartVisibleLineIndex) * rowCount / (double)visibleLineCount) * LineHeight;
            }

            return (visibleLineIndex - StartVisibleLineIndex + TopPaddingLineCount) * LineHeight;
        }

        public int GetLineNumberAtVisibleIndex(int visibleLineIndex)
        {
            return FoldedLineMap.GetLineNumberAtVisibleIndex(visibleLineIndex);
        }

        public int GetLineNumberForSampleRow(int row)
        {
            var rowCount = Math.Max(1, SampleRowCount);
            var visibleLineCount = Math.Max(1, EndVisibleLineIndex - StartVisibleLineIndex + 1);
            if (rowCount == 1 || visibleLineCount == 1)
            {
                return FoldedLineMap.GetLineNumberAtVisibleIndex(StartVisibleLineIndex);
            }

            var visibleLineOffset = (int)Math.Round(row * (visibleLineCount - 1) / (double)(rowCount - 1));
            return FoldedLineMap.GetLineNumberAtVisibleIndex(
                Math.Clamp(StartVisibleLineIndex + visibleLineOffset, StartVisibleLineIndex, EndVisibleLineIndex));
        }
    }

    private sealed class FoldedLineMap
    {
        private readonly int[] _visibleLineNumbers;
        private readonly int[] _lineToVisibleIndex;

        public FoldedLineMap(int[] visibleLineNumbers, int[] lineToVisibleIndex)
        {
            _visibleLineNumbers = visibleLineNumbers;
            _lineToVisibleIndex = lineToVisibleIndex;
        }

        public int VisibleLineCount => _visibleLineNumbers.Length;

        public int GetVisibleIndex(int lineNumber)
        {
            return lineNumber >= 0 && lineNumber < _lineToVisibleIndex.Length
                ? _lineToVisibleIndex[lineNumber]
                : -1;
        }

        public int GetVisibleIndexOrNearest(int lineNumber)
        {
            var visibleIndex = GetVisibleIndex(lineNumber);
            if (visibleIndex >= 0)
            {
                return visibleIndex;
            }

            var nearestLine = Math.Clamp(lineNumber, 1, _lineToVisibleIndex.Length - 1);
            for (var line = nearestLine; line >= 1; line--)
            {
                visibleIndex = GetVisibleIndex(line);
                if (visibleIndex >= 0)
                {
                    return visibleIndex;
                }
            }

            return 0;
        }

        public int GetLineNumberAtVisibleIndex(int visibleLineIndex)
        {
            return _visibleLineNumbers[Math.Clamp(visibleLineIndex, 0, _visibleLineNumbers.Length - 1)];
        }
    }

    private readonly record struct HiddenLineRange(int StartLineNumber, int EndLineNumber);

    private readonly record struct SectionHeader(string Text, TextMinimapSectionHeaderStyle Style);
}
