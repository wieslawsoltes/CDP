using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using AvaloniaEdit;
using XamlPlayground.Editor.Minimap.Inline;

namespace XamlPlayground.Editor.Minimap;

public class MinimapTextEditor : TextEditor
{
    public static readonly StyledProperty<bool> MinimapEnabledProperty =
        AvaloniaProperty.Register<MinimapTextEditor, bool>(nameof(MinimapEnabled), true);

    public static readonly StyledProperty<TextMinimapAutoHide> MinimapAutoHideProperty =
        AvaloniaProperty.Register<MinimapTextEditor, TextMinimapAutoHide>(nameof(MinimapAutoHide));

    public static readonly StyledProperty<TextMinimapSide> MinimapSideProperty =
        AvaloniaProperty.Register<MinimapTextEditor, TextMinimapSide>(nameof(MinimapSide));

    public static readonly StyledProperty<TextMinimapSize> MinimapSizeProperty =
        AvaloniaProperty.Register<MinimapTextEditor, TextMinimapSize>(nameof(MinimapSize));

    public static readonly StyledProperty<TextMinimapSliderVisibility> MinimapShowSliderProperty =
        AvaloniaProperty.Register<MinimapTextEditor, TextMinimapSliderVisibility>(
            nameof(MinimapShowSlider),
            TextMinimapSliderVisibility.MouseOver);

    public static readonly StyledProperty<bool> MinimapRenderCharactersProperty =
        AvaloniaProperty.Register<MinimapTextEditor, bool>(nameof(MinimapRenderCharacters), true);

    public static readonly StyledProperty<int> MinimapMaxColumnProperty =
        AvaloniaProperty.Register<MinimapTextEditor, int>(nameof(MinimapMaxColumn), 120);

    public static readonly StyledProperty<int> MinimapScaleProperty =
        AvaloniaProperty.Register<MinimapTextEditor, int>(nameof(MinimapScale), 1);

    public static readonly StyledProperty<bool> MinimapShowRegionSectionHeadersProperty =
        AvaloniaProperty.Register<MinimapTextEditor, bool>(nameof(MinimapShowRegionSectionHeaders), true);

    public static readonly StyledProperty<bool> MinimapShowMarkSectionHeadersProperty =
        AvaloniaProperty.Register<MinimapTextEditor, bool>(nameof(MinimapShowMarkSectionHeaders), true);

    public static readonly StyledProperty<string> MinimapMarkSectionHeaderRegexProperty =
        AvaloniaProperty.Register<MinimapTextEditor, string>(
            nameof(MinimapMarkSectionHeaderRegex),
            @"\bMARK:\s*(?<separator>-?)\s*(?<label>.*)$");

    public static readonly StyledProperty<double> MinimapSectionHeaderFontSizeProperty =
        AvaloniaProperty.Register<MinimapTextEditor, double>(nameof(MinimapSectionHeaderFontSize), 9);

    public static readonly StyledProperty<double> MinimapSectionHeaderLetterSpacingProperty =
        AvaloniaProperty.Register<MinimapTextEditor, double>(nameof(MinimapSectionHeaderLetterSpacing), 1);

    public static readonly StyledProperty<string?> MinimapLanguageProperty =
        AvaloniaProperty.Register<MinimapTextEditor, string?>(nameof(MinimapLanguage));

    public static readonly StyledProperty<IBrush?> MinimapBackgroundProperty =
        AvaloniaProperty.Register<MinimapTextEditor, IBrush?>(nameof(MinimapBackground));

    public static readonly StyledProperty<IBrush?> MinimapForegroundProperty =
        AvaloniaProperty.Register<MinimapTextEditor, IBrush?>(nameof(MinimapForeground));

    public static readonly StyledProperty<IBrush?> MinimapCommentBrushProperty =
        AvaloniaProperty.Register<MinimapTextEditor, IBrush?>(nameof(MinimapCommentBrush));

    public static readonly StyledProperty<IBrush?> MinimapKeywordBrushProperty =
        AvaloniaProperty.Register<MinimapTextEditor, IBrush?>(nameof(MinimapKeywordBrush));

    public static readonly StyledProperty<IBrush?> MinimapStringBrushProperty =
        AvaloniaProperty.Register<MinimapTextEditor, IBrush?>(nameof(MinimapStringBrush));

    public static readonly StyledProperty<IBrush?> MinimapTagBrushProperty =
        AvaloniaProperty.Register<MinimapTextEditor, IBrush?>(nameof(MinimapTagBrush));

    public static readonly StyledProperty<IBrush?> MinimapSliderBrushProperty =
        AvaloniaProperty.Register<MinimapTextEditor, IBrush?>(nameof(MinimapSliderBrush));

    public static readonly StyledProperty<IBrush?> MinimapSliderPointerOverBrushProperty =
        AvaloniaProperty.Register<MinimapTextEditor, IBrush?>(nameof(MinimapSliderPointerOverBrush));

    public static readonly StyledProperty<IBrush?> MinimapSliderActiveBrushProperty =
        AvaloniaProperty.Register<MinimapTextEditor, IBrush?>(nameof(MinimapSliderActiveBrush));

    public static readonly StyledProperty<IBrush?> MinimapSectionHeaderForegroundProperty =
        AvaloniaProperty.Register<MinimapTextEditor, IBrush?>(nameof(MinimapSectionHeaderForeground));

    public static readonly StyledProperty<IBrush?> MinimapSectionHeaderSeparatorBrushProperty =
        AvaloniaProperty.Register<MinimapTextEditor, IBrush?>(nameof(MinimapSectionHeaderSeparatorBrush));

    public static readonly StyledProperty<bool> InlineFeaturesEnabledProperty =
        AvaloniaProperty.Register<MinimapTextEditor, bool>(nameof(InlineFeaturesEnabled), true);

    private readonly EditorInlineFeatureHost _inlineFeatureHost;
    private TextEditorMinimap? _leftMinimap;
    private TextEditorMinimap? _rightMinimap;
    private Panel? _inlineOverlayLayer;

    public MinimapTextEditor()
    {
        _inlineFeatureHost = new EditorInlineFeatureHost(this);
        _inlineFeatureHost.Attach();
    }

    public bool MinimapEnabled
    {
        get => GetValue(MinimapEnabledProperty);
        set => SetValue(MinimapEnabledProperty, value);
    }

    public TextMinimapAutoHide MinimapAutoHide
    {
        get => GetValue(MinimapAutoHideProperty);
        set => SetValue(MinimapAutoHideProperty, value);
    }

    public TextMinimapSide MinimapSide
    {
        get => GetValue(MinimapSideProperty);
        set => SetValue(MinimapSideProperty, value);
    }

    public TextMinimapSize MinimapSize
    {
        get => GetValue(MinimapSizeProperty);
        set => SetValue(MinimapSizeProperty, value);
    }

    public TextMinimapSliderVisibility MinimapShowSlider
    {
        get => GetValue(MinimapShowSliderProperty);
        set => SetValue(MinimapShowSliderProperty, value);
    }

    public bool MinimapRenderCharacters
    {
        get => GetValue(MinimapRenderCharactersProperty);
        set => SetValue(MinimapRenderCharactersProperty, value);
    }

    public int MinimapMaxColumn
    {
        get => GetValue(MinimapMaxColumnProperty);
        set => SetValue(MinimapMaxColumnProperty, value);
    }

    public int MinimapScale
    {
        get => GetValue(MinimapScaleProperty);
        set => SetValue(MinimapScaleProperty, value);
    }

    public bool MinimapShowRegionSectionHeaders
    {
        get => GetValue(MinimapShowRegionSectionHeadersProperty);
        set => SetValue(MinimapShowRegionSectionHeadersProperty, value);
    }

    public bool MinimapShowMarkSectionHeaders
    {
        get => GetValue(MinimapShowMarkSectionHeadersProperty);
        set => SetValue(MinimapShowMarkSectionHeadersProperty, value);
    }

    public string MinimapMarkSectionHeaderRegex
    {
        get => GetValue(MinimapMarkSectionHeaderRegexProperty);
        set => SetValue(MinimapMarkSectionHeaderRegexProperty, value);
    }

    public double MinimapSectionHeaderFontSize
    {
        get => GetValue(MinimapSectionHeaderFontSizeProperty);
        set => SetValue(MinimapSectionHeaderFontSizeProperty, value);
    }

    public double MinimapSectionHeaderLetterSpacing
    {
        get => GetValue(MinimapSectionHeaderLetterSpacingProperty);
        set => SetValue(MinimapSectionHeaderLetterSpacingProperty, value);
    }

    public string? MinimapLanguage
    {
        get => GetValue(MinimapLanguageProperty);
        set => SetValue(MinimapLanguageProperty, value);
    }

    public IBrush? MinimapBackground
    {
        get => GetValue(MinimapBackgroundProperty);
        set => SetValue(MinimapBackgroundProperty, value);
    }

    public IBrush? MinimapForeground
    {
        get => GetValue(MinimapForegroundProperty);
        set => SetValue(MinimapForegroundProperty, value);
    }

    public IBrush? MinimapCommentBrush
    {
        get => GetValue(MinimapCommentBrushProperty);
        set => SetValue(MinimapCommentBrushProperty, value);
    }

    public IBrush? MinimapKeywordBrush
    {
        get => GetValue(MinimapKeywordBrushProperty);
        set => SetValue(MinimapKeywordBrushProperty, value);
    }

    public IBrush? MinimapStringBrush
    {
        get => GetValue(MinimapStringBrushProperty);
        set => SetValue(MinimapStringBrushProperty, value);
    }

    public IBrush? MinimapTagBrush
    {
        get => GetValue(MinimapTagBrushProperty);
        set => SetValue(MinimapTagBrushProperty, value);
    }

    public IBrush? MinimapSliderBrush
    {
        get => GetValue(MinimapSliderBrushProperty);
        set => SetValue(MinimapSliderBrushProperty, value);
    }

    public IBrush? MinimapSliderPointerOverBrush
    {
        get => GetValue(MinimapSliderPointerOverBrushProperty);
        set => SetValue(MinimapSliderPointerOverBrushProperty, value);
    }

    public IBrush? MinimapSliderActiveBrush
    {
        get => GetValue(MinimapSliderActiveBrushProperty);
        set => SetValue(MinimapSliderActiveBrushProperty, value);
    }

    public IBrush? MinimapSectionHeaderForeground
    {
        get => GetValue(MinimapSectionHeaderForegroundProperty);
        set => SetValue(MinimapSectionHeaderForegroundProperty, value);
    }

    public IBrush? MinimapSectionHeaderSeparatorBrush
    {
        get => GetValue(MinimapSectionHeaderSeparatorBrushProperty);
        set => SetValue(MinimapSectionHeaderSeparatorBrushProperty, value);
    }

    public bool InlineFeaturesEnabled
    {
        get => GetValue(InlineFeaturesEnabledProperty);
        set => SetValue(InlineFeaturesEnabledProperty, value);
    }

    public IList<EditorViewZone> InlineViewZones => _inlineFeatureHost.ViewZones;

    public IList<EditorInlineControl> InlineControls => _inlineFeatureHost.InlineControls;

    public IList<EditorCodeAnnotation> InlineAnnotations => _inlineFeatureHost.Annotations;

    public IList<IEditorInlineExtension> InlineExtensions => _inlineFeatureHost.Extensions;

    public EditorViewZone ShowInlinePeek(
        int lineNumber,
        string title,
        string? subtitle,
        string text,
        string? language = null,
        double height = 240)
    {
        return _inlineFeatureHost.ShowPeek(lineNumber, title, subtitle, text, language, height);
    }

    public void CloseInlinePeek()
    {
        _inlineFeatureHost.ClosePeek();
    }

    public EditorViewZone AddInlineViewZone(
        int lineNumber,
        EditorInlinePlacement placement,
        double height,
        Control content,
        EditorInlineZoneKind kind = EditorInlineZoneKind.Custom)
    {
        var zone = new EditorViewZone
        {
            LineNumber = lineNumber,
            Placement = placement,
            Height = height,
            Kind = kind,
            Content = content
        };

        InlineViewZones.Add(zone);
        return zone;
    }

    public EditorInlineControl AddInlineControl(int offset, Control control)
    {
        var inlineControl = new EditorInlineControl
        {
            Offset = offset,
            Control = control
        };

        InlineControls.Add(inlineControl);
        return inlineControl;
    }

    public EditorCodeAnnotation AddInlineAnnotation(
        int lineNumber,
        string text,
        double priority = 0)
    {
        var annotation = new EditorCodeAnnotation
        {
            LineNumber = lineNumber,
            Text = text,
            Priority = priority
        };

        InlineAnnotations.Add(annotation);
        return annotation;
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        if (_leftMinimap is not null)
        {
            _leftMinimap.Editor = null;
        }

        if (_rightMinimap is not null)
        {
            _rightMinimap.Editor = null;
        }

        _inlineFeatureHost.SetOverlayLayer(null);

        base.OnApplyTemplate(e);

        _leftMinimap = e.NameScope.Find<TextEditorMinimap>("PART_LeftMinimap");
        _rightMinimap = e.NameScope.Find<TextEditorMinimap>("PART_RightMinimap");
        _inlineOverlayLayer = e.NameScope.Find<Panel>("PART_InlineOverlayLayer");

        if (_leftMinimap is not null)
        {
            _leftMinimap.Editor = this;
        }

        if (_rightMinimap is not null)
        {
            _rightMinimap.Editor = this;
        }

        _inlineFeatureHost.SetOverlayLayer(_inlineOverlayLayer);
        UpdateMinimapSide();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == MinimapSideProperty ||
            change.Property == MinimapEnabledProperty)
        {
            UpdateMinimapSide();
        }

        if (change.Property == InlineFeaturesEnabledProperty)
        {
            if (InlineFeaturesEnabled)
            {
                _inlineFeatureHost.Attach();
            }
            else
            {
                _inlineFeatureHost.Detach();
            }
        }
    }

    private void UpdateMinimapSide()
    {
        var showLeft = MinimapEnabled && MinimapSide == TextMinimapSide.Left;
        var showRight = MinimapEnabled && MinimapSide == TextMinimapSide.Right;

        if (_leftMinimap is not null)
        {
            _leftMinimap.IsVisible = showLeft;
        }

        if (_rightMinimap is not null)
        {
            _rightMinimap.IsVisible = showRight;
        }
    }
}
