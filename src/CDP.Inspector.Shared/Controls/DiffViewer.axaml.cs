using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using AvaloniaEdit;
using AvaloniaEdit.Document;
using AvaloniaEdit.Rendering;
using AvaloniaEdit.TextMate;
using TextMateSharp.Grammars;
using CdpInspectorApp.Services;
using CdpInspectorApp.ViewModels;

namespace CdpInspectorApp.Controls;

public partial class DiffViewer : UserControl
{
    private bool _isSyncingScroll;
    private DiffViewModel? _viewModel;

    private readonly RegistryOptions _registryOptions = new(ThemeName.DarkPlus);
    private TextMate.Installation? _leftTextMate;
    private TextMate.Installation? _rightTextMate;
    private TextMate.Installation? _inlineTextMate;

    public DiffViewer()
    {
        InitializeComponent();

        // Initialize TextMate highlighting for JSON
        try
        {
            _leftTextMate = LeftEditor.InstallTextMate(_registryOptions);
            _leftTextMate.SetGrammar(_registryOptions.GetScopeByLanguageId("json"));

            _rightTextMate = RightEditor.InstallTextMate(_registryOptions);
            _rightTextMate.SetGrammar(_registryOptions.GetScopeByLanguageId("json"));

            _inlineTextMate = InlineEditor.InstallTextMate(_registryOptions);
            _inlineTextMate.SetGrammar(_registryOptions.GetScopeByLanguageId("json"));
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[DiffViewer] Failed to initialize TextMate: {ex.Message}");
        }

        // Wire up synchronized scrolling for side-by-side mode
        LeftEditor.AddHandler(ScrollViewer.ScrollChangedEvent, OnLeftScrollChanged);
        RightEditor.AddHandler(ScrollViewer.ScrollChangedEvent, OnRightScrollChanged);
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        if (_viewModel != null)
        {
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }

        _viewModel = DataContext as DiffViewModel;

        if (_viewModel != null)
        {
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
            RefreshEditors();
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(DiffViewModel.DiffLines) ||
            e.PropertyName == nameof(DiffViewModel.IsInlineMode))
        {
            RefreshEditors();
        }
    }

    private void OnLeftScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        if (_isSyncingScroll || _viewModel == null || _viewModel.IsInlineMode) return;
        _isSyncingScroll = true;
        try
        {
            RightEditor.ScrollToVerticalOffset(LeftEditor.VerticalOffset);
            RightEditor.ScrollToHorizontalOffset(LeftEditor.HorizontalOffset);
        }
        finally
        {
            _isSyncingScroll = false;
        }
    }

    private void OnRightScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        if (_isSyncingScroll || _viewModel == null || _viewModel.IsInlineMode) return;
        _isSyncingScroll = true;
        try
        {
            LeftEditor.ScrollToVerticalOffset(RightEditor.VerticalOffset);
            LeftEditor.ScrollToHorizontalOffset(RightEditor.HorizontalOffset);
        }
        finally
        {
            _isSyncingScroll = false;
        }
    }

    private void RefreshEditors()
    {
        if (_viewModel == null) return;

        var diffLines = _viewModel.DiffLines ?? new List<DiffLine>();

        if (_viewModel.IsInlineMode)
        {
            var inlineBuilder = new StringBuilder();

            foreach (var line in diffLines)
            {
                if (line.Type == DiffType.Unchanged)
                {
                    inlineBuilder.AppendLine("  " + line.Text);
                }
                else if (line.Type == DiffType.Deleted)
                {
                    inlineBuilder.AppendLine("- " + line.Text);
                }
                else if (line.Type == DiffType.Added)
                {
                    inlineBuilder.AppendLine("+ " + line.Text);
                }
            }

            InlineEditor.Text = inlineBuilder.ToString();
            ApplyColorizer(InlineEditor, diffLines, isInlineMode: true);
        }
        else
        {
            var leftBuilder = new StringBuilder();
            var rightBuilder = new StringBuilder();

            // Construct Left and Right aligned texts
            foreach (var line in diffLines)
            {
                if (line.Type == DiffType.Unchanged)
                {
                    leftBuilder.AppendLine(line.Text);
                    rightBuilder.AppendLine(line.Text);
                }
                else if (line.Type == DiffType.Deleted)
                {
                    leftBuilder.AppendLine(line.Text);
                    rightBuilder.AppendLine("");
                }
                else if (line.Type == DiffType.Added)
                {
                    leftBuilder.AppendLine("");
                    rightBuilder.AppendLine(line.Text);
                }
            }

            LeftEditor.Text = leftBuilder.ToString();
            RightEditor.Text = rightBuilder.ToString();

            ApplyColorizer(LeftEditor, diffLines, isInlineMode: false);
            ApplyColorizer(RightEditor, diffLines, isInlineMode: false);
        }
    }

    private void ApplyColorizer(TextEditor editor, List<DiffLine> diffLines, bool isInlineMode)
    {
        var transformers = editor.TextArea.TextView.LineTransformers;
        for (int i = transformers.Count - 1; i >= 0; i--)
        {
            if (transformers[i] is DiffColorizingTransformer)
            {
                transformers.RemoveAt(i);
            }
        }

        transformers.Add(new DiffColorizingTransformer(diffLines, isInlineMode));
    }
}

public class DiffColorizingTransformer : DocumentColorizingTransformer
{
    private readonly List<DiffLine> _diffLines;
    private readonly bool _isInlineMode;

    private static readonly IBrush AddedBackground = new SolidColorBrush(Color.FromArgb(0x1a, 0x4c, 0xaf, 0x50));   // Green translucent (~10%)
    private static readonly IBrush DeletedBackground = new SolidColorBrush(Color.FromArgb(0x1a, 0xf4, 0x43, 0x36)); // Red translucent (~10%)

    private static readonly IBrush AddedWordBackground = new SolidColorBrush(Color.FromArgb(0x40, 0x4c, 0xaf, 0x50)); // Darker green translucent (~25%)
    private static readonly IBrush DeletedWordBackground = new SolidColorBrush(Color.FromArgb(0x40, 0xf4, 0x43, 0x36)); // Darker red translucent (~25%)

    public DiffColorizingTransformer(List<DiffLine> diffLines, bool isInlineMode)
    {
        _diffLines = diffLines;
        _isInlineMode = isInlineMode;
    }

    protected override void ColorizeLine(DocumentLine line)
    {
        int lineNum = line.LineNumber;
        if (lineNum <= 0 || lineNum > _diffLines.Count) return;

        var diffLine = _diffLines[lineNum - 1];

        IBrush? baseBrush = null;
        IBrush? wordBrush = null;

        // In side-by-side mode, LeftEditor transformer only colors Deleted, RightEditor only colors Added
        // In inline mode, we color both additions and deletions in the same editor
        if (diffLine.Type == DiffType.Added)
        {
            baseBrush = AddedBackground;
            wordBrush = AddedWordBackground;
        }
        else if (diffLine.Type == DiffType.Deleted)
        {
            baseBrush = DeletedBackground;
            wordBrush = DeletedWordBackground;
        }

        if (baseBrush != null)
        {
            // 1. Colorize the entire line background
            ChangeLinePart(
                line.Offset,
                line.EndOffset,
                element =>
                {
                    element.TextRunProperties.SetBackgroundBrush(baseBrush);
                });

            // 2. Colorize specific intra-line change ranges if available
            if (wordBrush != null && diffLine.ChangeRanges.Count > 0)
            {
                int offsetShift = _isInlineMode ? 2 : 0;
                foreach (var range in diffLine.ChangeRanges)
                {
                    int start = line.Offset + range.Offset + offsetShift;
                    int end = start + range.Length;

                    if (start >= line.Offset && end <= line.EndOffset && end > start)
                    {
                        ChangeLinePart(
                            start,
                            end,
                            element =>
                            {
                                element.TextRunProperties.SetBackgroundBrush(wordBrush);
                              // We can also adjust foreground to ensure readable contrast
                          });
                    }
                }
            }
        }
    }
}
