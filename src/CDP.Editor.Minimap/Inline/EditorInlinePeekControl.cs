using System;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using AvaloniaEdit;
using AvaloniaEdit.Document;
using AvaloniaEdit.Highlighting;

namespace XamlPlayground.Editor.Minimap.Inline;

public sealed class EditorInlinePeekControl : Border
{
    private readonly Action _close;
    private readonly TextEditor _previewEditor;
    private readonly int? _targetLine;

    public EditorInlinePeekControl(
        Control owner,
        string title,
        string? subtitle,
        string text,
        string? language,
        Action close)
    {
        _close = close;
        var palette = EditorPeekPalette.Create(owner);
        var fontFamily = owner is TextEditor editorOwner
            ? editorOwner.FontFamily
            : FontFamily.Default;
        var fontSize = owner is TextEditor fontOwner
            ? Math.Max(11, fontOwner.FontSize - 2)
            : 12;
        _targetLine = ExtractLineNumber(subtitle);

        Background = palette.BodyBackground;
        BorderBrush = palette.Accent;
        BorderThickness = new Thickness(0, 1, 0, 1);
        ClipToBounds = true;
        Focusable = true;

        _previewEditor = CreatePreviewEditor(text, language, fontFamily, fontSize, _targetLine, palette);
        var header = CreateHeader(title, subtitle, close, palette);
        var body = CreateBody(title, subtitle, _previewEditor, palette);

        Child = new Grid
        {
            RowDefinitions =
            {
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Star)
            },
            Children =
            {
                header,
                body
            }
        };

        Grid.SetRow(body, 1);
        AttachedToVisualTree += OnAttachedToVisualTree;
        DetachedFromVisualTree += OnDetachedFromVisualTree;
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        _previewEditor.Focus();
        e.Handled = true;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key == Key.Escape)
        {
            _close();
            e.Handled = true;
        }
    }

    protected override void OnTextInput(TextInputEventArgs e)
    {
        base.OnTextInput(e);
        e.Handled = true;
    }

    private void OnAttachedToVisualTree(object? sender, Avalonia.VisualTreeAttachmentEventArgs e)
    {
        Dispatcher.UIThread.Post(FocusPreviewEditor, DispatcherPriority.Input);
    }

    private void OnDetachedFromVisualTree(object? sender, Avalonia.VisualTreeAttachmentEventArgs e)
    {
        AttachedToVisualTree -= OnAttachedToVisualTree;
        DetachedFromVisualTree -= OnDetachedFromVisualTree;
    }

    private static Control CreateHeader(
        string title,
        string? subtitle,
        Action close,
        EditorPeekPalette palette)
    {
        var titlePanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            VerticalAlignment = VerticalAlignment.Center
        };

        titlePanel.Children.Add(new TextBlock
        {
            Text = title,
            FontSize = 12,
            FontWeight = FontWeight.SemiBold,
            Foreground = palette.Foreground,
            VerticalAlignment = VerticalAlignment.Center
        });

        if (!string.IsNullOrWhiteSpace(subtitle))
        {
            titlePanel.Children.Add(new TextBlock
            {
                Text = subtitle,
                FontSize = 12,
                Foreground = palette.MutedForeground,
                TextTrimming = TextTrimming.CharacterEllipsis,
                VerticalAlignment = VerticalAlignment.Center
            });
        }

        titlePanel.Children.Add(new TextBlock
        {
            Text = "- Definition",
            FontSize = 12,
            Foreground = palette.MutedForeground,
            VerticalAlignment = VerticalAlignment.Center
        });

        var closeButton = new Border
        {
            Width = 28,
            Height = 24,
            Background = Brushes.Transparent,
            Cursor = new Cursor(StandardCursorType.Hand),
            Child = new TextBlock
            {
                Text = "x",
                FontSize = 13,
                Foreground = palette.MutedForeground,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            }
        };
        closeButton.PointerPressed += (_, e) =>
        {
            close();
            e.Handled = true;
        };
        ToolTip.SetTip(closeButton, "Close peek");

        var grid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)
            },
            Children =
            {
                titlePanel,
                closeButton
            }
        };
        Grid.SetColumn(closeButton, 1);

        return new Border
        {
            Background = palette.HeaderBackground,
            Padding = new Thickness(10, 4, 8, 4),
            BorderBrush = palette.Border,
            BorderThickness = new Thickness(0, 0, 0, 1),
            Child = grid
        };
    }

    private static TextEditor CreatePreviewEditor(
        string text,
        string? language,
        FontFamily fontFamily,
        double fontSize,
        int? targetLine,
        EditorPeekPalette palette)
    {
        var document = new TextDocument(text ?? string.Empty);
        var editor = new TextEditor
        {
            Document = document,
            IsReadOnly = true,
            ShowLineNumbers = true,
            WordWrap = false,
            FontFamily = fontFamily,
            FontSize = fontSize,
            FontWeight = FontWeight.Normal,
            Background = palette.BodyBackground,
            Foreground = palette.Foreground,
            LineNumbersForeground = palette.LineNumberForeground,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Visible,
            Padding = new Thickness(0),
            SyntaxHighlighting = GetHighlighting(language),
            Focusable = true
        };

        editor.Classes.Add("theme-aware");
        editor.TextArea.TextView.CurrentLineBackground = palette.ActiveLineBackground;
        editor.TextArea.SelectionBrush = palette.SelectionBackground;

        if (targetLine is > 0 && document.LineCount > 0)
        {
            var line = document.GetLineByNumber(Math.Clamp(targetLine.Value, 1, document.LineCount));
            editor.CaretOffset = line.Offset;
        }

        return editor;
    }

    private static Control CreateBody(
        string title,
        string? subtitle,
        TextEditor previewEditor,
        EditorPeekPalette palette)
    {
        var sidePane = new EditorPeekSidePane(title, subtitle, palette);

        var grid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(new GridLength(280))
            },
            Children =
            {
                previewEditor,
                sidePane
            }
        };
        Grid.SetColumn(sidePane, 1);

        return grid;
    }

    private static int? ExtractLineNumber(string? subtitle)
    {
        if (string.IsNullOrWhiteSpace(subtitle))
        {
            return null;
        }

        var index = subtitle.LastIndexOf(':');
        return index >= 0 && int.TryParse(subtitle[(index + 1)..], NumberStyles.Integer, CultureInfo.InvariantCulture, out var line)
            ? line
            : null;
    }

    private static IHighlightingDefinition? GetHighlighting(string? language)
    {
        if (string.IsNullOrWhiteSpace(language))
        {
            return null;
        }

        var extension = language.Trim().TrimStart('.');
        return extension.Equals("csharp", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals("cs", StringComparison.OrdinalIgnoreCase)
            ? HighlightingManager.Instance.GetDefinitionByExtension(".cs")
            : HighlightingManager.Instance.GetDefinitionByExtension(extension.Equals("xaml", StringComparison.OrdinalIgnoreCase) ||
                                                                     extension.Equals("axaml", StringComparison.OrdinalIgnoreCase) ||
                                                                     extension.Equals("xml", StringComparison.OrdinalIgnoreCase)
                ? ".xml"
                : "." + extension);
    }

    private void FocusPreviewEditor()
    {
        _previewEditor.Focus();
        if (_targetLine is not > 0 || _previewEditor.Document is not { } document || document.LineCount == 0)
        {
            return;
        }

        var lineNumber = Math.Clamp(_targetLine.Value, 1, document.LineCount);
        var line = document.GetLineByNumber(lineNumber);
        _previewEditor.CaretOffset = line.Offset;
        _previewEditor.ScrollToLine(Math.Max(1, lineNumber - 4));
    }

    private sealed class EditorPeekSidePane : Control
    {
        private readonly string _title;
        private readonly string? _subtitle;
        private readonly EditorPeekPalette _palette;

        public EditorPeekSidePane(string title, string? subtitle, EditorPeekPalette palette)
        {
            _title = title;
            _subtitle = subtitle;
            _palette = palette;
            Focusable = false;
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            return new Size(Math.Min(320, availableSize.Width), availableSize.Height);
        }

        public override void Render(DrawingContext context)
        {
            base.Render(context);

            var bounds = new Rect(Bounds.Size);
            if (bounds.Width <= 0 || bounds.Height <= 0)
            {
                return;
            }

            using (context.PushClip(bounds))
            {
                context.FillRectangle(_palette.SidePaneBackground, bounds);
                context.DrawLine(new Pen(_palette.Border, 1), new Point(0, 0), new Point(0, bounds.Height));
                context.FillRectangle(_palette.SidePaneSelectedBackground, new Rect(0, 0, bounds.Width, 60));
                context.FillRectangle(_palette.Accent, new Rect(0, 0, 3, 60));

                DrawText(context, _title, new Rect(12, 10, bounds.Width - 24, 18), _palette.Foreground, FontWeight.SemiBold, 12);
                DrawText(context, _subtitle ?? "Current preview", new Rect(12, 36, bounds.Width - 24, 16), _palette.MutedForeground, FontWeight.Normal, 11);
            }
        }

        private static void DrawText(
            DrawingContext context,
            string text,
            Rect rect,
            IBrush brush,
            FontWeight weight,
            double fontSize)
        {
            var formattedText = new FormattedText(
                text,
                CultureInfo.CurrentUICulture,
                FlowDirection.LeftToRight,
                new Typeface(FontFamily.Default, FontStyle.Normal, weight),
                fontSize,
                brush)
            {
                MaxTextWidth = rect.Width,
                MaxLineCount = 1,
                Trimming = TextTrimming.CharacterEllipsis
            };

            context.DrawText(formattedText, rect.Position);
        }
    }

    private sealed class EditorPeekPalette
    {
        private EditorPeekPalette(
            IBrush accent,
            IBrush border,
            IBrush headerBackground,
            IBrush bodyBackground,
            IBrush gutterBackground,
            IBrush activeLineBackground,
            IBrush sidePaneBackground,
            IBrush sidePaneSelectedBackground,
            IBrush foreground,
            IBrush mutedForeground,
            IBrush lineNumberForeground,
            IBrush selectionBackground)
        {
            Accent = accent;
            Border = border;
            HeaderBackground = headerBackground;
            BodyBackground = bodyBackground;
            GutterBackground = gutterBackground;
            ActiveLineBackground = activeLineBackground;
            SidePaneBackground = sidePaneBackground;
            SidePaneSelectedBackground = sidePaneSelectedBackground;
            Foreground = foreground;
            MutedForeground = mutedForeground;
            LineNumberForeground = lineNumberForeground;
            SelectionBackground = selectionBackground;
        }

        public IBrush Accent { get; }

        public IBrush Border { get; }

        public IBrush HeaderBackground { get; }

        public IBrush BodyBackground { get; }

        public IBrush GutterBackground { get; }

        public IBrush ActiveLineBackground { get; }

        public IBrush SidePaneBackground { get; }

        public IBrush SidePaneSelectedBackground { get; }

        public IBrush Foreground { get; }

        public IBrush MutedForeground { get; }

        public IBrush LineNumberForeground { get; }

        public IBrush SelectionBackground { get; }

        public static EditorPeekPalette Create(Control owner)
        {
            var isDark = owner.ActualThemeVariant == ThemeVariant.Dark;
            return new EditorPeekPalette(
                EditorInlineTheme.Brush(owner, "DockTabSelectedForegroundBrush", Brush(0x00, 0x7A, 0xCC)),
                EditorInlineTheme.Brush(owner, "EditorBorderBrush", Brush(isDark ? 0x3C : 0xCE, isDark ? 0x3C : 0xCE, isDark ? 0x3C : 0xCE)),
                EditorInlineTheme.Brush(owner, "DockSurfaceHeaderBrush", Brush(isDark ? 0x25 : 0xF3, isDark ? 0x25 : 0xF3, isDark ? 0x26 : 0xF3)),
                Brush(isDark ? 0x1E : 0xF3, isDark ? 0x1E : 0xF8, isDark ? 0x1E : 0xFF),
                Brush(isDark ? 0x25 : 0xF3, isDark ? 0x25 : 0xF3, isDark ? 0x26 : 0xF3),
                Brush(isDark ? 0x26 : 0xD9, isDark ? 0x4F : 0xEC, isDark ? 0x78 : 0xFF),
                Brush(isDark ? 0x25 : 0xFF, isDark ? 0x25 : 0xFF, isDark ? 0x26 : 0xFF),
                Brush(isDark ? 0x37 : 0xE4, isDark ? 0x37 : 0xF1, isDark ? 0x3D : 0xFE),
                EditorInlineTheme.Brush(owner, "EditorForegroundBrush", Brush(isDark ? 0xD4 : 0x1F, isDark ? 0xD4 : 0x1F, isDark ? 0xD4 : 0x1F)),
                EditorInlineTheme.Brush(owner, "DockChromeButtonForegroundBrush", Brush(isDark ? 0xA0 : 0x6A, isDark ? 0xA0 : 0x6A, isDark ? 0xA0 : 0x6A)),
                Brush(isDark ? 0x85 : 0x6E, isDark ? 0x85 : 0x6E, isDark ? 0x85 : 0x6E),
                Brush(90, 0x00, 0x7A, 0xCC));
        }

        private static IBrush Brush(int r, int g, int b)
        {
            return new SolidColorBrush(Color.FromRgb((byte)r, (byte)g, (byte)b));
        }

        private static IBrush Brush(byte a, int r, int g, int b)
        {
            return new SolidColorBrush(Color.FromArgb(a, (byte)r, (byte)g, (byte)b));
        }
    }
}
